using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class SimpleForm : Form
{
    private static readonly Color Canvas = DashboardTheme.Canvas, PanelColor = DashboardTheme.Surface,
        Ink = DashboardTheme.Ink, Muted = DashboardTheme.Muted, Mint = DashboardTheme.Green;
    private readonly ComboBox _game = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210, ForeColor = Color.Black, BackColor = Color.White };
    private readonly Label _lockedGame = new() { Width = 210, Height = 40, BackColor = PanelColor, ForeColor = Ink, TextAlign = ContentAlignment.MiddleLeft, Visible = false };
    private readonly RadioButton _play = new() { Text = "Vou jogar neste PC", Checked = true, AutoSize = true };
    private readonly RadioButton _host = new() { Text = "Este PC será a ponte", AutoSize = true };
    private readonly Button _activate = Button("Ativar", true), _deactivate = Button("Desativar"), _setup = Button("Preparar conexão"), _export = Button("Salvar resultado");
    private readonly Label _state = Label("Desativado", 23, Ink), _message = Label("Abra o jogo e ative antes de entrar em uma partida competitiva.", 10, Muted),
        _path = Label("Caminho: conexão direta", 11, Mint), _baseline = Label("Direta: —     |     Caminho escolhido: —     |     Diferença: —", 11, Ink),
        _destination = Label("O destino será encontrado automaticamente no jogo.", 9, Muted);
    private readonly Label[] _metrics = new Label[4];
    private readonly LatencyChart _chart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _table = new();
    private readonly Queue<Sample> _rolling = new();
    private readonly TailscaleClient _client = new(new CommandRunner());
    private readonly IAutomaticRoutes _routes;
    private readonly IAutomaticMeter _meter;
    private readonly IRecoveryStore _store;
    private readonly RouteLease _lease;
    private readonly IBridgeOffers _bridgeOffers;
    private readonly IBridgeOfferStore _bridgeStore;
    private readonly BridgeOfferLease _bridgeLease;
    private DateTimeOffset _bridgeStartedAt;
    private BridgeReadiness _bridgeReadiness;
    private readonly ToolTip _tips = new();
    private CancellationTokenSource? _cts;
    private Task? _operation;
    private bool _active, _offering, _closing, _restoring;
    private AutomaticReport? _report;
    private RouteOption _selected = RouteOption.Direct;
    private readonly GameLibrary _library;
    private readonly IConnectionSession? _connectionSession;
    private readonly Action<string>? _connectionUrl;
    private readonly IReadOnlyList<GameProfile>? _startupProfiles;
    private readonly IInviteClipboard? _inviteClipboard;

    public SimpleForm(IAutomaticRoutes? routes = null, IAutomaticMeter? meter = null, IRecoveryStore? store = null, GameLibrary? library = null,
        IConnectionSession? connectionSession = null, Action<string>? connectionUrl = null, IReadOnlyList<GameProfile>? startupProfiles = null, IInviteClipboard? inviteClipboard = null, IDeviceProfileStore? deviceStore = null, IBridgeOffers? bridgeOffers = null)
    {
        _controlled = routes is not null;
        _deviceStore = deviceStore ?? (_controlled ? new MemoryDeviceProfileStore() : new DeviceProfileStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame", "device.json")));
        try { _device = _deviceStore.Load(); } catch (Exception) { _device = DeviceProfile.Create(); }
        _host.Checked = _device.Role == DeviceRole.Bridge; _play.Checked = !_host.Checked;
        _connectionSession = connectionSession; _connectionUrl = connectionUrl;
        _startupProfiles = startupProfiles;
        _inviteClipboard = inviteClipboard;
        _routes = routes ?? _client;
        _meter = new DiagramMeter(this, meter ?? new AutomaticMeter(new IcmpProbe(), new WindowsRouteObserver(), _client));
        _store = store ?? new FileRecoveryStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame", "connection-recovery.json"));
        _lease = new(_routes, _store);
        _bridgeOffers = bridgeOffers ?? (_controlled ? new SmokeBridgeOffers() : _client);
        _bridgeStore = _controlled ? new MemoryBridgeOfferStore() : new FileBridgeOfferStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame", "bridge-recovery.json"));
        _bridgeLease = new(_bridgeOffers, _bridgeStore);
        _library = library ?? new GameLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame", "games.json"));
        BuildDashboard();
        ConfigureDevices();
        _tips.SetToolTip(_metrics[0], "Média das últimas 30 sondas respondidas no caminho atual. Atualizada a cada resposta; não é o contador interno do jogo.");
        _tips.SetToolTip(_metrics[3], "Falhas de sondas ICMP podem ser filtragem do destino; não comprovam perda dos pacotes do jogo.");
        _tips.SetToolTip(_routeMap, "Clique para escolher automática pelo menor ping, direta ou ponte manual. O caminho ativo é medido ao vivo; os demais mostram o último teste. Ping ICMP do caminho completo; animações ilustrativas.");
        _activate.Click += async (_, _) => await ToggleConnectionAsync();
        _deactivate.Click += async (_, _) => await DeactivateAsync();
        _setup.Click += (_, _) => ShowConnectionPage();
        _export.Click += (_, _) => SaveReport();
        FormClosing += async (_, e) =>
        {
            if (_closing) return;
            e.Cancel = true;
            _updatesLifetime.Cancel();
            if (_updateTask is not null) await _updateTask;
            await DeactivateAsync();
            if (!_lease.Active && !_offering && !HasRecovery()) { await StopDevicesAsync(); _closing = true; Close(); }
        };
        Shown += async (_, _) =>
        {
            if (HasRecovery())
            {
                _state.Text = "Restaurando a conexão anterior…";
                await DeactivateAsync();
            }
        };
        SetControls();
    }

    private bool HasRecovery()
    {
        try { return _store.Load() is not null || _bridgeStore.Load() is not null; }
        catch { _message.Text = "Não foi possível ler a recuperação da conexão. Use Preparar conexão para conferir o caminho ativo."; return true; }
    }

    private void SetControls()
    {
        _activate.Enabled = !_starting && !_restoring && _connectionPage?.Busy != true;
        _deactivate.Enabled = !_restoring && (_active || _offering || HasRecovery());
        _game.Enabled = _play.Checked; _game.Visible = !_active; _lockedGame.Visible = _active; _lockedGame.Text = _host.Checked ? "Ponte deste computador" : _game.Text;
        _play.AutoCheck = _host.AutoCheck = !_active && _connectionPage?.Busy != true;
        _setup.Enabled = (!_active || _offering) && !_restoring;
        _connectionPage?.SetOffering(_offering, _active || _restoring, _bridgeReadiness == BridgeReadiness.Confirming);
        _export.Enabled = _report is not null;
        _activate.Text = _starting ? "Verificando…" : _restoring ? "Desativando…" : _active || _offering ? "■  Desativar" : HasRecovery() ? "Restaurar conexão" : _device.Role == DeviceRole.Unknown ? "Conectar este PC" : _host.Checked ? "▶  Ativar ponte" : "▶  Ativar conexão";
        ((DashboardButton)_activate).SelectedStyle = _active; _activate.Font = FontAssets.Title(19);
        UpdateDeviceLabels();
        SyncDashboard();
    }

    private async Task ActivateAsync(ObservedDestination? forced = null, ProbeSettings? settings = null, bool smoke = false)
    {
        if (_active) return;
        _active = true; _cts?.Dispose(); _cts = new(); _report = null; _routeMap.Destination = null; _rolling.Clear(); _chart.Samples.Clear(); _chart.Invalidate();
        _baseline.Text = "Direta: —     |     Caminho escolhido: —     |     Diferença: —"; _table.Rows.Clear(); ClearLive();
        SetControls();
        try
        {
            if (_host.Checked)
            {
                _state.Text = "Preparando este PC como ponte…";
                // Mark the intended offer before the command: cancellation can
                // arrive after the service changed but before the CLI returned.
                _offering = true;
                _bridgeStartedAt = DateTimeOffset.UtcNow;
                var offer = await _bridgeLease.BeginAsync(_cts.Token);
                _path.Text = "Ponte: " + _device.Name;
                UpdateBridgeAvailability(offer.Running, offer.Approved);
                return;
            }
            await RunPlayerLoopAsync(forced, settings, smoke);
        }
        catch (OperationCanceledException) { _state.Text = "Desativando…"; }
        catch (Exception ex) { _state.Text = "Não foi possível concluir"; _message.Text = ex.Message; _tips.SetToolTip(_message, ex.Message); }
        finally
        {
            _comparingPaths = false; _switchingPath = false;
            if (!_offering && !smoke)
            {
                try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)); await _lease.RestoreAsync(timeout.Token); }
                catch (Exception ex) { _message.Text = "Não consegui restaurar a conexão. Clique em Desativar para tentar novamente. " + ex.Message; }
                _active = false;
            }
            SetControls();
        }
    }

    private async Task DeactivateAsync()
    {
        if (_restoring) return; _restoring = true; SetControls(); _cts?.Cancel();
        try
        {
            if (_operation is not null) await _operation;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await _lease.RestoreAsync(timeout.Token);
            await _bridgeLease.RestoreAsync(timeout.Token); _offering = false; _bridgeReadiness = BridgeReadiness.Inactive;
            _active = false; _state.Text = "Desativado"; _path.Text = "Caminho anterior restaurado";
            _message.Text = "A medição parou e as alterações de conexão desta sessão foram desfeitas.";
            if (_device.Role == DeviceRole.Bridge && _bridgeLease.PreservedExisting) _message.Text = "RotaGame parado. A oferta que você já tinha configurado no Tailscale foi preservada.";
        }
        catch (Exception ex) { _message.Text = "Restauração pendente. Tente Desativar novamente. " + ex.Message; }
        finally { _restoring = false; SetControls(); }
    }

    private void ShowProgress(MeasurementProgress progress)
    {
        if (progress.Sample is not { } sample) return;
        if (sample.Sequence == 1 && progress.Block.Settings.Count > 1) { _rolling.Clear(); _chart.Samples.Clear(); }
        if (_report is null) _path.Text = "Caminho da medição: " + sample.RouteBefore.Name;
        _rolling.Enqueue(sample); while (_rolling.Count > 30) _rolling.Dequeue();
        var list = _rolling.Select((s, i) => s with { Sequence = i + 1 }).ToList();
        var s = Statistics.From(list);
        _metrics[0].Text = N(s.AverageMs) + " ms"; _metrics[1].Text = N(s.JitterMs) + " ms";
        _metrics[2].Text = N(s.P95Ms) + " ms"; _metrics[3].Text = N(s.FailurePercent) + " %";
        _chart.Samples.Add(sample); if (_chart.Samples.Count > 180) _chart.Samples.RemoveAt(0); _chart.Invalidate();
    }

    private void ClearLive()
    {
        _rolling.Clear(); _chart.Samples.Clear(); _chart.Invalidate();
        _routeMap.ResetLive();
        foreach (var metric in _metrics) metric.Text = "—";
    }

    private void RefreshResult()
    {
        if (_report is null) return;
        if (_report.ManualSelection)
        {
            _baseline.Text = "Manual · " + _selected.Name + (_report.Trials.Count > 0 ? " · comparação anterior abaixo" : " · sem comparação automática concluída");
            _emptyResults.Text = "Escolha manual aplicada. Acompanhe o ping atual acima.";
        }
        else if (_report.Choice is { } choice)
        {
            var gain = choice.DirectStats.AverageMs - choice.WinnerStats.AverageMs;
            var difference = !gain.HasValue ? "sem medição suficiente" : Math.Abs(gain.Value) < .1 ? "sem diferença" : gain.Value.ToString("0.0") + " ms menor";
            var at = (_report.CompletedAt ?? _report.CreatedAt).ToLocalTime();
            _baseline.Text = $"Teste {at:HH:mm} · Direta: {N(choice.DirectStats.AverageMs)} ms · Escolhida: {N(choice.WinnerStats.AverageMs)} ms · {difference}";
        }
        _table.Rows.Clear();
        foreach (var group in _report.Trials.GroupBy(t => t.Route.Id))
        {
            var stats = Statistics.Combine(group.Select(t => t.Block));
            var option = group.First().Route;
            var assessment = _report.Choice?.Assessments.FirstOrDefault(a => a.Route.Id == option.Id);
            var selected = option.Id == _selected.Id;
            var row = _table.Rows.Add(option.Name, N(stats.AverageMs), N(stats.P95Ms), N(stats.JitterMs), N(stats.FailurePercent) + "%", selected ? _report.ManualSelection ? "Manual · em uso" : "Escolhido" : assessment?.Eligible == false ? "Não escolhido" : "Comparado");
            var explanation = assessment?.Reason ?? (selected ? _report.Choice?.Reason : null);
            _table.Rows[row].Cells[5].ToolTipText = explanation ?? "Resultado do último teste, não é uma medição simultânea ao caminho atual.";
        }
    }

    private void SaveReport()
    {
        if (_report is null) return;
        using var dialog = new SaveFileDialog { Filter = "Resultado JSON (*.json)|*.json", FileName = $"rotagame-auto-{DateTime.Now:yyyyMMdd-HHmmss}.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(dialog.FileName, SerializeReport()); }
        catch (Exception ex) { _message.Text = "Não foi possível salvar: " + ex.Message; }
    }
    private string SerializeReport() => JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });
    private static string N(double? value) => value?.ToString("0.0") ?? "—";
    private static Label Label(string text, float size, Color color) => new() { Text = text, Dock = DockStyle.Fill, ForeColor = color, Font = size >= 18 ? FontAssets.Title(size) : FontAssets.Ui(size), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
    private static Button Button(string text, bool primary = false) => new DashboardButton() { Text = text, Height = 40, AutoSize = true, Padding = new Padding(10, 0, 10, 0), FlatStyle = FlatStyle.Flat, BackColor = primary ? DashboardTheme.Accent : PanelColor, ForeColor = Ink, Margin = new Padding(0, 0, 8, 0) };
    private void SetupTable()
    {
        _table.Dock = DockStyle.Fill; _table.BackgroundColor = PanelColor; _table.BorderStyle = BorderStyle.None;
        _table.ReadOnly = true; _table.AllowUserToAddRows = false; _table.RowHeadersVisible = false; _table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _table.EnableHeadersVisualStyles = false; _table.ColumnHeadersDefaultCellStyle = new() { BackColor = PanelColor, ForeColor = Muted };
        _table.DefaultCellStyle = new() { BackColor = PanelColor, ForeColor = Ink, SelectionBackColor = Color.FromArgb(40, 65, 80), SelectionForeColor = Ink };
        foreach (var name in new[] { "Caminho", "Ping mediano", "Picos · p95", "Variação", "Falhas do teste", "Resultado" }) _table.Columns.Add(name, name);
        _table.Columns[0].FillWeight = 150;
    }

    internal async Task RunSmokeAsync(string directory)
    {
        Directory.CreateDirectory(directory); Text += " · validação local";
        WindowState = FormWindowState.Normal; ClientSize = new Size(1340, 860); await Task.Delay(150);
        if (_profiles.Count != 0) throw new InvalidOperationException("A biblioteca de validação não iniciou vazia.");
        var initialSize = new Size(1340, 860); ClientSize = new Size(1600, 860); ArrangeDashboard(); ResizeCards(); PerformLayout();
        if (_pageScroll.Overflowing) throw new InvalidOperationException("A biblioteca vazia criou rolagem desnecessária.");
        using (var idle = new Bitmap(Width, Height)) { DrawToBitmap(idle, new Rectangle(Point.Empty, Size)); idle.Save(Path.Combine(directory, "interface-library.png")); }
        var added = _library.Add(Environment.ProcessPath!, "Teste local de reconhecimento"); ReloadLibrary(added.Id); RefreshRunningGames();
        if (_runningGames.GetValueOrDefault(added.Id)?.Contains(Environment.ProcessId) != true) throw new InvalidOperationException("O executável cadastrado não foi reconhecido.");
        if ((await WaitForGameAsync(default)).Id != added.Id) throw new InvalidOperationException("O jogo cadastrado não entrou na seleção automática.");
        if (!_gamesPanel.Controls.OfType<GameCard>().First(c => c.Profile is not null).HasExecutableIcon) throw new InvalidOperationException("O cartão não extraiu o ícone do executável.");
        using (var selected = new Bitmap(Width, Height)) { DrawToBitmap(selected, new Rectangle(Point.Empty, Size)); selected.Save(Path.Combine(directory, "interface-style.png")); }
        var fixtures = Enumerable.Range(1, 3).Select(i => new GameProfile("layout-" + i, "Teste de layout " + i, ["NoGameRunning"], Environment.ProcessPath)).ToArray();
        ApplyLibrary(fixtures); ClientSize = new Size(1340, 860); ArrangeDashboard(); ResizeCards(); PerformLayout();
        if (_gameRows != 1) throw new InvalidOperationException("Quatro cartões não couberam na primeira linha.");
        foreach (Control card in _gamesPanel.Controls) if (card.Right > _gamesPanel.ClientSize.Width || card.Bottom > _gamesPanel.ClientSize.Height) throw new InvalidOperationException("Cartão cortado sem necessidade.");
        ApplyLibrary(Enumerable.Range(1, 4).Select(i => new GameProfile("layout-" + i, "Teste de layout " + i, ["NoGameRunning"], Environment.ProcessPath)).ToArray());
        ArrangeDashboard(); ResizeCards(); PerformLayout();
        var firstCard = _gamesPanel.Controls[0]; var lastCard = _gamesPanel.Controls[^1];
        if (_gameRows != 2 || lastCard.Top <= firstCard.Top || lastCard.Width != firstCard.Width) throw new InvalidOperationException("O cartão excedente não criou a linha seguinte com o mesmo tamanho.");
        using (var wrapped = new Bitmap(Width, Height)) { DrawToBitmap(wrapped, new Rectangle(Point.Empty, Size)); wrapped.Save(Path.Combine(directory, "interface-wrapped.png")); }
        ApplyLibrary(Enumerable.Range(1, 9).Select(i => new GameProfile("layout-" + i, "Teste de layout " + i, ["NoGameRunning"], Environment.ProcessPath)).ToArray());
        ArrangeDashboard(); ResizeCards(); PerformLayout();
        if (_gameRows < 2) throw new InvalidOperationException("A biblioteca excedente não quebrou em linhas.");
        ClientSize = new Size(1080, 720); ArrangeDashboard(); PerformLayout();
        if (!_pageScroll.Overflowing) throw new InvalidOperationException("O painel menor não permitiu rolar o conteúdo.");
        _pageScroll.CheckInteraction();
        using (var overflow = new Bitmap(Width, Height)) { DrawToBitmap(overflow, new Rectangle(Point.Empty, Size)); overflow.Save(Path.Combine(directory, "interface-scroll.png")); }
        ClientSize = initialSize;
        _library.Remove(added.Id); ReloadLibrary();
        var waitingProfile = new GameProfile("smoke-wait", "Teste de espera", ["RotaGameNotStarted-" + Guid.NewGuid().ToString("N")]);
        _profiles.Add(waitingProfile); _game.Items.Add(waitingProfile.Name); _game.SelectedIndex = _game.Items.Count - 1;
        _play.Checked = true;
        _operation = ActivateAsync(); await Task.Delay(100);
        if (!_state.Text.Contains("aguardando um jogo")) throw new InvalidOperationException("Ativar não permaneceu aguardando o jogo iniciar: " + _state.Text + " · " + _message.Text);
        await DeactivateAsync(); ReloadLibrary();
        await ActivateAsync(new("127.0.0.1", 0, "ICMP", Environment.ProcessId, "Validação local", 1), new(4, 100, 300, 32, 0), true);
        if (_report is null || _report.Trials.Count != 2 || _report.Trials.Any(t => t.Block.Stats.Received != 4) || !_metrics[0].Text.Contains("ms")) throw new InvalidOperationException("Falha no fluxo automático local.");
        File.WriteAllText(Path.Combine(directory, "simple-loopback.json"), SerializeReport());
        await Task.Delay(400);
        WindowState = FormWindowState.Normal; ClientSize = initialSize; PerformLayout();
        using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(directory, "interface-automatic.png"));
        var originalSize = ClientSize; ClientSize = new Size(1120, 780); PerformLayout(); await Task.Delay(100);
        if (ClientSize.Width < 1080 || ClientSize.Height < 720) throw new InvalidOperationException("A janela de validação foi minimizada.");
        if (_pageScroll.Overflowing) throw new InvalidOperationException("O painel compacto vazio criou rolagem desnecessária.");
        _pageScroll.CheckVisibility();
        using (var compact = new Bitmap(Width, Height)) { DrawToBitmap(compact, new Rectangle(Point.Empty, Size)); compact.Save(Path.Combine(directory, "interface-compact.png")); }
        ClientSize = originalSize;
        await DeactivateAsync();
        if (HasRecovery()) throw new InvalidOperationException("Recuperação permaneceu após Desativar.");
        File.WriteAllText(Path.Combine(directory, "simple-smoke.txt"), "PASS: biblioteca vazia sem jogos pré-carregados, ícone extraído do executável, quatro cartões sem corte, cartão excedente sozinho na linha seguinte com o mesmo tamanho, biblioteca em várias linhas, barra vertical com arraste/roda/teclado, painel adaptável em 1600x860/1340x860/1120x780/1080x720, cadastro/reconhecimento do PID, espera/cancelamento, ICMP local 4/4 e monitor 5/5, ping médio e restauração. Rede não foi alterada.\n");
    }

    private sealed class Inline<T>(Action<T> report) : IProgress<T>
    {
        private readonly SynchronizationContext? _context = SynchronizationContext.Current;
        public void Report(T value)
        {
            if (_context is not null && SynchronizationContext.Current != _context) _context.Send(_ => report(value), null);
            else report(value);
        }
    }
    private sealed class DiagramMeter(SimpleForm owner, IAutomaticMeter inner) : IAutomaticMeter
    {
        public async Task<MeasurementBlock> MeasureAsync(string target, RouteOption route, ProbeSettings settings, IProgress<MeasurementProgress>? progress, CancellationToken ct)
        {
            owner._routeMap.BeginMeasurement(route);
            var forwarded = new Inline<MeasurementProgress>(p =>
            {
                progress?.Report(p);
                owner._routeMap.Observe(route, p, Statistics.From(owner._rolling));
            });
            var result = await inner.MeasureAsync(target, route, settings, forwarded, ct);
            owner._routeMap.CompleteMeasurement(route, result); return result;
        }
    }
    private sealed class SimpleButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Enabled)
            {
                using var fill = new SolidBrush(PanelColor); e.Graphics.FillRectangle(fill, ClientRectangle);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}

// Smoke adapters are selected only by the explicit --simple-smoke CLI option.
// They never advertise, install, connect to, or select a real exit node.
internal sealed class SmokeRoutes : IAutomaticRoutes
{
    public Task<RouteReading> ReadAsync(CancellationToken ct) => Task.FromResult(new RouteReading("", true, "Local smoke"));
    public Task<IReadOnlyList<RouteOption>> DiscoverAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RouteOption>>([]);
    public Task SelectAsync(RouteOption option, CancellationToken ct) => Task.CompletedTask;
}
internal sealed class SmokeMeter : IAutomaticMeter
{
    public async Task<MeasurementBlock> MeasureAsync(string target, RouteOption route, ProbeSettings settings, IProgress<MeasurementProgress>? progress, CancellationToken ct)
    {
        var block = new MeasurementBlock { Path = PathKind.Diagnostic, TargetIp = "127.0.0.1", Settings = settings };
        await new MeasurementRunner(new IcmpProbe(), new WindowsRouteObserver()).RunAsync(block, progress, ct); return block;
    }
}
internal sealed class SmokeStore : IRecoveryStore
{
    private RecoveryRecord? _value;
    public RecoveryRecord? Load() => _value;
    public void Save(RecoveryRecord record) => _value = record;
    public void Clear() => _value = null;
}
