namespace RotaGame.App;

internal sealed partial class OptimizerForm
{
    private readonly CancellationTokenSource _updateLifetime = new();
    private readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 15 * 60 * 1000 };
    private readonly DashboardPanel _updatePanel = new() { Dock = DockStyle.Top, Height = 0, Visible = false, Padding = new Padding(22, 5, 22, 5) };
    private readonly Label _updateText = Caption("Atualizações", 11);
    private readonly DashboardButton _updateNow = Button("Atualizar agora", true, 170), _updateLater = Button("Depois", false, 105);
    private UpdateWorkflow? _updates;
    private UpdateWindow? _updateWindow;
    private Task? _updateTask;
    private void BuildUpdatePanel()
    {
        var layout = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        _updateNow.Dock = _updateLater.Dock = DockStyle.Fill; layout.Controls.Add(_updateText, 0, 0); layout.Controls.Add(_updateNow, 1, 0); layout.Controls.Add(_updateLater, 2, 0); _updatePanel.Controls.Add(layout);
        _updateNow.Click += (_, _) => _updateTask = _updates?.Release is null ? CheckUpdatesAsync() : InstallUpdateAsync(); _updateLater.Click += (_, _) => _updates?.Defer();
    }
    internal void ConfigureUpdates(IAppUpdates source, UpdateWorkflow? startup = null)
    {
        // Preserve startup errors and pending releases instead of hiding them
        // in a discarded workflow until the next 15-minute check.
        _updates = startup ?? new(source, PrepareForUpdateAsync); _updates.Changed += (_, _) => UpdateUi();
        _updateTimer.Tick += (_, _) => { if (_updates?.Busy != true) _updateTask = CheckUpdatesAsync(); }; _updateTimer.Start(); UpdateUi();
    }
    private Task CheckUpdatesAsync() => _updates?.CheckAsync(false, _updateLifetime.Token) ?? Task.CompletedTask;
    private async Task InstallUpdateAsync()
    {
        if (_updates is not null && await _updates.InstallAsync(_updateLifetime.Token)) { _closing = true; _updateWindow?.Close(); Close(); }
    }
    private void ShowUpdates()
    {
        if (_updates is null) return;
        if (_updateWindow is { IsDisposed: false }) { _updateWindow.Activate(); return; }
        _updateWindow = new(_updates, () => _updateTask = CheckUpdatesAsync(), () => _updateTask = InstallUpdateAsync()); _updateWindow.Show(this);
        if (!_updates.Busy && _updates.Release is null) _updateTask = CheckUpdatesAsync();
    }
    private void UpdateUi()
    {
        if (_updates is null || IsDisposed) return;
        var visible = _updates.State is UpdateState.Available or UpdateState.Downloading or UpdateState.Preparing or UpdateState.Restarting or UpdateState.Error or UpdateState.Unsupported;
        _updatePanel.Height = visible ? (int)(66 * DeviceDpi / 96f) : 0; _updatePanel.Visible = visible;
        _updateText.Text = _updates.Message; _updateNow.Text = _updates.Release is null ? "Verificar agora" : "Atualizar agora";
        _updateNow.Enabled = _updateLater.Enabled = !_updates.Busy; _updateLater.Visible = _updates.Release is not null; _updateWindow?.RefreshState(); Arrange();
    }
    internal async Task<bool> PrepareForUpdateAsync()
    {
        if (_settingUp || _stopping) return false;
        await StopAsync(); if (PendingRecovery()) return false;
        if (!_controlled) try { using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(25)); await RotaGame.Windows.LegacyRecovery.RestoreAsync(_dataDirectory, ct.Token); }
            catch (Exception ex) { _hint.Text = ex.Message; return false; }
        return true;
    }
}
