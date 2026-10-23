using System.Net;
using System.Runtime.InteropServices;
using RotaGame.Core;
using RotaGame.Windows;

internal static class PanelWindowsTests
{
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    public static List<(string Name, Func<Task> Run)> All(bool firewallIntegration)
    {
        var tests = new List<(string, Func<Task>)>
        {
            ("FIREWALL: missing native rule is normal on first launch", () =>
            {
                object? policy = null, rules = null;
                try
                {
                    policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!); rules = ((dynamic)policy!).Rules;
                    Check(PeerPanelFirewall.FindRule(rules!, "RotaGame.ReadOnlyMissing." + Guid.NewGuid().ToString("N")) is null, "Missing native rule was not recognized.");
                }
                finally { Release(rules); Release(policy); }
                return Task.CompletedTask;
            }),
            ("FIREWALL: access denied is not mistaken for a missing rule", () =>
            {
                try { PeerPanelFirewall.FindRule(new DeniedRuleFixture(), "test"); throw new Exception("Expected access denial."); } catch (UnauthorizedAccessException) { }
                return Task.CompletedTask;
            }),
            ("REAL PAIRING: two fresh profiles recognize each other and transfer ICMP without manual selection", async () =>
            {
                var player = DeviceProfile.Create() with { Role = DeviceRole.Player, Name = "Jogador de teste" };
                var bridge = DeviceProfile.Create() with { Role = DeviceRole.Bridge, Name = "Ponte de teste" };
                var playerStore = new MemoryDeviceProfileStore(); var bridgeStore = new MemoryDeviceProfileStore();
                var playerInbox = new DeviceInbox(); var bridgeInbox = new DeviceInbox();
                var bridgeFrame = new DeviceFrame(1, Guid.NewGuid().ToString("N"), 1, bridge.DeviceId, bridge.Role, bridge.Name, null);
                await using var server = new PeerPanelServer(IPAddress.Loopback, ip => ip == "127.0.0.1" ? "authenticated-player" : null, () => bridgeFrame, bridgeInbox, 0, true);
                var reply = await new IcmpProbe().SendAsync(IPAddress.Loopback, new(1, 100, 500, 32, 0), default);
                Check(reply.Outcome == ProbeOutcome.Success, "No local ICMP response.");
                var now = DateTimeOffset.UtcNow;
                var measurements = new SharedMeasurements(true, "Teste local", "ICMP", "127.0.0.1", "", "Direta", now, reply.RttMs, null, reply.RttMs, 0, [new(now, reply.RttMs, reply.Outcome)], []) { SampleAgeMs = 0, Confirmed = true };
                var source = new DeviceFrame(1, Guid.NewGuid().ToString("N"), 1, player.DeviceId, player.Role, player.Name, measurements);
                var discovery = PeerMatching.Outgoing(player, "authenticated-bridge", true, source);
                Check(!discovery.PairRequested && discovery.Measurements is null, "Discovery disclosed measurements.");
                var response = await PeerPanelServer.ExchangeAsync(IPAddress.Loopback, discovery, default, server.BoundPort, true);
                playerInbox.Receive("authenticated-bridge", "127.0.0.1", response, DateTimeOffset.UtcNow);
                player = PeerMatching.Recognize(player, playerInbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Bridge)); playerStore.Save(player);
                Check(player.PartnerNodeId == "authenticated-bridge" && bridgeInbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player).Length == 0, "Automatic recognition or discovery privacy failed.");
                var paired = PeerMatching.Outgoing(playerStore.Load(), "authenticated-bridge", true, source with { Sequence = 2 });
                await PeerPanelServer.ExchangeAsync(IPAddress.Loopback, paired, default, server.BoundPort, true);
                bridge = PeerMatching.Recognize(bridge, bridgeInbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player)); bridgeStore.Save(bridge);
                var received = bridgeInbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player).Single();
                Check(bridgeStore.Load().PartnerNodeId == "authenticated-player" && received.Frame.Measurements?.AverageMs == reply.RttMs, "Bridge did not recognize the player or measurements changed.");
            }),
            ("PAIRING: multiple bridges remain a choice and a selected partner is preserved", () =>
            {
                var profile = DeviceProfile.Create() with { Role = DeviceRole.Player }; var frame = new DeviceFrame(1, Guid.NewGuid().ToString("N"), 1, Guid.NewGuid().ToString("N"), DeviceRole.Bridge, "test", null);
                ReceivedDevice[] candidates = [new("one", "100.64.0.1", frame, DateTimeOffset.UtcNow), new("two", "100.64.0.2", frame, DateTimeOffset.UtcNow)];
                Check(PeerMatching.Recognize(profile, candidates).PartnerNodeId is null, "Ambiguous bridges were silently paired.");
                Check(PeerMatching.Recognize(profile with { PartnerNodeId = "one" }, candidates).PartnerNodeId == "one", "An existing partner was replaced.");
                return Task.CompletedTask;
            })
        };
        if (firewallIntegration) tests.Add(("REAL FIREWALL: first creation, readback, private listener and cleanup", FirewallAsync));
        return tests;
    }
    private static async Task FirewallAsync()
    {
        var status = await new TailscaleClient(new CommandRunner()).StatusAsync(default); var local = IPAddress.Parse(status.SelfIp);
        using var firewall = new PeerPanelFirewall(Guid.NewGuid().ToString("N"));
        Check(firewall.ReadScope() is null, "Probe rule unexpectedly exists.");
        firewall.Configure(status.SelfIp, [status.SelfIp]); var scope = firewall.ReadScope() ?? throw new Exception("Native rule was not created.");
        Check(scope.Enabled && !scope.EdgeTraversal && scope.Action == 1 && scope.Direction == 1 && scope.Protocol == 6 && scope.LocalPorts == "52941" && scope.Application == Environment.ProcessPath, "Native rule has incorrect scope.");
        Check(scope.LocalAddresses.Split('/')[0] == status.SelfIp && scope.RemoteAddresses.Split('/')[0] == status.SelfIp, "Native rule addresses widened.");
        firewall.Configure(status.SelfIp, [status.SelfIp]); Check(firewall.ReadScope() == scope, "Repeated configuration changed the scope.");
        var bridge = new DeviceFrame(1, Guid.NewGuid().ToString("N"), 1, Guid.NewGuid().ToString("N"), DeviceRole.Bridge, "Native private test", null);
        var inbox = new DeviceInbox();
        await using (var server = new PeerPanelServer(local, ip => ip == status.SelfIp ? "local-only-test" : null, () => bridge, inbox))
        {
            var player = new DeviceFrame(1, Guid.NewGuid().ToString("N"), 1, Guid.NewGuid().ToString("N"), DeviceRole.Player, "Native test player", null) { PairRequested = true };
            await PeerPanelServer.ExchangeAsync(local, player, default);
            Check(inbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player).Length == 1, "Private native listener did not exchange identification.");
        }
        firewall.Dispose(); Check(firewall.ReadScope() is null, "Probe rule was left behind.");
        Console.WriteLine("  Native scoped rule and TCP to this PC's private adapter passed; this does not prove inbound access from a second PC.");
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
public sealed class DeniedRuleFixture { public object Item(string name) => throw new UnauthorizedAccessException("Controlled access denial."); }
