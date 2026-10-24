using System.ComponentModel;
using System.Drawing.Drawing2D;
using RotaGame.Core;

namespace RotaGame.App;

internal sealed class NetworkMap : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private NetworkTrace? _trace; private ObservedDestination? _destination;
    private RouteOption _route = RouteOption.Direct; private DateTimeOffset? _lastReply;
    internal event EventHandler? ContentChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal string? PreviewLabel { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal NetworkTrace? Trace { get => _trace; set { _trace = value; Changed(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal ObservedDestination? Destination { get => _destination; set { _destination = value; _trace = null; _lastReply = null; Changed(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal RouteOption Route { get => _route; set { _route = value; _trace = null; _lastReply = null; Changed(); } }
    internal int ContentHeight => Math.Max(185, 60 + (int)Math.Ceiling(Nodes().Count / (double)Columns) * 100);
    private int Columns => Math.Max(1, Math.Min(6, (int)(Width / (190 * DeviceDpi / 96f))));
    internal void SetSample(Sample? sample) { _lastReply = sample?.Outcome == ProbeOutcome.Success ? sample.At : null; Invalidate(); }
    public NetworkMap()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = DashboardTheme.Surface; AccessibleName = "Trajeto observado até o destino do jogo";
        _timer.Tick += (_, _) => { if (Visible && FindForm()?.WindowState != FormWindowState.Minimized && _lastReply is not null) Invalidate(); };
        _timer.Start(); Resize += (_, _) => Changed();
    }
    private void Changed() { ContentChanged?.Invoke(this, EventArgs.Empty); Invalidate(); }
    private List<(string Title, string Address, string Detail, bool Known)> Nodes()
    {
        var nodes = new List<(string, string, string, bool)> { ("SEU PC", _trace?.Route.Name ?? "Origem da conexão", "Início do trajeto", true) };
        if (!_route.IsDirect) nodes.Add(("CLOUDFLARE WARP", "Túnel criptografado", "Trechos internos não revelados", true));
        if (_trace is { Status: not "route-changed" } trace)
            nodes.AddRange(trace.Hops.Select(h => (h.Outcome == TraceOutcome.Destination ? "DESTINO RESPONDEU · ICMP" : "SALTO " + h.Number,
                h.Outcome == TraceOutcome.Destination && _destination is { } d ? d.Ip + ":" + d.Port : h.Address ?? "Sem resposta",
                h.RttMs is { } rtt ? $"RTT da sonda · {rtt:0.0} ms" : "IP e latência não revelados", h.Address is not null)));
        if (_destination is { } d && _trace?.Reached != true) nodes.Add(("DESTINO OBSERVADO", d.Ip + ":" + d.Port, d.Protocol + " · resposta final não confirmada", true));
        if (_destination is null) nodes.Add(("DESTINO DO JOGO", "Aguardando o jogo iniciar", "Detecção pelo processo", false));
        return nodes;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.Clear(BackColor); if (Width < 40 || Height < 30) return; g.SmoothingMode = SmoothingMode.AntiAlias;
        var nodes = Nodes(); var cols = Columns; var cell = Width / (float)cols; var positions = new List<RectangleF>();
        var live = _lastReply is { } at && DateTimeOffset.UtcNow - at < TimeSpan.FromSeconds(5);
        var color = live ? DashboardTheme.Green : Color.FromArgb(89, 159, 214);
        DashboardTheme.Text(g, _trace?.Status == "scanning" ? "REVELANDO O TRAJETO…" : _destination is null ? "AGUARDANDO DESTINO" : "TRAJETO OBSERVADO · " + _route.Name.ToUpperInvariant(), new RectangleF(8, 2, Width - 16, 24), 9, color, FontStyle.Bold);
        for (var i = 0; i < nodes.Count; i++)
        {
            var row = i / cols; var col = row % 2 == 0 ? i % cols : cols - 1 - i % cols;
            positions.Add(new RectangleF(col * cell + 8, 40 + row * 100, cell - 16, 76));
        }
        for (var i = 1; i < positions.Count; i++)
        {
            var a = positions[i - 1]; var b = positions[i];
            var same = Math.Abs(a.Y - b.Y) < 1;
            var start = same ? new PointF(a.X < b.X ? a.Right : a.Left, a.Y + 38) : new PointF(a.X + a.Width / 2, a.Bottom);
            var end = same ? new PointF(a.X < b.X ? b.Left : b.Right, b.Y + 38) : new PointF(b.X + b.Width / 2, b.Top);
            using var path = new GraphicsPath(); path.AddLine(start, end);
            using var glow = new Pen(Color.FromArgb(live ? 42 : 16, color), 7); g.DrawPath(glow, path);
            using var line = new Pen(Color.FromArgb(140, color), 1.4f) { DashStyle = nodes[i].Known && nodes[i - 1].Known ? DashStyle.Solid : DashStyle.Dash }; g.DrawPath(line, path);
            if (live)
            {
                var p = (Environment.TickCount64 % 2200) / 2200f; using var dot = new SolidBrush(color);
                g.FillEllipse(dot, start.X + (end.X - start.X) * p - 2, start.Y + (end.Y - start.Y) * p - 2, 4, 4);
            }
        }
        for (var i = 0; i < nodes.Count; i++)
        {
            var b = positions[i]; var node = nodes[i];
            if (i == 0 || i == nodes.Count - 1) DashboardTheme.Glow(g, b, 9, color, 1, live ? 38 : 17);
            using var path = DashboardTheme.Round(b, 9); using var fill = new LinearGradientBrush(b, Color.FromArgb(29, 45, 58), DashboardTheme.Raised, 50f); using var border = new Pen(node.Known ? Color.FromArgb(100, color) : DashboardTheme.Border);
            g.FillPath(fill, path); g.DrawPath(border, path);
            DashboardTheme.Text(g, node.Title, new RectangleF(b.X + 10, b.Y + 6, b.Width - 20, 18), 8, color, FontStyle.Bold);
            DashboardTheme.Text(g, node.Address, new RectangleF(b.X + 10, b.Y + 25, b.Width - 20, 23), 11, DashboardTheme.Ink, FontStyle.Bold);
            DashboardTheme.Text(g, node.Detail, new RectangleF(b.X + 10, b.Y + 51, b.Width - 20, 17), 7.5f, DashboardTheme.Muted);
        }
        if (PreviewLabel is { } label) DashboardTheme.Text(g, label, new RectangleF(8, Height - 19, Width - 16, 18), 8, DashboardTheme.Accent, center: true);
    }
    protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }
}
