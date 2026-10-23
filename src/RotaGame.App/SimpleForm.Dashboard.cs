using System.Diagnostics;
using System.Runtime.InteropServices;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    private readonly List<GameProfile> _profiles = [];
    private readonly DashboardPanel _gamesPanel = new() { BackColor = DashboardTheme.Canvas };
    private readonly DashboardScrollHost _pageScroll = new(false) { Dock = DockStyle.Fill };
    private readonly DashboardScrollHost _resultScroll = new(false) { Dock = DockStyle.Fill, BackColor = DashboardTheme.Surface };
    private readonly DashboardTableLayout _body = new() { ColumnCount = 1, RowCount = 7, BackColor = DashboardTheme.Canvas };
    private readonly DashboardPanel _pageHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = DashboardTheme.Canvas };
    private ConnectionPage? _connectionPage;
    private bool _sizingCards, _sizingPage, _libraryScanBusy;
    private int _gameRows = 1;
    private DateTime _lastLibraryScan = DateTime.MinValue;
    private readonly TextBox _search = new() { BorderStyle = BorderStyle.None, BackColor = DashboardTheme.Raised, ForeColor = DashboardTheme.Ink };
    private readonly DashboardToggle _master = new();
    private readonly RouteDiagram _routeMap = new() { Dock = DockStyle.Fill };
    private readonly Label _liveTag = Label("AGUARDANDO", 8, Muted), _libraryStatus = Label("Reconhecimento automático ao iniciar", 9, Muted);
    private readonly Label _emptyResults = Label("Ative para comparar os caminhos disponíveis.", 10, Muted);
    private readonly System.Windows.Forms.Timer _gameWatcher = new() { Interval = 2000 };
    private readonly Button _automaticGame = Button("Detecção automática");
    private readonly Button _addGameHeader = Button("+ Adicionar jogo");
    private Dictionary<string, int[]> _runningGames = [];
    private string? _activeProfileId;

    private void BuildDashboard()
    {
        Text = "RotaGame · jogos e conexões"; ClientSize = new Size(1340, 900); MinimumSize = new Size(1080, 720);
        Icon = BrandAssets.LoadIcon();
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        BackColor = Canvas; ForeColor = Ink; Font = FontAssets.Ui(10);
        Padding = new Padding(1);
        var shell = _shell = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Canvas, Margin = Padding.Empty };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 0)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);
        var top = new DashboardPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 19, 26), Margin = Padding.Empty };
        var headerGrid = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty, BackColor = top.BackColor };
        headerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310)); headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142)); top.Controls.Add(headerGrid);
        _master.Dock = DockStyle.Fill; _master.Margin = new Padding(25, 13, 0, 13); _master.BackColor = top.BackColor;
        _master.Click += async (_, _) => await ToggleConnectionAsync();
        headerGrid.Controls.Add(_master, 0, 0);
        var chrome = new DashboardFlowLayout { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(6, 12, 0, 0), BackColor = top.BackColor, Margin = Padding.Empty };
        AddWindowButton(DashboardWindowAction.Minimize, "Minimizar", () => WindowState = FormWindowState.Minimized);
        AddWindowButton(DashboardWindowAction.Maximize, "Maximizar ou restaurar", ToggleMaximize);
        AddWindowButton(DashboardWindowAction.Close, "Fechar", Close);
        headerGrid.Controls.Add(chrome, 3, 0);
        var searchBox = new DashboardSurface { Dock = DockStyle.Fill, Margin = new Padding(10, 13, 10, 13), BackColor = DashboardTheme.Raised, Padding = new Padding(13, 9, 12, 7) };
        _search.Dock = DockStyle.Fill; searchBox.Controls.Add(_search); headerGrid.Controls.Add(searchBox, 2, 0);
        var searchCaption = Label("⌕  Buscar jogos", 9, Muted); searchCaption.Dock = DockStyle.Left; searchCaption.Width = 98; searchCaption.BackColor = DashboardTheme.Raised;
        searchBox.Controls.Add(searchCaption);
        _search.TextChanged += (_, _) => RebuildCards();
        top.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } };
        headerGrid.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } };
        top.DoubleClick += (_, _) => ToggleMaximize();
        shell.Controls.Add(top, 0, 0);
        var workspace = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78)); workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); shell.Controls.Add(workspace, 0, 2);
        BuildUpdateBanner(); shell.Controls.Add(_updateBanner, 0, 1);
        var sidebar = new DashboardFlowLayout { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(13, 17, 23), Padding = new Padding(11, 18, 0, 0), Margin = Padding.Empty };
        var logo = new DashboardBrand { Size = new Size(52, 52), BackColor = sidebar.BackColor, AccessibleName = "RotaGame" }; sidebar.Controls.Add(logo); _tips.SetToolTip(logo, "RotaGame");
        AddNav("⌂", "Jogos e conexões", ShowDashboard);
        AddNav("⇄", "Usar este PC como ponte", () => { ShowDashboard(); SelectRole(RotaGame.Core.DeviceRole.Bridge); });
        AddNav("⚙", "Conta e conexão", () => _setup.PerformClick());
        AddNav("↻", "Atualizações do RotaGame", ShowUpdates);
        var version = Label("0.8", 9, Muted); version.Dock = DockStyle.None; version.Size = new Size(52, 30); version.TextAlign = ContentAlignment.MiddleCenter; sidebar.Controls.Add(version);
        workspace.Controls.Add(sidebar, 0, 0);
        workspace.Controls.Add(_pageHost, 1, 0); _pageHost.Controls.Add(_pageScroll);
        var body = _body; body.Padding = new Padding(24, 10, 24, 16);
        foreach (var h in new[] { 52, 108, 70, 96, 190, 128, 50 }) body.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        body.RowStyles[4].SizeType = body.RowStyles[5].SizeType = SizeType.Percent; body.RowStyles[4].Height = 62; body.RowStyles[5].Height = 38;
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); _pageScroll.SetContent(body);
        _pageScroll.SizeChanged += (_, _) => ArrangeDashboard(); _pageScroll.ViewportChanged += (_, _) => ArrangeDashboard();
        var title = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        title.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        title.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); title.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var libraryTitle = new DashboardPanel { Dock = DockStyle.Fill };
        libraryTitle.Controls.Add(Label("Jogos e conexões", 21, Ink));
        _automaticGame.Dock = DockStyle.Right; _automaticGame.Width = 205; _automaticGame.AutoSize = false; _automaticGame.Font = FontAssets.Ui(8.5f);
        libraryTitle.Controls.Add(_automaticGame); title.Controls.Add(libraryTitle, 0, 0);
        var libraryHeading = new DashboardPanel { Dock = DockStyle.Fill, BackColor = Canvas };
        var addHolder = new DashboardPanel { Dock = DockStyle.Right, Width = 151, BackColor = Canvas };
        _addGameHeader.Size = new Size(146, 34); _addGameHeader.AutoSize = false; _addGameHeader.Location = new Point(5, 12); _addGameHeader.Font = FontAssets.Ui(9);
        _addGameHeader.Click += (_, _) => AddGame(); addHolder.Controls.Add(_addGameHeader);
        _libraryStatus.TextAlign = ContentAlignment.MiddleRight; _libraryStatus.Font = FontAssets.Ui(8);
        libraryHeading.Controls.Add(_libraryStatus); libraryHeading.Controls.Add(addHolder); title.Controls.Add(libraryHeading, 1, 0); body.Controls.Add(title, 0, 0);
        _gamesPanel.Dock = DockStyle.Fill; _gamesPanel.Margin = Padding.Empty; body.Controls.Add(_gamesPanel, 0, 1); _gamesPanel.SizeChanged += (_, _) => ResizeCards();
        var actionCard = new DashboardSurface { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 7), Padding = new Padding(10, 7, 10, 7) };
        var controlGrid = _actionsGrid = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        controlGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); controlGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 244));
        controlGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 94)); controlGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
        var choices = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); choices.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _playerChoice.Dock = _bridgeChoice.Dock = DockStyle.Fill; _playerChoice.Margin = _bridgeChoice.Margin = Padding.Empty;
        choices.Controls.Add(_playerChoice, 0, 0); choices.Controls.Add(_bridgeChoice, 1, 0); controlGrid.Controls.Add(choices, 0, 0);
        _activate.Dock = DockStyle.Fill; _activate.AutoSize = false; _activate.Margin = new Padding(10, 17, 0, 17); controlGrid.Controls.Add(_activate, 1, 0);
        _deactivate.Visible = false;
        var identityGrid = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(7, 0, 0, 0) };
        identityGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); identityGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        identityGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); identityGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        identityGrid.Controls.Add(_identity, 0, 0); identityGrid.Controls.Add(_partnerCaption, 0, 1);
        _pickPartner.Dock = DockStyle.Fill; _pickPartner.AutoSize = false; _pickPartner.Font = FontAssets.Ui(9); identityGrid.Controls.Add(_pickPartner, 1, 0); identityGrid.SetRowSpan(_pickPartner, 2);
        controlGrid.Controls.Add(identityGrid, 0, 1);
        _automaticGame.Click += (_, _) => { if (!_active) { _game.SelectedIndex = 0; RebuildCards(); } };
        _quickState.TextAlign = ContentAlignment.MiddleCenter; controlGrid.Controls.Add(_quickState, 1, 1);
        actionCard.Controls.Add(controlGrid); body.Controls.Add(actionCard, 0, 2);
        var metrics = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Canvas };
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var metricNames = new[] { "PING MÉDIO ATUAL", "VARIAÇÃO DO PING", "PICOS DE LATÊNCIA · P95", "FALHAS DO TESTE" };
        for (var i = 0; i < 4; i++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var card = new DashboardSurface { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 12, 8), Margin = new Padding(0, 3, i == 3 ? 0 : 10, 8) };
            var cardLayout = new DashboardTableLayout { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = PanelColor };
            cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _metricTitles[i] = Label(metricNames[i], 8, Muted); cardLayout.Controls.Add(_metricTitles[i], 0, 0); _metrics[i] = Label("—", i == 0 ? 28 : 24, i == 0 ? Mint : Ink); cardLayout.Controls.Add(_metrics[i], 0, 1);
            card.Controls.Add(cardLayout); metrics.Controls.Add(card, i, 0);
        }
        body.Controls.Add(metrics, 0, 3);
        var middle = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61)); middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        var diagram = Section("Caminhos da conexão", _routeMap); diagram.Margin = new Padding(0, 0, 10, 8); middle.Controls.Add(diagram, 0, 0);
        _routeMap.LayoutNeedsChanged += (_, _) => ArrangeDashboard();
        var chart = Section("Latência em tempo real", _chart); chart.Margin = new Padding(0, 0, 0, 8); _chart.BackColor = PanelColor; middle.Controls.Add(chart, 1, 0); body.Controls.Add(middle, 0, 4);
        var resultCard = new DashboardSurface { Dock = DockStyle.Fill, Padding = new Padding(16, 11, 16, 12), Margin = new Padding(0, 0, 0, 7) };
        SetupTable(); _table.BackColor = PanelColor;
        _table.ColumnHeadersDefaultCellStyle.BackColor = PanelColor; _table.GridColor = DashboardTheme.Border; _table.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _table.Columns[1].HeaderText = "Ping médio"; _table.AllowUserToResizeRows = false; _table.RowTemplate.Height = 28;
        var resultBody = new DashboardPanel { Dock = DockStyle.Fill, BackColor = PanelColor }; _table.ScrollBars = ScrollBars.None; _resultScroll.SetContent(_table); resultBody.Controls.Add(_resultScroll);
        _resultScroll.SizeChanged += (_, _) => UpdateResultScroll(); _table.RowsAdded += (_, _) => UpdateResultScroll(); _table.RowsRemoved += (_, _) => UpdateResultScroll();
        _emptyResults.TextAlign = ContentAlignment.MiddleCenter; resultBody.Controls.Add(_emptyResults); _emptyResults.BringToFront();
        var resultHeader = new DashboardTableLayout { Dock = DockStyle.Top, Height = 34, BackColor = PanelColor, RowCount = 1, ColumnCount = 3 };
        resultHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 186)); resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        var resultTitle = Label("Caminhos comparados", 11, Ink);
        _baseline.Font = FontAssets.Ui(8.5f); _baseline.ForeColor = Muted;
        resultHeader.Controls.Add(resultTitle, 0, 0); resultHeader.Controls.Add(_baseline, 1, 0);
        _export.Dock = DockStyle.Fill; _export.AutoSize = false; _export.Font = FontAssets.Ui(8); _export.Margin = new Padding(4, 0, 0, 2); resultHeader.Controls.Add(_export, 2, 0);
        resultCard.Controls.Add(resultBody); resultCard.Controls.Add(resultHeader); body.Controls.Add(resultCard, 0, 5);
        var footer = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        var signature = Label("PANT_CORE", 12, Muted); signature.Font = FontAssets.Title(15, FontStyle.Bold); signature.TextAlign = ContentAlignment.BottomRight;
        footer.Controls.Add(signature, 1, 0); footer.SetRowSpan(signature, 2);
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _state.Font = FontAssets.Ui(11, FontStyle.Bold); _state.ForeColor = Ink; footer.Controls.Add(_state, 0, 0); footer.Controls.Add(_message, 0, 1); body.Controls.Add(footer, 0, 6);
        // Context labels remain available as tooltips; the main screen stays focused on play and current measurements.
        _baseline.TextChanged += (_, _) => _tips.SetToolTip(_table, _baseline.Text);
        _destination.TextChanged += (_, _) => _tips.SetToolTip(_routeMap, _destination.Text);
        _message.TextChanged += (_, _) => _tips.SetToolTip(_message, _message.Text);
        _game.SelectedIndexChanged += (_, _) => { UpdateCardStates(); if (!_active) SetControls(); };
        if (_startupProfiles is { } profiles) { ApplyLibrary(profiles); _lastLibraryScan = DateTime.UtcNow; }
        else ReloadLibrary();
        _gameWatcher.Tick += async (_, _) =>
        {
            RefreshRunningGames();
            if ((!_active || _activeProfileId is null) && !_libraryScanBusy && DateTime.UtcNow - _lastLibraryScan > TimeSpan.FromSeconds(30))
            {
                _libraryScanBusy = true;
                try { var found = await Task.Run(_library.Load); if (!IsDisposed && (!_active || _activeProfileId is null)) ApplyLibrary(found); }
                catch (Exception ex) { if (!IsDisposed) _message.Text = "Não consegui atualizar os jogos instalados: " + ex.Message; }
                finally { _libraryScanBusy = false; _lastLibraryScan = DateTime.UtcNow; }
            }
        }; _gameWatcher.Start();
        FormClosed += (_, _) => { _gameWatcher.Stop(); _gameWatcher.Dispose(); };
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            if (Width > area.Width || Height > area.Height) { Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); Location = area.Location; }
            ArrangeDashboard(); ResizeCards();
        };
        ArrangeDashboard();

        DashboardSurface Section(string heading, Control content)
        {
            var surface = new DashboardSurface { Dock = DockStyle.Fill, Padding = new Padding(16, 11, 16, 8) };
            var caption = Label(heading, 11, Ink); caption.Dock = DockStyle.Top; caption.Height = 31;
            content.Dock = DockStyle.Fill; surface.Controls.Add(content); surface.Controls.Add(caption); return surface;
        }
        void AddWindowButton(DashboardWindowAction kind, string name, Action action)
        {
            var button = new DashboardWindowButton(kind) { Text = "", Size = new Size(37, 34), BackColor = top.BackColor, ForeColor = Ink, AccessibleName = name, Margin = new Padding(0, 0, 3, 0) };
            button.Click += (_, _) => action(); _tips.SetToolTip(button, name); chrome.Controls.Add(button);
        }
        void AddNav(string text, string name, Action action)
        {
            var button = new DashboardButton { Text = text, Size = new Size(52, 45), BackColor = sidebar.BackColor, ForeColor = Muted, Font = new Font("Segoe UI Symbol", 19), AccessibleName = name, Margin = new Padding(0, 12, 0, 0) };
            button.Click += (_, _) => action(); _tips.SetToolTip(button, name); sidebar.Controls.Add(button);
        }
    }

    private void ShowConnectionPage(ConnectionSection section = ConnectionSection.Account)
    {
        if ((_active && !_offering) || _restoring) return;
        if (_connectionPage is null)
        {
            _connectionPage = new ConnectionPage(_connectionSession, _connectionUrl, _inviteClipboard, () => _device, SelectRole);
            _connectionPage.BusyChanged += (_, _) => SetControls();
            _connectionPage.PrepareBridgeRequested += (_, _) => { if (!_active) { _host.Checked = true; _operation = ActivateAsync(); } };
            _connectionPage.StatusChanged += status =>
            {
                if (_offering) { _state.Text = status?.SelfApproved == true ? "Ponte disponível" : "Falta autorizar este PC uma vez"; SyncDashboard(); }
            };
            _connectionPage.BackRequested += (_, _) => ShowDashboard(); _pageHost.Controls.Add(_connectionPage);
        }
        _connectionPage.SetOffering(_offering, _active || _restoring); _connectionPage.SelectSection(section);
        _pageScroll.Visible = false; _connectionPage.Visible = true; _connectionPage.BringToFront(); _search.Enabled = false; Invalidate(true);
    }
    private void ShowDashboard()
    {
        if (_connectionPage is not null) _connectionPage.Visible = false;
        _pageScroll.Visible = true; _pageScroll.BringToFront(); _search.Enabled = true; ArrangeDashboard(); Invalidate(true);
    }

    internal async Task RunAccountSmokeAsync(string directory)
    {
        WindowState = FormWindowState.Normal; ClientSize = new Size(1340, 860); ShowConnectionPage(); await Task.Delay(100);
        await _connectionPage!.RunSmokeAsync(directory); await Task.Delay(100);
        using var image = new Bitmap(Width, Height); DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save(Path.Combine(directory, "interface-account-test.png"));
        ShowDashboard(); if (!_pageScroll.Visible || _connectionPage.Visible) throw new InvalidOperationException("Não voltou à biblioteca.");
    }

    private void ReloadLibrary(string? selectId = null)
    {
        try { ApplyLibrary(_library.Load(), selectId); _lastLibraryScan = DateTime.UtcNow; }
        catch (Exception ex) { ApplyLibrary([]); _message.Text = "Não foi possível ler a biblioteca: " + ex.Message; }
    }

    private void ApplyLibrary(IReadOnlyList<GameProfile> found, string? selectId = null)
    {
        var prior = selectId ?? (_game.SelectedIndex > 0 && _game.SelectedIndex <= _profiles.Count ? _profiles[_game.SelectedIndex - 1].Id : null);
        _profiles.Clear(); _profiles.AddRange(found);
        _game.Items.Clear(); _game.Items.Add("Detectar automaticamente"); foreach (var profile in _profiles) _game.Items.Add(profile.Name);
        _game.SelectedIndex = prior is null ? 0 : Math.Max(0, _profiles.FindIndex(p => p.Id == prior) + 1);
        RefreshRunningGames(); RebuildCards();
    }

    private void RebuildCards()
    {
        _gamesPanel.SuspendLayout();
        var old = _gamesPanel.Controls.Cast<Control>().ToList(); _gamesPanel.Controls.Clear(); foreach (var control in old) control.Dispose();
        foreach (var profile in _profiles.Where(p => p.Name.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            var card = new GameCard(profile) { Height = 106, Margin = new Padding(0, 0, 11, 0), Enabled = !_active };
            card.GotFocus += (_, _) => _pageScroll.Reveal(_gamesPanel.Top + card.Top, card.Height);
            card.Click += (_, _) => { if (!_active) { _game.SelectedIndex = _profiles.FindIndex(p => p.Id == profile.Id) + 1; UpdateCardStates(); } };
            if (profile.ExecutablePath is not null)
            {
                var menu = new ContextMenuStrip(); menu.Items.Add("Remover da biblioteca", null, (_, _) => { if (!_active) { _library.Remove(profile.Id); ReloadLibrary(); } }); card.ContextMenuStrip = menu;
            }
            _gamesPanel.Controls.Add(card);
        }
        var add = new GameCard(null) { Height = 106, Margin = new Padding(0), Enabled = !_active, EmptyLibrary = _profiles.Count == 0 }; add.Click += (_, _) => AddGame(); _gamesPanel.Controls.Add(add);
        add.GotFocus += (_, _) => _pageScroll.Reveal(_gamesPanel.Top + add.Top, add.Height);
        ResizeCards(); UpdateCardStates(); _gamesPanel.ResumeLayout();
    }

    private void ResizeCards()
    {
        if (_sizingCards || _gamesPanel.Controls.Count == 0) return; _sizingCards = true;
        try
        {
            var scale = DeviceDpi / 96f; var gap = (int)(11 * scale); var count = _gamesPanel.Controls.Count;
            var available = _gamesPanel.ClientSize.Width;
            var columns = Math.Min(count, Math.Max(1, (available + gap) / ((int)(240 * scale) + gap)));
            var width = Math.Max(1, (available - (columns - 1) * gap) / columns); var height = (int)((_pageScroll.ClientSize.Height < 790 * scale ? 80 : 108) * scale);
            _gameRows = (count + columns - 1) / columns;
            for (var i = 0; i < count; i++) _gamesPanel.Controls[i].SetBounds(i % columns * (width + gap), i / columns * (height + gap), width, height);
        }
        finally { _sizingCards = false; }
        ArrangeDashboard();
    }

    private void ArrangeDashboard()
    {
        if (_sizingPage || _body.RowStyles.Count < 7) return; _sizingPage = true;
        try
        {
            var s = DeviceDpi / 96f; var compact = _pageScroll.ClientSize.Height < 790 * s;
            var cardHeight = compact ? 80 : 108;
            if (_gamesPanel.Controls.Count > 0 && _gamesPanel.Controls[0].Height != (int)(cardHeight * s)) ResizeCards();
            var libraryHeight = (_gameRows * cardHeight + (_gameRows - 1) * 11) * s;
            _body.RowStyles[0].Height = (compact ? 40 : 52) * s; _body.RowStyles[1].Height = libraryHeight;
            _body.RowStyles[2].Height = (compact ? 132 : 160) * s; _body.RowStyles[3].Height = (compact ? 84 : 96) * s; _body.RowStyles[6].Height = (compact ? 48 : 50) * s;
            if (_actionsGrid is not null) { _actionsGrid.RowStyles[0].Height = (compact ? 80 : 94) * s; _actionsGrid.RowStyles[1].Height = (compact ? 27 : 41) * s; }
            _activate.Margin = new Padding((int)(10 * s), (int)((compact ? 10 : 17) * s), 0, (int)((compact ? 10 : 17) * s));
            _identity.Font = _partnerCaption.Font = FontAssets.Ui(compact ? 8.5f : 10);
            for (var i = 0; i < 4; i++) if (_metrics[i] is not null) _metrics[i].Font = FontAssets.Title(i == 0 ? compact ? 24 : 28 : compact ? 20 : 24);
            _addGameHeader.Top = (int)((compact ? 4 : 12) * s);
            var middleHeight = Math.Max(190, _routeMap.PreferredContentHeight + 66) * s;
            var minimum = (compact ? 710 : 790) * s + libraryHeight - cardHeight * s + middleHeight - 190 * s;
            var extra = Math.Max(0, _pageScroll.ClientSize.Height - minimum);
            _body.RowStyles[4].SizeType = SizeType.Absolute; _body.RowStyles[4].Height = middleHeight + extra * .62f;
            _pageScroll.SetExtent(Math.Max(_pageScroll.ClientSize.Height, (int)minimum));
        }
        finally { _sizingPage = false; }
    }

    private void RefreshRunningGames()
    {
        _runningGames = GameLibrary.Running(_profiles); UpdateCardStates();
        var count = _runningGames.Count(p => p.Value.Length > 0);
        _libraryStatus.Text = _profiles.Count == 0 ? "Biblioteca vazia" : count == 0 ? $"{_profiles.Count} jogo(s) instalado(s) · fechados" : $"{count} {(count == 1 ? "jogo reconhecido" : "jogos reconhecidos")} · em execução";
    }

    private void UpdateCardStates()
    {
        foreach (var card in _gamesPanel.Controls.OfType<GameCard>())
        {
            card.Running = card.Profile is { } p && _runningGames.GetValueOrDefault(p.Id)?.Length > 0;
            card.SelectedGame = card.Profile is { } profile && (profile.Id == _activeProfileId || _game.SelectedIndex > 0 && _profiles[_game.SelectedIndex - 1].Id == profile.Id);
            card.Invalidate();
        }
    }

    private void AddGame()
    {
        if (_active) return;
        using var picker = new OpenFileDialog { Title = "Adicionar jogo · escolha o executável da partida", Filter = "Executável do jogo (*.exe)|*.exe", CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var profile = _library.Add(picker.FileName); ReloadLibrary(profile.Id);
            _message.Text = profile.Name + " adicionado. O programa reconhece quando esse executável é iniciado.";
        }
        catch (Exception ex) { _message.Text = "Não foi possível adicionar: " + ex.Message; }
    }

    private void SyncDashboard()
    {
        _master.Checked = _active; _master.Enabled = !_restoring && _connectionPage?.Busy != true;
        _addGameHeader.Enabled = !_active;
        _automaticGame.Enabled = !_active; _automaticGame.Text = _game.SelectedIndex == 0 ? "Automático · todos os jogos" : "Usar detecção automática";
        ((DashboardButton)_automaticGame).SelectedStyle = _game.SelectedIndex == 0; _automaticGame.Invalidate();
        foreach (Control card in _gamesPanel.Controls) card.Enabled = !_active;
        _table.Visible = _resultScroll.Visible = _table.Rows.Count > 0; _emptyResults.Visible = !_table.Visible; UpdateResultScroll();
        if (_remoteShown) return;
        _routeMap.BridgeMode = _host.Checked;
        _routeMap.Report = _routeMap.Destination is not null && _report?.Destination == _routeMap.Destination ? _report : null;
        _routeMap.SetActivity(_active && !_host.Checked && !_restoring && _cts?.IsCancellationRequested != true && _routeMap.Destination is not null,
            _routeMap.Report?.Choice is not null ? _selected : null);
        _liveTag.Text = _active ? "AO VIVO" : "AGUARDANDO";
    }

    private void UpdateResultScroll() => _resultScroll.SetExtent(_table.ColumnHeadersHeight + _table.Rows.Cast<DataGridViewRow>().Sum(row => row.Height) + 2);

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wparam, IntPtr lparam);
    private void ToggleMaximize()
    {
        if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
        else { MaximizedBounds = Screen.FromControl(this).WorkingArea; WindowState = FormWindowState.Maximized; }
        Invalidate(true);
    }

    private bool _repaintQueued;
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e); Invalidate(true);
        if (!IsHandleCreated || _repaintQueued || IsDisposed) return;
        _repaintQueued = true;
        BeginInvoke(() => { _repaintQueued = false; if (!IsDisposed) Invalidate(true); });
    }
    protected override void OnResizeEnd(EventArgs e) { base.OnResizeEnd(e); Invalidate(true); }
    protected override void OnDpiChanged(DpiChangedEventArgs e) { base.OnDpiChanged(e); ArrangeDashboard(); ResizeCards(); Invalidate(true); }
    protected override void WndProc(ref Message message)
    {
        const int hitTest = 0x84;
        if (message.Msg == hitTest && WindowState == FormWindowState.Normal)
        {
            var p = PointToClient(new Point(unchecked((short)(long)message.LParam), unchecked((short)((long)message.LParam >> 16))));
            var left = p.X < 6; var right = p.X >= ClientSize.Width - 6; var top = p.Y < 6; var bottom = p.Y >= ClientSize.Height - 6;
            if (left || right || top || bottom) { message.Result = (IntPtr)(top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : 15); return; }
        }
        base.WndProc(ref message);
    }
}
