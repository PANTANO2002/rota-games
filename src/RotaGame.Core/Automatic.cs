namespace RotaGame.Core;

public sealed record RouteOption(string Id, string Name, string Link = "direct")
{
    public bool IsDirect => Id.Length == 0;
    public static RouteOption Direct { get; } = new("", "Conexão direta");
}

public sealed record ObservedDestination(string Ip, int Port, string Protocol, int ProcessId, string ProcessName, long Packets);
public sealed record RouteReading(string Selector, bool Ready, string Detail) { public string? Identity { get; init; } }

public interface IAutomaticRoutes
{
    Task<RouteReading> ReadAsync(CancellationToken ct);
    Task<IReadOnlyList<RouteOption>> DiscoverAsync(CancellationToken ct);
    Task SelectAsync(RouteOption option, CancellationToken ct);
}

public interface IRouteDiscoveryDiagnostics { IReadOnlyList<string> DiscoveryNotes { get; } }

public interface IAutomaticMeter
{
    Task<MeasurementBlock> MeasureAsync(string target, RouteOption route, ProbeSettings settings,
        IProgress<MeasurementProgress>? progress, CancellationToken ct);
}

public sealed record RecoveryRecord(string OriginalSelector, string LastAppliedSelector, DateTimeOffset CreatedAt, string? PreviousSelector = null, string? Identity = null);
public interface IRecoveryStore
{
    RecoveryRecord? Load();
    void Save(RecoveryRecord record);
    void Clear();
}

// Owns only the exit-node preference changed by this activation. Recovery is
// journaled before mutation and never overwrites an unrelated external change.
public sealed class RouteLease(IAutomaticRoutes routes, IRecoveryStore store)
{
    private RecoveryRecord? _record;
    public bool Active => _record is not null;

    public async Task BeginAsync(CancellationToken ct)
    {
        if (store.Load() is not null) throw new InvalidOperationException("Existe uma conexão anterior para restaurar. Clique em Desativar primeiro.");
        var current = await routes.ReadAsync(ct);
        _record = new(current.Selector, current.Selector, DateTimeOffset.UtcNow, Identity: current.Identity);
        store.Save(_record);
    }

    public async Task ApplyAsync(RouteOption option, CancellationToken ct)
    {
        if (_record is null) throw new InvalidOperationException("Sessão de conexão não iniciada.");
        var current = await routes.ReadAsync(ct);
        if (_record.Identity is not null && current.Identity != _record.Identity) throw new InvalidOperationException("A identificação da conexão mudou. A nova configuração foi preservada.");
        if (current.Selector != _record.LastAppliedSelector && current.Selector != _record.PreviousSelector)
            throw new InvalidOperationException("A conexão foi alterada fora do RotaGame. A alteração externa será preservada.");
        _record = _record with { LastAppliedSelector = option.Id, PreviousSelector = current.Selector };
        store.Save(_record);
        await routes.SelectAsync(option, ct);
    }

    public async Task RestoreAsync(CancellationToken ct)
    {
        var record = _record ?? store.Load();
        if (record is null) return;
        var current = await routes.ReadAsync(ct);
        if (record.Identity is not null && current.Identity != record.Identity) throw new InvalidOperationException("A identificação da conexão mudou. A nova configuração foi preservada; restauração pendente.");
        if (current.Selector == record.LastAppliedSelector || current.Selector == record.OriginalSelector || current.Selector == record.PreviousSelector)
            await routes.SelectAsync(new(record.OriginalSelector, "Conexão anterior"), ct);
        // A manual change outside this program wins over a stale recovery record.
        store.Clear(); _record = null;
    }
}

public sealed record RouteTrial(RouteOption Route, MeasurementBlock Block);
public sealed record RouteAssessment(RouteOption Route, Statistics Stats, bool Eligible, string Reason);
public sealed record AutomaticChoice(RouteOption Winner, string Reason, Statistics DirectStats, Statistics WinnerStats)
{
    public IReadOnlyList<RouteAssessment> Assessments { get; init; } = [];
}

public static class AutomaticPolicy
{
    public static AutomaticChoice Choose(IEnumerable<RouteTrial> input)
    {
        var entries = input.ToList();
        var trials = entries.Where(t => t.Block.Status == "complete" && t.Block.Samples.Count == t.Block.Settings.Count && t.Block.Stats.LocalErrors == 0).DistinctBy(t => t.Block.Id).ToList();
        var directBlocks = trials.Where(t => t.Route.IsDirect).Select(t => t.Block).ToList();
        var baseline = Statistics.Combine(directBlocks);
        if (directBlocks.Count < 2 || directBlocks.Any(b => b.Stats.Received < 10) || baseline.Received < 20 || baseline.MedianMs is null)
            return new(RouteOption.Direct, "Sem respostas suficientes para escolher outro caminho. Conexão direta mantida.", baseline, baseline);
        var reference = directBlocks[0];
        var directFingerprint = reference.Samples[0].RouteBefore.Fingerprint;
        if (directBlocks.Any(b => b.TargetIp != reference.TargetIp || b.Settings != reference.Settings ||
            b.Samples.Any(s => s.RouteBefore.Fingerprint != directFingerprint || s.RouteAfter.Fingerprint != directFingerprint)))
            return new(RouteOption.Direct, "A conexão direta mudou durante os testes. Conexão direta mantida sem concluir sobre os caminhos.", baseline, baseline);
        var winner = RouteOption.Direct; var best = baseline;
        var assessments = new List<RouteAssessment>();
        foreach (var group in entries.Where(t => !t.Route.IsDirect).GroupBy(t => t.Route.Id))
        {
            var route = group.First().Route;
            var measured = trials.Where(t => t.Route.Id == group.Key).ToList();
            var stats = Statistics.Combine(measured.Select(t => t.Block));
            string? rejection = null;
            if (route.Link is not ("direct" or "relay" or "warp") || group.Any(t => t.Route.Link != route.Link)) rejection = "O tipo de túnel não foi confirmado ou mudou entre os testes.";
            else if (measured.Count < 2 || stats.Received < 20 || measured.Any(t => t.Block.Stats.Received < 10) || stats.AverageMs is null || stats.MedianMs is null || stats.P95Ms is null || stats.FailurePercent is null) rejection = "Medições completas ou respostas insuficientes.";
            else if (measured.Any(t => t.Block.TargetIp != reference.TargetIp || t.Block.Settings != reference.Settings)) rejection = "Destino ou condições de medição diferentes da conexão direta.";
            else if (stats.AverageMs >= baseline.AverageMs) rejection = "O ping médio não foi menor que o da conexão direta.";
            else if (measured.Any(t => t.Block.Stats.AverageMs >= directBlocks.Min(b => b.Stats.AverageMs))) rejection = "A melhora não se repetiu nos dois testes; a comparação ficou inconclusiva.";
            else if (stats.P95Ms > baseline.P95Ms) rejection = "O ping médio caiu, mas os picos de latência aumentaram.";
            else if (stats.FailurePercent > baseline.FailurePercent) rejection = "O ping médio caiu, mas as falhas do teste aumentaram.";
            else if (stats.JitterMs.HasValue && baseline.JitterMs.HasValue && stats.JitterMs > baseline.JitterMs + 3) rejection = "O ping médio caiu, mas a variação aumentou mais de 3 ms.";
            assessments.Add(new(route, stats, rejection is null, rejection ?? "Ping médio menor confirmado nos dois testes, dentro dos limites de qualidade."));
            if (rejection is null && stats.AverageMs < best.AverageMs) { winner = route; best = stats; }
        }
        var reason = winner.IsDirect
            ? "Conexão direta mantida: nenhum outro caminho confirmou ping médio menor dentro dos limites de qualidade."
            : $"Menor ping médio confirmado: {winner.Name}, {best.AverageMs:0.0} ms; direta {baseline.AverageMs:0.0} ms. Confirme dentro do jogo.";
        return new(winner, reason, baseline, best) { Assessments = assessments };
    }
}

public sealed class AutomaticReport
{
    public string Version { get; init; } = "0.9.0";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public required string Game { get; init; }
    public required ObservedDestination Destination { get; init; }
    public string Method => ManualSelection ? "Escolha manual do usuário, acompanhada por ICMP IPv4. Não representa vencedor automático nem comprova ping/perda UDP do jogo. Ensaios existentes são históricos."
        : "Menor ping médio ICMP IPv4 ao mesmo destino observado no processo, confirmado em dois blocos por caminho. Empates ou melhora inconsistente mantêm a direta. Limites: sem piora de falhas ou p95, jitter até 3 ms acima da direta. Não comprova ping/perda UDP do jogo.";
    public List<RouteTrial> Trials { get; } = [];
    public AutomaticChoice? Choice { get; set; }
    public bool ManualSelection { get; set; }
    public RouteOption? RequestedRoute { get; set; }
    public RouteOption? CurrentRoute { get; set; }
    public List<string> Notes { get; } = [];
}

public sealed class AutomaticOptimizer(IAutomaticRoutes routes, IAutomaticMeter meter, RouteLease lease)
{
    public async Task<AutomaticReport> ActivateAsync(string game, ObservedDestination destination,
        ProbeSettings settings, IProgress<string>? status, IProgress<MeasurementProgress>? samples, CancellationToken ct, IProgress<string>? diagnosis = null)
    {
        settings.Validate();
        var report = new AutomaticReport { Game = game, Destination = destination };
        await lease.BeginAsync(ct);
        try
        {
            status?.Report("Procurando caminhos disponíveis…");
            var candidates = (await routes.DiscoverAsync(ct)).Where(c => !c.IsDirect && c.Link is "direct" or "relay" or "warp").GroupBy(c => c.Id).Select(g => g.First()).Take(8).ToList();
            if (routes is IRouteDiscoveryDiagnostics diagnostics) foreach (var note in diagnostics.DiscoveryNotes) Note(note);
            status?.Report("Medindo conexão direta…");
            await Measure(RouteOption.Direct);
            if (report.Trials[0].Block.Stats.Received >= 10)
            {
                foreach (var candidate in candidates)
                {
                    try
                    {
                        status?.Report($"Comparando caminho {candidate.Name}…");
                        await Measure(candidate); await Measure(candidate);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        Note($"{candidate.Name}: {ex.Message}");
                        await lease.ApplyAsync(RouteOption.Direct, ct);
                    }
                }
            }
            else Note("Destino respondeu pouco ou não respondeu a ICMP. Os caminhos alternativos não foram testados.");
            status?.Report("Confirmando a conexão direta…");
            await Measure(RouteOption.Direct);
            report.Choice = AutomaticPolicy.Choose(report.Trials);
            if (!report.Trials.Any(t => !t.Route.IsDirect))
            {
                var reason = candidates.Count == 0 ? report.Notes.FirstOrDefault() ?? "Nenhum caminho alternativo disponível para comparar." : report.Notes.LastOrDefault() ?? "Os caminhos alternativos não puderam ser medidos.";
                report.Choice = report.Choice with { Reason = reason + " Conexão direta mantida." };
            }
            await lease.ApplyAsync(report.Choice.Winner, ct);
            report.CurrentRoute = report.Choice.Winner;
            report.CompletedAt = DateTimeOffset.UtcNow;
            status?.Report(report.Choice.Reason);
            return report;
        }
        catch
        {
            // Cleanup has its own bounded lifetime; a cancelled user operation
            // must not cancel restoration of the previous route.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await lease.RestoreAsync(cleanup.Token);
            throw;
        }

        async Task Measure(RouteOption option)
        {
            await lease.ApplyAsync(option, ct);
            var block = await meter.MeasureAsync(destination.Ip, option, settings, samples, ct);
            report.Trials.Add(new(option, block));
        }
        void Note(string note) { report.Notes.Add(note); diagnosis?.Report(note); }
    }
}

