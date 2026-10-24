using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class OptimizerForm : Form
{
    private readonly IAutomaticRoutes _routes;
    private readonly IAutomaticMeter _meter;
    private readonly NetworkTracer _tracer;
    private readonly RouteLease _lease;
    private readonly IRecoveryStore _recovery;
    private readonly GameLibrary _library;
    private readonly bool _controlled;
    private readonly string _dataDirectory;
    private IReadOnlyList<GameProfile> _profiles;
    private string? _chosenGame;
    private CancellationTokenSource? _runCts, _traceCts;
    private Task? _runTask, _traceTask;
    private bool _stopping, _closing, _scanningLibrary, _settingUp, _comparing;
    private AutomaticReport? _report;
    private ObservedDestination? _destination;
    private RouteOption _selected = RouteOption.Direct, _measuring = RouteOption.Direct;
    private readonly Queue<Sample> _rolling = new();
    private readonly List<GameCard> _cards = [];
    private readonly System.Windows.Forms.Timer _libraryTimer = new() { Interval = 2000 };
    private DateTime _lastLibraryScan;
    private readonly DashboardScrollHost _scroll = new(false) { Dock = DockStyle.Fill };
    private readonly DashboardTableLayout _body = new() { ColumnCount = 1, RowCount = 7, Padding = new Padding(22, 10, 22, 12) };
    private readonly DashboardTableLayout _games = new() { ColumnCount = 1, RowCount = 1 };
    private readonly NetworkMap _map = new() { Dock = DockStyle.Fill };
    private readonly LatencyChart _chart = new() { Dock = DockStyle.Fill };
    private readonly Label _status = Caption("Pronto para analisar sua conexão", 19), _hint = Caption("Ative e abra um jogo da biblioteca. Sem convite, sem ponte entre computadores.", 10),
        _destinationLabel = Caption("Destino do jogo · aguardando detecção", 12), _summary = Caption("Os resultados aparecerão após a comparação.", 10), _mode = Caption("Automático · direta ou WARP gratuito", 10);
    private readonly Label[] _metrics = new Label[4];
    private readonly TextBox _search = new() { BorderStyle = BorderStyle.None, BackColor = DashboardTheme.Raised, ForeColor = DashboardTheme.Ink, PlaceholderText = "Buscar nos jogos instalados" };
    private readonly DashboardButton _activate = Button("▶  Ativar", true, 185), _prepare = Button("Preparar rota gratuita", false, 210), _refresh = Button("Atualizar trajeto", false, 155), _export = Button("Salvar resultado", false, 150);
    private readonly DataGridView _table = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, BackgroundColor = DashboardTheme.Surface, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false };
    private readonly ToolTip _tips = new();

    internal OptimizerForm(GameLibrary library, IReadOnlyList<GameProfile> profiles, IAutomaticRoutes? routes = null, IAutomaticMeter? meter = null, NetworkTracer? tracer = null, IRecoveryStore? recovery = null, string? dataDirectory = null)
    {
        _controlled = routes is not null; _library = library; _profiles = profiles;
        _dataDirectory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame");
        var warp = new WarpClient(new CommandRunner()); _routes = routes ?? warp;
        _meter = new ViewMeter(this, meter ?? new WarpMeter(new IcmpProbe(), new WindowsRouteObserver(), warp));
        _tracer = tracer ?? new NetworkTracer(new NetworkTraceProbe(), new WindowsRouteObserver());
        _recovery = recovery ?? new FileRecoveryStore(Path.Combine(_dataDirectory, "warp-recovery.json")); _lease = new(_routes, _recovery);
        Text = "RotaGame · PANT_CORE"; Icon = BrandAssets.LoadIcon(); BackColor = DashboardTheme.Canvas; ForeColor = DashboardTheme.Ink;
        Font = FontAssets.Ui(10); AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(1440, 880); MinimumSize = new Size(1000, 680);
        DoubleBuffered = true; BuildInterface();
        _activate.Click += async (_, _) => { if (_runTask is { IsCompleted: false } || PendingRecovery()) await StopAsync(); else _runTask = ActivateAsync(); };
        _prepare.Click += async (_, _) => await PrepareWarpAsync();
        _refresh.Text = "Recomparar rotas";
        _refresh.Click += (_, _) => { if (_destination is not null && !_comparing) { _repeatRequested = true; _targetCts?.Cancel(); } };
        _map.Cursor = Cursors.Hand; _map.Click += async (_, _) => { if (_destination is not null && !_comparing) await StartTraceAsync(_targetCts?.Token ?? default); };
        _export.Click += (_, _) => SaveResult(); _search.TextChanged += (_, _) => RebuildGames();
        _libraryTimer.Tick += async (_, _) => await RefreshLibraryAsync(); _libraryTimer.Start();
        Shown += async (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea; Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Arrange();
            if (!_controlled) try { using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(25)); await LegacyRecovery.RestoreAsync(_dataDirectory, ct.Token); await _lease.RestoreAsync(ct.Token); }
                catch (Exception ex) { _status.Text = "Restauração anterior pendente"; _hint.Text = ex.Message; }
        };
        FormClosing += async (_, e) =>
        {
            if (_closing) return; e.Cancel = true;
            _updateLifetime.Cancel(); if (_updateTask is not null) await _updateTask;
            if (await PrepareForUpdateAsync()) { _closing = true; Close(); }
        };
        FormClosed += (_, _) => { _libraryTimer.Dispose(); _updateTimer.Dispose(); _updateLifetime.Dispose(); _runCts?.Dispose(); _traceCts?.Dispose(); _tips.Dispose(); };
        Resize += (_, _) => Arrange(); RebuildGames(); SetControls();
    }
    private void BuildInterface()
    {
        var shell = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); shell.RowStyles.Add(new RowStyle(SizeType.AutoSize)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(shell);
        var top = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Padding = new Padding(14, 6, 12, 6), BackColor = DashboardTheme.Surface };
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
        top.Controls.Add(new DashboardBrand { Dock = DockStyle.Fill }, 0, 0);
        var brandTitle = Caption("RotaGame  ·  " + Application.ProductVersion.Split('+')[0] + "     /     PANT_CORE", 19);
        brandTitle.MouseDown += DragWindow; top.MouseDown += DragWindow; brandTitle.DoubleClick += (_,_) => ToggleMaximize();
        top.Controls.Add(brandTitle, 1, 0);
        var updates = Button("↻  Atualizações", false, 160); updates.Dock = DockStyle.Fill; updates.AccessibleName = "Atualizações do RotaGame"; updates.Click += (_, _) => ShowUpdates(); top.Controls.Add(updates, 2, 0);
        var chrome = new DashboardFlowLayout { Dock = DockStyle.Fill, WrapContents = false, BackColor = top.BackColor };
        AddChrome(DashboardWindowAction.Minimize, "Minimizar", () => WindowState = FormWindowState.Minimized);
        AddChrome(DashboardWindowAction.Maximize, "Maximizar ou restaurar", ToggleMaximize);
        AddChrome(DashboardWindowAction.Close, "Fechar", Close); top.Controls.Add(chrome, 3, 0); shell.Controls.Add(top, 0, 0);
        BuildUpdatePanel(); shell.Controls.Add(_updatePanel, 0, 1); shell.Controls.Add(_scroll, 0, 2); _scroll.SetContent(_body); _scroll.ViewportChanged += (_, _) => Arrange();
        _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); for (var i = 0; i < 7; i++) _body.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        var heading = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        heading.Controls.Add(Caption("Seus jogos. O melhor caminho medido.", 22), 0, 0);
        var searchBox = new DashboardSurface { Dock = DockStyle.Fill, Padding = new Padding(14, 7, 14, 7) }; _search.Dock = DockStyle.Fill; searchBox.Controls.Add(_search); heading.Controls.Add(searchBox, 1, 0);
        var add = Button("+ Adicionar jogo", false, 165); add.Dock = DockStyle.Fill; add.Click += (_, _) => AddGame(); heading.Controls.Add(add, 2, 0); _body.Controls.Add(heading, 0, 0);
        _games.Dock = DockStyle.Fill; _body.Controls.Add(_games, 0, 1);
        var actions = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(12, 8, 12, 8), BackColor = DashboardTheme.Surface, Margin = new Padding(0, 6, 0, 6) };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195));
        actions.Controls.Add(_mode, 0, 0); _prepare.Dock = _activate.Dock = DockStyle.Fill; actions.Controls.Add(_prepare, 1, 0); actions.Controls.Add(_activate, 2, 0); _body.Controls.Add(actions, 0, 2);
        var metrics = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var titles = new[] { "PING MÉDIO ATUAL", "VARIAÇÃO · JITTER", "PICOS DE LATÊNCIA · P95", "FALHAS DO TESTE ICMP" };
        for (var i = 0; i < 4; i++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); var card = new DashboardSurface { Dock = DockStyle.Fill, Padding = new Padding(15, 9, 15, 8), Margin = new Padding(0, 2, i == 3 ? 0 : 10, 5) };
            var title = Caption(titles[i], 9); title.Dock = DockStyle.Top; title.Height = 21; _metrics[i] = Caption("—", 28); _metrics[i].ForeColor = i == 0 ? DashboardTheme.Green : DashboardTheme.Ink; card.Controls.Add(_metrics[i]); card.Controls.Add(title); metrics.Controls.Add(card, i, 0);
        }
        _body.Controls.Add(metrics, 0, 3);
        var middle = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70)); middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        middle.Controls.Add(Section("Por onde sua conexão passa", _map), 0, 0); middle.Controls.Add(Section("Latência em tempo real", _chart), 1, 0); _body.Controls.Add(middle, 0, 4); _map.ContentChanged += (_, _) => Arrange();
        var results = new DashboardSurface { Dock = DockStyle.Fill, Padding = new Padding(12), Margin = new Padding(0, 5, 0, 5) };
        var resultHead = new DashboardTableLayout { Dock = DockStyle.Top, Height = 34, ColumnCount = 3, RowCount = 1 };
        resultHead.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resultHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); resultHead.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165)); resultHead.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        resultHead.Controls.Add(_summary, 0, 0); _refresh.Dock = _export.Dock = DockStyle.Fill; resultHead.Controls.Add(_refresh, 1, 0); resultHead.Controls.Add(_export, 2, 0);
        _table.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = DashboardTheme.Surface, ForeColor = DashboardTheme.Muted, Font = FontAssets.Ui(9) };
        _table.DefaultCellStyle = new DataGridViewCellStyle { BackColor = DashboardTheme.Surface, ForeColor = DashboardTheme.Ink, SelectionBackColor = DashboardTheme.Raised, SelectionForeColor = DashboardTheme.Ink, Font = FontAssets.Ui(10) }; _table.GridColor = DashboardTheme.Border; _table.RowTemplate.Height = 26;
        foreach (var name in new[] { "Caminho", "Ping médio", "P95", "Variação", "Falhas ICMP", "Decisão" }) _table.Columns.Add(name, name); _table.Columns[0].FillWeight = 160;
        results.Controls.Add(_table); results.Controls.Add(resultHead); _body.Controls.Add(results, 0, 5);
        var footer = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 30)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        footer.Controls.Add(_status, 0, 0); footer.Controls.Add(_hint, 0, 1); footer.Controls.Add(_destinationLabel, 0, 2); _body.Controls.Add(footer, 0, 6);
        _tips.SetToolTip(_map, "Saltos observados por ICMP. RTT acumulado, não latência por trecho. Saltos silenciosos e o interior do WARP podem ficar ocultos; as luzes são ilustrativas.");
        _tips.SetToolTip(_metrics[3], "ICMP pode ser filtrado. Falhas deste teste não comprovam perda dos pacotes UDP do jogo.");
        void AddChrome(DashboardWindowAction kind, string name, Action action)
        {
            var button = new DashboardWindowButton(kind) { Width = 39, Height = 36, AccessibleName = name, Margin = new Padding(2, 6, 2, 0) }; button.Click += (_, _) => action(); chrome.Controls.Add(button); _tips.SetToolTip(button, name);
        }
    }
    private static DashboardSurface Section(string title, Control body)
    {
        var card = new DashboardSurface { Dock = DockStyle.Fill, Padding = new Padding(13, 9, 13, 8), Margin = new Padding(0, 4, 8, 4) };
        var caption = Caption(title, 12); caption.Dock = DockStyle.Top; caption.Height = 28; body.Dock = DockStyle.Fill; card.Controls.Add(body); card.Controls.Add(caption); return card;
    }
    private static Label Caption(string text, float size) => new() { Text = text, Dock = DockStyle.Fill, ForeColor = size < 12 ? DashboardTheme.Muted : DashboardTheme.Ink, Font = size >= 18 ? FontAssets.Title(size, FontStyle.Bold) : FontAssets.Ui(size), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private static DashboardButton Button(string text, bool primary, int width) => new() { Text = text, Width = width, Height = 42, Font = FontAssets.Ui(11), BackColor = primary ? DashboardTheme.Accent : DashboardTheme.Raised, ForeColor = DashboardTheme.Ink, Margin = new Padding(3) };
    private bool _arranging;
    private void Arrange()
    {
        if (_arranging || _body.RowStyles.Count < 7) return; _arranging = true;
        try
        {
            var scale = DeviceDpi / 96f; var compact = _scroll.Height / scale < 820;
            var columns = _cards.Count == 1 ? 1 : Math.Clamp((int)(_games.Width / (275 * scale)), 1, 4);
            if (columns != _games.ColumnCount) LayoutGames(columns);
            var gameRows = Math.Max(1, (int)Math.Ceiling(_cards.Count / (double)columns));
            var heights = new[] { compact ? 43 : 54, gameRows * (compact ? 94 : 104), 76, compact ? 86 : 96, Math.Max(230, (int)Math.Ceiling(_map.ContentHeight / scale) + 64), _table.Rows.Count == 0 ? 97 : 146, compact ? 78 : 90 };
            for (var i = 0; i < heights.Length; i++) _body.RowStyles[i].Height = heights[i] * scale;
            _scroll.SetExtent((int)((heights.Sum() + 22) * scale));
        }
        finally { _arranging = false; }
    }
    private void LayoutGames(int columns)
    {
        _games.SuspendLayout(); _games.Controls.Clear(); _games.ColumnStyles.Clear(); _games.RowStyles.Clear(); _games.ColumnCount = columns; _games.RowCount = Math.Max(1, (int)Math.Ceiling(_cards.Count / (double)columns));
        for (var i = 0; i < columns; i++) _games.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        for (var i = 0; i < _games.RowCount; i++) _games.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _games.RowCount));
        for (var i = 0; i < _cards.Count; i++) _games.Controls.Add(_cards[i], i % columns, i / columns); _games.ResumeLayout();
    }
    private void RebuildGames()
    {
        foreach (var card in _cards) card.Dispose(); _cards.Clear(); _games.Controls.Clear();
        foreach (var profile in _profiles.Where(p => p.Name.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            var card = new GameCard(profile) { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 10, 4), SelectedGame = _chosenGame == profile.Id };
            card.Click += (_, _) => { if (_runTask is { IsCompleted: false }) return; _chosenGame = _chosenGame == profile.Id ? null : profile.Id; RebuildGames(); SetControls(); };
            var menu = new ContextMenuStrip(); var remove = menu.Items.Add("Remover da biblioteca"); remove.Click += (_, _) => { if (_runTask is { IsCompleted: false }) return; _library.Remove(profile.Id); _profiles = _library.Load(); if (_chosenGame == profile.Id) _chosenGame = null; RebuildGames(); }; card.ContextMenuStrip = menu; _cards.Add(card);
        }
        var adding = new GameCard(null) { Dock = DockStyle.Fill, EmptyLibrary = _profiles.Count == 0, Margin = new Padding(0, 4, 0, 4) }; adding.Click += (_, _) => AddGame(); _cards.Add(adding);
        LayoutGames(Math.Max(1, Math.Min(4, _cards.Count))); Arrange(); UpdateRunningGames();
    }
    private void AddGame()
    {
        using var dialog = new OpenFileDialog { Filter = "Executável do jogo (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { _library.Add(dialog.FileName); _profiles = _library.Load(); RebuildGames(); } catch (Exception ex) { _hint.Text = ex.Message; }
    }
    private void UpdateRunningGames()
    {
        var running = GameLibrary.Running(_profiles); foreach (var card in _cards.Where(c => c.Profile is not null)) { card.Running = running.GetValueOrDefault(card.Profile!.Id)?.Length > 0; card.Invalidate(); }
    }
    private async Task RefreshLibraryAsync()
    {
        if (_scanningLibrary || IsDisposed) return; UpdateRunningGames();
        if (_rolling.LastOrDefault() is { } last && DateTimeOffset.UtcNow - last.At > TimeSpan.FromSeconds(5)) { foreach (var metric in _metrics) metric.Text = "—"; _map.SetSample(null); }
        if (DateTime.UtcNow - _lastLibraryScan < TimeSpan.FromSeconds(30) || _runTask is { IsCompleted: false }) return;
        _scanningLibrary = true;
        try { var found = await Task.Run(_library.Load); if (!IsDisposed) { _profiles = found; RebuildGames(); } }
        catch (Exception ex) { _hint.Text = "Não consegui atualizar a biblioteca: " + ex.Message; }
        finally { _scanningLibrary = false; _lastLibraryScan = DateTime.UtcNow; }
    }
    private void SetControls()
    {
        var active = _runCts is { IsCancellationRequested: false };
        _activate.Text = _stopping ? "Restaurando…" : active ? "■  Desativar" : PendingRecovery() ? "Restaurar conexão" : "▶  Ativar"; _activate.Enabled = !_stopping && !_settingUp;
        _prepare.Enabled = !active && !_settingUp && !_stopping; _refresh.Enabled = active && !_comparing && _destination is not null; _export.Enabled = _report is not null;
        _mode.Text = _chosenGame is null ? "AUTOMÁTICO · todos os jogos\nConexão direta ou Cloudflare WARP gratuito" : "JOGO SELECIONADO\n" + _profiles.FirstOrDefault(p => p.Id == _chosenGame)?.Name;
        Arrange();
    }
    private async Task PrepareWarpAsync()
    {
        if (_controlled || _settingUp) return; _settingUp = true; SetControls();
        try
        {
            if (!WarpConsent.Granted()) { using var terms = new WarpConsentWindow(); if (terms.ShowDialog(this) != DialogResult.OK) return; }
            await OfficialWarpSetup.LaunchAsync(new UiProgress<string>(s => _hint.Text = s), _updateLifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _hint.Text = ex.Message; }
        finally { _settingUp = false; SetControls(); }
    }
    private bool PendingRecovery()
    {
        try { return _lease.Active || _recovery.Load() is not null || (!_controlled && (new FileRecoveryStore(Path.Combine(_dataDirectory,"connection-recovery.json")).Load() is not null || new FileBridgeOfferStore(Path.Combine(_dataDirectory,"bridge-recovery.json")).Load() is not null)); }
        catch (Exception) { return true; }
    }
    private void SaveResult()
    {
        if (_report is null) return;
        using var save = new SaveFileDialog { Filter = "Resultado JSON (*.json)|*.json", FileName = "RotaGame-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json" };
        if (save.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(save.FileName, JsonSerializer.Serialize(new { Report = _report, Trace = _map.Trace, Current = _selected, Recent = _chart.Samples.TakeLast(180) }, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } })); }
        catch (Exception ex) { _hint.Text = "Não foi possível salvar: " + ex.Message; }
    }
    private sealed class UiProgress<T>(Action<T> action) : IProgress<T>
    {
        private readonly SynchronizationContext? _context = SynchronizationContext.Current;
        public void Report(T value) { if (_context is not null && SynchronizationContext.Current != _context) _context.Send(_ => action(value), null); else action(value); }
    }
    private sealed class ViewMeter(OptimizerForm owner, IAutomaticMeter inner) : IAutomaticMeter
    {
        public Task<MeasurementBlock> MeasureAsync(string target, RouteOption route, ProbeSettings settings, IProgress<MeasurementProgress>? progress, CancellationToken ct)
        {
            if (owner._measuring != route) { owner._measuring = route; owner.ClearLive(); }
            return inner.MeasureAsync(target, route, settings, progress, ct);
        }
    }
}
