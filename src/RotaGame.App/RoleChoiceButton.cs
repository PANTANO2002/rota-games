using System.ComponentModel;
using System.Drawing.Drawing2D;
using RotaGame.Core;

namespace RotaGame.App;

internal sealed class RoleChoiceButton(DeviceRole role) : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Chosen { get; set; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; var s = DeviceDpi / 96f; g.Clear(Parent?.BackColor ?? DashboardTheme.Canvas); g.SmoothingMode = SmoothingMode.AntiAlias;
        var color = role == DeviceRole.Player ? Color.FromArgb(80, 185, 255) : DashboardTheme.Green;
        var rect = new RectangleF(5 * s, 5 * s, Width - 10 * s, Height - 10 * s); if (rect.Width < 1 || rect.Height < 1) return;
        if (Chosen || Focused) DashboardTheme.Glow(g, rect, 11 * s, color, s, 85);
        using var shape = DashboardTheme.Round(rect, 11 * s);
        using var fill = new LinearGradientBrush(rect, Chosen ? Color.FromArgb(30, 78, 89) : Color.FromArgb(38, 49, 66), Chosen ? Color.FromArgb(15, 44, 53) : Color.FromArgb(21, 29, 42), 35f);
        using var pen = new Pen(Enabled ? Chosen ? color : Color.FromArgb(106, 128, 156) : DashboardTheme.Border, (Chosen ? 2 : 1) * s); g.FillPath(fill, shape); g.DrawPath(pen, shape);
        var state = g.Save(); g.ScaleTransform(s, s); var w = Width / s;
        var compact = Height / s < 90; var readable = Enabled || Chosen;
        DashboardTheme.Text(g, role == DeviceRole.Player ? "VOU JOGAR NESTE PC" : "ESTE PC É A PONTE", new RectangleF(19, compact ? 6 : 11, w - 38, 29), compact ? 16 : 18, readable ? DashboardTheme.Ink : DashboardTheme.Muted, FontStyle.Bold);
        DashboardTheme.Text(g, role == DeviceRole.Player ? "Acre · recebe a conexão" : "Cascavel · oferece a conexão", new RectangleF(20, compact ? 31 : 43, w - 40, 24), compact ? 9 : 10, readable ? color : DashboardTheme.Muted);
        DashboardTheme.Text(g, Chosen ? "✓  ESCOLHIDO NESTE PC" : "Clique para escolher", new RectangleF(20, compact ? 55 : 70, w - 40, 21), compact ? 8 : 9, readable ? color : DashboardTheme.Muted, FontStyle.Bold);
        g.Restore(state);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
