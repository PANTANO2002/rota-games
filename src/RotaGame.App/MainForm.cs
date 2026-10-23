using System.Diagnostics;
using System.Globalization;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(12, 19, 30);
    private static readonly Color PanelColor = Color.FromArgb(22, 31, 44);
    private static readonly Color Ink = Color.FromArgb(235, 243, 251);
    private static readonly Color Muted = Color.FromArgb(154, 174, 195);
    private static readonly Color Mint = Color.FromArgb(65, 221, 179);
    private readonly ComboBox _game = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _region = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _target = new() { PlaceholderText = "IPv4 ou hostname do destino" };
    private readonly NumericUpDown _count = new() { Minimum = 20, Maximum = 900, Value = 60 };
    private readonly Button _diagnose = MakeButton("Iniciar diagnóstico", true);
    private readonly Button _compare = MakeButton("Comparar A–B–B–A");
    private readonly Button _next = MakeButton("Medir próximo bloco");
    private readonly Button _stop = MakeButton("Parar");
    private readonly Button _export = MakeButton("Exportar");
    private readonly Button _trace = MakeButton("Traçar rota");
    private readonly Label _status = MakeLabel("Pronto para medir. Informe um destino ou use o teste local.", 10, Muted);
    private readonly Label _route = MakeLabel("ROTA  ·  Nenhuma medição iniciada", 10, Muted);
    private readonly Label _instruction = MakeLabel("Sem VPS: comece pelo diagnóstico. A comparação exige um relay WireGuard configurado.", 10, Mint);
    private readonly Label _verdict = MakeLabel("O caminho mais rápido depende da operadora, do destino e do horário.", 10, Ink);
    private readonly Label _settingsNote = MakeLabel("1 s entre envios · timeout 1 s · 32 bytes · 3 aquecimentos", 8, Muted);
    private readonly Label[] _values = new Label[4];
    private readonly LatencyChart _chart = new() { Dock = DockStyle.Fill };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Maximum = 100, Height = 5 };
    private readonly DataGridView _grid = new();
    private readonly ToolTip _tips = new();
    private readonly WindowsRouteObserver _routes = new();
    private MeasurementSession? _session;
    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _comparisonActive;
    private int _nextBlock;

    public MainForm()
    {
        Icon = BrandAssets.LoadIcon();
        Text = "RotaGame · Laboratório de conexão";
        BackColor = Canvas; ForeColor = Ink; Font = FontAssets.Ui(10);
        ClientSize = new Size(1180, 910); MinimumSize = new Size(1020, 760);
        StartPosition = FormStartPosition.CenterScreen;
        var scroll = new DashboardScrollHost(false) { Dock = DockStyle.Fill };
        var body = new TableLayoutPanel { AutoSize = false, Height = 870, ColumnCount = 1, RowCount = 12, Padding = new Padding(24, 16, 24, 16) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new float[] { 61, 34, 99, 39, 33, 33, 88, 220, 30, 139, 38, 24 })
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        scroll.SetContent(body); scroll.SetExtent(870); Controls.Add(scroll);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        var brand = MakeLabel("RotaGame", 27, Ink); brand.Font = FontAssets.Title(27); heading.Controls.Add(brand, 0, 0);
        var badge = MakeLabel("LAB 0.1   /   WINDOWS   /   IPv4", 10, Mint); badge.TextAlign = ContentAlignment.MiddleRight; heading.Controls.Add(badge, 1, 0);
        body.Controls.Add(heading, 0, 0);
        body.Controls.Add(MakeLabel("Meça primeiro. Compare caminhos. Escolha com evidência.", 11, Muted), 0, 1);

        var config = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, BackColor = PanelColor, Padding = new Padding(12, 6, 12, 6) };
        foreach (var width in new float[] { 22, 24, 39, 15 }) config.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        config.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); config.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); config.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        string[] names = ["JOGO", "REGIÃO INFORMADA", "DESTINO DA MEDIÇÃO", "SONDAS / BLOCO"];
        Control[] editors = [_game, _region, _target, _count];
        _game.Items.AddRange(["The Finals", "Rocket League", "GTA Online", "Outro / diagnóstico"]); _game.SelectedIndex = 0;
        _region.Items.AddRange(["A confirmar no jogo", "Salvador (a confirmar)", "São Paulo (a confirmar)", "Porto Alegre (a confirmar)"]); _region.SelectedIndex = 0;
        for (var i = 0; i < 4; i++)
        {
            config.Controls.Add(MakeLabel(names[i], 8, Muted), i, 0);
            editors[i].Dock = DockStyle.Fill; editors[i].Margin = new Padding(3, 0, 9, 0);
            editors[i].BackColor = Color.FromArgb(32, 44, 61); editors[i].ForeColor = Ink;
            config.Controls.Add(editors[i], i, 1);
        }
        var local = new LinkLabel { Text = "Usar teste local (127.0.0.1)", Dock = DockStyle.Fill, LinkColor = Mint, ActiveLinkColor = Ink, Font = FontAssets.Ui(8), Margin = new Padding(3, 3, 0, 0) };
        local.LinkClicked += (_, _) => { if (!_busy && !_comparisonActive) { _target.Text = "127.0.0.1"; _game.SelectedIndex = 3; _region.Text = "Teste deste computador"; } };
        config.Controls.Add(local, 0, 2); config.SetColumnSpan(local, 2);
        config.Controls.Add(_settingsNote, 2, 2); config.SetColumnSpan(_settingsNote, 2);
        body.Controls.Add(config, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        actions.Controls.AddRange([_diagnose, _compare, _next, _stop, _export, _trace]);
        body.Controls.Add(actions, 0, 3);
        body.Controls.Add(_instruction, 0, 4); body.Controls.Add(_route, 0, 5);
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        for (var i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        string[] labels = ["PING MÉDIO", "P95 DE LATÊNCIA", "JITTER DE RTT", "FALHAS ICMP / PERDA APARENTE"];
        for (var i = 0; i < 4; i++)
        {
            var card = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = PanelColor, Padding = new Padding(14, 7, 8, 6), Margin = new Padding(0, 4, i == 3 ? 0 : 10, 6) };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(MakeLabel(labels[i], 8, Muted), 0, 0);
            _values[i] = MakeLabel("—", 23, i == 3 ? Color.FromArgb(255, 180, 140) : Mint);
            card.Controls.Add(_values[i], 0, 1); cards.Controls.Add(card, i, 0);
        }
        body.Controls.Add(cards, 0, 6); body.Controls.Add(_chart, 0, 7);
        body.Controls.Add(MakeLabel("BLOCOS MEDIDOS   /   A = direta   ·   B = relay WireGuard", 9, Muted), 0, 8);
        SetupGrid(); body.Controls.Add(_grid, 0, 9); body.Controls.Add(_verdict, 0, 10);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 86)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        bottom.Controls.Add(_status, 0, 0); bottom.Controls.Add(_progress, 1, 0); body.Controls.Add(bottom, 0, 11);
        _tips.SetToolTip(_target, "Use o IPv4 observado na sessão do jogo. Hostnames são resolvidos uma única vez e o mesmo IPv4 é mantido nos blocos.");
        _tips.SetToolTip(_values[2], "Média das diferenças absolutas entre RTTs consecutivos respondidos. Lacunas e fronteiras de blocos não são unidas.");
        _tips.SetToolTip(_values[3], "Timeouts e ICMP inalcançável / envios válidos. ICMP pode ser bloqueado ou limitado: este número não prova perda dos pacotes do jogo.");
        _diagnose.Click += async (_, _) => await StartDiagnosticAsync();
        _compare.Click += async (_, _) => await StartComparisonAsync();
        _next.Click += async (_, _) => await MeasureNextAsync();
        _stop.Click += (_, _) => Stop();
        _export.Click += (_, _) => ExportSession();
        _trace.Click += async (_, _) => await TraceAsync();
        FormClosing += (_, e) => { if (_busy) { _cts?.Cancel(); e.Cancel = true; _status.Text = "Interrompendo; feche novamente após a parada."; } };
        SetBusy(false);
    }

    private void SetupGrid()
    {
        _grid.Dock = DockStyle.Fill; _grid.BackgroundColor = PanelColor; _grid.BorderStyle = BorderStyle.None;
        _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.EnableHeadersVisualStyles = false; _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(30, 43, 60), ForeColor = Muted, Font = FontAssets.Ui(9) };
        _grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = PanelColor, ForeColor = Ink, SelectionBackColor = Color.FromArgb(40, 65, 80), SelectionForeColor = Ink, Font = FontAssets.Ui(9) };
        _grid.GridColor = Color.FromArgb(43, 55, 72); _grid.RowTemplate.Height = 26; _grid.ColumnHeadersHeight = 28;
        foreach (var name in new[] { "Bloco", "Caminho", "Sondas", "Média", "Mediana", "p95", "Jitter", "Falhas ICMP", "Estado" })
            _grid.Columns.Add(name, name);
        _grid.Columns[1].FillWeight = 115; _grid.Columns[8].FillWeight = 140;
    }

    private static Label MakeLabel(string text, float size, Color color) => new()
    { Text = text, Font = FontAssets.Ui(size), ForeColor = color, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = new Padding(0) };

    private static Button MakeButton(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 31, FlatStyle = FlatStyle.Flat, Padding = new Padding(8, 0, 8, 0),
            BackColor = primary ? Mint : PanelColor, ForeColor = primary ? Canvas : Ink, Font = FontAssets.Ui(9), Margin = new Padding(0, 0, 8, 0) };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _diagnose.Enabled = _compare.Enabled = !busy && !_comparisonActive;
        _next.Enabled = !busy && _comparisonActive && _nextBlock < 4;
        _stop.Enabled = busy || _comparisonActive;
        _stop.Text = busy ? "Parar" : "Encerrar";
        _export.Enabled = !busy && _session is not null;
        _trace.Enabled = !busy && !_comparisonActive;
        _game.Enabled = _region.Enabled = _target.Enabled = _count.Enabled = !busy && !_comparisonActive;
    }

    private async Task<MeasurementSession> NewSessionAsync(bool comparison, ProbeSettings? settings = null)
    {
        var targetText = _target.Text.Trim();
        var ip = await TargetResolver.ResolveAsync(targetText, _cts?.Token ?? default);
        var result = new MeasurementSession { Game = _game.Text, RegionNote = _region.Text, OriginalTarget = targetText, TargetIp = ip.ToString(),
            Settings = settings ?? new((int)_count.Value), IsComparison = comparison };
        return result;
    }

    private void ResetSessionDisplay()
    {
        _chart.Samples.Clear(); _chart.Invalidate(); _grid.Rows.Clear(); ShowStats(Statistics.From([])); _progress.Value = 0;
        _route.Text = "ROTA  ·  Aguardando a primeira sonda desta sessão";
        _verdict.Text = "ICMP pode ser limitado. Confirme qualquer resultado com a telemetria do próprio jogo.";
        var s = _session!.Settings;
        _settingsNote.Text = $"Intervalo {s.IntervalMs} ms · timeout {s.TimeoutMs} ms · {s.PayloadBytes} bytes · {s.WarmupCount} aquecimentos";
    }

    private async Task StartDiagnosticAsync(ProbeSettings? settings = null)
    {
        if (_busy) return;
        _cts?.Dispose(); _cts = new(); SetBusy(true);
        try
        {
            _session = await NewSessionAsync(false, settings);
            ResetSessionDisplay();
            _instruction.Text = $"Diagnóstico · IPv4 fixado em {_session.TargetIp}. Mede o caminho atualmente ativo.";
            var block = NewBlock(PathKind.Diagnostic); _session.Blocks.Add(block);
            await RunBlockAsync(block);
            _status.Text = $"Concluído · {block.Stats.Received}/{block.Stats.Sent} respostas · resultado deste computador.";
        }
        catch (OperationCanceledException) { _status.Text = "Diagnóstico interrompido. Dados parciais disponíveis para exportação."; }
        catch (Exception ex) { ReportError(ex); }
        finally { SetBusy(false); RefreshGrid(); }
    }

    private async Task StartComparisonAsync()
    {
        if (_busy) return;
        _cts?.Dispose(); _cts = new(); SetBusy(true);
        try
        {
            var session = await NewSessionAsync(true);
            var ip = System.Net.IPAddress.Parse(session.TargetIp);
            var bytes = ip.GetAddressBytes();
            if (System.Net.IPAddress.IsLoopback(ip) || bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 169 && bytes[1] == 254))
                throw new ArgumentException("Para comparar relay, use o IPv4 público do destino. Endereços locais servem somente ao diagnóstico.");
            _session = session; _nextBlock = 0; _comparisonActive = true; ResetSessionDisplay();
            PromptNext();
        }
        catch (OperationCanceledException) { _status.Text = "Resolução cancelada."; }
        catch (Exception ex) { ReportError(ex); }
        finally { SetBusy(false); }
    }

    private MeasurementBlock NewBlock(PathKind path) => new() { Path = path, TargetIp = _session!.TargetIp, Settings = _session.Settings };

    private async Task MeasureNextAsync()
    {
        if (_busy || !_comparisonActive || _session is null) return;
        _cts?.Dispose(); _cts = new(); SetBusy(true);
        var block = NewBlock(MeasurementRunner.AbbaPlan[_nextBlock]);
        if (_session.Blocks.Count > _nextBlock) _session.Blocks[_nextBlock] = block;
        else _session.Blocks.Add(block);
        try
        {
            await RunBlockAsync(block); _nextBlock++;
            if (_nextBlock == 4)
            {
                _comparisonActive = false;
                var result = Comparison.Evaluate(_session);
                _instruction.Text = "Quatro blocos concluídos. Exporte os dados e repita em outros horários.";
                _verdict.Text = result.Verdict;
                _tips.SetToolTip(_verdict, result.Verdict);
                _status.Text = result.IsValid
                    ? $"Relay − direta: mediana {Signed(result.MedianDeltaMs)} ms · p95 {Signed(result.P95DeltaMs)} ms · falhas {Signed(result.FailureDeltaPoints)} p.p."
                    : "Sem conclusão válida. Consulte os blocos e o relatório JSON.";
                _next.Text = "Comparação concluída";
            }
            else PromptNext();
        }
        catch (OperationCanceledException) { _status.Text = "Bloco interrompido. Repita-o ou encerre a comparação."; }
        catch (Exception ex) { ReportError(ex); }
        finally { SetBusy(false); RefreshGrid(); }
    }

    private void PromptNext()
    {
        var direct = MeasurementRunner.AbbaPlan[_nextBlock] == PathKind.Direct;
        _instruction.Text = $"Etapa {_nextBlock + 1}/4 · {(direct ? "Desative WireGuard e outras VPNs" : "Ative o mesmo relay no cliente WireGuard")}; depois clique em Medir. Destino: {_session!.TargetIp}";
        _next.Text = $"Medir {_nextBlock + 1}/4 · {(direct ? "direta" : "relay")}";
        _status.Text = "Aguardando a troca manual de caminho. A interface será verificada antes de cada sonda.";
    }

    private async Task RunBlockAsync(MeasurementBlock block)
    {
        _chart.Samples.Clear(); _chart.Invalidate(); _progress.Value = 0;
        var progress = new UiProgress(p =>
        {
            if (IsDisposed) return;
            _status.Text = p.Message;
            if (p.Sample is not null)
            {
                _chart.Samples.Add(p.Sample); _chart.Invalidate();
                _route.Text = $"ROTA  ·  {block.TargetIp}  →  {p.Sample.RouteAfter.Display}";
                _tips.SetToolTip(_route, p.Sample.RouteAfter.Description + " | " + p.Sample.RouteAfter.Prefix);
                ShowStats(block.Stats); _progress.Value = Math.Min(100, block.Samples.Count * 100 / block.Settings.Count);
            }
        });
        await new MeasurementRunner(new IcmpProbe(), _routes).RunAsync(block, progress, _cts!.Token);
        RefreshGrid();
    }

    // The runner is awaited on the WinForms synchronization context. Synchronous
    // reporting keeps the final progress event from overwriting the completion text.
    private sealed class UiProgress(Action<MeasurementProgress> report) : IProgress<MeasurementProgress>
    {
        public void Report(MeasurementProgress value) => report(value);
    }

    private static string Number(double? value) => value?.ToString("0.0", CultureInfo.CurrentCulture) ?? "—";
    private static string Signed(double? value) => value?.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture) ?? "—";
    private void ShowStats(Statistics s)
    {
        _values[0].Text = Number(s.AverageMs) + " ms"; _values[1].Text = Number(s.P95Ms) + " ms";
        _values[2].Text = Number(s.JitterMs) + " ms"; _values[3].Text = Number(s.FailurePercent) + " %";
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear(); if (_session is null) return;
        foreach (var (b, index) in _session.Blocks.Select((b, index) => (b, index)))
        {
            var s = b.Stats;
            var state = b.Status switch { "complete" => "Concluído", "invalid" => "Inválido", "cancelled" => "Parcial", _ => "Medindo" };
            _grid.Rows.Add(index + 1, b.Path switch { PathKind.Direct => "A · Direta", PathKind.Relay => "B · Relay", _ => "Diagnóstico" },
                $"{s.Received}/{s.Sent}", Number(s.AverageMs), Number(s.MedianMs), Number(s.P95Ms), Number(s.JitterMs), Number(s.FailurePercent) + "%", state);
            _grid.Rows[^1].Cells[8].ToolTipText = b.Error ?? state;
        }
    }

    private void Stop()
    {
        if (_busy) _cts?.Cancel();
        else { _comparisonActive = false; _instruction.Text = "Comparação encerrada. Os blocos já medidos podem ser exportados."; SetBusy(false); }
    }

    private void ReportError(Exception ex)
    {
        _status.Text = ex.Message; _tips.SetToolTip(_status, ex.Message);
        _verdict.Text = ex.Message; _tips.SetToolTip(_verdict, ex.Message);
    }

    private void ExportSession()
    {
        if (_session is null) return;
        using var dialog = new SaveFileDialog { Filter = "Relatório JSON (*.json)|*.json|Sondas CSV (*.csv)|*.csv", FileName = $"rotagame-{DateTime.Now:yyyyMMdd-HHmmss}.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (dialog.FilterIndex == 2) Export.WriteCsv(_session, dialog.FileName); else Export.WriteJson(_session, dialog.FileName);
            _status.Text = "Medições exportadas: " + Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private async Task TraceAsync()
    {
        if (_busy) return;
        _cts?.Dispose(); _cts = new(); SetBusy(true);
        try
        {
            var ip = await TargetResolver.ResolveAsync(_target.Text, _cts.Token);
            _status.Text = "Traçando até 20 saltos; pode levar alguns segundos. * não prova perda do jogo.";
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "tracert.exe")) { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, UseShellExecute = false };
            foreach (var arg in new[] { "-d", "-h", "20", "-w", "300", ip.ToString() }) psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível executar tracert.");
            using var cancel = _cts.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
            var output = process.StandardOutput.ReadToEndAsync(_cts.Token);
            var error = process.StandardError.ReadToEndAsync(_cts.Token);
            await process.WaitForExitAsync(_cts.Token);
            var text = await output + "\r\n" + await error;
            _cts.Token.ThrowIfCancellationRequested();
            using var viewer = new Form { Text = "Rota observada · " + ip, Size = new Size(860, 650), StartPosition = FormStartPosition.CenterParent, BackColor = Canvas };
            viewer.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, BackColor = PanelColor, ForeColor = Ink,
                Font = new Font("Consolas", 10), Text = "Asteriscos podem indicar filtragem de ICMP, sem provar perda no destino.\r\nO túnel oculta saltos internos.\r\n\r\n" + text });
            viewer.ShowDialog(this); _status.Text = "Traçado concluído; use o destino final para avaliar a conexão.";
        }
        catch (OperationCanceledException) { _status.Text = "Traçado interrompido."; }
        catch (Exception ex) { ReportError(ex); }
        finally { SetBusy(false); }
    }

    internal async Task RunSmokeAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        Text += " · validação local";
        _count.Minimum = 1; _count.Value = 8;
        _target.Text = "127.0.0.1"; _game.SelectedIndex = 3; _region.Text = "Validação local · não mede o Acre";
        await StartDiagnosticAsync(new(8, 100, 300, 32, 1));
        await Task.Delay(100);
        if (_session?.Blocks.SingleOrDefault()?.Status != "complete" || _session.Blocks[0].Stats.Received != 8)
            throw new InvalidOperationException("O fluxo da interface não concluiu o diagnóstico local.");
        Export.WriteJson(_session, Path.Combine(directory, "ui-loopback.json")); Export.WriteCsv(_session, Path.Combine(directory, "ui-loopback.csv"));
        using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(directory, "interface.png"));
        File.WriteAllText(Path.Combine(directory, "ui-smoke.txt"), "PASS · interface aberta, diagnóstico ICMP local 8/8, tabela e exportações geradas.\n");
    }
}
