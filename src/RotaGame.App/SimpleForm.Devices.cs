using System.Net;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    private readonly IDeviceProfileStore _deviceStore;
    private DeviceProfile _device;
    private readonly bool _controlled;
    private readonly RoleChoiceButton _playerChoice = new(DeviceRole.Player) { AccessibleName = "Vou jogar neste PC", Cursor = Cursors.Hand };
    private readonly RoleChoiceButton _bridgeChoice = new(DeviceRole.Bridge) { AccessibleName = "Este PC é a ponte", Cursor = Cursors.Hand };
    private readonly Label _identity = Label("", 10, Ink), _partnerCaption = Label("", 10, Muted);
    private readonly Label _quickState = Label("Desativado", 10, Ink);
    private DashboardTableLayout? _actionsGrid;
    private readonly Button _pickPartner = Button("Escolher PC");
    private readonly Label[] _metricTitles = new Label[4];
    private readonly DeviceInbox _devices = new();
    private readonly string _deviceSession = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _deviceLifetime = new();
    private readonly System.Windows.Forms.Timer _deviceTimer = new() { Interval = 2000 };
    private PeerPanelServer? _panelServer;
    private PeerPanelFirewall? _panelFirewall;
    private string? _panelAddress;
    private TailStatus? _tailStatus;
    private DeviceFrame? _outbound;
    private ReceivedDevice? _partner;
    private Task? _deviceTask;
    private long _frameSequence;
    private bool _deviceBusy, _devicesPaused, _devicesStopped, _settingRole, _remoteShown, _starting;

    private void ConfigureDevices()
    {
        _playerChoice.Click += (_, _) => SelectRole(DeviceRole.Player); _bridgeChoice.Click += (_, _) => SelectRole(DeviceRole.Bridge);
        _state.TextChanged += (_, _) => _quickState.Text = _state.Text;
        _pickPartner.Click += (_, _) => { if (_bridgeReadiness == BridgeReadiness.PermissionUnconfirmed) ShowConnectionPage(); else PickPartner(); };
        _play.CheckedChanged += (_, _) => { if (!_active && !_settingRole && _play.Checked) SelectRole(DeviceRole.Player); };
        _host.CheckedChanged += (_, _) => { if (!_active && !_settingRole && _host.Checked) SelectRole(DeviceRole.Bridge); };
        _deviceTimer.Tick += (_, _) => { if (!_deviceBusy) _deviceTask = RefreshDevicesAsync(); };
        Shown += (_, _) => { if (!_controlled) { _deviceTimer.Start(); _deviceTask = RefreshDevicesAsync(); } };
        FormClosed += (_, _) => { _deviceTimer.Stop(); _deviceTimer.Dispose(); _deviceLifetime.Cancel(); _deviceLifetime.Dispose(); };
        UpdateDeviceLabels();
    }
    private void SelectRole(DeviceRole role)
    {
        if (_active || _restoring || _connectionPage?.Busy == true) return;
        var changed = _device.Role != role;
        SaveDevice(_device with { Role = role, PartnerNodeId = changed ? null : _device.PartnerNodeId });
        _settingRole = true; _host.Checked = role == DeviceRole.Bridge; _play.Checked = role != DeviceRole.Bridge; _settingRole = false;
        if (changed) { _partner = null; ClearRemoteView(); ClearLive(); }
        SetControls(); _connectionPage?.RefreshPresentation();
        if (!_controlled && !_deviceBusy) _deviceTask = RefreshDevicesAsync();
    }
    private void SaveDevice(DeviceProfile value)
    {
        try { _deviceStore.Save(value); _device = value; }
        catch (Exception) { _device = value; _message.Text = "A função foi escolhida, mas não consegui salvá-la para a próxima abertura."; }
    }
    private void UpdateDeviceLabels()
    {
        _playerChoice.Chosen = _device.Role == DeviceRole.Player; _bridgeChoice.Chosen = _device.Role == DeviceRole.Bridge;
        _playerChoice.Enabled = _bridgeChoice.Enabled = !_active && !_restoring && _connectionPage?.Busy != true;
        _playerChoice.Invalidate(); _bridgeChoice.Invalidate();
        _identity.Text = _device.Role switch { DeviceRole.Bridge => "ESTE PC: " + _device.Name + "  ·  PONTE / CASCAVEL", DeviceRole.Player => "ESTE PC: " + _device.Name + "  ·  JOGADOR / ACRE", _ => "Escolha acima a função deste PC. Ela ficará salva." };
        if (_device.Role == DeviceRole.Unknown) _partnerCaption.Text = "A função identifica quem oferece a ponte e quem vai jogar.";
        if (_partner is { } partner) _partnerCaption.Text = "OUTRO PC: " + partner.Frame.Name + (partner.Frame.Role == DeviceRole.Player ? "  ·  JOGADOR / ACRE" : "  ·  PONTE / CASCAVEL") + "  ·  conectado ✓";
        _pickPartner.Visible = _tailStatus?.State == "Running" && _device.Role != DeviceRole.Unknown;
        _pickPartner.Text = _bridgeReadiness == BridgeReadiness.PermissionUnconfirmed ? "Verificar ponte" : "Escolher PC";
    }
    private void UpdateBridgeAvailability(bool running, bool approved)
    {
        if (!_offering || _restoring) return;
        var remote = _partner is { Frame.Role: DeviceRole.Player, Frame.BridgeUsable: true } && DateTimeOffset.UtcNow - _partner.ReceivedAt < TimeSpan.FromSeconds(10);
        _bridgeReadiness = BridgeConfirmation.Read(_offering, running, approved, remote, DateTimeOffset.UtcNow - _bridgeStartedAt);
        _state.Text = _bridgeReadiness switch { BridgeReadiness.Ready => "Ponte disponível", BridgeReadiness.Confirming => "Confirmando a ponte…", BridgeReadiness.Disconnected => "Ponte sem conexão com a conta", _ => "Permissão da ponte não confirmada" };
        if (!_remoteShown) _message.Text = _bridgeReadiness switch
        {
            BridgeReadiness.Ready => "Ponte reconhecida. Deixe este PC ligado; o amigo pode ativar a conexão dele.",
            BridgeReadiness.Confirming => "Se já autorizou, aguarde: verificando a confirmação automaticamente.",
            BridgeReadiness.Disconnected => "A conta perdeu a conexão. Confira Conta e conexão; a autorização não foi presumida como revogada.",
            _ => "Ainda sem confirmação do Tailscale. Se já autorizou, não repita: aguarde a sincronização ou use Verificar ponte."
        };
        _connectionPage?.SetOffering(true, _active || _restoring, _bridgeReadiness == BridgeReadiness.Confirming);
        UpdateDeviceLabels();
    }
    private async Task ToggleConnectionAsync()
    {
        if (_starting || _restoring) return;
        if (_active || _offering || HasRecovery()) { await DeactivateAsync(); return; }
        if (_device.Role == DeviceRole.Unknown) { ShowConnectionPage(); return; }
        _starting = true; SetControls(); _state.Text = "Verificando conexão…";
        try
        {
            if (!_controlled)
            {
                var status = _client.Installed ? await _client.StatusAsync(_deviceLifetime.Token) : null;
                if (status?.State != "Running") { ShowConnectionPage(); return; }
                _tailStatus = status;
            }
            _operation = ActivateAsync();
        }
        catch (Exception ex) { _state.Text = "Não consegui verificar a conexão"; _message.Text = ex.Message + " Tente Ativar novamente."; }
        finally { _starting = false; SetControls(); }
    }
    private DeviceFrame MakeFrame()
    {
        SharedMeasurements? measurements = null;
        if (_device.Role == DeviceRole.Player)
        {
            var now = DateTimeOffset.UtcNow; var stats = Statistics.From(_rolling); var last = _rolling.LastOrDefault();
            var views = _routeMap.Snapshot(now); var current = views.FirstOrDefault(r => r.Badge is "EM USO" or "EM TESTE")?.Route ?? _selected;
            var rows = _report?.Trials.GroupBy(t => t.Route.Id).Select(g =>
            {
                var s = Statistics.Combine(g.Select(t => t.Block)); var r = g.First().Route;
                return new SharedRoute(r.Id, Clean(r.Name, 100), s.AverageMs, s.P95Ms, s.JitterMs, s.FailurePercent, r.Id == _report.Choice?.Winner.Id);
            }).Take(9).ToArray() ?? views.Select(v => new SharedRoute(v.Route.Id, Clean(v.Route.Name, 100), null, null, null, null, false)).ToArray();
            measurements = new(_active && !_restoring, Clean(_state.Text, 180), Clean(_activeProfileId is { } id ? _profiles.FirstOrDefault(p => p.Id == id)?.Name ?? "" : "", 100),
                _routeMap.Destination?.Ip, current.Id, Clean(current.Name, 100), last?.At, stats.AverageMs, stats.JitterMs, stats.P95Ms, stats.FailurePercent,
                _chart.Samples.TakeLast(180).Select(s => new GraphPoint(s.At, s.Outcome == ProbeOutcome.Success ? s.RttMs : null, s.Outcome)).ToArray(), rows)
            { SampleAgeMs = last is null ? null : Math.Max(0, (now - last.At).TotalMilliseconds), Confirmed = views.Any(v => v.Moving), Comparing = _active && _report is null && _routeMap.Destination is not null, Explanation = Clean(_report?.Choice?.Reason ?? _message.Text, 500) };
        }
        return new(1, _deviceSession, Interlocked.Increment(ref _frameSequence), _device.DeviceId, _device.Role, Clean(_device.Name, 80), measurements);
    }
    private static string Clean(string value, int max) => new(value.Where(c => !char.IsControl(c)).Take(max).ToArray());
    private string? AuthorizePanel(string ip) => _tailStatus?.State == "Running" ? _tailStatus.Peers.FirstOrDefault(p => p.Online && p.Ip == ip) is { } peer ? Node(peer) : null : null;
    private static string Node(TailPeer peer) => peer.NodeId.Length > 0 ? peer.NodeId : peer.Ip;
    private async Task RefreshDevicesAsync()
    {
        if (_controlled || _deviceBusy || _devicesPaused || _devicesStopped || IsDisposed) return; _deviceBusy = true;
        try
        {
            if (_device.Role == DeviceRole.Unknown) return;
            Volatile.Write(ref _outbound, MakeFrame());
            _tailStatus = _client.Installed ? await _client.StatusAsync(_deviceLifetime.Token) : null;
            if (_tailStatus?.State != "Running")
            {
                UpdateBridgeAvailability(false, false);
                await StopPanelAsync(); _partner = null; ClearRemoteView(); _partnerCaption.Text = "Conta ainda não conectada. Use Conectar para continuar."; UpdateDeviceLabels(); return;
            }
            var status = _tailStatus;
            UpdateBridgeAvailability(true, status.SelfApproved);
            if (_device.Role == DeviceRole.Bridge)
            {
                if (_panelAddress != status.SelfIp) await StopPanelAsync();
                if (_panelServer is null)
                {
                    if (!IPAddress.TryParse(status.SelfIp, out var local)) throw new InvalidOperationException("Endereço privado ainda não disponível.");
                    _panelFirewall ??= new PeerPanelFirewall(); _panelFirewall.Configure(status.SelfIp, status.Peers.Where(p => p.Online).Select(p => p.Ip));
                    _panelServer = new(local, AuthorizePanel, () => Volatile.Read(ref _outbound)!, _devices); _panelAddress = status.SelfIp;
                }
                _panelFirewall?.Configure(status.SelfIp, status.Peers.Where(p => p.Online).Select(p => p.Ip));
            }
            else
            {
                await StopPanelAsync();
                var targets = status.Peers.Where(p => p.Online && (PeerPanelServer.IsTailAddress(IPAddress.TryParse(p.Ip, out var ip) ? ip : IPAddress.None)))
                    .Where(p => _device.PartnerNodeId is null || Node(p) == _device.PartnerNodeId).Take(8).ToArray();
                var frame = Volatile.Read(ref _outbound)!;
                // Discovery sends identity only. Measurements go exclusively to
                // the selected partner after a successful role handshake.
                await Task.WhenAll(targets.Select(async peer =>
                {
                    try
                    {
                        var paired = _device.PartnerNodeId == Node(peer);
                        var outgoing = frame with { Measurements = paired ? frame.Measurements : null, PairRequested = paired, BridgeUsable = peer.Offered };
                        var response = await PeerPanelServer.ExchangeAsync(IPAddress.Parse(peer.Ip), outgoing, _deviceLifetime.Token);
                        _devices.Receive(Node(peer), peer.Ip, response, DateTimeOffset.UtcNow);
                    }
                    catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException) { }
                }));
            }
            var opposite = _device.Role == DeviceRole.Bridge ? DeviceRole.Player : DeviceRole.Bridge;
            var candidates = _devices.Fresh(DateTimeOffset.UtcNow, opposite).Where(p => status.Peers.Any(s => s.Online && Node(s) == p.NodeId)).ToArray();
            if (_device.PartnerNodeId is null && candidates.Length == 1) SaveDevice(_device with { PartnerNodeId = candidates[0].NodeId });
            _partner = candidates.FirstOrDefault(p => p.NodeId == _device.PartnerNodeId);
            _connectionPage?.SetPlayerFound(_partner is { Frame.Role: DeviceRole.Player, Frame.BridgeUsable: true });
            UpdateBridgeAvailability(true, status.SelfApproved);
            if (_device.Role == DeviceRole.Bridge)
            {
                if (_partner?.Frame.Measurements is not null) ApplyRemoteView(_partner);
                else { ClearRemoteView(); _partnerCaption.Text = _partner is null ? "Aguardando o RotaGame do amigo. Abra a mesma versão nos dois PCs." : "Amigo reconhecido. Aguardando suas medições."; }
            }
            if (_partner is null && _device.Role == DeviceRole.Player) _partnerCaption.Text = candidates.Length > 1 ? "Há mais de uma ponte. Use Escolher PC para selecionar a sua." : "Procurando o RotaGame da ponte…";
            UpdateDeviceLabels();
        }
        catch (OperationCanceledException) when (_deviceLifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!IsDisposed)
            {
                if (_partner is not null && DateTimeOffset.UtcNow - _partner.ReceivedAt >= TimeSpan.FromSeconds(10)) { _partner = null; ClearRemoteView(); }
                _partnerCaption.Text = "Conexão do painel ainda indisponível. As medições locais continuam funcionando.";
            }
        }
        finally { _deviceBusy = false; }
    }
    private async Task StopPanelAsync()
    {
        if (_panelServer is { } server) { _panelServer = null; await server.DisposeAsync(); }
        _panelAddress = null; _panelFirewall?.Dispose(); _panelFirewall = null;
    }
    private async Task PauseDevicesAsync()
    {
        _devicesPaused = true; _deviceTimer.Stop(); if (_deviceTask is not null) await _deviceTask; await StopPanelAsync();
    }
    private async Task StopDevicesAsync()
    {
        if (_devicesStopped) return; _devicesStopped = true; _deviceTimer.Stop(); _deviceLifetime.Cancel(); if (_deviceTask is not null) await _deviceTask; await StopPanelAsync();
    }
    private void ResumeDevices()
    {
        if (!_devicesPaused || _devicesStopped || _controlled) return; _devicesPaused = false; _deviceTimer.Start();
    }
    private void ApplyRemoteView(ReceivedDevice partner)
    {
        var m = partner.Frame.Measurements!; _remoteShown = true;
        _metricTitles[0].Text = m.Active ? "PING MÉDIO · PC DO AMIGO" : "ÚLTIMO PING · PC DO AMIGO";
        _metricTitles[1].Text = "VARIAÇÃO · PC DO AMIGO"; _metricTitles[2].Text = "P95 · PC DO AMIGO"; _metricTitles[3].Text = "FALHAS · PC DO AMIGO";
        var stale = m.Active && (m.SampleAgeMs is null || m.SampleAgeMs + Math.Max(0, (DateTimeOffset.UtcNow - partner.ReceivedAt).TotalMilliseconds) > 5000);
        _metrics[0].Text = N(stale ? null : m.AverageMs) + " ms"; _metrics[1].Text = N(stale ? null : m.JitterMs) + " ms";
        _metrics[2].Text = N(stale ? null : m.P95Ms) + " ms"; _metrics[3].Text = N(stale ? null : m.FailurePercent) + " %";
        _chart.RemotePoints = m.Points; _chart.Invalidate(); _routeMap.Remote = partner;
        var thisBridge = _tailStatus?.SelfIp == m.RouteId;
        _state.Text = !m.Active ? "Amigo conectado · medição parada" : stale ? "Amigo conectado · aguardando medição" : m.RouteId.Length == 0 ? "Amigo na conexão direta" : thisBridge ? m.Comparing ? "Amigo testando esta ponte" : m.Confirmed ? "Esta ponte está em uso pelo amigo" : "Ponte selecionada · aguardando confirmação" : "Amigo usando outro caminho";
        _message.Text = "Medido em " + partner.Frame.Name + ": " + m.State + (m.Explanation.Length > 0 ? " · " + m.Explanation : "") + (partner.Frame.BridgeUsable ? "" : " · o amigo ainda não tem permissão de usar esta ponte.");
        _table.Rows.Clear(); foreach (var row in m.Routes) _table.Rows.Add(row.Name, N(row.AverageMs), N(row.P95Ms), N(row.JitterMs), N(row.FailurePercent) + "%", row.Selected ? "Usado pelo amigo" : "Comparado no amigo");
        _baseline.Text = "Medições recebidas de " + partner.Frame.Name + " · caminho: " + m.RouteName;
        _table.Visible = _resultScroll.Visible = _table.Rows.Count > 0; _emptyResults.Visible = !_table.Visible; UpdateResultScroll();
    }
    private void ClearRemoteView()
    {
        if (!_remoteShown) return; _remoteShown = false; _routeMap.Remote = null; _chart.RemotePoints = null;
        var names = new[] { "PING MÉDIO ATUAL", "VARIAÇÃO DO PING", "PICOS DE LATÊNCIA · P95", "FALHAS DO TESTE" };
        for (var i = 0; i < 4; i++) _metricTitles[i].Text = names[i];
        ClearLive(); _table.Rows.Clear(); _baseline.Text = "Aguardando medições recentes do PC do amigo.";
    }
    private void PickPartner()
    {
        using var dialog = new Form { Text = "Escolher o outro PC · RotaGame", BackColor = Canvas, ForeColor = Ink, ClientSize = new Size(520, 350), StartPosition = FormStartPosition.CenterParent, Font = FontAssets.Ui(11) };
        var list = new DashboardFlowLayout { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) }; dialog.Controls.Add(list);
        foreach (var peer in _tailStatus?.Peers.Where(p => p.Online).Take(8) ?? [])
        {
            var choice = Button(peer.Name); choice.Width = 470; choice.Height = 46;
            choice.Click += (_, _) => { SaveDevice(_device with { PartnerNodeId = Node(peer) }); _partner = null; dialog.Close(); }; list.Controls.Add(choice);
        }
        var forget = Button("Procurar automaticamente"); forget.Width = 470; forget.Height = 46; forget.Click += (_, _) => { SaveDevice(_device with { PartnerNodeId = null }); _partner = null; dialog.Close(); }; list.Controls.Add(forget);
        dialog.ShowDialog(this); if (!_controlled && !_deviceBusy) _deviceTask = RefreshDevicesAsync();
    }
}
