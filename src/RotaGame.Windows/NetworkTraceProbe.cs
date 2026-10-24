using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using RotaGame.Core;

namespace RotaGame.Windows;

public sealed class NetworkTraceProbe : ITraceProbe
{
    public async Task<TraceReply> SendAsync(IPAddress target, int ttl, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping(); var watch = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(target, TimeSpan.FromMilliseconds(timeoutMs), new byte[32], new PingOptions(ttl, false), ct);
            var outcome = reply.Status switch
            {
                IPStatus.Success => TraceOutcome.Destination,
                IPStatus.TtlExpired => TraceOutcome.Router,
                IPStatus.TimedOut => TraceOutcome.Timeout,
                IPStatus.DestinationHostUnreachable or IPStatus.DestinationNetworkUnreachable or IPStatus.DestinationUnreachable or IPStatus.DestinationPortUnreachable => TraceOutcome.Unreachable,
                _ => TraceOutcome.LocalError
            };
            return new(outcome, outcome == TraceOutcome.Timeout ? null : reply.Address?.ToString(),
                outcome is TraceOutcome.Router or TraceOutcome.Destination or TraceOutcome.Unreachable ? watch.Elapsed.TotalMilliseconds : null, reply.Status.ToString());
        }
        catch (PingException ex) { return new(TraceOutcome.LocalError, null, null, ex.InnerException?.Message ?? ex.Message); }
    }
}
