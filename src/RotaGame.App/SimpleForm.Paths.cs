using RotaGame.Core;

namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    private RouteOption? _manualRoute;
    private CancellationTokenSource? _pathCts;
    private int _pathRevision;
    private bool _comparingPaths, _switchingPath;

    private void ShowPathPicker()
    {
        if (_restoring) return;
        if (_device.Role == DeviceRole.Bridge) { _message.Text = "Escolha o caminho no RotaGame do Acre. Este painel acompanha a conexão dele."; return; }
        if (_device.Role != DeviceRole.Player) { ShowConnectionPage(); return; }
        var options = new[] { RouteOption.Direct }.Concat(_tailStatus?.Peers.Where(p => p.Online && p.Offered).Select(p => new RouteOption(p.Ip, p.Name, p.Link)) ?? [])
            .Concat(_routeMap.Snapshot(DateTimeOffset.UtcNow).Select(v => v.Route)).DistinctBy(r => r.Id).Take(9).ToArray();
        using var picker = new PathChoiceWindow(options, _manualRoute, _active ? _selected : null, RequestPath);
        picker.ShowDialog(this);
    }
    private void RequestPath(RouteOption? manual)
    {
        if (_device.Role != DeviceRole.Player || _restoring) return;
        _manualRoute = manual; _pathRevision++;
        _switchingPath = _active && _routeMap.Destination is not null;
        _pathCts?.Cancel();
        _state.Text = _switchingPath ? "Trocando caminho…" : manual is null ? "Escolha automática" : "Escolha manual: " + manual.Name;
        _message.Text = _active ? manual is null ? "Voltando à comparação automática dos caminhos." : "Aplicando sua escolha manual. Trocar o caminho pode reconectar a partida."
            : "Escolha salva para esta sessão. Clique em Ativar conexão para aplicar quando o destino do jogo for reconhecido.";
        SetControls();
    }
    private async Task RestorePathAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await _lease.RestoreAsync(cleanup.Token);
    }
    private async Task ApplyManualPathAsync(ObservedDestination destination, RouteOption requested, CancellationToken ct)
    {
        _switchingPath = true; _comparingPaths = false; ClearLive(); SyncDashboard();
        var route = _controlled ? requested : await _client.ResolveManualAsync(requested.Id, ct);
        await _lease.BeginAsync(ct); await _lease.ApplyAsync(route, ct);
        _selected = route;
        if (_report is null)
        {
            _routeMap.Destination = null; _routeMap.Destination = destination;
            _report = new AutomaticReport { Game = destination.ProcessName, Destination = destination };
        }
        _report.ManualSelection = true; _report.RequestedRoute = route; _report.CurrentRoute = route;
        _report.Notes.Add("Escolha manual do usuário: " + route.Name + ". Não representa vantagem comprovada sobre a direta.");
        _switchingPath = false; RefreshResult(); SetControls();
        _state.Text = "Manual · " + route.Name; _path.Text = "Caminho manual: " + route.Name;
        _message.Text = "Sua escolha manual está aplicada. O ping continua sendo medido; a escolha não garante melhora.";
    }
}

internal sealed class PathChoiceWindow : Form
{
    internal PathChoiceWindow(IReadOnlyList<RouteOption> options, RouteOption? manual, RouteOption? current, Action<RouteOption?> choose)
    {
        Text = "Escolher caminho · RotaGame"; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(490, Math.Min(178 + (options.Count + 1) * 53, (Screen.PrimaryScreen?.WorkingArea.Height ?? 800) - 40)); BackColor = DashboardTheme.Canvas; ForeColor = DashboardTheme.Ink; Icon = BrandAssets.LoadIcon(); Padding = new Padding(22);
        var body = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var h in new[] { 44, 30, (options.Count + 1) * 53, 56 }) body.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        body.RowStyles[2].SizeType = SizeType.Percent; body.RowStyles[2].Height = 100;
        Controls.Add(body);
        body.Controls.Add(new Label { Text = "Por onde quer conectar?", Dock = DockStyle.Fill, Font = FontAssets.Title(23), ForeColor = DashboardTheme.Ink }, 0, 0);
        body.Controls.Add(new Label { Text = "A escolha será aplicada no PC que vai jogar.", Dock = DockStyle.Fill, Font = FontAssets.Ui(10), ForeColor = DashboardTheme.Muted }, 0, 1);
        var rows = new DashboardTableLayout { ColumnCount = 1, RowCount = options.Count + 1 };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); var scroll = new DashboardScrollHost(false) { Dock = DockStyle.Fill }; scroll.SetContent(rows); scroll.SetExtent((options.Count + 1) * 53); body.Controls.Add(scroll, 0, 2);
        Add("Automático · escolher pelo teste", null, manual is null);
        foreach (var option in options) Add(option.Name + (current?.Id == option.Id ? "  ·  caminho atual" : ""), option, manual?.Id == option.Id);
        body.Controls.Add(new Label { Text = "Trocar o caminho pode reconectar a partida.\nVocê pode voltar ao automático a qualquer momento.", Dock = DockStyle.Fill, Font = FontAssets.Ui(10), ForeColor = DashboardTheme.Muted, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
        var close = new DashboardWindowButton(DashboardWindowAction.Close) { Size = new Size(32, 32), Location = new Point(445, 8), AccessibleName = "Fechar escolha de caminho" }; close.Click += (_, _) => Close(); Controls.Add(close); close.BringToFront();
        void Add(string text, RouteOption? option, bool selected)
        {
            var button = new DashboardButton { Text = text, Dock = DockStyle.Fill, BackColor = selected ? DashboardTheme.Accent : DashboardTheme.Raised, ForeColor = DashboardTheme.Ink, Font = FontAssets.Ui(11), Margin = new Padding(0, 2, 0, 3), AccessibleName = option?.Name ?? "Escolha automática" };
            rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 53)); button.Click += (_, _) => { choose(option); Close(); }; rows.Controls.Add(button, 0, rows.Controls.Count);
        }
    }
}
