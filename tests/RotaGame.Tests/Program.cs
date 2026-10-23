using System.Net;
using System.Text.Json;
using RotaGame.Core;
using RotaGame.Windows;

if (args.Length > 0 && args[0] == "--echo-args") { Console.WriteLine(JsonSerializer.Serialize(args.Skip(1))); return; }
if (args.Length > 0 && args[0] == "--game-child") { await Task.Delay(1500); return; }

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Action action) => tests.Add((name, () => { action(); return Task.CompletedTask; }));
void AsyncTest(string name, Func<Task> action) => tests.Add((name, action));
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Equal(double? actual, double expected) => Check(actual.HasValue && Math.Abs(actual.Value - expected) < .0001, $"Expected {expected}, got {actual}");
RouteSnapshot Route(bool wg = false, string gateway = "192.0.2.1") => new(wg ? 20u : 5u, wg ? "wg-id" : "ethernet-id", wg ? "wg-test" : "Ethernet", wg ? "WireGuard Tunnel" : "Ethernet adapter", gateway, "0.0.0.0/0", wg, !wg, DateTimeOffset.UtcNow);
Sample Sample(int sequence, double? value, ProbeOutcome? outcome = null, bool wg = false) => new(sequence, DateTimeOffset.UtcNow, outcome ?? (value.HasValue ? ProbeOutcome.Success : ProbeOutcome.Timeout), value, "test", Route(wg), Route(wg));
MeasurementSession Session(int count = 20) => new() { Game = "Test", RegionNote = "Synthetic unit fixtures", OriginalTarget = "192.0.2.10", TargetIp = "192.0.2.10", Settings = new(count, 100, 100, 32, 0), IsComparison = true };
MeasurementSession FullSession()
{
    var s = Session();
    foreach (var path in MeasurementRunner.AbbaPlan)
    {
        var b = new MeasurementBlock { Path = path, TargetIp = s.TargetIp, Settings = s.Settings, Status = "complete" };
        b.Samples.AddRange(Enumerable.Range(1, 20).Select(i => Sample(i, path == PathKind.Relay ? 60 : 80, wg: path == PathKind.Relay)));
        s.Blocks.Add(b);
    }
    return s;
}

Test("Statistics: mean, median, nearest-rank p95 and jitter", () =>
{
    var s = Statistics.From([Sample(1, 10), Sample(2, 30), Sample(3, 20), Sample(4, 40)]);
    Equal(s.AverageMs, 25); Equal(s.MedianMs, 25); Equal(s.P95Ms, 40); Equal(s.JitterMs, 50.0 / 3); Equal(s.FailurePercent, 0);
});
Test("Timeouts do not become zero RTT or bridge jitter gaps", () =>
{
    var s = Statistics.From([Sample(1, 10), Sample(2, null), Sample(3, 90), Sample(4, 100)]);
    Equal(s.JitterMs, 10); Equal(s.FailurePercent, 25); Equal(s.MedianMs, 90); Check(s.Sent == 4 && s.Received == 3);
});
Test("Local failures are not reported as packet loss", () =>
{
    var s = Statistics.From([Sample(1, null, ProbeOutcome.LocalError)]);
    Check(s.Sent == 0 && s.LocalErrors == 1 && s.FailurePercent is null && s.AverageMs is null);
});
Test("All unanswered has null latency and 100% apparent loss", () =>
{
    var s = Statistics.From([Sample(1, null), Sample(2, null)]);
    Equal(s.FailurePercent, 100); Check(s.MedianMs is null && s.JitterMs is null);
});
Test("Combined jitter does not cross block boundaries", () =>
{
    var s = FullSession(); s.Blocks[0].Samples.Clear(); s.Blocks[1].Samples.Clear();
    s.Blocks[0].Samples.AddRange([Sample(1, 10), Sample(2, 20)]);
    s.Blocks[1].Samples.AddRange([Sample(1, 200), Sample(2, 220)]);
    Equal(Statistics.Combine(s.Blocks.Take(2)).JitterMs, 15);
});
Test("ABBA compares the same endpoint and reports signed deltas", () =>
{
    var r = Comparison.Evaluate(FullSession()); Check(r.IsValid); Equal(r.MedianDeltaMs, -20); Equal(r.P95DeltaMs, -20);
});
Test("Incomplete experiment is inconclusive", () =>
{
    var s = FullSession(); s.Blocks.RemoveAt(3); Check(!Comparison.Evaluate(s).IsValid);
});
Test("Cancelled block cannot produce a valid verdict", () =>
{
    var s = FullSession(); s.Blocks[1].Status = "cancelled"; Check(!Comparison.Evaluate(s).IsValid);
});
Test("Different targets cannot be compared", () =>
{
    var s = FullSession(); var old = s.Blocks[1];
    var b = new MeasurementBlock { Path = old.Path, TargetIp = "192.0.2.11", Settings = old.Settings, Status = "complete" };
    b.Samples.AddRange(old.Samples); s.Blocks[1] = b; Check(!Comparison.Evaluate(s).IsValid);
});
Test("Insufficient ICMP replies are inconclusive", () =>
{
    var s = FullSession();
    foreach (var b in s.Blocks) { b.Samples.Clear(); b.Samples.AddRange(Enumerable.Range(1, 20).Select(i => Sample(i, null, wg: b.Path == PathKind.Relay))); }
    Check(!Comparison.Evaluate(s).IsValid);
});
Test("Wrong path is rejected even with a manually supplied label", () =>
{
    var s = FullSession(); s.Blocks[1].Samples[0] = Sample(1, 60); Check(!Comparison.Evaluate(s).IsValid);
});
AsyncTest("Runner completes measured samples and excludes warmup", async () =>
{
    var fake = new FakeProbe(); var b = new MeasurementBlock { Path = PathKind.Direct, TargetIp = "192.0.2.10", Settings = new(3, 100, 100, 32, 2) };
    await new MeasurementRunner(fake, new FakeRoutes(Route())).RunAsync(b, null, default);
    Check(b.Status == "complete" && b.Samples.Count == 3 && fake.Calls == 5);
});
AsyncTest("Runner rejects unverified relay before sending packets", async () =>
{
    var fake = new FakeProbe(); var b = new MeasurementBlock { Path = PathKind.Relay, TargetIp = "192.0.2.10", Settings = new(2, 100, 100, 32, 0) };
    try { await new MeasurementRunner(fake, new FakeRoutes(Route())).RunAsync(b, null, default); throw new Exception("Should have rejected route"); }
    catch (InvalidOperationException) { Check(b.Status == "invalid" && fake.Calls == 0); }
});
AsyncTest("Route changes during a probe invalidate its block", async () =>
{
    var b = new MeasurementBlock { Path = PathKind.Direct, TargetIp = "192.0.2.10", Settings = new(2, 100, 100, 32, 0) };
    try { await new MeasurementRunner(new FakeProbe(), new FakeRoutes(Route(), Route(), Route(gateway: "192.0.2.2"))).RunAsync(b, null, default); throw new Exception("Should have rejected change"); }
    catch (InvalidOperationException) { Check(b.Status == "invalid" && b.Samples.Count == 0); }
});
AsyncTest("Cancellation preserves a partial block without completing it", async () =>
{
    using var cts = new CancellationTokenSource();
    var fake = new FakeProbe { AfterSend = () => cts.Cancel() };
    var b = new MeasurementBlock { Path = PathKind.Direct, TargetIp = "192.0.2.10", Settings = new(5, 100, 100, 32, 0) };
    try { await new MeasurementRunner(fake, new FakeRoutes(Route())).RunAsync(b, null, cts.Token); throw new Exception("Should cancel"); }
    catch (OperationCanceledException) { Check(b.Status == "cancelled" && b.Samples.Count == 1); }
});
AsyncTest("Local send error invalidates the block", async () =>
{
    var fake = new FakeProbe { Reply = new(ProbeOutcome.LocalError, null, "access denied") };
    var b = new MeasurementBlock { Path = PathKind.Direct, TargetIp = "192.0.2.10", Settings = new(2, 100, 100, 32, 0) };
    try { await new MeasurementRunner(fake, new FakeRoutes(Route())).RunAsync(b, null, default); throw new Exception("Should reject local error"); }
    catch (InvalidOperationException) { Check(b.Status == "invalid" && b.Stats.LocalErrors == 1 && b.Stats.FailurePercent is null); }
});
AsyncTest("Resolver rejects command-like input and IPv6-only literals", async () =>
{
    foreach (var text in new[] { "127.0.0.1;whoami", "https://example.com/game", "::1", "224.0.0.1" })
    {
        try { await TargetResolver.ResolveAsync(text); throw new Exception("Accepted invalid target: " + text); }
        catch (ArgumentException) { }
    }
    Check((await TargetResolver.ResolveAsync("127.0.0.1")).Equals(IPAddress.Loopback));
});
Test("JSON preserves raw probes, settings and route evidence; CSV escapes formula text", () =>
{
    var path = Path.Combine(Path.GetTempPath(), "rotagame-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
    try
    {
        var s = new MeasurementSession { Game = "=unsafe()", RegionNote = "a,\"b", OriginalTarget = "127.0.0.1", TargetIp = "127.0.0.1", Settings = new(1), IsComparison = false };
        var b = new MeasurementBlock { Path = PathKind.Diagnostic, TargetIp = s.TargetIp, Settings = s.Settings, Status = "complete" }; b.Samples.Add(Sample(1, 10)); s.Blocks.Add(b);
        Export.WriteJson(s, Path.Combine(path, "out.json")); Export.WriteCsv(s, Path.Combine(path, "out.csv"));
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "out.json")));
        Check(doc.RootElement.GetProperty("Session").GetProperty("Blocks")[0].GetProperty("Samples").GetArrayLength() == 1);
        Check(File.ReadAllText(Path.Combine(path, "out.csv")).Contains("\"'=unsafe()\""));
    }
    finally { Directory.Delete(path, true); }
});

tests.AddRange(AutomaticTests.All(args.Contains("--public-probe")));
tests.AddRange(GameLibraryTests.All());
if (args.Contains("--discovery-integration"))
    tests.Add(("REAL ETW process endpoint discovery", AutomaticTests.DiscoveryIntegration));

if (args.Contains("--integration"))
{
    AsyncTest("REAL Windows route lookup and localhost ICMP", async () =>
    {
        var route = new WindowsRouteObserver().Observe(IPAddress.Loopback); Check(route.InterfaceIndex > 0);
        var b = new MeasurementBlock { Path = PathKind.Diagnostic, TargetIp = "127.0.0.1", Settings = new(4, 100, 300, 32, 1) };
        await new MeasurementRunner(new IcmpProbe(), new WindowsRouteObserver()).RunAsync(b, null, default);
        Check(b.Status == "complete" && b.Stats.Received == 4);
        Console.WriteLine($"  Live interface: {route.Name}; received {b.Stats.Received}/{b.Stats.Sent}");
    });
}
if (args.Contains("--public-probe"))
{
    AsyncTest("REAL external ICMP diagnostic (this machine, not Acre)", async () =>
    {
        var s = new MeasurementSession { Game = "External smoke test", RegionNote = "Execution machine; not the user's friend in Acre", OriginalTarget = "1.1.1.1", TargetIp = "1.1.1.1", Settings = new(12, 1000, 1000, 32, 1) };
        var b = new MeasurementBlock { Path = PathKind.Diagnostic, TargetIp = s.TargetIp, Settings = s.Settings }; s.Blocks.Add(b);
        await new MeasurementRunner(new IcmpProbe(), new WindowsRouteObserver()).RunAsync(b, null, default);
        var folder = Environment.GetEnvironmentVariable("ROTAGAME_EVIDENCE") ?? Path.GetTempPath(); Directory.CreateDirectory(folder);
        Export.WriteJson(s, Path.Combine(folder, "external-diagnostic.json")); Export.WriteCsv(s, Path.Combine(folder, "external-diagnostic.csv"));
        Check(b.Status == "complete" && b.Samples.Count == 12);
        Console.WriteLine($"  External responses: {b.Stats.Received}/{b.Stats.Sent}; apparent failure {b.Stats.FailurePercent:0.0}%; median {b.Stats.MedianMs:0.0}ms. No claim about Acre or game traffic.");
    });
}

tests.AddRange(DeviceTests.All());
tests.AddRange(BridgeOfferTests.All());
var failures = 0;
foreach (var (name, action) in tests)
{
    try { await action(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + "\n" + ex); }
}
Console.WriteLine($"\n{tests.Count - failures}/{tests.Count} passed. WireGuard end-to-end not tested: no VPS provided.");
Environment.ExitCode = failures == 0 ? 0 : 1;

sealed class FakeRoutes(params RouteSnapshot[] routes) : IRouteObserver
{
    private int _index;
    public RouteSnapshot Observe(IPAddress target) => routes[Math.Min(_index++, routes.Length - 1)];
}
sealed class FakeProbe : IProbe
{
    public int Calls { get; private set; }
    public Action? AfterSend { get; init; }
    public ProbeReply Reply { get; init; } = new(ProbeOutcome.Success, 20, "fake fixture");
    public Task<ProbeReply> SendAsync(IPAddress target, ProbeSettings settings, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Calls++; AfterSend?.Invoke(); return Task.FromResult(Reply); }
}
