using System.Drawing.Drawing2D;
using System.ComponentModel;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal static class DashboardTheme
{
    public static readonly Color Canvas = Color.FromArgb(9, 12, 17), Surface = Color.FromArgb(18, 23, 31), Raised = Color.FromArgb(26, 33, 44),
        Ink = Color.FromArgb(239, 243, 249), Muted = Color.FromArgb(145, 158, 179), Border = Color.FromArgb(39, 49, 64),
        Accent = Color.FromArgb(255, 71, 89), Green = Color.FromArgb(39, 221, 172);
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var path = new GraphicsPath(); var d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    public static void Glow(Graphics g, RectangleF bounds, float radius, Color color, float scale, int strength)
    {
        // Feather the halo inside the control's own bounds, so neighboring controls never clip it.
        for (var layer = 8; layer >= 1; layer--)
        {
            var spread = layer * .48f * scale;
            var halo = bounds; halo.Inflate(spread, spread);
            using var shape = Round(halo, radius + spread);
            var opacity = (int)(strength * Math.Pow(1 - layer / 10f, 2));
            using var pen = new Pen(Color.FromArgb(opacity, color), 1.8f * scale);
            g.DrawPath(pen, shape);
        }
    }
    public static void Text(Graphics g, string text, RectangleF bounds, float size, Color color, FontStyle style = FontStyle.Regular, bool center = false)
    {
        using var font = size >= 17 ? FontAssets.Title(size, FontStyle.Bold) : FontAssets.Ui(size, style);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = center ? StringAlignment.Center : StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(text, font, brush, bounds, format);
    }
}

internal class DashboardPanel : Panel
{
    public DashboardPanel() => SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
}

internal sealed class DashboardTableLayout : TableLayoutPanel
{
    public DashboardTableLayout() => SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
}

internal sealed class DashboardFlowLayout : FlowLayoutPanel
{
    public DashboardFlowLayout() => SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
}

internal class DashboardSurface : DashboardPanel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Stroke { get; set; } = DashboardTheme.Border;
    public DashboardSurface() { DoubleBuffered = true; BackColor = DashboardTheme.Surface; Padding = new Padding(18); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? DashboardTheme.Canvas); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = DashboardTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), 12 * DeviceDpi / 96f);
        using var fill = new SolidBrush(BackColor); using var border = new Pen(Stroke);
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path);
    }
}

internal class DashboardButton : Button
{
    private bool _hover, _pressed;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SelectedStyle { get; set; }
    public DashboardButton() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = _pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) _pressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; var s = DeviceDpi / 96f;
        g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Parent?.BackColor ?? DashboardTheme.Surface);
        if (Width <= 10 * s || Height <= 10 * s) return;
        var primary = BackColor == DashboardTheme.Accent;
        var tint = primary || SelectedStyle ? DashboardTheme.Accent : Color.FromArgb(104, 153, 215);
        var bounds = new RectangleF(4.5f * s, 4.5f * s, Width - 9 * s, Height - 9 * s);
        var radius = 7 * s;
        if (Enabled) DashboardTheme.Glow(g, bounds, radius, tint, s, primary ? (_hover ? 110 : 74) : SelectedStyle ? 55 : _hover ? 44 : 14);
        var top = !Enabled ? Color.FromArgb(29, 36, 46) : primary ? Color.FromArgb(255, 101, 115) : SelectedStyle ? Color.FromArgb(64, 32, 44) : Color.FromArgb(38, 48, 63);
        var bottom = !Enabled ? Color.FromArgb(22, 28, 37) : primary ? Color.FromArgb(209, 26, 66) : SelectedStyle ? Color.FromArgb(31, 22, 33) : Color.FromArgb(18, 24, 34);
        if (Enabled && _hover) { top = ControlPaint.Light(top, .12f); bottom = ControlPaint.Light(bottom, .06f); }
        if (Enabled && _pressed) { top = ControlPaint.Dark(top, .12f); bottom = ControlPaint.Dark(bottom, .12f); }
        using var path = DashboardTheme.Round(bounds, radius);
        using var fill = new LinearGradientBrush(bounds, top, bottom, 55f);
        using var outline = new LinearGradientBrush(bounds,
            !Enabled ? DashboardTheme.Border : primary ? Color.FromArgb(255, 156, 167) : SelectedStyle ? DashboardTheme.Accent : _hover ? tint : Color.FromArgb(64, 80, 103),
            !Enabled ? DashboardTheme.Border : primary ? Color.FromArgb(218, 40, 77) : SelectedStyle ? Color.FromArgb(126, 44, 65) : DashboardTheme.Border, 90f);
        using var pen = new Pen(outline, s);
        g.FillPath(fill, path); g.DrawPath(pen, path);
        if (Focused && ShowFocusCues)
        {
            var focusBounds = bounds; focusBounds.Inflate(-2 * s, -2 * s);
            using var focus = DashboardTheme.Round(focusBounds, 5 * s); using var focusPen = new Pen(Color.FromArgb(155, DashboardTheme.Ink), s) { DashStyle = DashStyle.Dot };
            g.DrawPath(focusPen, focus);
        }
        var textBounds = Rectangle.Round(bounds); if (_pressed) textBounds.Offset(0, (int)s);
        TextRenderer.DrawText(g, Text, Font, textBounds, Enabled ? ForeColor : DashboardTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }
}

internal enum DashboardWindowAction { Minimize, Maximize, Close }

internal sealed class DashboardWindowButton(DashboardWindowAction action) : DashboardButton
{
    internal DashboardWindowAction Action => action;
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; var s = DeviceDpi / 96f; var cx = Width / 2f; var cy = Height / 2f;
        g.SmoothingMode = SmoothingMode.None; g.PixelOffsetMode = PixelOffsetMode.Half;
        using var pen = new Pen(Enabled ? DashboardTheme.Ink : DashboardTheme.Muted, Math.Max(1, 1.2f * s));
        switch (action)
        {
            case DashboardWindowAction.Minimize: g.DrawLine(pen, cx - 5 * s, cy + 2 * s, cx + 5 * s, cy + 2 * s); break;
            case DashboardWindowAction.Maximize:
                if (FindForm()?.WindowState == FormWindowState.Maximized)
                {
                    g.DrawLines(pen, new PointF[] { new(cx - 3 * s, cy - 5 * s), new(cx + 5 * s, cy - 5 * s), new(cx + 5 * s, cy + 3 * s) });
                    g.DrawRectangle(pen, cx - 5 * s, cy - 3 * s, 8 * s, 8 * s);
                }
                else g.DrawRectangle(pen, cx - 4.5f * s, cy - 4.5f * s, 9 * s, 9 * s);
                break;
            case DashboardWindowAction.Close:
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(pen, cx - 4.5f * s, cy - 4.5f * s, cx + 4.5f * s, cy + 4.5f * s);
                g.DrawLine(pen, cx + 4.5f * s, cy - 4.5f * s, cx - 4.5f * s, cy + 4.5f * s); break;
        }
    }
}

internal sealed class DashboardToggle : CheckBox
{
    public DashboardToggle() { AutoCheck = false; SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; Size = new Size(192, 38); AccessibleName = "Ativar ou desativar o RotaGame"; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor); var s = DeviceDpi / 96f;
        var trackBounds = new RectangleF(4 * s, 8 * s, 40 * s, 22 * s);
        if (Checked) DashboardTheme.Glow(g, trackBounds, 11 * s, DashboardTheme.Green, s, 74);
        using var track = DashboardTheme.Round(trackBounds, 11 * s);
        using var fill = new LinearGradientBrush(trackBounds, Checked ? Color.FromArgb(74, 244, 189) : Color.FromArgb(69, 82, 102), Checked ? Color.FromArgb(7, 146, 116) : Color.FromArgb(34, 42, 56), 75f);
        var thumbBounds = new RectangleF((Checked ? 24 : 7) * s, 11 * s, 16 * s, 16 * s);
        using var thumb = new LinearGradientBrush(thumbBounds, Color.White, Color.FromArgb(206, 222, 231), 90f); g.FillPath(fill, track);
        g.FillEllipse(thumb, thumbBounds);
        DashboardTheme.Text(g, Checked ? "RotaGame ON" : "RotaGame OFF", new RectangleF(51 * s, 0, Width - 51 * s, Height), 10, DashboardTheme.Ink, FontStyle.Bold);
    }
}

internal sealed class GameCard : Control
{
    private bool _hover;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public GameProfile? Profile { get; init; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool SelectedGame { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Running { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool EmptyLibrary { get; init; }
    public bool Adding => Profile is null;
    private readonly Bitmap? _icon;
    internal bool HasExecutableIcon => _icon is not null;
    public GameCard(GameProfile? profile)
    {
        Profile = profile; SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; TabStop = true;
        AccessibleName = profile is null ? "Adicionar jogo" : "Selecionar " + profile.Name;
        AccessibleRole = AccessibleRole.PushButton;
        if (profile?.ExecutablePath is { } file)
            try { using var icon = Icon.ExtractAssociatedIcon(file); _icon = icon?.ToBitmap(); } catch (Exception ex) when (ex is ArgumentException or IOException or System.ComponentModel.Win32Exception) { }
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } }
    protected override void Dispose(bool disposing) { if (disposing) { _icon?.Dispose(); ContextMenuStrip?.Dispose(); } base.Dispose(disposing); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Parent?.BackColor ?? DashboardTheme.Canvas); var s = DeviceDpi / 96f;
        if (Width <= 10 * s || Height <= 10 * s) return;
        var bounds = new RectangleF(4.5f * s, 4.5f * s, Width - 9 * s, Height - 9 * s);
        if (SelectedGame || Focused || _hover) DashboardTheme.Glow(g, bounds, 11 * s, SelectedGame || Focused ? DashboardTheme.Accent : Color.FromArgb(104, 153, 215), s, SelectedGame || Focused ? 57 : 28);
        using var shape = DashboardTheme.Round(bounds, 11 * s);
        using var fill = new LinearGradientBrush(bounds, SelectedGame ? Color.FromArgb(45, 28, 39) : _hover ? Color.FromArgb(31, 42, 57) : Color.FromArgb(25, 32, 44), DashboardTheme.Surface, 55f);
        using var border = new Pen(SelectedGame || Focused ? DashboardTheme.Accent : DashboardTheme.Border, SelectedGame ? 1.5f : 1);
        if (Adding) border.DashStyle = DashStyle.Dash;
        g.FillPath(fill, shape); g.DrawPath(border, shape);
        var badge = new RectangleF(15 * s, 24 * s, 47 * s, 47 * s);
        var accent = Profile?.Id switch { "the-finals" => Color.FromArgb(230, 46, 62), "rocket-league" => Color.FromArgb(42, 119, 222), "gta-online" => Color.FromArgb(106, 76, 174), _ => Color.FromArgb(42, 54, 72) };
        using var badgeShape = DashboardTheme.Round(badge, 9 * s); using var badgeFill = new LinearGradientBrush(badge, ControlPaint.Light(accent, .18f), ControlPaint.Dark(accent, .18f), 55f); g.FillPath(badgeFill, badgeShape);
        if (_icon is not null) g.DrawImage(_icon, badge.X + 5 * s, badge.Y + 5 * s, badge.Width - 10 * s, badge.Height - 10 * s);
        else DashboardTheme.Text(g, Profile?.Id switch { "the-finals" => "TF", "rocket-league" => "RL", "gta-online" => "V", _ => Adding ? "+" : Profile!.Name[..1].ToUpperInvariant() }, badge, Adding ? 25 : 15, Color.White, FontStyle.Bold, true);
        DashboardTheme.Text(g, Profile?.Name ?? (EmptyLibrary ? "Nenhum jogo instalado encontrado" : "Adicionar jogo"), new RectangleF(75 * s, 22 * s, Width - 87 * s, 28 * s), 10.5f, DashboardTheme.Ink, FontStyle.Bold);
        var status = Adding ? EmptyLibrary ? "Instale um jogo ou clique aqui para adicionar o executável." : "Escolher executável" : Running ? "●  Em execução" : "Fechado";
        DashboardTheme.Text(g, status, new RectangleF(75 * s, 51 * s, Width - 87 * s, 22 * s), 9, Running ? DashboardTheme.Green : DashboardTheme.Muted);
        if (SelectedGame) DashboardTheme.Text(g, "SELECIONADO", new RectangleF(15 * s, Height - 24 * s, Width - 30 * s, 18 * s), 7, DashboardTheme.Accent, FontStyle.Bold);
    }
}

