using System.Text.Json;

namespace RotaGame.Core;

public sealed record BridgeOfferState(string NodeId, bool Running, bool Advertising, bool Approved);
public interface IBridgeOffers
{
    Task<BridgeOfferState> ReadOfferAsync(CancellationToken ct);
    Task SetOfferAsync(bool enabled, CancellationToken ct);
}
public sealed record BridgeOfferRecord(string NodeId);
public interface IBridgeOfferStore { BridgeOfferRecord? Load(); void Save(BridgeOfferRecord record); void Clear(); }
public sealed class FileBridgeOfferStore(string path) : IBridgeOfferStore
{
    public BridgeOfferRecord? Load() => File.Exists(path) ? JsonSerializer.Deserialize<BridgeOfferRecord>(File.ReadAllText(path)) ?? throw new InvalidDataException("Recuperação da ponte inválida.") : null;
    public void Save(BridgeOfferRecord record) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(record)); File.Move(path + ".tmp", path, true); }
    public void Clear() { if (File.Exists(path)) File.Delete(path); }
}
public sealed class MemoryBridgeOfferStore : IBridgeOfferStore
{
    private BridgeOfferRecord? _record;
    public BridgeOfferRecord? Load() => _record;
    public void Save(BridgeOfferRecord record) => _record = record;
    public void Clear() => _record = null;
}

// Only undo an advertisement introduced by this activation, on this node.
// A manually configured/approved advertisement belongs to the user.
public sealed class BridgeOfferLease(IBridgeOffers offers, IBridgeOfferStore store)
{
    public bool PreservedExisting { get; private set; }
    public async Task<BridgeOfferState> BeginAsync(CancellationToken ct)
    {
        if (store.Load() is not null) throw new InvalidOperationException("Restaure a ponte anterior antes de ativar novamente.");
        var state = await offers.ReadOfferAsync(ct);
        if (!state.Running || string.IsNullOrWhiteSpace(state.NodeId)) throw new InvalidOperationException("A identidade conectada da ponte ainda não foi confirmada.");
        PreservedExisting = state.Advertising;
        if (state.Advertising) return state;
        store.Save(new(state.NodeId));
        await offers.SetOfferAsync(true, ct);
        return state with { Advertising = true };
    }
    public async Task RestoreAsync(CancellationToken ct)
    {
        var record = store.Load(); if (record is null) return;
        var actual = await offers.ReadOfferAsync(ct);
        if (!actual.Running || string.IsNullOrWhiteSpace(actual.NodeId)) throw new InvalidOperationException("A ponte precisa estar conectada para restaurar a oferta com segurança.");
        if (actual.NodeId == record.NodeId && actual.Advertising) await offers.SetOfferAsync(false, ct);
        store.Clear();
    }
}
public enum BridgeReadiness { Inactive, Disconnected, Confirming, Ready, PermissionUnconfirmed }
public static class BridgeConfirmation
{
    public static BridgeReadiness Read(bool offering, bool running, bool approved, bool confirmedByPlayer, TimeSpan sinceStart) =>
        !offering ? BridgeReadiness.Inactive : !running ? BridgeReadiness.Disconnected : approved || confirmedByPlayer ? BridgeReadiness.Ready :
        sinceStart < TimeSpan.FromSeconds(8) ? BridgeReadiness.Confirming : BridgeReadiness.PermissionUnconfirmed;
}
