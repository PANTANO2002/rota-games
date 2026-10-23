using System.Drawing.Drawing2D;
using RotaGame.Core;
using System.ComponentModel;

namespace RotaGame.App;

internal sealed class LatencyChart : Control
{
    public List<Sample> Samples { get; } = [];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal IReadOnlyList<GraphPoint>? RemotePoints { get; set; }
    public LatencyChart() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = DashboardTheme.Surface; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var font = FontAssets.Ui(9);
        using var muted = new SolidBrush(DashboardTheme.Muted);
        using var grid = new Pen(DashboardTheme.Border);
        using var line = new Pen(Color.FromArgb(65, 221, 179), 2.5f);
        using var fail = new Pen(Color.FromArgb(255, 145, 113), 2);
        var rect = new Rectangle(60, 25, Math.Max(1, Width - 84), Math.Max(1, Height - 66));
        var samples = RemotePoints?.TakeLast(180).ToList() ?? Samples.TakeLast(180).Select(s => new GraphPoint(s.At, s.RttMs, s.Outcome)).ToList();
        var max = Math.Max(20, Math.Ceiling((samples.Where(s => s.RttMs.HasValue).Select(s => s.RttMs!.Value).DefaultIfEmpty(0).Max() * 1.15) / 20) * 20);
        for (var i = 0; i <= 4; i++)
        {
            var y = rect.Top + rect.Height * i / 4;
            g.DrawLine(grid, rect.Left, y, rect.Right, y);
            g.DrawString($"{max * (4 - i) / 4:0}", font, muted, 8, y - 8);
        }
        g.DrawString("ms", font, muted, 8, 4);
        g.DrawString(Width < 600 ? "Últimas 180 sondas · escala automática" : "Últimas 180 sondas  •  lacunas = sem resposta  •  escala automática", font, muted, rect.Left, rect.Bottom + 14);
        if (samples.Count == 0)
        {
            var message = "Aguardando medições";
            var size = g.MeasureString(message, font);
            g.DrawString(message, font, muted, rect.Left + (rect.Width - size.Width) / 2, rect.Top + rect.Height / 2 - 8);
            return;
        }
        PointF? previous = null;
        for (var i = 0; i < samples.Count; i++)
        {
            var x = rect.Left + rect.Width * i / (float)Math.Max(1, samples.Count - 1);
            if (samples[i].RttMs is double rtt && samples[i].Outcome == ProbeOutcome.Success)
            {
                var point = new PointF(x, rect.Bottom - (float)(rtt / max) * rect.Height);
                if (previous.HasValue) g.DrawLine(line, previous.Value, point);
                else { using var dot = new SolidBrush(line.Color); g.FillEllipse(dot, x - 2, point.Y - 2, 4, 4); }
                previous = point;
            }
            else
            {
                g.DrawLine(fail, x - 3, rect.Bottom - 5, x + 3, rect.Bottom + 1);
                g.DrawLine(fail, x - 3, rect.Bottom + 1, x + 3, rect.Bottom - 5);
                previous = null;
            }
        }
    }
}
