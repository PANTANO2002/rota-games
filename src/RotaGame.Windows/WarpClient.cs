using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RotaGame.Core;

namespace RotaGame.Windows;

public sealed record WarpState(bool Connected, bool Ready, bool Traffic, string? Identity);
public sealed class WarpClient(ICommandRunner runner, string? executable = null, Func<bool>? consent = null) : IAutomaticRoutes, IRouteDiscoveryDiagnostics
{
    public static RouteOption Warp { get; } = new("warp", "Cloudflare WARP", "warp");
    public static string? FindExecutable()
    {
        var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cloudflare", "Cloudflare WARP", "warp-cli.exe");
        return File.Exists(file) ? file : null;
    }
    public bool Installed => executable is not null || FindExecutable() is not null;
    private string Exe => executable ?? FindExecutable() ?? throw new InvalidOperationException("Use Preparar rota gratuita para instalar o componente oficial WARP.");
    private readonly List<string> _notes = [];
    public IReadOnlyList<string> DiscoveryNotes => _notes;
    private async Task<string> Query(CancellationToken ct, params string[] args)
    {
        var result = await runner.RunAsync(Exe, Flags().Concat(args), TimeSpan.FromSeconds(8), ct);
        if (result.ExitCode != 0) throw new InvalidOperationException("O WARP ainda não está pronto. Abra Preparar rota gratuita, conclua a instalação e aceite os termos no aplicativo oficial da Cloudflare.");
        return result.Output;
    }
    private IEnumerable<string> Flags() => (consent ?? WarpConsent.Granted)() ? ["--no-ansi", "--no-paginate", "--accept-tos"] : ["--no-ansi", "--no-paginate"];
    public async Task<WarpState> PrepareRegistrationAsync(CancellationToken ct)
    {
        if (!(consent ?? WarpConsent.Granted)()) throw new InvalidOperationException("Leia e aceite os termos da Cloudflare em Preparar rota gratuita antes de criar o registro do dispositivo.");
        var current = await runner.RunAsync(Exe, Flags().Concat(["registration", "show"]), TimeSpan.FromSeconds(8), ct);
        if (current.ExitCode != 0)
        {
            if (!Regex.IsMatch(current.Output + " " + current.Error, @"Missing Registration|not registered|RegistrationMissing", RegexOptions.IgnoreCase))
                throw new InvalidOperationException("O serviço WARP ainda não respondeu. Conclua o instalador oficial e tente Preparar rota gratuita novamente.");
            await Query(ct, "registration", "new");
        }
        return await StateAsync(ct);
    }
    public static WarpState Parse(string status, string settings, string registration)
    {
        var match = Regex.Match(status, @"(?:Status update:|Status:)\s*(Connected|Disconnected|Connecting|Disconnecting)\b", RegexOptions.IgnoreCase);
        if (!match.Success) throw new InvalidDataException("Estado do WARP não reconhecido. Atualize o componente oficial antes de comparar.");
        var mode = Regex.Match(settings, @"(?:Service mode:|Mode:)\s*([A-Za-z]+)", RegexOptions.IgnoreCase).Groups[1].Value;
        var traffic = mode is "Warp" or "WarpWithDnsOverHttps" or "WarpWithDnsOverTls" or "TunnelOnly";
        if (!traffic && mode is not ("DnsOverHttps" or "DnsOverTls")) throw new InvalidDataException("Modo do WARP não reconhecido. A configuração existente foi preservada.");
        var account = Regex.Match(registration, @"Account type:\s*(\w+)", RegexOptions.IgnoreCase).Groups[1].Value;
        if (account is not ("Limited" or "Free")) throw new InvalidOperationException("A comparação exige o WARP pessoal gratuito. Contas corporativas, pagas ou desconhecidas são preservadas.");
        var id = Regex.Match(registration, @"(?im)^\s*(?:Registration ID|Device ID|ID):\s*([0-9a-f-]{36})\s*$").Groups[1].Value;
        if (!Guid.TryParse(id, out var guid)) throw new InvalidDataException("Não consegui confirmar a identidade do WARP. Nenhuma configuração foi alterada.");
        var state = match.Groups[1].Value.ToLowerInvariant();
        return new(state == "connected", state is "connected" or "disconnected", traffic, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(guid.ToString("N")))));
    }
    public async Task<WarpState> StateAsync(CancellationToken ct)
    {
        // Registration output is parsed in memory only; it is never logged or exported.
        var status = await Query(ct, "status"); var settings = await Query(ct, "settings"); var registration = await Query(ct, "registration", "show");
        return Parse(status, settings, registration);
    }
    public async Task<RouteReading> ReadAsync(CancellationToken ct)
    {
        if (!Installed) return new("", true, "WARP não instalado; conexão atual será medida sem alterações.");
        var state = await StateAsync(ct);
        return new(state.Connected && state.Traffic ? "warp" : "", state.Ready, state.Traffic ? "WARP pessoal" : "Somente DNS; sem túnel de tráfego") { Identity = state.Identity };
    }
    public async Task<IReadOnlyList<RouteOption>> DiscoverAsync(CancellationToken ct)
    {
        _notes.Clear();
        if (!Installed) { _notes.Add("WARP não instalado. Preparar rota gratuita habilita uma alternativa; por enquanto só a conexão atual será medida."); return []; }
        var state = await StateAsync(ct);
        if (!state.Ready || !state.Traffic) { _notes.Add("Selecione WARP no aplicativo oficial: o modo somente DNS não cria outra rota para o jogo."); return []; }
        return [Warp];
    }
    public async Task SelectAsync(RouteOption option, CancellationToken ct)
    {
        if (!option.IsDirect && option.Id != Warp.Id) throw new ArgumentException("Caminho não suportado.");
        if (!Installed) { if (option.IsDirect) return; throw new InvalidOperationException("Prepare a rota gratuita antes de usar WARP."); }
        var original = await StateAsync(ct);
        if (!original.Traffic)
        {
            if (option.IsDirect) return;
            throw new InvalidOperationException("O WARP está no modo somente DNS. Ative o modo WARP no componente oficial.");
        }
        if (original.Ready && original.Connected == !option.IsDirect) return;
        await Query(ct, option.IsDirect ? "disconnect" : "connect");
        for (var i = 0; i < 25; i++)
        {
            ct.ThrowIfCancellationRequested(); var actual = await StateAsync(ct);
            if (actual.Identity != original.Identity || !actual.Traffic) throw new InvalidOperationException("A configuração do WARP mudou fora do RotaGame. Ela será preservada.");
            if (actual.Ready && actual.Connected == !option.IsDirect) { await Task.Delay(1000, ct); return; }
            await Task.Delay(400, ct);
        }
        throw new TimeoutException("O WARP não confirmou o caminho. A preferência anterior será restaurada.");
    }
    public async Task VerifyAsync(RouteOption route, CancellationToken ct)
    {
        var read = await ReadAsync(ct);
        if (!read.Ready || read.Selector != route.Id) throw new InvalidOperationException("O caminho ativo mudou. Compare novamente.");
    }
    public static bool IsWarp(RouteSnapshot route) => (route.Name + " " + route.Description).Contains("Cloudflare", StringComparison.OrdinalIgnoreCase) || (route.Name + " " + route.Description).Contains("WARP", StringComparison.OrdinalIgnoreCase);
}

public sealed class WarpMeter(IProbe probe, IRouteObserver observer, WarpClient client) : IAutomaticMeter
{
    public async Task<MeasurementBlock> MeasureAsync(string target, RouteOption route, ProbeSettings settings, IProgress<MeasurementProgress>? progress, CancellationToken ct)
    {
        await client.VerifyAsync(route, ct); var before = observer.Observe(IPAddress.Parse(target));
        if (route.IsDirect ? !before.IsPhysicalCandidate || WarpClient.IsWarp(before) || (before.Name + before.Description).Contains("Tailscale", StringComparison.OrdinalIgnoreCase) : !WarpClient.IsWarp(before))
            throw new InvalidOperationException("A interface do destino não confirma este caminho. A comparação não será apresentada como válida.");
        var block = new MeasurementBlock { Path = PathKind.Diagnostic, TargetIp = target, Settings = settings };
        await new MeasurementRunner(probe, observer).RunAsync(block, progress, ct); await client.VerifyAsync(route, ct); return block;
    }
}
