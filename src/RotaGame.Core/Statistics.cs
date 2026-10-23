namespace RotaGame.Core;

public sealed record Statistics(int Attempts, int Sent, int Received, int Failed, int LocalErrors,
    double? FailurePercent, double? AverageMs, double? MedianMs, double? P95Ms, double? JitterMs, double? MinMs, double? MaxMs)
{
    public static Statistics From(IEnumerable<Sample> input)
    {
        var samples = input.ToList();
        var rtts = samples.Where(s => s.Outcome == ProbeOutcome.Success && s.RttMs.HasValue).Select(s => s.RttMs!.Value).Order().ToArray();
        var differences = new List<double>();
        for (var i = 1; i < samples.Count; i++)
        {
            var previous = samples[i - 1];
            var current = samples[i];
            if (previous.Outcome == ProbeOutcome.Success && current.Outcome == ProbeOutcome.Success &&
                previous.RttMs.HasValue && current.RttMs.HasValue && current.Sequence == previous.Sequence + 1)
                differences.Add(Math.Abs(current.RttMs.Value - previous.RttMs.Value));
        }
        var localErrors = samples.Count(s => s.Outcome == ProbeOutcome.LocalError);
        var sent = samples.Count - localErrors;
        var failed = samples.Count(s => s.Outcome is ProbeOutcome.Timeout or ProbeOutcome.Unreachable);
        return new(samples.Count, sent, rtts.Length, failed, localErrors,
            sent > 0 ? 100.0 * failed / sent : null,
            rtts.Length > 0 ? rtts.Average() : null,
            rtts.Length > 0 ? (rtts[(rtts.Length - 1) / 2] + rtts[rtts.Length / 2]) / 2 : null,
            rtts.Length > 0 ? rtts[(int)Math.Ceiling(rtts.Length * .95) - 1] : null,
            differences.Count > 0 ? differences.Average() : null,
            rtts.Length > 0 ? rtts[0] : null, rtts.Length > 0 ? rtts[^1] : null);
    }

    // Never join jitter pairs across block boundaries.
    public static Statistics Combine(IEnumerable<MeasurementBlock> blocks)
    {
        var list = blocks.ToList();
        var merged = From(list.SelectMany(b => b.Samples.Select(s => s with { Sequence = -1 })));
        double sum = 0;
        var pairs = 0;
        foreach (var block in list)
        {
            for (var i = 1; i < block.Samples.Count; i++)
            {
                var a = block.Samples[i - 1]; var b = block.Samples[i];
                if (a.Outcome == ProbeOutcome.Success && b.Outcome == ProbeOutcome.Success &&
                    a.RttMs.HasValue && b.RttMs.HasValue && b.Sequence == a.Sequence + 1)
                { sum += Math.Abs(b.RttMs.Value - a.RttMs.Value); pairs++; }
            }
        }
        return merged with { JitterMs = pairs > 0 ? sum / pairs : null };
    }
}
