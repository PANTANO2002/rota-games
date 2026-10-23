using RotaGame.Core;
using RotaGame.Windows;

internal static class BridgeOfferTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Bridge offer assertion failed."); }
    public static List<(string Name, Func<Task> Run)> All()
    {
        return [
            ("BRIDGE: existing manual advertisement is neither rewritten nor withdrawn on repeated cycles", async () =>
            {
                var offers = new Offers { State = new("same-node", true, true, true) }; var store = new MemoryBridgeOfferStore(); var lease = new BridgeOfferLease(offers, store);
                for (var i = 0; i < 3; i++) { await lease.BeginAsync(default); await lease.RestoreAsync(default); }
                Check(offers.Writes.Count == 0 && offers.State.Advertising && store.Load() is null && lease.PreservedExisting);
            }),
            ("BRIDGE: restores only the advertisement introduced by this activation", async () =>
            {
                var offers = new Offers(); var store = new MemoryBridgeOfferStore(); var lease = new BridgeOfferLease(offers, store);
                await lease.BeginAsync(default); Check(offers.State.Advertising && store.Load() is not null);
                await new BridgeOfferLease(offers, store).RestoreAsync(default); Check(!offers.State.Advertising && store.Load() is null && offers.Writes.SequenceEqual(new[] { true, false }));
            }),
            ("BRIDGE: a partial mutation is journaled and recoverable", async () =>
            {
                var offers = new Offers { FailAfterWrite = true }; var store = new MemoryBridgeOfferStore(); var lease = new BridgeOfferLease(offers, store);
                try { await lease.BeginAsync(default); throw new Exception("Expected partial failure"); } catch (IOException) { }
                Check(offers.State.Advertising && store.Load() is not null); offers.FailAfterWrite = false;
                await lease.RestoreAsync(default); Check(!offers.State.Advertising && store.Load() is null);
            }),
            ("BRIDGE: recovery cannot withdraw a different account or node", async () =>
            {
                var offers = new Offers(); var store = new MemoryBridgeOfferStore(); var lease = new BridgeOfferLease(offers, store);
                await lease.BeginAsync(default); offers.State = offers.State with { NodeId = "other-account-node" };
                await lease.RestoreAsync(default); Check(offers.State.Advertising && offers.Writes.Count == 1 && store.Load() is null);
            }),
            ("BRIDGE: offline recovery retains its journal for retry", async () =>
            {
                var offers = new Offers(); var store = new MemoryBridgeOfferStore(); var lease = new BridgeOfferLease(offers, store); await lease.BeginAsync(default);
                offers.State = offers.State with { Running = false }; try { await lease.RestoreAsync(default); throw new Exception("Expected offline rejection"); } catch (InvalidOperationException) { }
                Check(store.Load() is not null && offers.Writes.Count == 1);
            }),
            ("BRIDGE: startup propagation is distinct from unconfirmed permission and from disconnected account", () =>
            {
                Check(BridgeConfirmation.Read(true, true, false, false, TimeSpan.Zero) == BridgeReadiness.Confirming);
                Check(BridgeConfirmation.Read(true, true, true, false, TimeSpan.Zero) == BridgeReadiness.Ready);
                Check(BridgeConfirmation.Read(true, true, false, true, TimeSpan.FromSeconds(30)) == BridgeReadiness.Ready);
                Check(BridgeConfirmation.Read(true, true, false, false, TimeSpan.FromSeconds(9)) == BridgeReadiness.PermissionUnconfirmed);
                Check(BridgeConfirmation.Read(true, false, true, true, TimeSpan.Zero) == BridgeReadiness.Disconnected);
                Check(ConnectionSteps.Next(DeviceRole.Bridge, true, true, false, false, true) == ConnectionStep.ConfirmBridge);
                Check(ConnectionSteps.Next(DeviceRole.Bridge, true, true, false, true) == ConnectionStep.Ready);
                return Task.CompletedTask;
            }),
            ("BRIDGE: reads advertised defaults independently from approval and rejects partial families", () =>
            {
                var status = new TailStatus("Running", "", true, "test", true, []) { SelfNodeId = "stable-node" };
                Check(TailscaleClient.ParseOffer(status, """{"AdvertiseRoutes":["0.0.0.0/0","::/0","10.0.0.0/24"]}""").Advertising);
                Check(!TailscaleClient.ParseOffer(status, """{"AdvertiseRoutes":null}""").Advertising);
                try { TailscaleClient.ParseOffer(status, """{"AdvertiseRoutes":["0.0.0.0/0"]}"""); throw new Exception("Partial defaults should fail"); } catch (InvalidOperationException) { }
                return Task.CompletedTask;
            })
        ];
    }
    private sealed class Offers : IBridgeOffers
    {
        public BridgeOfferState State = new("same-node", true, false, false);
        public readonly List<bool> Writes = [];
        public bool FailAfterWrite;
        public Task<BridgeOfferState> ReadOfferAsync(CancellationToken ct) => Task.FromResult(State);
        public Task SetOfferAsync(bool enabled, CancellationToken ct) { Writes.Add(enabled); State = State with { Advertising = enabled }; if (FailAfterWrite) throw new IOException("Controlled partial failure"); return Task.CompletedTask; }
    }
}
