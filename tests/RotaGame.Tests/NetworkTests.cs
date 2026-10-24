using System.Net;
using RotaGame.Core;
using RotaGame.Windows;

internal static class NetworkTests
{
    private static void Check(bool v, string message = "Assertion failed") { if (!v) throw new Exception(message); }
    private static RouteSnapshot Snapshot(string id = "fixture") => new(7, id, id, id, "192.0.2.1", "0.0.0.0/0", false, true, DateTimeOffset.UtcNow);
    internal static List<(string, Func<Task>)> All(bool integration)
    {
        var tests = new List<(string, Func<Task>)>();
        void Test(string name, Action action) => tests.Add((name, () => { action(); return Task.CompletedTask; }));
        void Async(string name, Func<Task> action) => tests.Add((name, action));
        Async("TRACE: silent hops retain position and never become zero latency", async () =>
        {
            var probe = new TraceFixture([new(TraceOutcome.Router,"192.0.2.1",5,"reply"), new(TraceOutcome.Timeout,null,null,"silent"), new(TraceOutcome.Destination,"1.1.1.1",30,"reply")]);
            var result = await new NetworkTracer(probe, new Observer()).RunAsync("1.1.1.1",null,default,3,100);
            Check(result.Reached && result.Hops.Count == 3 && result.Hops[1].Number == 2 && result.Hops[1].Address is null && result.Hops[1].RttMs is null && probe.Ttls.SequenceEqual(new[]{1,2,3}));
        });
        Async("TRACE: unanswered destination remains partial", async () =>
        {
            var result = await new NetworkTracer(new TraceFixture([new(TraceOutcome.Timeout,null,null,"silent")]),new Observer()).RunAsync("1.1.1.1",null,default,1,100);
            Check(!result.Reached && result.Status == "partial" && result.Hops.Count == 1);
        });
        Async("TRACE: route change invalidates old hops", async () =>
        {
            var observer = new Observer(); var probe = new TraceFixture([new(TraceOutcome.Router,"192.0.2.1",5,"reply")]) { After = () => observer.Id="changed" };
            var result = await new NetworkTracer(probe,observer).RunAsync("1.1.1.1",null,default,2,100);
            Check(result.Status == "route-changed" && !result.Reached && result.Hops.Count == 0);
        });
        Async("TRACE: local errors stop without inventing network loss", async () =>
        {
            var probe = new TraceFixture([new(TraceOutcome.LocalError,null,null,"access denied")]);
            var result = await new NetworkTracer(probe,new Observer()).RunAsync("1.1.1.1",null,default,5,100);
            Check(result.Status == "error" && result.Hops.Count == 1 && probe.Ttls.Count == 1);
        });
        Async("TRACE: a success from a different address is not the destination", async () =>
        {
            var result = await new NetworkTracer(new TraceFixture([new(TraceOutcome.Destination,"8.8.8.8",5,"unexpected")]),new Observer()).RunAsync("1.1.1.1",null,default,2,100);
            Check(!result.Reached && result.Status == "error" && result.Hops.Single().Address is null);
        });
        Async("TRACE: cancellation stops additional probes", async () =>
        {
            using var ct = new CancellationTokenSource(); var probe = new TraceFixture([new(TraceOutcome.Router,"192.0.2.1",5,"reply")]) { After=ct.Cancel };
            try { await new NetworkTracer(probe,new Observer()).RunAsync("1.1.1.1",null,ct.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(probe.Ttls.Count == 1); }
        });
        Async("TRACE: multicast and IPv6 rejected before networking", async () =>
        {
            var probe = new TraceFixture([]);
            foreach(var target in new[]{"224.0.0.1","::1","invalid; command"})
                try { await new NetworkTracer(probe,new Observer()).RunAsync(target,null,default); throw new Exception("Invalid target accepted"); } catch(ArgumentException) { }
            Check(probe.Ttls.Count == 0);
        });
        Test("WARP: free consumer status and identity parsed without storing registration", () =>
        {
            var state=WarpClient.Parse("Status update: Connected", "Mode: WarpWithDnsOverHttps", Registration);
            Check(state.Connected && state.Ready && state.Traffic && state.Identity?.Length==64 && !state.Identity.Contains("01234567"));
        });
        Test("WARP: DNS-only is not a game tunnel", () => Check(!WarpClient.Parse("Status update: Connected","Mode: DnsOverHttps",Registration).Traffic));
        Test("WARP: managed, paid and unknown registrations cannot be changed", () =>
        {
            foreach(var account in new[]{"Team","Unlimited","Unknown"})
                try { WarpClient.Parse("Status update: Connected","Mode: WarpWithDnsOverHttps",Registration.Replace("Limited",account)); throw new Exception("Unsafe account accepted"); } catch(InvalidOperationException) { }
        });
        Test("WARP: unknown connection or mode cannot look ready", () =>
        {
            foreach(var value in new[]{("unrecognized","Mode: WarpWithDnsOverHttps"),("Status update: Connected","Mode: FutureMode")})
                try { WarpClient.Parse(value.Item1,value.Item2,Registration); throw new Exception("Unknown state accepted"); } catch(InvalidDataException) { }
        });
        Async("WARP: changes only connection state and restores original", async () =>
        {
            var runner=new WarpFixture(); var client=new WarpClient(runner,"fixture-cli.exe"); var store=new Memory(); var lease=new RouteLease(client,store);
            await lease.BeginAsync(default); Check((await client.DiscoverAsync(default)).Single()==WarpClient.Warp);
            await lease.ApplyAsync(WarpClient.Warp,default); Check(runner.Connected);
            await lease.RestoreAsync(default); Check(!runner.Connected && store.Value is null && runner.Mutations.SequenceEqual(new[]{"connect","disconnect"}));
        });
        Async("WARP: registration cannot be created without explicit user consent", async()=>
        {
            var runner=new WarpFixture();var client=new WarpClient(runner,"fixture-cli.exe",()=>false);
            try {await client.PrepareRegistrationAsync(default);throw new Exception("Consent bypassed");}catch(InvalidOperationException){Check(runner.Calls==0);}
        });
        Async("WARP: explicit preparation preserves an existing registration", async()=>
        {
            var runner=new WarpFixture();var client=new WarpClient(runner,"fixture-cli.exe",()=>true);
            var state=await client.PrepareRegistrationAsync(default);Check(state.Ready&&runner.Mutations.Count==0&&runner.SawAcceptedTerms);
        });
        Async("WARP: only a recognized missing registration can create a free device",async()=>
        {
            var runner=new WarpFixture {MissingRegistration=true};var client=new WarpClient(runner,"fixture-cli.exe",()=>true);
            var state=await client.PrepareRegistrationAsync(default);Check(state.Ready&&runner.Mutations.SequenceEqual(new[]{"registration new"}));
        });
        Async("WARP: a service failure never resets or recreates an account",async()=>
        {
            var runner=new WarpFixture {RegistrationError=true};var client=new WarpClient(runner,"fixture-cli.exe",()=>true);
            try {await client.PrepareRegistrationAsync(default);throw new Exception("Service failure ignored");}catch(InvalidOperationException){Check(runner.Mutations.Count==0);}
        });
        Async("WARP: DNS-only preference survives direct diagnostics", async () =>
        {
            var runner=new WarpFixture {Connected=true,Mode="DnsOverHttps"}; var client=new WarpClient(runner,"fixture-cli.exe");
            Check((await client.ReadAsync(default)).Selector=="" && (await client.DiscoverAsync(default)).Count==0);
            await client.SelectAsync(RouteOption.Direct,default); Check(runner.Connected && runner.Mutations.Count==0);
        });
        Async("WARP: recovery cannot reconnect a different registration", async () =>
        {
            var runner=new WarpFixture {Connected=true}; var client=new WarpClient(runner,"fixture-cli.exe"); var store=new Memory();var lease=new RouteLease(client,store);await lease.BeginAsync(default);
            await lease.ApplyAsync(RouteOption.Direct,default);runner.Id="12345678-1234-1234-1234-123456789abc";
            try { await lease.RestoreAsync(default); throw new Exception("Different identity overwritten"); } catch(InvalidOperationException) { Check(!runner.Connected && store.Value is not null); }
        });
        Async("WARP METER: actual target must use Cloudflare adapter", async () =>
        {
            var runner=new WarpFixture {Connected=true}; var client=new WarpClient(runner,"fixture-cli.exe");
            try { await new WarpMeter(new Probe(),new Observer(),client).MeasureAsync("1.1.1.1",WarpClient.Warp,new(1,100,100,32,0),null,default); throw new Exception("Unverified tunnel accepted"); } catch(InvalidOperationException) { }
            var measured=await new WarpMeter(new Probe(),new Observer { Id="Cloudflare WARP" },client).MeasureAsync("1.1.1.1",WarpClient.Warp,new(1,100,100,32,0),null,default);
            Check(measured.Stats.Received==1);
        });
        if(integration) Async("REAL TRACE: loopback reaches actual destination at TTL one",async()=>
        {
            var result=await new NetworkTracer(new NetworkTraceProbe(),new WindowsRouteObserver()).RunAsync("127.0.0.1",null,default,2,500);
            Check(result.Reached && result.Hops.Count==1 && result.Hops[0].Address=="127.0.0.1" && result.Hops[0].RttMs>=0);
        });
        return tests;
    }
    private const string Registration="ID: 01234567-89ab-cdef-0123-456789abcdef\nAccount type: Limited\n";
    private sealed class Observer : IRouteObserver { internal string Id="fixture"; public RouteSnapshot Observe(IPAddress target)=>Snapshot(Id); }
    private sealed class TraceFixture(IReadOnlyList<TraceReply> replies) : ITraceProbe
    {
        internal List<int> Ttls=[]; internal Action? After;
        public Task<TraceReply> SendAsync(IPAddress target,int ttl,int timeoutMs,CancellationToken ct) { ct.ThrowIfCancellationRequested();Ttls.Add(ttl);After?.Invoke();return Task.FromResult(replies[Math.Min(ttl-1,replies.Count-1)]); }
    }
    private sealed class Probe:IProbe { public Task<ProbeReply> SendAsync(IPAddress target,ProbeSettings settings,CancellationToken ct)=>Task.FromResult(new ProbeReply(ProbeOutcome.Success,15,"fixture")); }
    private sealed class Memory:IRecoveryStore { internal RecoveryRecord? Value;public RecoveryRecord? Load()=>Value;public void Save(RecoveryRecord value)=>Value=value;public void Clear()=>Value=null; }
    private sealed class WarpFixture:ICommandRunner
    {
        internal bool Connected,MissingRegistration,RegistrationError;internal string Mode="WarpWithDnsOverHttps",Id="01234567-89ab-cdef-0123-456789abcdef";internal List<string> Mutations=[];internal int Calls;internal bool SawAcceptedTerms;
        public Task<CommandResult> RunAsync(string file,IEnumerable<string> arguments,TimeSpan timeout,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();Calls++;var all=arguments.ToArray();SawAcceptedTerms|=all.Contains("--accept-tos");var args=all.Where(a=>!a.StartsWith("--")).ToArray();
            if(args.SequenceEqual(new[]{"registration","show"})&&(MissingRegistration||RegistrationError))return Task.FromResult(new CommandResult(1,"",MissingRegistration?"Missing Registration":"Service unavailable"));
            if(args.SequenceEqual(new[]{"registration","new"})){MissingRegistration=false;Mutations.Add("registration new");}
            var text=args[0] switch {"status"=>"Status update: "+(Connected?"Connected":"Disconnected"),"settings"=>"Mode: "+Mode,"registration"=>Registration.Replace("01234567-89ab-cdef-0123-456789abcdef",Id),_=>"Success"};
            if(args[0] is "connect" or "disconnect") { Mutations.Add(args[0]);Connected=args[0]=="connect"; }
            return Task.FromResult(new CommandResult(0,text,""));
        }
    }
}
