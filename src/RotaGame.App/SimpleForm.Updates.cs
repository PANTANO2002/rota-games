namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    private UpdateWorkflow? _updates;
    private readonly CancellationTokenSource _updatesLifetime = new();
    private readonly System.Windows.Forms.Timer _updatesTimer = new() { Interval = 15 * 60 * 1000 };
    private Task? _updateTask;
    private readonly DashboardPanel _updateBanner = new() { BackColor = DashboardTheme.Canvas, Dock = DockStyle.Fill, Visible = false, Margin = Padding.Empty, Padding = new Padding(90, 2, 20, 3) };
    private readonly Label _updateCaption = Label("Nova atualização disponível", 18, DashboardTheme.Ink);
    private readonly Label _updateHint = Label("Atualize agora ou continue e decida depois.", 9, DashboardTheme.Muted);
    private DashboardTableLayout? _shell;
    private DashboardButton? _updateNow, _updateLater;
    private UpdateWindow? _updateWindow;

    private void BuildUpdateBanner()
    {
        var surface = new DashboardSurface { Dock = DockStyle.Fill, Padding = new Padding(14, 5, 10, 5) }; _updateBanner.Controls.Add(surface);
        var actions = new DashboardFlowLayout { Dock = DockStyle.Right, Width = 305, WrapContents = false, BackColor = DashboardTheme.Surface };
        _updateNow = new DashboardButton { Text = "Atualizar agora", Width = 170, Height = 42, BackColor = DashboardTheme.Accent, ForeColor = DashboardTheme.Ink, Font = FontAssets.Ui(10) };
        _updateLater = new DashboardButton { Text = "Depois", Width = 115, Height = 42, BackColor = DashboardTheme.Raised, ForeColor = DashboardTheme.Ink, Font = FontAssets.Ui(10) };
        _updateNow.Click += (_, _) => _updateTask = InstallUpdateAsync(); _updateLater.Click += (_, _) => _updates?.Defer(); actions.Controls.AddRange([_updateNow, _updateLater]);
        var text = new DashboardPanel { Dock = DockStyle.Fill, BackColor = DashboardTheme.Surface };
        _updateCaption.Dock = DockStyle.Top; _updateCaption.Height = 27; _updateHint.Dock = DockStyle.Fill; text.Controls.Add(_updateHint); text.Controls.Add(_updateCaption);
        surface.Controls.Add(text); surface.Controls.Add(actions);
    }
    internal void ConfigureUpdates(IAppUpdates source, string? startupMessage = null)
    {
        _updates = new(source, PrepareForUpdateAsync); _updates.Changed += (_, _) => UpdateUi();
        _updatesTimer.Tick += (_, _) => { if (_updates?.Busy != true) _updateTask = CheckUpdatesAsync(); };
        _updatesTimer.Start();
        if (startupMessage is not null) _tips.SetToolTip(_updateBanner, startupMessage);
        FormClosed += (_, _) => { _updatesTimer.Stop(); _updatesTimer.Dispose(); _updatesLifetime.Cancel(); _updatesLifetime.Dispose(); };
        UpdateUi();
    }
    internal async Task<bool> PrepareForUpdateAsync()
    {
        if (_connectionPage?.Busy == true || _restoring) return false;
        await DeactivateAsync(); return !_active && !_offering && !_lease.Active && !HasRecovery();
    }
    private async Task CheckUpdatesAsync()
    {
        if (_updates is not null) await _updates.CheckAsync(false, _updatesLifetime.Token);
    }
    private async Task InstallUpdateAsync()
    {
        if (_updates is null) return;
        if (await _updates.InstallAsync(_updatesLifetime.Token))
        {
            _closing = true; _updateWindow?.Close(); Close();
        }
    }
    private void ShowUpdates()
    {
        if (_updates is null) return;
        if (_updateWindow is { IsDisposed: false }) { _updateWindow.Activate(); return; }
        _updateWindow = new UpdateWindow(_updates, () => _updateTask = CheckUpdatesAsync(), () => _updateTask = InstallUpdateAsync());
        _updateWindow.Show(this);
        if (!_updates.Busy && _updates.Release is null) _updateTask = CheckUpdatesAsync();
    }
    private void UpdateUi()
    {
        if (_updates is null || IsDisposed || _shell is null) return;
        var show = _updates.State is UpdateState.Available or UpdateState.Downloading or UpdateState.Preparing or UpdateState.Restarting;
        _updateBanner.Visible = show; _shell.RowStyles[1].Height = show ? 69 * DeviceDpi / 96f : 0;
        _updateCaption.Text = _updates.Release is { } release ? "RotaGame " + release.Version + " disponível" : "Atualizações";
        _updateHint.Text = _updates.Busy ? _updates.Message : "Atualizar reinicia o app e encerra a conexão atual.";
        if (_updateNow is not null) _updateNow.Enabled = !_updates.Busy;
        if (_updateLater is not null) _updateLater.Enabled = !_updates.Busy;
        _updateWindow?.RefreshState(); ArrangeDashboard(); Invalidate(true);
    }
}

internal sealed class UpdateWindow : Form
{
    private readonly UpdateWorkflow _flow;
    private readonly Label _status, _version;
    private readonly DashboardButton _check, _now, _later;
    internal UpdateWindow(UpdateWorkflow flow, Action check, Action now)
    {
        _flow = flow; Text = "Atualizações · RotaGame"; Icon = BrandAssets.LoadIcon(); BackColor = DashboardTheme.Canvas; ForeColor = DashboardTheme.Ink;
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(620, 355); MinimumSize = Size; MaximumSize = Size; MaximizeBox = false; MinimizeBox = false; Font = FontAssets.Ui(11); Padding = new Padding(24);
        FormBorderStyle = FormBorderStyle.None;
        var layout = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); foreach (var h in new[] { 48, 40, 0, 42, 52 }) layout.RowStyles.Add(h == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, h)); Controls.Add(layout);
        var title = new Label { Text = "Sempre na melhor versão", Dock = DockStyle.Fill, Font = FontAssets.Title(26), ForeColor = DashboardTheme.Ink }; layout.Controls.Add(title, 0, 0);
        _version = new Label { Dock = DockStyle.Fill, ForeColor = DashboardTheme.Green, Font = FontAssets.Title(19) }; layout.Controls.Add(_version, 0, 1);
        _status = new Label { Dock = DockStyle.Fill, ForeColor = DashboardTheme.Muted, Font = FontAssets.Ui(11) }; layout.Controls.Add(_status, 0, 2);
        layout.Controls.Add(new Label { Text = "Atualizar reinicia o RotaGame e encerra a conexão atual.", Dock = DockStyle.Fill, ForeColor = DashboardTheme.Muted, Font = FontAssets.Ui(10) }, 0, 3);
        var actions = new DashboardFlowLayout { Dock = DockStyle.Fill, WrapContents = false };
        _now = Button("Atualizar agora", true, 184); _check = Button("Verificar agora", false, 180); _later = Button("Depois", false, 128);
        _now.Click += (_, _) => now(); _check.Click += (_, _) => check(); _later.Click += (_, _) => { _flow.Defer(); Close(); };
        actions.Controls.AddRange([_now, _check, _later]); layout.Controls.Add(actions, 0, 4); RefreshState();
        var close = new DashboardWindowButton(DashboardWindowAction.Close) { Bounds = new Rectangle(575, 10, 32, 32), AccessibleName = "Fechar atualizações" };
        close.Click += (_, _) => Close(); Controls.Add(close); close.BringToFront();
    }
    internal void RefreshState()
    {
        if (IsDisposed) return;
        _version.Text = _flow.Release is { } release ? "Nova versão " + release.Version : "Versão atual " + Application.ProductVersion.Split('+')[0];
        _status.Text = _flow.Message; _now.Enabled = _flow.Release is not null && !_flow.Busy; _check.Enabled = _later.Enabled = !_flow.Busy;
    }
    private static DashboardButton Button(string text, bool primary, int width) => new() { Text = text, Width = width, Height = 43, Font = FontAssets.Ui(10), ForeColor = DashboardTheme.Ink, BackColor = primary ? DashboardTheme.Accent : DashboardTheme.Raised, Margin = new Padding(0, 0, 8, 0) };
}
