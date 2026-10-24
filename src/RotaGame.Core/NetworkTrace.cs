using System.Net;
using System.Net.Sockets;

namespace RotaGame.Core;

public enum TraceOutcome { Router, Destination, Timeout, Unreachable, LocalError }
public sealed record TraceReply(TraceOutcome Outcome, string? Address, double? RttMs, string Detail);
public sealed record TraceHop(int Number, TraceOutcome Outcome, string? Address, double? RttMs, string Detail);
public sealed record NetworkTrace(string Target, RouteSnapshot Route, IReadOnlyList<TraceHop> Hops, string Status, DateTimeOffset At)
{
    public bool Reached => Status == "complete" && Hops.LastOrDefault()?.Outcome == TraceOutcome.Destination;
    public string Method => "Trajeto observado por sondas ICMP TTL. RTT acumulado de ida e volta a cada resposta; não é latência por trecho nem prova do trajeto UDP. Saltos silenciosos e o interior de túneis podem ficar ocultos.";
}
public interface ITraceProbe { Task<TraceReply> SendAsync(IPAddress target, int ttl, int timeoutMs, CancellationToken ct); }

public sealed class NetworkTracer(ITraceProbe probe, IRouteObserver routes)
{
    public async Task<NetworkTrace> RunAsync(string destination, IProgress<NetworkTrace>? progress, CancellationToken ct, int maxHops = 30, int timeoutMs = 800)
    {
        if (!IPAddress.TryParse(destination, out var target) || target.AddressFamily != AddressFamily.InterNetwork || target.GetAddressBytes()[0] is 0 or >= 224 || maxHops is < 1 or > 40 || timeoutMs is < 100 or > 2000)
            throw new ArgumentException("Destino ou limite do trajeto inválido.");
        var original = routes.Observe(target); var hops = new List<TraceHop>();
        NetworkTrace Snapshot(string state) => new(destination, original, hops.ToArray(), state, DateTimeOffset.UtcNow);
        for (var ttl = 1; ttl <= maxHops; ttl++)
        {
            ct.ThrowIfCancellationRequested();
            if (routes.Observe(target).Fingerprint != original.Fingerprint) return Snapshot("route-changed");
            var reply = await probe.SendAsync(target, ttl, timeoutMs, ct); ct.ThrowIfCancellationRequested();
            if (routes.Observe(target).Fingerprint != original.Fingerprint) return Snapshot("route-changed");
            var outcome = reply.Outcome;
            var valid = IPAddress.TryParse(reply.Address, out var address) && address.AddressFamily == AddressFamily.InterNetwork;
            if (outcome == TraceOutcome.Destination && (!valid || !address!.Equals(target))) outcome = TraceOutcome.LocalError;
            var answered = outcome is TraceOutcome.Router or TraceOutcome.Destination or TraceOutcome.Unreachable;
            if (answered && !valid) outcome = TraceOutcome.LocalError;
            var rtt = answered && reply.RttMs is >= 0 && double.IsFinite(reply.RttMs.Value) ? reply.RttMs : null;
            hops.Add(new(ttl, outcome, answered && valid ? address!.ToString() : null, rtt, reply.Detail));
            var state = outcome switch { TraceOutcome.Destination => "complete", TraceOutcome.Unreachable => "unreachable", TraceOutcome.LocalError => "error", _ => "scanning" };
            var snapshot = Snapshot(state); progress?.Report(snapshot);
            if (state != "scanning") return snapshot;
            if (ttl < maxHops) await Task.Delay(100, ct);
        }
        var partial = Snapshot("partial"); progress?.Report(partial); return partial;
    }
}
