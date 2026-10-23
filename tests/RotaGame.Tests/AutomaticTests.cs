using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using RotaGame.Core;
using RotaGame.Windows;

internal static class AutomaticTests
{
    private static void Check(bool value, string detail = "Assertion failed") { if (!value) throw new Exception(detail); }
    private static readonly RouteOption Bridge = new("100.70.0.2", "PC Cascavel");
    private static MeasurementBlock Block(double rtt, int count = 24, bool failures = false, double? tail = null, double? alternating = null)
    {
        var b = new MeasurementBlock { Path = PathKind.Diagnostic, TargetIp = "1.1.1.1", Settings = new(count, 100, 100, 32, 0), Status = "complete" };
        var route = new RouteSnapshot(5, "id", "Test fixture", "Synthetic", "192.168.1.1", "0.0.0.0/0", false, true, DateTimeOffset.UtcNow);
        for (var i = 1; i <= count; i++)
        {
            var lost = failures && i == 1;
            var value = tail.HasValue && i > count - 3 ? tail.Value : alternating.HasValue && i % 2 == 0 ? rtt + alternating.Value : rtt;
            b.Samples.Add(new(i, DateTimeOffset.UtcNow, lost ? ProbeOutcome.Timeout : ProbeOutcome.Success, lost ? null : value, "unit fixture", route, route));
        }
        return b;
    }
    private static List<RouteTrial> Trials(double a = 100, double b = 60) => [new(RouteOption.Direct, Block(a)), new(Bridge, Block(b)), new(Bridge, Block(b)), new(RouteOption.Direct, Block(a))];

    public static List<(string Name, Func<Task> Run)> All(bool realNetwork = false)
    {
        var list = new List<(string, Func<Task>)>();
        void Test(string name, Action action) => list.Add((name, () => { action(); return Task.CompletedTask; }));
        void Async(string name, Func<Task> action) => list.Add((name, action));
        Test("AUTO: selects an adequately faster bridge", () => Check(AutomaticPolicy.Choose(Trials()).Winner == Bridge));
        Test("AUTO: small differences retain direct", () => Check(AutomaticPolicy.Choose(Trials(100, 97)).Winner.IsDirect));
        Test("AUTO: relatively tiny improvements retain direct", () => Check(AutomaticPolicy.Choose(Trials(400, 390)).Winner.IsDirect));
        Test("AUTO: a lower median cannot hide a worse mean", () =>
        {
            var t = Trials(); t[0] = new(RouteOption.Direct, Block(0, alternating: 200)); t[3] = new(RouteOption.Direct, Block(0, alternating: 200));
            foreach (var index in new[] { 1, 2 })
            {
                var b = Block(20); b.Samples[^1] = b.Samples[^1] with { RttMs = 2500 }; t[index] = new(Bridge, b);
            }
            Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Test("AUTO: worse p95 rejects a lower median", () =>
        {
            var t = Trials(); t[1] = new(Bridge, Block(60, tail: 150)); t[2] = new(Bridge, Block(60, tail: 150)); Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Test("AUTO: apparent loss increase rejects a bridge", () =>
        {
            var t = Trials(); t[1] = new(Bridge, Block(60, failures: true)); Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Test("AUTO: jitter increase rejects a bridge", () =>
        {
            var t = Trials(); t[1] = new(Bridge, Block(50, alternating: 20)); t[2] = new(Bridge, Block(50, alternating: 20)); Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Test("AUTO: no successful probes cannot fabricate a winner", () =>
        {
            var t = Trials(); foreach (var trial in t) { for (var i = 0; i < trial.Block.Samples.Count; i++) trial.Block.Samples[i] = trial.Block.Samples[i] with { Outcome = ProbeOutcome.Timeout, RttMs = null }; }
            var choice = AutomaticPolicy.Choose(t); Check(choice.Winner.IsDirect && choice.DirectStats.MedianMs is null);
        });
        Test("AUTO: incomplete relay candidates are excluded", () =>
        {
            var t = Trials(); t[1] = new(Bridge with { Link = "relay" }, t[1].Block); t.RemoveAt(2); Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Test("AUTO: a measured faster DERP path can win with the same quality limits", () =>
        {
            var t = Trials(); foreach (var i in new[] { 1, 2 }) t[i] = t[i] with { Route = Bridge with { Link = "relay" } };
            Check(AutomaticPolicy.Choose(t).Winner.Link == "relay");
            foreach (var i in new[] { 1, 2 }) t[i] = new(Bridge with { Link = "relay" }, Block(130));
            Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Test("AUTO: unknown peer-relay paths remain excluded", () =>
        {
            var t = Trials(); foreach (var i in new[] { 1, 2 }) t[i] = t[i] with { Route = Bridge with { Link = "peer-relay" } };
            Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Test("AUTO: chooses the best eligible registered bridge", () =>
        {
            var t = Trials(); var faster = new RouteOption("100.70.0.3", "Other bridge"); t.Insert(2, new(faster, Block(40))); t.Insert(3, new(faster, Block(40)));
            Check(AutomaticPolicy.Choose(t).Winner == faster);
        });
        Test("AUTO: a changed direct interface invalidates selection", () =>
        {
            var t = Trials(); t[3].Block.Samples[0] = t[3].Block.Samples[0] with { RouteBefore = t[3].Block.Samples[0].RouteBefore with { InterfaceIndex = 99 } };
            Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Test("AUTO: results for a different endpoint cannot win", () =>
        {
            var t = Trials();
            var wrong = new MeasurementBlock { TargetIp = "8.8.8.8", Settings = t[1].Block.Settings, Path = PathKind.Diagnostic, Status = "complete" };
            wrong.Samples.AddRange(t[1].Block.Samples); t[1] = new(Bridge, wrong); Check(AutomaticPolicy.Choose(t).Winner.IsDirect);
        });
        Async("AUTO: activation applies the measured winner", async () =>
        {
            var routes = new FakeAutomaticRoutes([Bridge]); var store = new MemoryStore(); var lease = new RouteLease(routes, store);
            var report = await new AutomaticOptimizer(routes, new FakeAutomaticMeter(), lease).ActivateAsync("Test", Destination(), new(24, 100, 100, 32, 0), null, null, default);
            Check(report.Trials.Count == 4 && report.Choice?.Winner == Bridge && routes.Current == Bridge.Id);
            await lease.RestoreAsync(default); Check(routes.Current == "" && store.Value is null);
        });
        Async("AUTO: no bridge is a functional direct-only activation", async () =>
        {
            var routes = new FakeAutomaticRoutes([]); var store = new MemoryStore(); var lease = new RouteLease(routes, store);
            var report = await new AutomaticOptimizer(routes, new FakeAutomaticMeter(), lease).ActivateAsync("Test", Destination(), new(24, 100, 100, 32, 0), null, null, default);
            Check(report.Trials.Count == 2 && report.Choice!.Winner.IsDirect); await lease.RestoreAsync(default);
        });
        Async("AUTO: cancellation restores original connection", async () =>
        {
            var routes = new FakeAutomaticRoutes([Bridge]) { Current = "100.70.0.9" }; var store = new MemoryStore(); var lease = new RouteLease(routes, store);
            using var cts = new CancellationTokenSource();
            var meter = new FakeAutomaticMeter { After = r => { if (!r.IsDirect) cts.Cancel(); } };
            try { await new AutomaticOptimizer(routes, meter, lease).ActivateAsync("Test", Destination(), new(24, 100, 100, 32, 0), null, null, cts.Token); throw new Exception("Not cancelled"); }
            catch (OperationCanceledException) { Check(routes.Current == "100.70.0.9" && store.Value is null); }
        });
        Async("AUTO: a failing bridge does not prevent another candidate", async () =>
        {
            var second = new RouteOption("100.70.0.3", "Second"); var routes = new FakeAutomaticRoutes([Bridge, second]) { FailOn = Bridge.Id };
            var store = new MemoryStore(); var lease = new RouteLease(routes, store);
            var report = await new AutomaticOptimizer(routes, new FakeAutomaticMeter(), lease).ActivateAsync("Test", Destination(), new(24, 100, 100, 32, 0), null, null, default);
            Check(report.Choice!.Winner == second && report.Notes.Count == 1); await lease.RestoreAsync(default);
        });
        Async("RECOVERY: interrupted operation restores from persistent record", async () =>
        {
            var routes = new FakeAutomaticRoutes([]) { Current = Bridge.Id }; var store = new MemoryStore { Value = new("", Bridge.Id, DateTimeOffset.UtcNow) };
            await new RouteLease(routes, store).RestoreAsync(default); Check(routes.Current == "" && store.Value is null);
        });
        Async("RECOVERY: a later external manual change is preserved", async () =>
        {
            var routes = new FakeAutomaticRoutes([]) { Current = "100.70.0.8" }; var store = new MemoryStore { Value = new("", Bridge.Id, DateTimeOffset.UtcNow) };
            await new RouteLease(routes, store).RestoreAsync(default); Check(routes.Current == "100.70.0.8" && routes.Selected.Count == 0 && store.Value is null);
        });
        Async("RECOVERY: failed mutation still restores previous applied path", async () =>
        {
            var routes = new FakeAutomaticRoutes([]); var store = new MemoryStore(); var lease = new RouteLease(routes, store); await lease.BeginAsync(default);
            await lease.ApplyAsync(Bridge, default); routes.FailOn = "100.70.0.3";
            try { await lease.ApplyAsync(new("100.70.0.3", "Broken"), default); } catch (InvalidOperationException) { }
            await lease.RestoreAsync(default); Check(routes.Current == "" && store.Value is null);
        });
        Async("RECOVERY: failed restore retains the journal for retry", async () =>
        {
            var routes = new FakeAutomaticRoutes([]); var store = new MemoryStore(); var lease = new RouteLease(routes, store); await lease.BeginAsync(default);
            await lease.ApplyAsync(Bridge, default); routes.FailOn = "";
            try { await lease.RestoreAsync(default); throw new Exception("Restore should fail"); }
            catch (InvalidOperationException) { Check(store.Value is not null && lease.Active); }
            routes.FailOn = null; await lease.RestoreAsync(default); Check(store.Value is null);
        });
        Test("TAILSCALE: parses approved online candidates and selected IPv4 prefix", () =>
        {
            var status = TailscaleClient.ParseStatus("""{"BackendState":"Running","ExitNodeStatus":{"TailscaleIPs":["100.70.0.2/32"],"Online":true},"Self":{"HostName":"me"},"Peer":{"a":{"TailscaleIPs":["100.70.0.2"],"HostName":"home","Online":true,"ExitNodeOption":true,"CurAddr":"1.2.3.4:1234"},"b":{"TailscaleIPs":["100.70.0.3"],"Online":true,"ExitNodeOption":false}}} """);
            Check(status.CurrentExit == "100.70.0.2" && status.Peers[0].Link == "direct" && status.Peers[0].Offered && !status.Peers[1].Offered);
        });
        Test("TAILSCALE: distinguishes expired and peer-relayed peers", () =>
        {
            var status = TailscaleClient.ParseStatus("""{"BackendState":"Running","Peer":{"a":{"TailscaleIPs":["100.70.0.2"],"Online":true,"Expired":true,"ExitNodeOption":true,"PeerRelay":"relay","CurAddr":"1.2.3.4:1"}}} """);
            Check(status.CurrentExit == "" && !status.Peers[0].Online && status.Peers[0].Link == "peer-relay");
        });
        Async("TAILSCALE CLI: changes only exit selection and verifies it", async () =>
        {
            var runner = new TailRunner(); var client = new TailscaleClient(runner, "test-fixture.exe");
            await client.SelectAsync(Bridge, default); Check((await client.ReadAsync(default)).Selector == Bridge.Id);
            await client.SelectAsync(RouteOption.Direct, default); Check((await client.ReadAsync(default)).Selector == "");
            Check(runner.Mutations.SequenceEqual(new[] { "--exit-node=100.70.0.2", "--exit-node=" }));
        });
        Async("TAILSCALE CLI: measures direct and DERP peers, excluding offline or unapproved ones", async () =>
        {
            var client = new TailscaleClient(new TailRunner(), "test-fixture.exe");
            var options = await client.DiscoverAsync(default); Check(options.Count == 2 && options.Any(o => o.Id == Bridge.Id && o.Link == "direct") && options.Any(o => o.Id == "100.70.0.4" && o.Link == "relay"));
            await client.VerifyAsync(RouteOption.Direct, default);
            var relay = options.Single(o => o.Link == "relay"); await client.SelectAsync(relay, default); await client.VerifyAsync(relay, default);
            try { await client.VerifyAsync(relay with { Link = "direct" }, default); throw new Exception("Changed tunnel should fail."); } catch (InvalidOperationException) { }
        });
        Test("DISCOVERY: ignores private, CGNAT, multicast and documentation IPs", () =>
        {
            foreach (var ip in new[] { "127.0.0.1", "10.0.0.1", "100.64.0.1", "192.168.1.1", "192.0.2.1", "198.51.100.2", "203.0.113.5", "224.0.0.1", "255.255.255.255" }) Check(!PublicIpv4.IsPublic(IPAddress.Parse(ip)), ip);
            Check(PublicIpv4.IsPublic(IPAddress.Parse("1.1.1.1")));
        });
        Test("DISCOVERY: sustained UDP is preferred to TCP and DNS is excluded", () =>
        {
            var a = new EndpointAccumulator(); for (var i = 0; i < 20; i++) a.Add(IPAddress.Parse("1.1.1.1"), 443, "TCP", 123, "game");
            for (var i = 0; i < 8; i++) a.Add(IPAddress.Parse("8.8.8.8"), 7777, "UDP", 123, "game");
            for (var i = 0; i < 50; i++) a.Add(IPAddress.Parse("1.0.0.1"), 53, "UDP", 123, "game");
            Check(a.Best()?.Ip == "8.8.8.8");
        });
        Async("COMMAND: preserves an empty exit selector without shell interpretation", async () =>
        {
            var args = new[] { "--exit-node=", "space in name", "x;whoami", "`literal`" };
            var result = await new CommandRunner().RunAsync(Environment.ProcessPath!, new[] { "--echo-args" }.Concat(args), TimeSpan.FromSeconds(10), default);
            var parsed = JsonSerializer.Deserialize<string[]>(result.Output.Trim()); Check(result.ExitCode == 0 && parsed!.SequenceEqual(args));
        });
        if (realNetwork && TailscaleClient.FindExecutable() is null)
            Async("REAL automatic direct meter without connection component installed", async () =>
            {
                var client = new TailscaleClient(new CommandRunner()); var routes = new FakeAutomaticRoutes([]); var store = new MemoryStore(); var lease = new RouteLease(routes, store);
                var meter = new AutomaticMeter(new IcmpProbe(), new WindowsRouteObserver(), client);
                var report = await new AutomaticOptimizer(routes, meter, lease).ActivateAsync("Validation", Destination(), new(3, 100, 300, 32, 0), null, null, default);
                Check(report.Trials.Count == 2 && report.Choice!.Winner.IsDirect && report.Trials.All(t => t.Block.Samples.Count == 3));
                await lease.RestoreAsync(default); Check(store.Value is null);
            });
        return list;
    }

    public static async Task DiscoveryIntegration()
    {
        var token = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var discovery = new GameDiscovery().FindAsync("Jogo cadastrado de validação", null, token.Token, TimeSpan.FromSeconds(4), customProcessNames: [System.Diagnostics.Process.GetCurrentProcess().ProcessName]);
        await Task.Delay(1300, token.Token);
        using var udp = new UdpClient();
        for (var i = 0; i < 12; i++) { await udp.SendAsync(new byte[] { 82, 71 }, new IPEndPoint(IPAddress.Parse("1.1.1.1"), 9), token.Token); await Task.Delay(75, token.Token); }
        var result = await discovery;
        Check(result.Ip == "1.1.1.1" && result.ProcessId == Environment.ProcessId && result.Protocol == "UDP" && result.Packets >= 8);
        Console.WriteLine($"  ETW observed {result.Packets} controlled UDP sends for PID {result.ProcessId}; no payload captured.");
    }
    private static ObservedDestination Destination() => new("1.1.1.1", 7777, "UDP", 123, "fixture", 100);

    private sealed class FakeAutomaticRoutes(IReadOnlyList<RouteOption> options) : IAutomaticRoutes
    {
        public string Current { get; set; } = "";
        public string? FailOn { get; set; }
        public List<string> Selected { get; } = [];
        public Task<RouteReading> ReadAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(new RouteReading(Current, true, "fixture")); }
        public Task<IReadOnlyList<RouteOption>> DiscoverAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(options); }
        public Task SelectAsync(RouteOption option, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); if (option.Id == FailOn) throw new InvalidOperationException("Synthetic route failure");
            Current = option.Id; Selected.Add(Current); return Task.CompletedTask;
        }
    }
    private sealed class FakeAutomaticMeter : IAutomaticMeter
    {
        public Action<RouteOption>? After { get; init; }
        public Task<MeasurementBlock> MeasureAsync(string target, RouteOption route, ProbeSettings settings, IProgress<MeasurementProgress>? progress, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); var b = Block(route.IsDirect ? 100 : 60, settings.Count); After?.Invoke(route); return Task.FromResult(b); }
    }
    private sealed class MemoryStore : IRecoveryStore
    {
        public RecoveryRecord? Value;
        public RecoveryRecord? Load() => Value;
        public void Save(RecoveryRecord record) => Value = record;
        public void Clear() => Value = null;
    }
    private sealed class TailRunner : ICommandRunner
    {
        private string _selected = "";
        public List<string> Mutations { get; } = [];
        public Task<CommandResult> RunAsync(string file, IEnumerable<string> args, TimeSpan timeout, CancellationToken ct)
        {
            var a = args.ToArray(); ct.ThrowIfCancellationRequested();
            if (a[0] == "set") { Mutations.Add(a[1]); _selected = a[1]["--exit-node=".Length..]; return Task.FromResult(new CommandResult(0, "", "")); }
            if (a[0] == "ping") return Task.FromResult(a.Contains("--until-direct=false") || a[^1] == Bridge.Id
                ? new CommandResult(0, "controlled ping fixture", "")
                : new CommandResult(1, "pong via DERP", "direct connection not established"));
            var exit = _selected.Length == 0 ? "null" : JsonSerializer.Serialize(new { TailscaleIPs = new[] { _selected + "/32" }, Online = true });
            var json = """{"BackendState":"Running","Self":{"HostName":"test"},"ExitNodeStatus":EXIT,"Peer":{"a":{"TailscaleIPs":["100.70.0.2"],"HostName":"home","Online":true,"ExitNodeOption":true,"CurAddr":"1.2.3.4:1"},"b":{"TailscaleIPs":["100.70.0.3"],"Online":false,"ExitNodeOption":true},"c":{"TailscaleIPs":["100.70.0.4"],"Online":true,"ExitNodeOption":true,"Relay":"gru"}}} """.Replace("EXIT", exit);
            return Task.FromResult(new CommandResult(0, json, ""));
        }
    }
}
