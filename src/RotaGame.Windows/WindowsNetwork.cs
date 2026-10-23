using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using RotaGame.Core;

namespace RotaGame.Windows;

public sealed class IcmpProbe : IProbe
{
    public async Task<ProbeReply> SendAsync(IPAddress target, ProbeSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(target, TimeSpan.FromMilliseconds(settings.TimeoutMs),
                new byte[settings.PayloadBytes], new PingOptions(128, false), cancellationToken);
            return reply.Status switch
            {
                IPStatus.Success => new(ProbeOutcome.Success, reply.RoundtripTime, "Resposta ICMP"),
                IPStatus.TimedOut => new(ProbeOutcome.Timeout, null, "Sem resposta dentro do timeout"),
                IPStatus.DestinationHostUnreachable or IPStatus.DestinationNetworkUnreachable or
                IPStatus.DestinationPortUnreachable or IPStatus.DestinationProtocolUnreachable or
                IPStatus.DestinationUnreachable or IPStatus.TtlExpired or IPStatus.TtlReassemblyTimeExceeded =>
                    new(ProbeOutcome.Unreachable, null, reply.Status.ToString()),
                _ => new(ProbeOutcome.LocalError, null, reply.Status.ToString())
            };
        }
        catch (PingException ex) { return new(ProbeOutcome.LocalError, null, ex.InnerException?.Message ?? ex.Message); }
    }
}

public sealed class WindowsRouteObserver : IRouteObserver
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ForwardRow
    {
        public uint Destination, Mask, Policy, NextHop, InterfaceIndex, Type, Protocol, Age,
            NextHopAs, Metric1, Metric2, Metric3, Metric4, Metric5;
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetBestRoute(uint destination, uint source, out ForwardRow route);

    public RouteSnapshot Observe(IPAddress target)
    {
        if (target.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Esta versão mede somente IPv4.");
        var code = GetBestRoute(BitConverter.ToUInt32(target.GetAddressBytes()), 0, out var row);
        if (code != 0) throw new Win32Exception((int)code, "O Windows não encontrou uma rota utilizável para o destino.");
        var adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n =>
        {
            try { return n.GetIPProperties().GetIPv4Properties()?.Index == row.InterfaceIndex; }
            catch (NetworkInformationException) { return false; }
        }) ?? throw new InvalidOperationException("A interface da rota não está disponível. Verifique a conexão.");
        var wg = adapter.Description.Contains("WireGuard", StringComparison.OrdinalIgnoreCase);
        var physical = !wg && adapter.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211
            && !new[] { "vpn", "wintun", "tap-windows", "tunnel" }.Any(s => adapter.Description.Contains(s, StringComparison.OrdinalIgnoreCase));
        var mask = BitConverter.GetBytes(row.Mask);
        var prefixLength = mask.Sum(b => System.Numerics.BitOperations.PopCount((uint)b));
        return new(row.InterfaceIndex, adapter.Id, adapter.Name, adapter.Description,
            new IPAddress(BitConverter.GetBytes(row.NextHop)).ToString(),
            $"{new IPAddress(BitConverter.GetBytes(row.Destination))}/{prefixLength}", wg, physical, DateTimeOffset.UtcNow);
    }
}

public static class TargetResolver
{
    public static async Task<IPAddress> ResolveAsync(string input, CancellationToken ct = default)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input) || input.Length > 253 || Uri.CheckHostName(input) == UriHostNameType.Unknown)
            throw new ArgumentException("Informe um IPv4 ou hostname válido, sem http://, porta ou caminho.");
        var addresses = await Dns.GetHostAddressesAsync(input, ct);
        var target = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .OrderBy(a => a.ToString(), StringComparer.Ordinal).FirstOrDefault()
            ?? throw new ArgumentException("O destino não possui IPv4. IPv6 ainda não é suportado.");
        var bytes = target.GetAddressBytes();
        if (bytes[0] is 0 or >= 224 || target.Equals(IPAddress.Broadcast))
            throw new ArgumentException("Use um endereço IPv4 unicast.");
        return target;
    }
}
