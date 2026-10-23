namespace RotaGame.Core;

public sealed record RouteOption(string Id, string Name, string Link = "direct")
{
    public bool IsDirect => Id.Length == 0;
    public static RouteOption Direct { get; } = new("", "Conexão direta");
}

public sealed record ObservedDestination(string Ip, int Port, string Protocol, int ProcessId, string ProcessName, long Packets);
public sealed record RouteReading(string Selector, bool Ready, string Detail);

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

public sealed record RecoveryRecord(string OriginalSelector, string LastAppliedSelector, DateTimeOffset CreatedAt, string? PreviousSelector = null);
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
        _record = new(current.Selector, current.Selector, DateTimeOffset.UtcNow);
        store.Save(_record);
    }

    public async Task ApplyAsync(RouteOption option, CancellationToken ct)
    {
        if (_record is null) throw new InvalidOperationException("Sessão de conexão não iniciada.");
        var current = await routes.ReadAsync(ct);
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
        if (current.Selector == record.LastAppliedSelector || current.Selector == record.OriginalSelector || current.Selector == record.PreviousSelector)
            await routes.SelectAsync(new(record.OriginalSelector, "Conexão anterior"), ct);
        // A manual change outside this program wins over a stale recovery record.
        store.Clear(); _record = null;
    }
}

public sealed record RouteTrial(RouteOption Route, MeasurementBlock Block);
public sealed record AutomaticChoice(RouteOption Winner, string Reason, Statistics DirectStats, Statistics WinnerStats);

public static class AutomaticPolicy
{
    public static AutomaticChoice Choose(IEnumerable<RouteTrial> input)
    {
        var trials = input.Where(t => t.Block.Status == "complete" && t.Block.Samples.Count == t.Block.Settings.Count && t.Block.Stats.LocalErrors == 0).ToList();
        var directBlocks = trials.Where(t => t.Route.IsDirect).Select(t => t.Block).ToList();
        var baseline = Statistics.Combine(directBlocks);
        if (directBlocks.Count < 2 || baseline.Received < 20 || baseline.MedianMs is null)
            return new(RouteOption.Direct, "Sem respostas suficientes para escolher uma ponte. Conexão direta mantida.", baseline, baseline);
        var reference = directBlocks[0];
        var directFingerprint = reference.Samples[0].RouteBefore.Fingerprint;
        if (directBlocks.Any(b => b.TargetIp != reference.TargetIp || b.Settings != reference.Settings ||
            b.Samples.Any(s => s.RouteBefore.Fingerprint != directFingerprint || s.RouteAfter.Fingerprint != directFingerprint)))
            return new(RouteOption.Direct, "A conexão direta mudou durante os testes. Conexão direta mantida sem concluir sobre as pontes.", baseline, baseline);
        var winner = RouteOption.Direct; var best = baseline;
        foreach (var group in trials.Where(t => !t.Route.IsDirect && t.Route.Link is "direct" or "relay").GroupBy(t => t.Route.Id))
        {
            if (group.Count() < 2) continue;
            if (group.Any(t => t.Block.TargetIp != reference.TargetIp || t.Block.Settings != reference.Settings)) continue;
            var stats = Statistics.Combine(group.Select(t => t.Block));
            if (stats.Received < 20 || stats.MedianMs is null || stats.P95Ms is null || stats.FailurePercent is null) continue;
            var improvement = baseline.MedianMs.Value - stats.MedianMs.Value;
            if (improvement < Math.Max(5, baseline.MedianMs.Value * .05) || stats.AverageMs >= baseline.AverageMs || stats.P95Ms > baseline.P95Ms ||
                stats.FailurePercent > baseline.FailurePercent ||
                (stats.JitterMs.HasValue && baseline.JitterMs.HasValue && stats.JitterMs > baseline.JitterMs + 3)) continue;
            if (winner.IsDirect || Score(stats) < Score(best)) { winner = group.First().Route; best = stats; }
        }
        return new(winner, winner.IsDirect
            ? "Nenhuma ponte demonstrou vantagem suficiente. Conexão direta mantida."
            : $"{winner.Name} teve melhor resultado nos testes. Confirme a melhora dentro do jogo.", baseline, best);
    }

    private static double Score(Statistics stats) => stats.MedianMs!.Value + .25 * stats.P95Ms!.Value + .1 * (stats.JitterMs ?? 0);
}

public sealed class AutomaticReport
{
    public string Version { get; init; } = "0.8.1";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public required string Game { get; init; }
    public required ObservedDestination Destination { get; init; }
    public string Method => "ICMP IPv4 ao destino observado no processo. Seleção exploratória entre caminhos disponíveis; não comprova ping/perda dos pacotes UDP do jogo.";
    public List<RouteTrial> Trials { get; } = [];
    public AutomaticChoice? Choice { get; set; }
    public List<string> Notes { get; } = [];
}

public sealed class AutomaticOptimizer(IAutomaticRoutes routes, IAutomaticMeter meter, RouteLease lease)
{
    public async Task<AutomaticReport> ActivateAsync(string game, ObservedDestination destination,
        ProbeSettings settings, IProgress<string>? status, IProgress<MeasurementProgress>? samples, CancellationToken ct)
    {
        settings.Validate();
        var report = new AutomaticReport { Game = game, Destination = destination };
        await lease.BeginAsync(ct);
        try
        {
            status?.Report("Procurando pontes disponíveis…");
            var candidates = (await routes.DiscoverAsync(ct)).Where(c => !c.IsDirect && c.Link is "direct" or "relay").GroupBy(c => c.Id).Select(g => g.First()).Take(8).ToList();
            if (routes is IRouteDiscoveryDiagnostics diagnostics) report.Notes.AddRange(diagnostics.DiscoveryNotes);
            status?.Report("Medindo conexão direta…");
            await Measure(RouteOption.Direct);
            if (report.Trials[0].Block.Stats.Received >= 10)
            {
                foreach (var candidate in candidates)
                {
                    try
                    {
                        status?.Report($"Comparando caminho por {candidate.Name}…");
                        await Measure(candidate); await Measure(candidate);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        report.Notes.Add($"{candidate.Name}: {ex.Message}");
                        await lease.ApplyAsync(RouteOption.Direct, ct);
                    }
                }
            }
            else report.Notes.Add("Destino respondeu pouco ou não respondeu a ICMP. Pontes não foram testadas.");
            status?.Report("Confirmando a conexão direta…");
            await Measure(RouteOption.Direct);
            report.Choice = AutomaticPolicy.Choose(report.Trials);
            if (!report.Trials.Any(t => !t.Route.IsDirect))
            {
                var reason = candidates.Count == 0 ? report.Notes.FirstOrDefault() ?? "Nenhuma ponte disponível para comparar." : report.Notes.LastOrDefault() ?? "As pontes disponíveis não puderam ser medidas.";
                report.Choice = report.Choice with { Reason = reason + " Conexão direta mantida." };
            }
            await lease.ApplyAsync(report.Choice.Winner, ct);
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
    }
}
