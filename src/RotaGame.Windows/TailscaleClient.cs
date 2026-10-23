using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using RotaGame.Core;

namespace RotaGame.Windows;

public sealed record CommandResult(int ExitCode, string Output, string Error);
public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string file, IEnumerable<string> args, TimeSpan timeout, CancellationToken ct);
}

public sealed class CommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(string file, IEnumerable<string> args, TimeSpan timeout, CancellationToken ct)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct); lifetime.CancelAfter(timeout);
        var start = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Não foi possível iniciar o componente de conexão.");
        var stdout = process.StandardOutput.ReadToEndAsync(lifetime.Token); var stderr = process.StandardError.ReadToEndAsync(lifetime.Token);
        try
        {
            await process.WaitForExitAsync(lifetime.Token);
            return new(process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            throw new TimeoutException("O componente de conexão demorou para responder. Tente novamente.");
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            throw;
        }
    }
}

public sealed record TailPeer(string Ip, string Name, bool Online, bool Offered, string Link);
public sealed record TailStatus(string State, string CurrentExit, bool ExitOnline, string SelfName, bool SelfApproved, IReadOnlyList<TailPeer> Peers);

public sealed class TailscaleClient(ICommandRunner runner, string? executableOverride = null) : IAutomaticRoutes
{
    public static string? FindExecutable()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe");
        return File.Exists(path) ? path : null;
    }

    public bool Installed => executableOverride is not null || FindExecutable() is not null;
    private string Executable => executableOverride ?? FindExecutable() ?? throw new InvalidOperationException("Clique em Preparar conexão para instalar o componente necessário à ponte.");

    public static TailStatus ParseStatus(string json)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        static bool Flag(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
        static string Ip(JsonElement e)
        {
            if (!e.TryGetProperty("TailscaleIPs", out var ips) || ips.ValueKind != JsonValueKind.Array) return "";
            return ips.EnumerateArray().Select(v => (v.GetString() ?? "").Split('/')[0])
                .FirstOrDefault(s => IPAddress.TryParse(s, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? "";
        }
        var peers = new List<TailPeer>();
        if (root.TryGetProperty("Peer", out var map) && map.ValueKind == JsonValueKind.Object)
            foreach (var entry in map.EnumerateObject())
            {
                var e = entry.Value;
                var link = Str(e, "PeerRelay").Length > 0 ? "peer-relay" : Str(e, "CurAddr").Length > 0 ? "direct" : "relay";
                peers.Add(new(Ip(e), Str(e, "HostName"), Flag(e, "Online") && !Flag(e, "Expired"), Flag(e, "ExitNodeOption"), link));
            }
        var current = ""; var online = true;
        if (root.TryGetProperty("ExitNodeStatus", out var exit) && exit.ValueKind == JsonValueKind.Object)
        { current = Ip(exit); online = Flag(exit, "Online"); if (current.Length == 0) throw new InvalidOperationException("O ponto de saída atual não pôde ser identificado; a configuração foi preservada."); }
        root.TryGetProperty("Self", out var self);
        return new(Str(root, "BackendState"), current, online, self.ValueKind == JsonValueKind.Object ? Str(self, "HostName") : Environment.MachineName,
            self.ValueKind == JsonValueKind.Object && Flag(self, "ExitNodeOption"), peers);
    }

    public async Task<TailStatus> StatusAsync(CancellationToken ct)
    {
        var result = await runner.RunAsync(Executable, ["status", "--json"], TimeSpan.FromSeconds(8), ct);
        if (string.IsNullOrWhiteSpace(result.Output)) throw new InvalidOperationException("Abra Preparar conexão e entre na sua conta antes de usar a ponte.");
        return ParseStatus(result.Output);
    }

    public async Task<RouteReading> ReadAsync(CancellationToken ct)
    {
        if (!Installed) return new("", true, "Ponte ainda não preparada");
        var status = await StatusAsync(ct);
        if (status.State is not ("Running" or "Stopped" or "NeedsLogin" or "NeedsMachineAuth" or "Starting"))
            throw new InvalidOperationException("Não foi possível verificar o estado da conexão.");
        if (status.CurrentExit.Length > 0 && status.State != "Running")
            throw new InvalidOperationException("A ponte anterior está indisponível. Prepare a conexão antes de continuar.");
        return new(status.CurrentExit, status.CurrentExit.Length == 0 || status.ExitOnline, status.State);
    }

    public async Task<IReadOnlyList<RouteOption>> DiscoverAsync(CancellationToken ct)
    {
        if (!Installed) return [];
        var status = await StatusAsync(ct); if (status.State != "Running") return [];
        var candidates = status.Peers.Where(p => p.Online && p.Offered && p.Ip.Length > 0).OrderBy(p => p.Name).Take(8).ToList();
        var result = new List<RouteOption>();
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try { await runner.RunAsync(Executable, ["ping", "--c=3", "--timeout=2s", candidate.Ip], TimeSpan.FromSeconds(10), ct); }
            catch (TimeoutException) { continue; }
            var fresh = (await StatusAsync(ct)).Peers.FirstOrDefault(p => p.Ip == candidate.Ip);
            if (fresh is { Online: true, Offered: true, Link: "direct" }) result.Add(new(fresh.Ip, fresh.Name, fresh.Link));
        }
        return result;
    }

    public async Task SelectAsync(RouteOption option, CancellationToken ct)
    {
        if (!Installed)
        {
            if (!option.IsDirect) throw new InvalidOperationException("Ponte ainda não preparada.");
            return;
        }
        var before = await ReadAsync(ct);
        if (before.Selector == option.Id && before.Ready) return;
        var response = await runner.RunAsync(Executable, ["set", "--exit-node=" + option.Id], TimeSpan.FromSeconds(10), ct);
        if (response.ExitCode != 0) throw new InvalidOperationException("Não foi possível mudar o caminho. Verifique a conexão e a autorização do PC de casa.");
        for (var i = 0; i < 16; i++)
        {
            var actual = await ReadAsync(ct);
            if (actual.Selector == option.Id && actual.Ready) { await Task.Delay(750, ct); return; }
            await Task.Delay(400, ct);
        }
        throw new InvalidOperationException("O caminho selecionado não ficou pronto. A conexão anterior será restaurada.");
    }

    public async Task VerifyAsync(RouteOption option, CancellationToken ct)
    {
        var reading = await ReadAsync(ct);
        if (reading.Selector != option.Id || !reading.Ready) throw new InvalidOperationException("A conexão foi alterada ou a ponte ficou indisponível.");
        if (!option.IsDirect)
        {
            var peer = (await StatusAsync(ct)).Peers.FirstOrDefault(p => p.Ip == option.Id);
            if (peer is not { Online: true, Link: "direct" }) throw new InvalidOperationException("A ponte passou a usar um intermediário ou ficou indisponível.");
        }
    }

    public async Task OfferBridgeAsync(bool enable, CancellationToken ct)
    {
        var result = await runner.RunAsync(Executable, ["set", "--advertise-exit-node=" + (enable ? "true" : "false")], TimeSpan.FromSeconds(10), ct);
        if (result.ExitCode != 0) throw new InvalidOperationException("Não foi possível preparar este PC como ponte. Entre na conta primeiro.");
    }
}

public sealed class FileRecoveryStore(string path) : IRecoveryStore
{
    public RecoveryRecord? Load() => File.Exists(path) ? JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(path)) : null;
    public void Save(RecoveryRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(record)); File.Move(path + ".tmp", path, true);
    }
    public void Clear() { if (File.Exists(path)) File.Delete(path); }
}

public sealed class AutomaticMeter(IProbe probe, WindowsRouteObserver observer, TailscaleClient routes) : IAutomaticMeter
{
    public async Task<MeasurementBlock> MeasureAsync(string target, RouteOption route, ProbeSettings settings,
        IProgress<MeasurementProgress>? progress, CancellationToken ct)
    {
        await routes.VerifyAsync(route, ct);
        var snapshot = observer.Observe(IPAddress.Parse(target));
        var adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == snapshot.InterfaceId);
        var isTail = adapter is not null && (adapter.Description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) || adapter.Name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase));
        if (route.IsDirect && (!snapshot.IsPhysicalCandidate || isTail)) throw new InvalidOperationException("A conexão direta ainda usa uma VPN. Desconecte outras VPNs para comparar.");
        if (!route.IsDirect && !isTail) throw new InvalidOperationException("A ponte foi selecionada, mas o Windows ainda não envia este destino por ela.");
        var block = new MeasurementBlock { Path = PathKind.Diagnostic, TargetIp = target, Settings = settings };
        await new MeasurementRunner(probe, observer).RunAsync(block, progress, ct);
        try { await routes.VerifyAsync(route, ct); }
        catch { block.Status = "invalid"; throw; }
        return block;
    }
}
