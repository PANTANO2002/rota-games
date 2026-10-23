namespace RotaGame.App;

// Measure text and actions before reserving the row. Docked controls with fixed
// row heights used to overlap when font metrics or DPI needed a few more pixels.
internal sealed class ConnectionCard : DashboardSurface
{
    private readonly Label _heading, _detail;
    private readonly List<(Control Control, int Height)> _fields = [];
    private readonly List<(DashboardButton Button, int Width, int Height)> _actions = [];
    internal ConnectionCard(string title, string detail, float titleSize)
    {
        Padding = new Padding(20, 14, 20, 14); Margin = new Padding(0, 0, 0, 12);
        _heading = new Label { Text = title, Font = FontAssets.Title(titleSize), ForeColor = DashboardTheme.Ink, BackColor = Color.Transparent, AutoEllipsis = false };
        _detail = new Label { Text = detail, Font = FontAssets.Ui(10.5f), ForeColor = DashboardTheme.Muted, BackColor = Color.Transparent, AutoEllipsis = false };
        Controls.Add(_heading); Controls.Add(_detail);
    }
    internal void AddField(Control control, int height) { _fields.Add((control, height)); control.Dock = DockStyle.None; Controls.Add(control); PerformLayout(); }
    internal void AddActions(params DashboardButton[] actions) { _actions.AddRange(actions.Select(b => (b, b.Width, b.Height))); Controls.AddRange(actions); PerformLayout(); }
    internal int PreferredHeight(int width) => Arrange(width, false);
    private int Arrange(int width, bool apply)
    {
        if (_heading is null) return 0;
        var scale = DeviceDpi / 96f; var available = Math.Max(1, width - Padding.Horizontal);
        int Measure(Label label) => TextRenderer.MeasureText(label.Text, label.Font, new Size(available, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height + (int)(5 * scale);
        var y = Padding.Top; var headingHeight = Math.Max((int)(29 * scale), Measure(_heading));
        if (apply) _heading.SetBounds(Padding.Left, y, available, headingHeight); y += headingHeight + (int)(4 * scale);
        var detailHeight = Measure(_detail);
        if (apply) _detail.SetBounds(Padding.Left, y, available, detailHeight); y += detailHeight + (int)(9 * scale);
        foreach (var (control, height) in _fields)
        {
            if (apply) control.SetBounds(Padding.Left, y, available, (int)(height * scale)); y += (int)((height + 8) * scale);
        }
        var x = 0; var rowHeight = 0;
        foreach (var (button, requestedWidth, requestedHeight) in _actions)
        {
            var buttonWidth = Math.Max((int)(requestedWidth * scale), TextRenderer.MeasureText(button.Text, button.Font).Width + (int)(24 * scale));
            var buttonHeight = Math.Max((int)(requestedHeight * scale), button.Font.Height + (int)(18 * scale));
            if (x > 0 && x + buttonWidth > available) { y += rowHeight + (int)(7 * scale); x = 0; rowHeight = 0; }
            if (apply) button.SetBounds(Padding.Left + x, y, Math.Min(buttonWidth, available), buttonHeight);
            x += buttonWidth + (int)(10 * scale); rowHeight = Math.Max(rowHeight, buttonHeight);
        }
        return y + rowHeight + Padding.Bottom;
    }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Arrange(ClientSize.Width, true); }
    internal void CheckTextAndActions()
    {
        var children = Controls.Cast<Control>().Where(c => c.Visible).ToArray();
        foreach (var child in children)
        {
            if (child.Bottom > ClientSize.Height - Padding.Bottom + 1) throw new InvalidOperationException("Conteúdo cortado no cartão: " + _heading.Text);
            if (children.Any(other => other != child && other.Bounds.IntersectsWith(child.Bounds))) throw new InvalidOperationException("Texto e botões sobrepostos: " + _heading.Text);
        }
    }
}

internal interface IInviteClipboard { string Read(); void Write(string text); }
internal sealed class InviteClipboard : IInviteClipboard
{
    public string Read() => Clipboard.ContainsText() ? Clipboard.GetText() : "";
    public void Write(string text) => Clipboard.SetText(text);
}
internal sealed class MemoryInviteClipboard : IInviteClipboard
{
    private string _text = "";
    public string Read() => _text;
    public void Write(string text) => _text = text;
}
