namespace RotaGame.Core;

public sealed record ComparisonResult(bool IsValid, string Verdict, Statistics? Direct = null, Statistics? Relay = null,
    double? MedianDeltaMs = null, double? P95DeltaMs = null, double? FailureDeltaPoints = null);

public static class Comparison
{
    public static ComparisonResult Evaluate(MeasurementSession session)
    {
        var b = session.Blocks;
        if (!session.IsComparison || b.Count != 4 || !b.Select(x => x.Path).SequenceEqual(MeasurementRunner.AbbaPlan))
            return new(false, "Complete os quatro blocos A–B–B–A para comparar.");
        if (b.Any(x => x.Status != "complete" || x.Samples.Count != x.Settings.Count || x.Settings != session.Settings || x.TargetIp != session.TargetIp))
            return new(false, "Comparação incompleta ou inválida: repita o bloco afetado.");
        if (b.Any(x => x.Samples.Any(s => s.Outcome == ProbeOutcome.LocalError)))
            return new(false, "Erros locais impedem uma comparação válida.");
        foreach (var block in b)
        {
            var first = block.Samples[0].RouteBefore;
            if (block.Samples.Any(s => s.RouteBefore.Fingerprint != first.Fingerprint || s.RouteAfter.Fingerprint != first.Fingerprint ||
                (block.Path == PathKind.Relay ? !s.RouteBefore.IsWireGuard || !s.RouteAfter.IsWireGuard :
                 !s.RouteBefore.IsPhysicalCandidate || !s.RouteAfter.IsPhysicalCandidate || s.RouteBefore.IsWireGuard || s.RouteAfter.IsWireGuard)))
                return new(false, "A rota não foi consistente com o caminho solicitado.");
        }
        if (b[0].Samples[0].RouteBefore.Fingerprint != b[3].Samples[0].RouteBefore.Fingerprint)
            return new(false, "A conexão direta mudou entre A1 e A2. Repita o experimento.");
        // WireGuard may recreate an interface, but both B blocks must use the same adapter configuration and prefix.
        if (b[1].Samples[0].RouteBefore.InterfaceId != b[2].Samples[0].RouteBefore.InterfaceId ||
            b[1].Samples[0].RouteBefore.Prefix != b[2].Samples[0].RouteBefore.Prefix)
            return new(false, "Os blocos B usaram túneis diferentes. Repita com o mesmo relay.");
        var direct = Statistics.Combine(b.Where(x => x.Path == PathKind.Direct));
        var relay = Statistics.Combine(b.Where(x => x.Path == PathKind.Relay));
        if (direct.Received < 20 || relay.Received < 20)
            return new(false, "Respostas insuficientes: ICMP pode estar bloqueado ou limitado. Não conclua sobre o jogo.", direct, relay);
        var median = relay.MedianMs!.Value - direct.MedianMs!.Value;
        var p95 = relay.P95Ms!.Value - direct.P95Ms!.Value;
        var loss = relay.FailurePercent!.Value - direct.FailurePercent!.Value;
        var text = median < -5 && p95 <= 0 && loss <= 0
            ? "Relay teve resultados melhores nesta sessão. Confirme em outros horários e dentro do jogo."
            : median > 5 && p95 >= 0 && loss >= 0
                ? "Conexão direta teve resultados melhores nesta sessão. Confirme em outros horários e dentro do jogo."
                : "Resultado misto ou diferença pequena: sem vantagem clara nesta sessão.";
        return new(true, text + " Critério exploratório; sem garantia nem significância estatística.", direct, relay, median, p95, loss);
    }
}
