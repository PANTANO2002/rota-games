using System.Diagnostics;
using System.Net;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using RotaGame.Core;

namespace RotaGame.Windows;

public static class PublicIpv4
{
    public static bool IsPublic(IPAddress ip)
    {
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] is not (0 or 10 or 127) && b[0] < 224 &&
            !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 169 && b[1] == 254) &&
            !(b[0] == 172 && b[1] is >= 16 and <= 31) && !(b[0] == 192 && b[1] == 168) &&
            !(b[0] == 192 && b[1] == 0 && b[2] is 0 or 2) && !(b[0] == 198 && b[1] is 18 or 19) &&
            !(b[0] == 198 && b[1] == 51 && b[2] == 100) && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
}

public sealed class EndpointAccumulator
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ObservedDestination> _items = [];
    public void Add(IPAddress ip, int port, string protocol, int pid, string name)
    {
        if (!PublicIpv4.IsPublic(ip) || port is <= 0 or > 65535 || port is 53 or 853) return;
        var key = $"{ip}:{port}/{protocol}";
        lock (_gate)
        {
            _items.TryGetValue(key, out var previous);
            _items[key] = new(ip.ToString(), port, protocol, pid, name, (previous?.Packets ?? 0) + 1);
        }
    }
    public ObservedDestination? Best()
    {
        lock (_gate) return _items.Values.Where(e => e.Packets >= 8).OrderByDescending(e => e.Protocol == "UDP")
            .ThenByDescending(e => e.Packets).ThenBy(e => e.Ip, StringComparer.Ordinal).FirstOrDefault();
    }
}

public sealed class GameDiscovery
{
    public static readonly string[] Games = ["Detectar automaticamente", "The Finals", "Rocket League", "GTA Online"];
    private static bool Matches(string process, string game) => game switch
    {
        "The Finals" => process.Equals("Discovery", StringComparison.OrdinalIgnoreCase) || process.Equals("TheFinals", StringComparison.OrdinalIgnoreCase),
        "Rocket League" => process.Equals("RocketLeague", StringComparison.OrdinalIgnoreCase),
        "GTA Online" => process.Equals("GTA5", StringComparison.OrdinalIgnoreCase) || process.Equals("GTA5_Enhanced", StringComparison.OrdinalIgnoreCase),
        _ => Matches(process, "The Finals") || Matches(process, "Rocket League") || Matches(process, "GTA Online")
    };

    public Task<ObservedDestination> FindAsync(string game, IProgress<string>? progress, CancellationToken ct,
        TimeSpan? duration = null, int? testProcessId = null, IReadOnlyCollection<string>? customProcessNames = null)
    {
        return Task.Run(async () =>
        {
            if (TraceEventSession.IsElevated() != true) throw new InvalidOperationException("Abra o RotaGame como administrador para detectar o destino do jogo automaticamente.");
            var processMap = new Dictionary<int, string>(); var mapGate = new object();
            void Refresh()
            {
                var found = new Dictionary<int, string>();
                foreach (var process in Process.GetProcesses())
                {
                    using (process)
                        try
                        {
                            var known = Games.Contains(game) && Matches(process.ProcessName, game);
                            var custom = customProcessNames?.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase) == true;
                            if (testProcessId == process.Id || (testProcessId is null && (known || custom))) found[process.Id] = process.ProcessName;
                        }
                        catch (InvalidOperationException) { }
                }
                lock (mapGate) processMap = found;
            }
            Refresh();
            var accumulator = new EndpointAccumulator();
            using var session = new TraceEventSession("RotaGame-" + Guid.NewGuid().ToString("N")) { StopOnDispose = true };
            session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
            void Record(int pid, IPAddress destination, int port, string protocol)
            {
                string? name; lock (mapGate) processMap.TryGetValue(pid, out name);
                if (name is not null) accumulator.Add(destination, port, protocol, pid, name);
            }
            session.Source.Kernel.UdpIpSend += e => Record(e.ProcessID, e.daddr, e.dport, "UDP");
            session.Source.Kernel.TcpIpSend += e => Record(e.ProcessID, e.daddr, e.dport, "TCP");
            var consumer = Task.Run(() => session.Source.Process(), CancellationToken.None);
            using var cancel = ct.Register(() => session.Stop());
            try
            {
                var deadline = DateTimeOffset.UtcNow + (duration ?? TimeSpan.FromSeconds(18));
                while (DateTimeOffset.UtcNow < deadline)
                {
                    ct.ThrowIfCancellationRequested();
                    if (consumer.IsFaulted) await consumer;
                    Refresh();
                    int count; lock (mapGate) count = processMap.Count;
                    progress?.Report(count == 0 ? "Aguardando o jogo abrir…" : "Observando a conexão do jogo; entre em uma sessão de treino…");
                    await Task.Delay(1000, ct);
                }
            }
            finally { session.Stop(); await consumer.WaitAsync(TimeSpan.FromSeconds(5)); }
            ct.ThrowIfCancellationRequested();
            return accumulator.Best() ?? throw new GameNotReadyException("Jogo reconhecido. Aguardando tráfego de uma sessão de treino…");
        }, ct);
    }
}
