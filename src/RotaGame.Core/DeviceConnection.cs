using System.Text.Json;

namespace RotaGame.Core;

public enum DeviceRole { Unknown, Player, Bridge }
public sealed record DeviceProfile(string DeviceId, DeviceRole Role, string Name, string? PartnerNodeId = null)
{
    public static DeviceProfile Create() => new(Guid.NewGuid().ToString("N"), DeviceRole.Unknown, Environment.MachineName);
}
public interface IDeviceProfileStore { DeviceProfile Load(); void Save(DeviceProfile profile); }
public sealed class DeviceProfileStore(string path) : IDeviceProfileStore
{
    public DeviceProfile Load()
    {
        if (!File.Exists(path)) { var profile = DeviceProfile.Create(); Save(profile); return profile; }
        var value = JsonSerializer.Deserialize<DeviceProfile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Identificação deste PC inválida.");
        if (!Guid.TryParse(value.DeviceId, out _) || !Enum.IsDefined(value.Role) || string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 80) throw new InvalidDataException("Identificação deste PC inválida.");
        return value;
    }
    public void Save(DeviceProfile profile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(profile)); File.Move(path + ".tmp", path, true);
    }
}
public sealed class MemoryDeviceProfileStore : IDeviceProfileStore
{
    private DeviceProfile _value = DeviceProfile.Create();
    public DeviceProfile Load() => _value;
    public void Save(DeviceProfile profile) => _value = profile;
}

public enum ConnectionStep { ChooseRole, SignIn, StartBridge, ConfirmBridge, ApproveBridge, ShareBridge, AcceptBridge, Ready }
public static class ConnectionSteps
{
    public static ConnectionStep Next(DeviceRole role, bool connected, bool offering, bool approved, bool partnerFound, bool confirming = false) =>
        role == DeviceRole.Unknown ? ConnectionStep.ChooseRole : !connected ? ConnectionStep.SignIn :
        role == DeviceRole.Player ? partnerFound ? ConnectionStep.Ready : ConnectionStep.AcceptBridge :
        !offering ? ConnectionStep.StartBridge : partnerFound ? ConnectionStep.Ready : !approved ? confirming ? ConnectionStep.ConfirmBridge : ConnectionStep.ApproveBridge : ConnectionStep.ShareBridge;
}
public static class ConnectionInvites
{
    public static bool IsValid(string text) => Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort
        && uri.UserInfo.Length == 0 && uri.Host is "login.tailscale.com" or "console.tailscale.com"
        && ((uri.AbsolutePath.StartsWith("/uinv/", StringComparison.Ordinal) && uri.AbsolutePath.Length > 6)
        || (uri.AbsolutePath.StartsWith("/admin/invite/", StringComparison.Ordinal) && uri.AbsolutePath.Length > 14));
    public static bool IsNetworkInvite(string text) => IsValid(text) && new Uri(text.Trim()).AbsolutePath.StartsWith("/uinv/", StringComparison.Ordinal);
}

public sealed record GraphPoint(DateTimeOffset At, double? RttMs, ProbeOutcome Outcome);
public sealed record SharedRoute(string Id, string Name, double? AverageMs, double? P95Ms, double? JitterMs, double? FailurePercent, bool Selected);
public sealed record SharedMeasurements(bool Active, string State, string Game, string? Target, string RouteId, string RouteName,
    DateTimeOffset? MeasuredAt, double? AverageMs, double? JitterMs, double? P95Ms, double? FailurePercent,
    GraphPoint[] Points, SharedRoute[] Routes)
{
    public double? SampleAgeMs { get; init; }
    public bool Confirmed { get; init; }
    public bool Comparing { get; init; }
    public string Explanation { get; init; } = "";
}
public sealed record DeviceFrame(int Protocol, string SessionId, long Sequence, string DeviceId, DeviceRole Role, string Name, SharedMeasurements? Measurements)
{
    public bool PairRequested { get; init; }
    public bool BridgeUsable { get; init; }
    public void Validate()
    {
        static bool Text(string? value, int max) => value is not null && value.Length <= max && !value.Any(char.IsControl);
        static bool Number(double? value, double max = 60000) => value is null || double.IsFinite(value.Value) && value >= 0 && value <= max;
        if (Protocol != 1 || !Guid.TryParse(SessionId, out _) || !Guid.TryParse(DeviceId, out _) || Sequence < 1 || Role is not (DeviceRole.Player or DeviceRole.Bridge)
            || !Text(Name, 80) || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Identificação incompatível.");
        if (Measurements is not { } m) return;
        if (Role != DeviceRole.Player || !Text(m.State, 180) || !Text(m.Game, 100) || !Text(m.RouteId, 80) || !Text(m.RouteName, 100) || !Text(m.Explanation, 500)
            || m.Target is not null && (!System.Net.IPAddress.TryParse(m.Target, out var target) || target.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            || !Number(m.AverageMs) || !Number(m.JitterMs) || !Number(m.P95Ms) || !Number(m.FailurePercent, 100) || !Number(m.SampleAgeMs, 1e12)
            || m.Points is null || m.Routes is null || m.Points.Length > 180 || m.Routes.Length > 9
            || m.Points.Any(p => p is null || !Enum.IsDefined(p.Outcome) || !Number(p.RttMs) || p.Outcome != ProbeOutcome.Success && p.RttMs is not null)
            || m.Routes.Any(r => r is null || !Text(r.Id, 80) || !Text(r.Name, 100) || !Number(r.AverageMs) || !Number(r.P95Ms) || !Number(r.JitterMs) || !Number(r.FailurePercent, 100)))
            throw new InvalidDataException("Medições incompatíveis.");
    }
}

public sealed record ReceivedDevice(string NodeId, string Ip, DeviceFrame Frame, DateTimeOffset ReceivedAt);
public sealed class DeviceInbox
{
    private readonly Dictionary<string, ReceivedDevice> _devices = [];
    private readonly Dictionary<string, (string Session, long Sequence)> _sequence = [];
    private readonly object _gate = new();
    public void Receive(string nodeId, string ip, DeviceFrame frame, DateTimeOffset now)
    {
        frame.Validate();
        lock (_gate)
        {
            if (_sequence.TryGetValue(nodeId, out var old) && old.Session == frame.SessionId && old.Sequence >= frame.Sequence) throw new InvalidDataException("Mensagem repetida.");
            if (_devices.Count >= 128 && !_devices.ContainsKey(nodeId)) throw new InvalidDataException("Limite de computadores excedido.");
            _sequence[nodeId] = (frame.SessionId, frame.Sequence); _devices[nodeId] = new(nodeId, ip, frame, now);
        }
    }
    public ReceivedDevice[] Fresh(DateTimeOffset now, DeviceRole role)
    {
        lock (_gate) return _devices.Values.Where(p => p.Frame.Role == role && now >= p.ReceivedAt && now - p.ReceivedAt < TimeSpan.FromSeconds(10)).ToArray();
    }
}
