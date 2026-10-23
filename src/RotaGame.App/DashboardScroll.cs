using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RotaGame.App;

internal sealed class DashboardScrollBar : Control
{
    private int _content, _viewport, _value;
    private bool _hover, _dragging;
    private float _grab;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Horizontal { get; init; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Maximum => Math.Max(0, _content - _viewport);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Value
    {
        get => _value;
        set { var next = Math.Clamp(value, 0, Maximum); if (_value == next) return; _value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? ValueChanged;
    public DashboardScrollBar() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.ScrollBar; }
    public void Configure(int content, int viewport) { _content = content; _viewport = viewport; Value = _value; Invalidate(); }
    private float TrackLength => Math.Max(1, (Horizontal ? Width : Height) - 10 * DeviceDpi / 96f);
    private float ThumbLength => Math.Min(TrackLength, Math.Max(36 * DeviceDpi / 96f, TrackLength * _viewport / Math.Max(1, _content)));
    private float Start => 5 * DeviceDpi / 96f + (TrackLength - ThumbLength) * Value / Math.Max(1, Maximum);
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; var s = DeviceDpi / 96f; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = Horizontal ? new RectangleF(5 * s, Height / 2f - 2 * s, TrackLength, 4 * s) : new RectangleF(Width / 2f - 2 * s, 5 * s, 4 * s, TrackLength);
        using var rail = DashboardTheme.Round(track, 2 * s); using var railFill = new SolidBrush(Color.FromArgb(23, 31, 43)); g.FillPath(railFill, rail);
        var thumb = Horizontal ? new RectangleF(Start, Height / 2f - 4 * s, ThumbLength, 8 * s) : new RectangleF(Width / 2f - 4 * s, Start, 8 * s, ThumbLength);
        var active = _hover || _dragging || Focused;
        DashboardTheme.Glow(g, thumb, 4 * s, DashboardTheme.Accent, s, _dragging ? 82 : active ? 55 : 10);
        using var shape = DashboardTheme.Round(thumb, 4 * s);
        using var fill = new LinearGradientBrush(thumb, active ? Color.FromArgb(255, 113, 132) : Color.FromArgb(89, 111, 140), active ? Color.FromArgb(190, 35, 78) : Color.FromArgb(41, 56, 77), Horizontal ? 90f : 0f);
        using var border = new Pen(active ? Color.FromArgb(255, 151, 165) : Color.FromArgb(108, 129, 156), .7f * s);
        g.FillPath(fill, shape); g.DrawPath(border, shape);
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; Focus();
        var p = Horizontal ? e.X : e.Y;
        if (p < Start || p > Start + ThumbLength) Value += (p < Start ? -1 : 1) * Math.Max(1, _viewport * 4 / 5);
        _grab = Math.Clamp(p - Start, 0, ThumbLength); _dragging = true; Capture = true; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!_dragging) return;
        var p = Horizontal ? e.X : e.Y;
        Value = (int)Math.Round((p - 5 * DeviceDpi / 96f - _grab) * Maximum / Math.Max(1, TrackLength - ThumbLength));
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _dragging = false; Capture = false; Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { _dragging = false; Invalidate(); } }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Value = e.KeyCode switch { Keys.Home => 0, Keys.End => Maximum, Keys.PageUp => Value - _viewport, Keys.PageDown => Value + _viewport,
            Keys.Left or Keys.Up => Value - (int)(40 * DeviceDpi / 96f), Keys.Right or Keys.Down => Value + (int)(40 * DeviceDpi / 96f), _ => Value };
        e.Handled = true;
    }
    internal void CheckInteraction()
    {
        if (Maximum == 0) throw new InvalidOperationException("Barra sem conteúdo excedente.");
        Value = 0; var middle = Start + ThumbLength / 2;
        var down = Horizontal ? new Point((int)middle, Height / 2) : new Point(Width / 2, (int)middle);
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, down.X, down.Y, 0));
        var end = Horizontal ? new Point(Width - 5, Height / 2) : new Point(Width / 2, Height - 5);
        OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0)); OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, end.X, end.Y, 0));
        if (Value == 0) throw new InvalidOperationException("Arrastar a barra não moveu o conteúdo.");
        OnKeyDown(new KeyEventArgs(Keys.Home)); if (Value != 0) throw new InvalidOperationException("Tecla Home não restaurou a posição.");
    }
}

// Content is clipped by an ordinary panel. No native Windows scrollbars are created.
internal sealed class DashboardScrollHost : DashboardPanel
{
    private readonly DashboardPanel _viewport = new() { Margin = Padding.Empty };
    private readonly DashboardScrollBar _bar;
    private Control? _content;
    private readonly HashSet<Control> _wheelTargets = [];
    private int _extent;
    private bool _arranging, _layoutAgain;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Horizontal { get; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Size ViewportSize => _viewport.ClientSize;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Overflowing => _extent > (Horizontal ? ClientSize.Width : ClientSize.Height);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int ScrollPosition { get => _bar.Value; set => _bar.Value = value; }
    public event EventHandler? ViewportChanged;
    public DashboardScrollHost(bool horizontal)
    {
        Horizontal = horizontal; DoubleBuffered = true; BackColor = DashboardTheme.Canvas; Margin = Padding.Empty;
        _bar = new DashboardScrollBar { Horizontal = horizontal, BackColor = BackColor, AccessibleName = horizontal ? "Rolar biblioteca de jogos" : "Rolar painel" };
        Controls.Add(_viewport); Controls.Add(_bar); _bar.ValueChanged += (_, _) => PositionContent();
        _bar.MouseWheel += ScrollWheel;
    }
    public void SetContent(Control content)
    {
        if (ReferenceEquals(_content, content)) return;
        if (_content is not null) _viewport.Controls.Remove(_content);
        _content = content; content.Dock = DockStyle.None; content.Margin = Padding.Empty; _viewport.Controls.Add(content); WireWheel(content); PerformLayout();
    }
    public void SetExtent(int extent) { extent = Math.Max(0, extent); if (_extent == extent) return; _extent = extent; if (_arranging) _layoutAgain = true; else PerformLayout(); }
    public void Reveal(int start, int length)
    {
        var visible = Horizontal ? ViewportSize.Width : ViewportSize.Height;
        if (start < ScrollPosition) ScrollPosition = start;
        else if (start + length > ScrollPosition + visible) ScrollPosition = start + length - visible;
    }
    internal void CheckVisibility() { if (Visible && _bar.Visible != Overflowing) throw new InvalidOperationException("A visibilidade da barra não acompanha o conteúdo."); }
    internal void CheckInteraction()
    {
        _bar.CheckInteraction();
        OnMouseWheel(new HandledMouseEventArgs(MouseButtons.None, 0, 0, 0, -120));
        if (ScrollPosition == 0) throw new InvalidOperationException("A roda não moveu a barra.");
        ScrollPosition = 0;
    }
    private void WireWheel(Control control)
    {
        if (!_wheelTargets.Add(control)) return;
        control.MouseWheel += ScrollWheel;
        control.ControlAdded += (_, e) => { if (e.Control is { } child) WireWheel(child); };
        foreach (Control child in control.Controls) WireWheel(child);
    }
    private void ScrollWheel(object? sender, MouseEventArgs e)
    {
        if (!Overflowing || e is HandledMouseEventArgs { Handled: true }) return;
        ScrollPosition -= (int)(e.Delta / 120f * 64 * DeviceDpi / 96f);
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }
    protected override void OnMouseWheel(MouseEventArgs e) { ScrollWheel(this, e); base.OnMouseWheel(e); }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_arranging) { _layoutAgain = true; return; }
        _arranging = true;
        try
        {
            do
            {
                _layoutAgain = false; base.OnLayout(e); if (_bar is null) return;
                var previousViewport = _viewport.ClientSize;
                var visibleLength = Horizontal ? ClientSize.Width : ClientSize.Height;
                var overflow = _extent > visibleLength;
                var thickness = overflow ? (int)(22 * DeviceDpi / 96f) : 0;
                _bar.Visible = overflow; _bar.BackColor = _viewport.BackColor = BackColor;
                _viewport.SetBounds(0, 0, Math.Max(0, Width - (Horizontal ? 0 : thickness)), Math.Max(0, Height - (Horizontal ? thickness : 0)));
                _bar.Bounds = Horizontal ? new Rectangle(0, Height - thickness, Width, thickness) : new Rectangle(Width - thickness, 0, thickness, Height);
                _bar.Configure(_extent, visibleLength); PositionContent();
                if (_viewport.ClientSize != previousViewport) ViewportChanged?.Invoke(this, EventArgs.Empty);
            } while (_layoutAgain);
        }
        finally { _arranging = false; }
    }
    private void PositionContent()
    {
        if (_content is null) return;
        var previous = _content.Bounds;
        _content.SetBounds(Horizontal ? -_bar.Value : 0, Horizontal ? 0 : -_bar.Value,
            Horizontal ? Math.Max(_extent, _viewport.Width) : _viewport.Width, Horizontal ? _viewport.Height : Math.Max(_extent, _viewport.Height));
        if (_content.Bounds != previous) { _viewport.Invalidate(true); _content.Invalidate(true); }
    }
}
