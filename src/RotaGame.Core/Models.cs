using System.Net;

namespace RotaGame.Core;

public enum PathKind { Diagnostic, Direct, Relay }
public enum ProbeOutcome { Success, Timeout, Unreachable, LocalError }

public sealed record ProbeSettings(int Count = 60, int IntervalMs = 1000, int TimeoutMs = 1000, int PayloadBytes = 32, int WarmupCount = 3)
{
    public void Validate()
    {
        if (Count is < 1 or > 3600 || IntervalMs is < 100 or > 10000 || TimeoutMs is < 100 or > 10000 ||
            PayloadBytes is < 0 or > 1200 || WarmupCount is < 0 or > 10)
            throw new ArgumentException("Parâmetros de medição fora dos limites.");
    }
}

public sealed record RouteSnapshot(uint InterfaceIndex, string InterfaceId, string Name, string Description,
    string Gateway, string Prefix, bool IsWireGuard, bool IsPhysicalCandidate, DateTimeOffset CheckedAt)
{
    public string Fingerprint => $"{InterfaceIndex}|{InterfaceId}|{Gateway}|{Prefix}";
    public string Display => $"{Name} · {(IsWireGuard ? "WireGuard" : "sem WireGuard identificado")} · próximo salto {Gateway}";
}

public sealed record ProbeReply(ProbeOutcome Outcome, double? RttMs, string Detail);
public sealed record Sample(int Sequence, DateTimeOffset At, ProbeOutcome Outcome, double? RttMs,
    string Detail, RouteSnapshot RouteBefore, RouteSnapshot RouteAfter);

public sealed class MeasurementBlock
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required PathKind Path { get; init; }
    public required string TargetIp { get; init; }
    public required ProbeSettings Settings { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public string Status { get; set; } = "running";
    public string? Error { get; set; }
    public List<Sample> Samples { get; } = [];
    public Statistics Stats => Statistics.From(Samples);
}

public sealed class MeasurementSession
{
    public string SchemaVersion { get; init; } = "1.0";
    public string AppVersion { get; init; } = "0.8.0";
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public required string Game { get; init; }
    public required string RegionNote { get; init; }
    public required string OriginalTarget { get; init; }
    public required string TargetIp { get; init; }
    public required ProbeSettings Settings { get; init; }
    public bool IsComparison { get; init; }
    public string MeasurementMethod => "ICMP echo IPv4; jitter = média de |RTT[n]-RTT[n-1]| apenas entre respostas consecutivas; p95 = nearest rank; falhas ICMP não provam perda do jogo.";
    public List<MeasurementBlock> Blocks { get; } = [];
}

public interface IProbe
{
    Task<ProbeReply> SendAsync(IPAddress target, ProbeSettings settings, CancellationToken cancellationToken);
}

public interface IRouteObserver
{
    RouteSnapshot Observe(IPAddress target);
}

public sealed record MeasurementProgress(MeasurementBlock Block, Sample? Sample, string Message);
