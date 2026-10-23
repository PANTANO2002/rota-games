using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using RotaGame.Core;

namespace RotaGame.Windows;

// Diagnostic data only. No command, route, file or update operations exist in
// this protocol. Production binds only to the verified Tailscale IPv4 adapter.
public sealed class PeerPanelServer : IAsyncDisposable
{
    public const int Port = 52941;
    public const int MaxBytes = 65536;
    private readonly TcpListener _listener;
    private readonly Func<string, string?> _authorize;
    private readonly Func<DeviceFrame> _local;
    private readonly DeviceInbox _inbox;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _slots = new(4);
    private readonly ConcurrentDictionary<long, Task> _clients = [];
    private readonly Task _accepting;
    private long _clientId;
    public int BoundPort => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public PeerPanelServer(IPAddress address, Func<string, string?> authorize, Func<DeviceFrame> local, DeviceInbox inbox, int port = Port, bool loopbackTest = false)
    {
        if (loopbackTest ? !IPAddress.IsLoopback(address) : !IsVerifiedAddress(address)) throw new InvalidOperationException("Painel disponível somente pelo adaptador privado Tailscale.");
        _authorize = authorize; _local = local; _inbox = inbox; _listener = new(address, port); _listener.Start(4); _accepting = AcceptAsync();
    }
    public static bool IsTailAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes(); return address.AddressFamily == AddressFamily.InterNetwork && bytes[0] == 100 && (bytes[1] & 192) == 64;
    }
    public static bool IsVerifiedAddress(IPAddress address) => IsTailAddress(address) && NetworkInterface.GetAllNetworkInterfaces().Any(n =>
        (n.Name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) || n.Description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase))
        && n.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(address)));
    private async Task AcceptAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_lifetime.Token);
                var ip = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString(); var node = _authorize(ip);
                if (node is null || !_slots.Wait(0)) { client.Dispose(); continue; }
                var id = Interlocked.Increment(ref _clientId); var task = HandleAsync(client, node, ip); _clients[id] = task;
                _ = task.ContinueWith(_ => _clients.TryRemove(id, out var _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (SocketException) when (_lifetime.IsCancellationRequested) { }
    }
    private async Task HandleAsync(TcpClient client, string node, string ip)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                var frame = await ReadAsync(client.GetStream(), timeout.Token);
                // The authenticated source comes from Tailscale's peer map and
                // TCP endpoint, never from a claimed address in the message.
                if (_authorize(ip) != node || frame.Role != DeviceRole.Player || _local().Role != DeviceRole.Bridge) return;
                if (frame.PairRequested) _inbox.Receive(node, ip, frame, DateTimeOffset.UtcNow);
                await WriteAsync(client.GetStream(), _local(), timeout.Token);
            }
            catch (Exception ex) when (ex is IOException or SocketException or JsonException or InvalidDataException or OperationCanceledException) { }
            finally { _slots.Release(); }
        }
    }
    public static async Task<DeviceFrame> ExchangeAsync(IPAddress address, DeviceFrame frame, CancellationToken ct, int port = Port, bool loopbackTest = false)
    {
        if (loopbackTest ? !IPAddress.IsLoopback(address) : !IsTailAddress(address)) throw new InvalidOperationException("O destino do painel não pertence à rede privada.");
        frame.Validate(); if (frame.Role != DeviceRole.Player) throw new InvalidDataException("Somente o jogador envia medições.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var client = new TcpClient(AddressFamily.InterNetwork); await client.ConnectAsync(address, port, timeout.Token);
        await WriteAsync(client.GetStream(), frame, timeout.Token); var response = await ReadAsync(client.GetStream(), timeout.Token);
        if (response.Role != DeviceRole.Bridge) throw new InvalidDataException("Este computador não é uma ponte RotaGame."); return response;
    }
    private static async Task<DeviceFrame> ReadAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, ct); var count = BinaryPrimitives.ReadInt32BigEndian(header);
        if (count is <= 0 or > MaxBytes) throw new InvalidDataException("Mensagem grande demais.");
        var bytes = new byte[count]; await stream.ReadExactlyAsync(bytes, ct);
        var frame = JsonSerializer.Deserialize<DeviceFrame>(bytes, new JsonSerializerOptions { MaxDepth = 12 }) ?? throw new InvalidDataException("Mensagem vazia."); frame.Validate(); return frame;
    }
    private static async Task WriteAsync(NetworkStream stream, DeviceFrame frame, CancellationToken ct)
    {
        frame.Validate(); var bytes = JsonSerializer.SerializeToUtf8Bytes(frame);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Mensagem grande demais.");
        var header = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(header, bytes.Length);
        await stream.WriteAsync(header, ct); await stream.WriteAsync(bytes, ct);
    }
    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel(); _listener.Stop(); await _accepting; await Task.WhenAll(_clients.Values); _slots.Dispose(); _lifetime.Dispose();
    }
}

public interface IPeerPanelFirewall : IDisposable { void Configure(string address, IEnumerable<string> peers); }
public sealed class PeerPanelFirewall : IPeerPanelFirewall
{
    private readonly string _application = Environment.ProcessPath ?? throw new InvalidOperationException("Aplicativo sem endereço local.");
    private string Name => "RotaGame.PrivatePanel." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_application)))[..12];
    private const string Group = "RotaGame private measurement panel";
    private string? _configured;
    public void Configure(string address, IEnumerable<string> peers)
    {
        if (!IPAddress.TryParse(address, out var local) || !PeerPanelServer.IsVerifiedAddress(local)) throw new InvalidOperationException("Adaptador privado não confirmado.");
        var allowed = peers.Distinct().Order().Where(p => IPAddress.TryParse(p, out var ip) && PeerPanelServer.IsTailAddress(ip)).ToArray();
        if (allowed.Length == 0) { Dispose(); return; }
        var key = address + ":" + string.Join(',', allowed); if (_configured == key) return;
        object? policy = null, rule = null;
        try
        {
            policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!);
            rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")!);
            dynamic fw = policy!; dynamic r = rule!;
            try { dynamic existing = fw.Rules.Item(Name); if ((string)existing.Grouping != Group) throw new InvalidOperationException("Regra de painel já existente não pertence ao RotaGame."); fw.Rules.Remove(Name); }
            catch (COMException) { }
            r.Name = Name; r.Description = "Somente medições e identificação entre PCs privados autorizados; sem controle remoto."; r.Grouping = Group;
            r.ApplicationName = _application; r.Protocol = 6; r.LocalPorts = PeerPanelServer.Port.ToString(); r.LocalAddresses = address; r.RemoteAddresses = string.Join(',', allowed);
            r.Direction = 1; r.Action = 1; r.Profiles = int.MaxValue; r.EdgeTraversal = false; r.Enabled = true; fw.Rules.Add(r); _configured = key;
        }
        finally { Release(rule); Release(policy); }
    }
    public void Dispose()
    {
        object? policy = null;
        try
        {
            policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!); dynamic fw = policy!;
            dynamic existing = fw.Rules.Item(Name);
            if ((string)existing.Grouping == Group && string.Equals((string)existing.ApplicationName, _application, StringComparison.OrdinalIgnoreCase)) fw.Rules.Remove(Name);
        }
        catch (COMException) { }
        finally { Release(policy); _configured = null; }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
