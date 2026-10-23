using System.Diagnostics;
using System.Net;

namespace RotaGame.Core;

public sealed class MeasurementRunner(IProbe probe, IRouteObserver routes)
{
    public static readonly PathKind[] AbbaPlan = [PathKind.Direct, PathKind.Relay, PathKind.Relay, PathKind.Direct];

    public static void ValidateRoute(RouteSnapshot route, PathKind expected)
    {
        if (expected == PathKind.Relay && !route.IsWireGuard)
            throw new InvalidOperationException("O destino ainda não usa uma interface WireGuard. Ative o túnel e inclua este IPv4 em AllowedIPs.");
        if (expected == PathKind.Direct && (route.IsWireGuard || !route.IsPhysicalCandidate))
            throw new InvalidOperationException("A etapa direta exige Ethernet ou Wi-Fi sem túnel identificado. Desative o WireGuard e outras VPNs.");
    }

    public async Task RunAsync(MeasurementBlock block, IProgress<MeasurementProgress>? progress, CancellationToken ct)
    {
        block.Settings.Validate();
        var target = IPAddress.Parse(block.TargetIp);
        try
        {
            var baseline = routes.Observe(target);
            ValidateRoute(baseline, block.Path);
            for (var i = 0; i < block.Settings.WarmupCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                EnsureRoute(target, baseline, block.Path);
                progress?.Report(new(block, null, $"Aquecimento {i + 1}/{block.Settings.WarmupCount}; fora das estatísticas."));
                var watch = Stopwatch.StartNew();
                await probe.SendAsync(target, block.Settings, ct);
                EnsureRoute(target, baseline, block.Path);
                await Pause(block.Settings.IntervalMs, watch, ct);
            }
            for (var i = 0; i < block.Settings.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var before = EnsureRoute(target, baseline, block.Path);
                var watch = Stopwatch.StartNew();
                var reply = await probe.SendAsync(target, block.Settings, ct);
                var after = EnsureRoute(target, baseline, block.Path);
                var sample = new Sample(i + 1, DateTimeOffset.UtcNow, reply.Outcome, reply.RttMs, reply.Detail, before, after);
                block.Samples.Add(sample);
                progress?.Report(new(block, sample, $"Sonda {i + 1}/{block.Settings.Count} · {reply.Detail}"));
                if (reply.Outcome == ProbeOutcome.LocalError)
                    throw new InvalidOperationException("Erro local ao enviar ICMP: " + reply.Detail);
                if (i + 1 < block.Settings.Count) await Pause(block.Settings.IntervalMs, watch, ct);
            }
            block.Status = "complete";
        }
        catch (OperationCanceledException) { block.Status = "cancelled"; throw; }
        catch (Exception ex) { block.Status = "invalid"; block.Error = ex.Message; throw; }
        finally { block.EndedAt = DateTimeOffset.UtcNow; }
    }

    private RouteSnapshot EnsureRoute(IPAddress target, RouteSnapshot baseline, PathKind expected)
    {
        var route = routes.Observe(target);
        ValidateRoute(route, expected);
        if (route.Fingerprint != baseline.Fingerprint)
            throw new InvalidOperationException("A rota mudou durante a medição. Este bloco foi invalidado; repita com a conexão estável.");
        return route;
    }

    private static Task Pause(int interval, Stopwatch watch, CancellationToken ct) =>
        Task.Delay(Math.Max(0, interval - (int)watch.ElapsedMilliseconds), ct);
}
