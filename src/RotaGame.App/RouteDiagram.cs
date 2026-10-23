using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using RotaGame.Core;

namespace RotaGame.App;

internal sealed record DiagramRouteView(RouteOption Route, string Ping, string Detail, string Badge, bool Selected, bool Moving);

internal sealed class RouteDiagram : Control
{
    private static readonly Color Cyan = Color.FromArgb(83, 192, 243);
    private readonly Dictionary<string, RouteOption> _routes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<MeasurementBlock>> _blocks = new(StringComparer.Ordinal);
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 33 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private AutomaticReport? _report;
    private ObservedDestination? _destination;
    private RouteOption? _current, _selected;
    private Statistics? _live;
    private Sample? _sample;
    private bool _active, _verified, _bridgeMode, _freeze;
    private int _rowCount = 1;
    private double _frozenPhase;
    private Form? _owner;
    private ReceivedDevice? _remote;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal ReceivedDevice? Remote { get => _remote; set { _remote = value; Changed(); } }
    internal event EventHandler? LayoutNeedsChanged;
    internal int PreferredContentHeight => _rowCount switch { 1 => 128, 2 => 196, _ => 264 };
    internal bool AnimationRunning => _animation.Enabled;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal string? PreviewLabel { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal bool FreezeAnimation { get => _freeze; set { if (value && !_freeze) _frozenPhase = _clock.Elapsed.TotalSeconds / 2.4; _freeze = value; UpdateAnimation(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AutomaticReport? Report
    {
        get => _report;
        set
        {
            if (ReferenceEquals(_report, value)) return; _report = value;
            if (value is not null)
                foreach (var group in value.Trials.GroupBy(t => t.Route.Id))
                {
                    _routes[group.Key] = group.First().Route;
                    _blocks[group.Key] = group.Select(t => t.Block).Where(b => b.Status == "complete").DistinctBy(b => b.Id).ToList();
                }
            Changed();
        }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservedDestination? Destination
    {
        get => _destination;
        set
        {
            if (_destination == value) return;
            _destination = value; _report = null; _current = _selected = null; _routes.Clear(); _blocks.Clear(); ResetLive(); Changed();
        }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool BridgeMode { get => _bridgeMode; set { if (_bridgeMode == value) return; _bridgeMode = value; Changed(); } }

    public RouteDiagram()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = DashboardTheme.Surface; AccessibleRole = AccessibleRole.Graphic; AccessibleName = "Caminhos da conexão";
        _animation.Tick += (_, _) => { UpdateAnimation(); Invalidate(); };
    }

    internal void SetActivity(bool active, RouteOption? selected)
    {
        if (_active == active && _selected == selected) return;
        if (selected is not null && _selected?.Id != selected.Id) { _current = selected; ResetLive(); }
        _active = active; _selected = selected; if (selected is not null) _routes[selected.Id] = selected; Changed();
    }
    internal void BeginMeasurement(RouteOption route)
    {
        if (_current?.Id != route.Id) ResetLive();
        _current = route; _routes[route.Id] = route; Changed();
    }
    internal void Observe(RouteOption route, MeasurementProgress progress, Statistics rolling)
    {
        if (progress.Sample is not { } sample || progress.Block.TargetIp != _destination?.Ip) return;
        _current = route; _routes[route.Id] = route; _sample = sample; _live = rolling;
        var tunnel = sample.RouteBefore.IsWireGuard || sample.RouteBefore.Name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)
            || sample.RouteBefore.Description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase);
        _verified = sample.RouteBefore.Fingerprint == sample.RouteAfter.Fingerprint && sample.Outcome != ProbeOutcome.LocalError
            && (route.IsDirect ? !tunnel : tunnel);
        Changed();
    }
    internal void CompleteMeasurement(RouteOption route, MeasurementBlock block)
    {
        // Keep comparison blocks only. The rolling monitor can run for hours.
        if (_report?.Choice is null && block.Status == "complete")
        {
            if (!_blocks.TryGetValue(route.Id, out var list)) _blocks[route.Id] = list = [];
            if (list.All(b => b.Id != block.Id)) list.Add(block);
        }
        Changed();
    }
    internal void ResetLive() { _sample = null; _live = null; _verified = false; UpdateAnimation(); Invalidate(); }

    internal IReadOnlyList<DiagramRouteView> Snapshot(DateTimeOffset now)
    {
        if (_remote is { Frame.Measurements: { } m } remote)
        {
            var fresh = m.SampleAgeMs is >= 0 and < 5000 && now >= remote.ReceivedAt && (now - remote.ReceivedAt).TotalMilliseconds + m.SampleAgeMs < 5000;
            var optionsRemote = m.Routes.ToList();
            if (optionsRemote.All(r => r.Id != m.RouteId)) optionsRemote.Add(new(m.RouteId, m.RouteName, null, null, null, null, false));
            return optionsRemote.OrderBy(r => r.Id.Length == 0 ? 0 : r.Id == m.RouteId ? 1 : 2).Take(3).Select(r =>
            {
                var current = r.Id == m.RouteId; var live = current && m.Active; var moving = live && fresh && m.Confirmed;
                var value = live ? fresh && m.Confirmed ? m.AverageMs : null : r.AverageMs;
                return new DiagramRouteView(new(r.Id, r.Name), value.HasValue ? value.Value.ToString("0.0") + " ms" : "—",
                    live ? moving ? "Ao vivo · PC do amigo" : "Aguardando sonda do amigo" : "Comparado no PC do amigo",
                    current ? m.Active ? "NO AMIGO" : "ÚLTIMA ROTA" : "COMPARADA", current, moving);
            }).ToArray();
        }
        var options = _routes.Values.ToList();
        if (options.All(r => !r.IsDirect)) options.Add(RouteOption.Direct);
        return options.OrderBy(r => r.IsDirect ? 0 : r.Id == (_active ? _current?.Id : _selected?.Id) ? 1 : r.Id == _selected?.Id ? 2 : 3)
            .ThenBy(r => r.Name).Take(3).Select(route =>
            {
                var selected = route.Id == _selected?.Id;
                var live = _active && route.Id == _current?.Id && _sample is not null;
                var fresh = live && now - _sample!.At <= TimeSpan.FromSeconds(5) && now >= _sample.At;
                var moving = fresh && _verified && _sample!.Outcome == ProbeOutcome.Success && (_selected is null || selected);
                var stats = _blocks.TryGetValue(route.Id, out var blocks) ? Statistics.Combine(blocks) : Statistics.From([]);
                var value = live ? fresh && _verified && _sample!.Outcome == ProbeOutcome.Success ? _live?.AverageMs : null : stats.AverageMs;
                var detail = live ? !fresh ? "Aguardando sonda" : !_verified ? "Rota sem confirmação" : _sample!.Outcome != ProbeOutcome.Success
                    ? "Sem resposta" : $"Média ao vivo · {_live?.Attempts ?? 0} sondas" : stats.Attempts > 0 ? $"Comparação · {stats.Received} respostas" : "Aguardando medição";
                var badge = selected ? _active ? "EM USO" : "ÚLTIMA ROTA" : _active && _selected is null && route.Id == _current?.Id ? "EM TESTE" : stats.Attempts > 0 ? "COMPARADA" : "";
                return new DiagramRouteView(route, value.HasValue ? value.Value.ToString("0.0") + " ms" : "—", detail, badge, selected, moving);
            }).ToArray();
    }

    private void Changed()
    {
        var rows = _remote is not null ? Snapshot(DateTimeOffset.UtcNow).Count : Math.Clamp(_routes.Count + (_routes.ContainsKey("") ? 0 : 1), 1, 3);
        if (rows != _rowCount) { _rowCount = rows; LayoutNeedsChanged?.Invoke(this, EventArgs.Empty); }
        UpdateAnimation(); Invalidate();
        AccessibleDescription = string.Join("; ", Snapshot(DateTimeOffset.UtcNow).Select(r => r.Route.Name + ": " + r.Ping + ", " + r.Detail + ", " + r.Badge));
    }
    private void UpdateAnimation()
    {
        if (IsDisposed) return;
        var moving = (_remote?.Frame.Measurements?.Active ?? _active) && (!_bridgeMode || _remote is not null) && !_freeze && Visible && IsHandleCreated && _owner?.WindowState != FormWindowState.Minimized
            && Snapshot(DateTimeOffset.UtcNow).Any(r => r.Moving);
        _animation.Enabled = moving;
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); AttachOwner(); UpdateAnimation(); }
    protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); AttachOwner(); UpdateAnimation(); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateAnimation(); }
    private void AttachOwner()
    {
        var owner = FindForm(); if (ReferenceEquals(owner, _owner)) return;
        if (_owner is not null) _owner.Resize -= OwnerResized;
        _owner = owner; if (_owner is not null) _owner.Resize += OwnerResized;
    }
    private void OwnerResized(object? sender, EventArgs e) => UpdateAnimation();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { if (_owner is not null) _owner.Resize -= OwnerResized; _animation.Stop(); _animation.Dispose(); }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); Draw(e.Graphics, ClientSize, _freeze ? _frozenPhase : _clock.Elapsed.TotalSeconds / 2.4);
    }
    internal void Draw(Graphics g, Size size, double phase, DateTimeOffset? at = null)
    {
        g.Clear(BackColor); if (size.Width < 20 || size.Height < 20) return;
        var state = g.Save(); var scale = DeviceDpi / 96f; g.ScaleTransform(scale, scale);
        var width = size.Width / scale; var height = size.Height / scale; g.SmoothingMode = SmoothingMode.AntiAlias;
        var rows = Snapshot(at ?? DateTimeOffset.UtcNow); var graphTop = 32f; var graphBottom = height - 28;
        var centerY = (graphTop + graphBottom) / 2; var leftWidth = width < 560 ? 88 : 100; var rightWidth = width < 560 ? 108 : 126;
        var left = new RectangleF(7, centerY - 42, leftWidth, 84); var right = new RectangleF(width - 7 - rightWidth, centerY - 42, rightWidth, 84);
        var cardWidth = Math.Clamp(width * .32f, 180, 236); var cardX = (left.Right + right.Left - cardWidth) / 2;
        var rowSpace = (graphBottom - graphTop) / rows.Count; var cardHeight = Math.Min(62, Math.Max(46, rowSpace - 9));
        var anyMoving = rows.Any(r => r.Moving); var tint = _selected is not null ? DashboardTheme.Green : Cyan;
        var label = _remote is not null ? "MEDIDO NO PC DO AMIGO" : _bridgeMode ? "MODO PONTE" : !_active ? _report is not null ? "COMPARAÇÃO ENCERRADA" : "AGUARDANDO CONEXÃO" : _selected is not null ? "CAMINHO EM USO" : _sample is null ? "BUSCANDO CONEXÃO" : "COMPARANDO ROTAS";
        using (var dot = new SolidBrush(_active && anyMoving ? tint : DashboardTheme.Muted)) g.FillEllipse(dot, 7, 11, 6, 6);
        DrawLabel(g, label, new RectangleF(20, 0, Math.Max(1, width - 255), 27), 8.2f, _active ? tint : DashboardTheme.Muted, FontStyle.Bold);
        DrawLabel(g, "PING ICMP · IDA E VOLTA", new RectangleF(Math.Max(210, width - 209), 0, 205, 27), 7.5f, DashboardTheme.Muted, center: true);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; var y = graphTop + (i + .5f) * rowSpace; var card = new RectangleF(cardX, y - cardHeight / 2, cardWidth, cardHeight);
            var highlight = row.Selected || row.Moving; var color = row.Selected ? DashboardTheme.Green : row.Moving ? Cyan : Color.FromArgb(71, 90, 117);
            Connection(new(left.Right, centerY), new(card.Left, y), row, color, phase * .5, false);
            Connection(new(card.Right, y), new(right.Left, centerY), row, color, phase * .5, true);
            if (highlight) DashboardTheme.Glow(g, card, 10, color, 1, row.Moving ? (int)(60 + 12 * Math.Sin(phase * Math.PI * 2)) : 38);
            using var shape = DashboardTheme.Round(card, 10);
            using var fill = new LinearGradientBrush(card, highlight ? Color.FromArgb(24, 57, 57) : Color.FromArgb(29, 38, 52), Color.FromArgb(15, 22, 31), 30f);
            using var stroke = new Pen(highlight ? Color.FromArgb(175, color) : DashboardTheme.Border, highlight ? 1.2f : 1);
            g.FillPath(fill, shape); g.DrawPath(stroke, shape);
            RouteIcon(g, new PointF(card.X + 15, card.Y + 15), row.Route.IsDirect, highlight ? color : DashboardTheme.Muted);
            DrawLabel(g, row.Route.Name, new RectangleF(card.X + 33, card.Y + 2, card.Width - 42, 24), 10, DashboardTheme.Ink, FontStyle.Bold);
            var bottomY = card.Bottom - 34;
            DrawLabel(g, row.Badge, new RectangleF(card.X + 12, bottomY, card.Width - 96, 14), 6.7f, highlight ? color : DashboardTheme.Muted, FontStyle.Bold);
            DrawLabel(g, row.Detail, new RectangleF(card.X + 12, bottomY + 14, card.Width - 15, 17), 7, DashboardTheme.Muted);
            var hasUnit = row.Ping.EndsWith(" ms", StringComparison.Ordinal); var digits = hasUnit ? row.Ping[..^3] : row.Ping;
            using var number = new Font(FontAssets.DisplayFamily, (digits.Length > 5 ? 18 : 21) * 96f / 72, FontStyle.Bold, GraphicsUnit.Pixel);
            using var numberBrush = new SolidBrush(highlight ? color : DashboardTheme.Ink);
            using var rightAlign = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(digits, number, numberBrush, new RectangleF(card.Right - 120, card.Y + 20, 84, 29), rightAlign);
            if (hasUnit) DrawLabel(g, "ms", new RectangleF(card.Right - 33, card.Y + 24, 27, 22), 8.5f, highlight ? color : DashboardTheme.Muted);
        }
        Endpoint(left, _remote is not null ? "Amigo · Acre" : _bridgeMode ? "Este PC · ponte" : "Seu PC", "ORIGEM", false, anyMoving ? tint : DashboardTheme.Muted);
        Endpoint(right, _destination?.Ip == "127.0.0.1" ? "Teste local" : "Destino do jogo", _remote?.Frame.Measurements?.Target ?? _destination?.Ip ?? "Detecção automática", true, anyMoving ? tint : DashboardTheme.Muted);
        var footer = PreviewLabel ?? (_remote is not null ? "Medições recebidas de " + _remote.Frame.Name + " · ICMP" : _bridgeMode ? "Aguardando as medições do PC do amigo" : !_active && _report is not null ? "Medição pausada · médias da comparação" : _sample is null ? "As pontes aparecem quando disponíveis e medidas" : "RTT do caminho completo · animação ilustrativa do fluxo");
        DrawLabel(g, footer, new RectangleF(0, height - 24, width, 22), 7.6f, PreviewLabel is null ? DashboardTheme.Muted : DashboardTheme.Accent, center: true);
        g.Restore(state);

        void Endpoint(RectangleF box, string title, string detail, bool server, Color color)
        {
            using var shape = DashboardTheme.Round(box, 12); using var fill = new LinearGradientBrush(box, Color.FromArgb(26, 35, 48), Color.FromArgb(16, 22, 31), 70f);
            using var outline = new Pen(Color.FromArgb(75, color)); g.FillPath(fill, shape); g.DrawPath(outline, shape);
            var cx = box.X + box.Width / 2; var cy = box.Y + 23;
            using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (!server) { g.DrawRectangle(pen, cx - 13, cy - 10, 26, 17); g.DrawLine(pen, cx, cy + 8, cx, cy + 13); g.DrawLine(pen, cx - 8, cy + 13, cx + 8, cy + 13); }
            else for (var n = 0; n < 3; n++) { var r = new RectangleF(cx - 13, cy - 13 + n * 9, 26, 6); using var rack = DashboardTheme.Round(r, 2); g.DrawPath(pen, rack); g.DrawLine(pen, cx - 9, r.Top + 3, cx - 7, r.Top + 3); }
            DrawLabel(g, title, new RectangleF(box.X + 3, box.Y + 44, box.Width - 6, 20), 9.2f, DashboardTheme.Ink, FontStyle.Bold, true);
            DrawLabel(g, detail, new RectangleF(box.X + 3, box.Y + 65, box.Width - 6, 16), 7.2f, DashboardTheme.Muted, center: true);
        }
        void Connection(PointF start, PointF end, DiagramRouteView row, Color color, double travel, bool second)
        {
            var bend = Math.Max(12, (end.X - start.X) * .45f); var p1 = new PointF(start.X + bend, start.Y); var p2 = new PointF(end.X - bend, end.Y);
            using var path = new GraphicsPath(); path.AddBezier(start, p1, p2, end);
            var highlighted = row.Selected || row.Moving;
            if (highlighted)
                for (var layer = 4; layer >= 1; layer--) { using var glow = new Pen(Color.FromArgb(7 + (4 - layer) * 5, color), 2 + layer * 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawPath(glow, path); }
            using var gradient = new LinearGradientBrush(new PointF(start.X, start.Y - 1), new PointF(Math.Max(start.X + 1, end.X), end.Y + 1), highlighted ? Color.FromArgb(105, Cyan) : Color.FromArgb(35, color), Color.FromArgb(highlighted ? 240 : 100, color));
            using var pen = new Pen(gradient, highlighted ? 2 : 1.2f);
            if (_sample is null && _report is null) pen.DashStyle = DashStyle.Dash;
            g.DrawPath(pen, path);
            if (!row.Moving) return;
            for (var n = 0; n < 2; n++)
            {
                var t = (float)((travel + n * .5 - (second ? .25 : 0) + 1) % 1);
                var point = Bezier(start, p1, p2, end, t);
                for (var halo = 3; halo > 0; halo--) { var radius = 2 + halo * 2; using var blur = new SolidBrush(Color.FromArgb(12, color)); g.FillEllipse(blur, point.X - radius, point.Y - radius, radius * 2, radius * 2); }
                using var light = new SolidBrush(Color.FromArgb(223, 251, 248)); g.FillEllipse(light, point.X - 2, point.Y - 2, 4, 4);
            }
        }
    }

    private static PointF Bezier(PointF a, PointF b, PointF c, PointF d, float t)
    {
        var u = 1 - t; return new(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
            u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
    }
    private static void RouteIcon(Graphics g, PointF center, bool direct, Color color)
    {
        using var pen = new Pen(color, 1.4f); using var dot = new SolidBrush(color);
        if (direct) { g.DrawEllipse(pen, center.X - 6, center.Y - 6, 12, 12); g.DrawLine(pen, center.X - 4, center.Y + 3, center.X + 4, center.Y - 3); }
        else { g.DrawLine(pen, center.X - 6, center.Y + 4, center.X, center.Y - 5); g.DrawLine(pen, center.X, center.Y - 5, center.X + 6, center.Y + 4); g.FillEllipse(dot, center.X - 8, center.Y + 2, 4, 4); g.FillEllipse(dot, center.X - 2, center.Y - 7, 4, 4); g.FillEllipse(dot, center.X + 4, center.Y + 2, 4, 4); }
    }
    private static void DrawLabel(Graphics g, string text, RectangleF bounds, float size, Color color, FontStyle style = FontStyle.Regular, bool center = false)
    {
        using var font = new Font(size >= 17 ? FontAssets.DisplayFamily : FontAssets.BodyFamily, size * 96f / 72, size >= 17 ? FontStyle.Bold : style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = center ? StringAlignment.Center : StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(text, font, brush, bounds, format);
    }
}
