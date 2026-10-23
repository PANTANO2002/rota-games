using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using RotaGame.Core;
using RotaGame.Windows;

internal static class DeviceTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Device assertion failed."); }
    private static DeviceFrame Frame(DeviceRole role, long sequence = 1) => new(1, Guid.NewGuid().ToString("N"), sequence, Guid.NewGuid().ToString("N"), role, role == DeviceRole.Bridge ? "Ponte de teste" : "Jogador de teste", null) { PairRequested = role == DeviceRole.Player };
    private static async Task Reject(Func<Task> action) { try { await action(); } catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException or OperationCanceledException or InvalidOperationException) { return; } throw new Exception("Expected rejection."); }
    public static List<(string Name, Func<Task> Run)> All()
    {
        var tests = new List<(string, Func<Task>)>();
        void Test(string name, Action action) => tests.Add((name, () => { action(); return Task.CompletedTask; }));
        void Async(string name, Func<Task> action) => tests.Add((name, action));
        Test("DEVICE: role and selected peer persist across a fresh load", () =>
        {
            var path = Path.Combine(Path.GetTempPath(), "RotaGame-device-test-" + Guid.NewGuid().ToString("N"), "device.json");
            var store = new DeviceProfileStore(path); var original = store.Load(); Check(original.Role == DeviceRole.Unknown);
            store.Save(original with { Role = DeviceRole.Bridge, PartnerNodeId = "authenticated-node" });
            var loaded = new DeviceProfileStore(path).Load(); Check(loaded.DeviceId == original.DeviceId && loaded.Role == DeviceRole.Bridge && loaded.PartnerNodeId == "authenticated-node");
        });
        Test("SETUP: connected login immediately advances to the next action", () =>
        {
            Check(ConnectionSteps.Next(DeviceRole.Unknown, true, true, true, true) == ConnectionStep.ChooseRole);
            Check(ConnectionSteps.Next(DeviceRole.Bridge, false, false, false, false) == ConnectionStep.SignIn);
            Check(ConnectionSteps.Next(DeviceRole.Bridge, true, false, false, false) == ConnectionStep.StartBridge);
            Check(ConnectionSteps.Next(DeviceRole.Bridge, true, true, false, false) == ConnectionStep.ApproveBridge);
            Check(ConnectionSteps.Next(DeviceRole.Bridge, true, true, true, false) == ConnectionStep.ShareBridge);
            Check(ConnectionSteps.Next(DeviceRole.Bridge, true, true, true, true) == ConnectionStep.Ready);
            Check(ConnectionSteps.Next(DeviceRole.Player, true, false, false, false) == ConnectionStep.AcceptBridge);
            Check(ConnectionSteps.Next(DeviceRole.Player, true, false, false, true) == ConnectionStep.Ready);
        });
        Test("INVITE: official node shares and old user invites are accepted", () =>
        {
            foreach (var host in new[] { "login.tailscale.com", "console.tailscale.com" })
                foreach (var path in new[] { "/uinv/abc", "/admin/invite/abc" }) Check(ConnectionInvites.IsValid("https://" + host + path));
            Check(ConnectionInvites.IsNetworkInvite("https://login.tailscale.com/uinv/abc"));
            Check(!ConnectionInvites.IsNetworkInvite("https://console.tailscale.com/admin/invite/abc"));
        });
        Test("INVITE: foreign hosts, credentials, non-HTTPS and empty invitations rejected", () =>
        {
            foreach (var s in new[] { "http://login.tailscale.com/uinv/abc", "https://login.tailscale.com.evil/uinv/abc", "https://user@console.tailscale.com/admin/invite/abc", "https://console.tailscale.com/admin/invite/", "https://console.tailscale.com/admin/machines", "https://login.tailscale.com:8443/uinv/abc" }) Check(!ConnectionInvites.IsValid(s));
        });
        Async("LOGIN: running service completes while the CLI is still waiting", async () =>
        {
            var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
            await LoginCompletion.WaitAsync(pending.Task, () => Task.FromResult(++calls >= 2), new CancellationTokenSource(2000).Token, pollMs: 10);
            Check(!pending.Task.IsCompleted && calls == 2);
        });
        Async("LOGIN: a process exiting without connection cannot report success", async () => await Reject(() => LoginCompletion.WaitAsync(Task.CompletedTask, () => Task.FromResult(false), default)));
        Async("LOGIN: explicit network change waits for its process", async () =>
        {
            var process = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiting = LoginCompletion.WaitAsync(process.Task, () => Task.FromResult(true), new CancellationTokenSource(2000).Token, true, 10);
            Check(!waiting.IsCompleted); process.SetResult(); await waiting;
        });
        Async("LOGIN: cancellation exits a pending authorization", async () =>
        {
            using var cts = new CancellationTokenSource(25);
            await Reject(() => LoginCompletion.WaitAsync(new TaskCompletionSource().Task, () => Task.FromResult(false), cts.Token, pollMs: 10));
        });
        Test("PANEL: rejects malformed identities, protocol and nonfinite measurements", () =>
        {
            var good = Frame(DeviceRole.Player); good.Validate();
            var values = new[] { good with { Protocol = 2 }, good with { Name = "bad\nname" }, good with { Role = DeviceRole.Unknown }, good with { Sequence = 0 }, good with { DeviceId = "not-a-device" } };
            foreach (var value in values) { var rejected = false; try { value.Validate(); } catch (InvalidDataException) { rejected = true; } Check(rejected); }
            var m = new SharedMeasurements(true, "Test", "", "127.0.0.1", "", "Direta", DateTimeOffset.UtcNow, double.NaN, 0, 0, 0, [], []);
            var bad = good with { Measurements = m }; var thrown = false; try { bad.Validate(); } catch (InvalidDataException) { thrown = true; } Check(thrown);
        });
        Test("PANEL: repeated frames rejected and stale data expires by receipt time", () =>
        {
            var inbox = new DeviceInbox(); var frame = Frame(DeviceRole.Player); var at = DateTimeOffset.UtcNow; inbox.Receive("node", "100.64.0.2", frame, at);
            var duplicate = false; try { inbox.Receive("node", "100.64.0.2", frame, at); } catch (InvalidDataException) { duplicate = true; } Check(duplicate);
            Check(inbox.Fresh(at.AddSeconds(9), DeviceRole.Player).Length == 1 && inbox.Fresh(at.AddSeconds(10), DeviceRole.Player).Length == 0);
        });
        Test("PANEL: production rejects wildcard and loopback binding", () =>
        {
            foreach (var ip in new[] { IPAddress.Any, IPAddress.Loopback, IPAddress.IPv6Any })
            {
                var rejected = false; try { _ = new PeerPanelServer(ip, _ => "node", () => Frame(DeviceRole.Bridge), new DeviceInbox(), 0); } catch (InvalidOperationException) { rejected = true; } Check(rejected);
            }
            Check(!PeerPanelServer.IsTailAddress(IPAddress.Parse("1.1.1.1")) && !PeerPanelServer.IsTailAddress(IPAddress.Parse("100.128.0.1")) && PeerPanelServer.IsTailAddress(IPAddress.Parse("100.64.0.1")));
        });
        Async("PANEL: private client refuses a public destination before connecting", async () => await Reject(() => PeerPanelServer.ExchangeAsync(IPAddress.Parse("1.1.1.1"), Frame(DeviceRole.Player), default)));
        Async("REAL PANEL: local ICMP results arrive intact on the bridge over TCP", async () =>
        {
            var reply = await new IcmpProbe().SendAsync(IPAddress.Loopback, new(1, 100, 300, 32, 0), default); Check(reply.Outcome == ProbeOutcome.Success && reply.RttMs is not null);
            var now = DateTimeOffset.UtcNow;
            var frame = Frame(DeviceRole.Player) with { Measurements = new(true, "Loopback real", "Teste", "127.0.0.1", "", "Direta", now, reply.RttMs, null, reply.RttMs, 0, [new(now, reply.RttMs, reply.Outcome)], []) { SampleAgeMs = 0, Confirmed = true } };
            var inbox = new DeviceInbox(); var bridge = Frame(DeviceRole.Bridge);
            await using var server = new PeerPanelServer(IPAddress.Loopback, ip => ip == "127.0.0.1" ? "authenticated-player" : null, () => bridge, inbox, 0, true);
            var response = await PeerPanelServer.ExchangeAsync(IPAddress.Loopback, frame, default, server.BoundPort, true);
            var received = inbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player).Single();
            Check(response.DeviceId == bridge.DeviceId && received.NodeId == "authenticated-player" && received.Frame.Measurements!.AverageMs == reply.RttMs);
        });
        Async("PANEL: identity-only discovery cannot publish measurements or select a partner", async () =>
        {
            var inbox = new DeviceInbox(); await using var server = new PeerPanelServer(IPAddress.Loopback, _ => "node", () => Frame(DeviceRole.Bridge), inbox, 0, true);
            await PeerPanelServer.ExchangeAsync(IPAddress.Loopback, Frame(DeviceRole.Player) with { PairRequested = false }, default, server.BoundPort, true);
            Check(inbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player).Length == 0);
        });
        Async("PANEL: a peer absent from the authenticated map is refused", async () =>
        {
            var inbox = new DeviceInbox(); await using var server = new PeerPanelServer(IPAddress.Loopback, _ => null, () => Frame(DeviceRole.Bridge), inbox, 0, true);
            await Reject(() => PeerPanelServer.ExchangeAsync(IPAddress.Loopback, Frame(DeviceRole.Player), default, server.BoundPort, true));
            Check(inbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player).Length == 0);
        });
        Async("PANEL: oversized frames are closed without allocating their claimed size", async () =>
        {
            var inbox = new DeviceInbox(); await using var server = new PeerPanelServer(IPAddress.Loopback, _ => "node", () => Frame(DeviceRole.Bridge), inbox, 0, true);
            using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
            var bytes = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(bytes, PeerPanelServer.MaxBytes + 1); await client.GetStream().WriteAsync(bytes);
            var count = await client.GetStream().ReadAsync(new byte[1], new CancellationTokenSource(2000).Token); Check(count == 0 && inbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player).Length == 0);
        });
        return tests;
    }
}
