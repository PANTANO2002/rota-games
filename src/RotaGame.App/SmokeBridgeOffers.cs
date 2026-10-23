using RotaGame.Core;
namespace RotaGame.App;
// Explicit test modes only. Never calls the installed transport.
internal sealed class SmokeBridgeOffers : IBridgeOffers
{
    internal bool Advertising, Approved;
    internal int Writes;
    public Task<BridgeOfferState> ReadOfferAsync(CancellationToken ct) => Task.FromResult(new BridgeOfferState("controlled-node", true, Advertising, Approved));
    public Task SetOfferAsync(bool enabled, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Advertising = enabled; Writes++; return Task.CompletedTask; }
}
