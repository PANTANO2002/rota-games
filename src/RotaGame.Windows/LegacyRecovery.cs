using RotaGame.Core;

namespace RotaGame.Windows;

public static class LegacyRecovery
{
    // Tailscale is used only to undo an interrupted mutation from 0.8.
    // The normal 0.9 flow never installs, signs in, advertises or uses a peer.
    public static async Task RestoreAsync(string directory, CancellationToken ct)
    {
        var route = new FileRecoveryStore(Path.Combine(directory, "connection-recovery.json"));
        var bridge = new FileBridgeOfferStore(Path.Combine(directory, "bridge-recovery.json"));
        if (route.Load() is null && bridge.Load() is null) return;
        var client = new TailscaleClient(new CommandRunner());
        if (!client.Installed) throw new InvalidOperationException("Há uma alteração da versão antiga pendente. Restaure-a no Tailscale antes de comparar novos caminhos; o RotaGame não reinstala esse componente.");
        await new RouteLease(client, route).RestoreAsync(ct);
        await new BridgeOfferLease(client, bridge).RestoreAsync(ct);
    }
}
