using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace RotaGame.App;

internal sealed class IntroForm : Form
{
    private readonly Func<CancellationToken, Task<Form?>> _load;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _watch = new();
    private readonly DashboardButton _skip;
    private double _elapsed;
    private bool _ready;
    private string? _error;
    private string? _startupMessage;
    internal Form? LoadedForm { get; private set; }
    internal static readonly TimeSpan Duration = TimeSpan.FromSeconds(4);

    public IntroForm(Func<CancellationToken, Task<Form?>> load)
    {
        _load = load; ClientSize = new Size(640, 370); FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96); ShowInTaskbar = false; BackColor = DashboardTheme.Canvas; Icon = BrandAssets.LoadIcon();
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _skip = new DashboardButton { Text = "Pular", BackColor = DashboardTheme.Surface, ForeColor = DashboardTheme.Muted, Size = new Size(70, 30), Enabled = false, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Location = new Point(550, 323), AccessibleName = "Pular introdução quando o painel estiver pronto" };
        _skip.Click += (_, _) => { if (_ready || _error is not null) { DialogResult = _ready ? DialogResult.OK : DialogResult.Cancel; Close(); } }; Controls.Add(_skip);
        _timer.Tick += (_, _) =>
        {
            _elapsed = _watch.Elapsed.TotalSeconds;
            if (_ready && _elapsed >= Duration.TotalSeconds) { DialogResult = DialogResult.OK; Close(); return; }
            Invalidate();
        };
        Shown += async (_, _) =>
        {
            _watch.Start(); _timer.Start();
            try
            {
                var form = await _load(_lifetime.Token);
                if (form is null) { DialogResult = DialogResult.Cancel; Close(); return; }
                if (IsDisposed || _lifetime.IsCancellationRequested) { form.Dispose(); return; }
                LoadedForm = form; _ready = true; _skip.Enabled = true;
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (Exception ex) { _error = ex.Message; _skip.Text = "Fechar"; _skip.Enabled = true; Invalidate(); }
        };
    }
    internal void SetStartupStatus(string? message) { _startupMessage = message; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) { IntroArtwork.Draw(e.Graphics, ClientRectangle, _elapsed, _ready, _error, _startupMessage); base.OnPaint(e); }
    protected override void OnFormClosed(FormClosedEventArgs e) { _timer.Stop(); _lifetime.Cancel(); if (DialogResult != DialogResult.OK) { LoadedForm?.Dispose(); LoadedForm = null; } base.OnFormClosed(e); }
    protected override void Dispose(bool disposing) { if (disposing) { _timer.Dispose(); _lifetime.Cancel(); _lifetime.Dispose(); } base.Dispose(disposing); }
}

internal static class IntroArtwork
{
    internal static void Draw(Graphics graphics, Rectangle bounds, double seconds, bool ready, string? error = null, string? startupMessage = null)
    {
        var g = graphics; g.Clear(DashboardTheme.Canvas); g.SmoothingMode = SmoothingMode.AntiAlias;
        var state = g.Save(); g.ScaleTransform(bounds.Width / 640f, bounds.Height / 370f);
        using var borderShape = DashboardTheme.Round(new RectangleF(.5f, .5f, 639, 369), 20);
        using var border = new Pen(DashboardTheme.Border); g.DrawPath(border, borderShape);
        var intro = Ease(seconds / .65); var size = (float)(118 + 16 * intro); var mark = new RectangleF(320 - size / 2, 26 + (float)(7 * (1 - intro)), size, size);
        var reveal = (float)Math.Clamp((seconds - .2) / 1.55, 0, 1);
        var pulse = (float)((seconds * .75) % 1);
        DashboardTheme.Glow(g, mark, 26, DashboardTheme.Accent, 1, (int)(17 + 14 * Math.Sin(seconds * 2.6) * Math.Sin(seconds * 2.6)));
        BrandAssets.Draw(g, mark, reveal, pulse);
        var textAlpha = (int)(255 * Ease((seconds - .9) / 1.0));
        using var title = new GraphicsPath(); using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var family = FontAssets.DisplayFamily;
        title.AddString("RotaGame", family, (int)FontStyle.Bold, 49, new RectangleF(0, 176 + (float)(12 * (1 - Ease((seconds - .9) / 1.0))), 640, 66), format);
        using var ink = new LinearGradientBrush(new RectangleF(190, 175, 270, 70), Color.FromArgb(textAlpha, 244, 247, 251), Color.FromArgb(textAlpha, 255, 71, 102), 0f); g.FillPath(ink, title);
        DashboardTheme.Text(g, "Conexão em foco", new RectangleF(0, 241, 640, 28), 10.5f, Color.FromArgb(textAlpha, DashboardTheme.Muted), center: true);
        DashboardTheme.Text(g, "PANT_CORE", new RectangleF(0, 351, 640, 16), 8, Color.FromArgb(textAlpha, DashboardTheme.Muted), center: true);
        var routeProgress = Math.Clamp((seconds - 1.5) / 2.1, 0, 1);
        using var track = new Pen(DashboardTheme.Border, 2); using var route = new Pen(DashboardTheme.Green, 2.2f);
        g.DrawLine(track, 238, 294, 402, 294); g.DrawLine(route, 238, 294, (float)(238 + 164 * routeProgress), 294);
        for (var i = 0; i < 3; i++)
        {
            var node = new RectangleF(234 + i * 82, 290, 8, 8); var passed = routeProgress >= i / 2.0;
            if (passed) DashboardTheme.Glow(g, node, 4, DashboardTheme.Green, 1, 38);
            using var brush = new SolidBrush(passed ? DashboardTheme.Green : DashboardTheme.Raised); g.FillEllipse(brush, node);
        }
        var status = error is not null ? "Não foi possível abrir: " + error : startupMessage ?? (ready ? "Abrindo seu painel…" : "Preparando o RotaGame…");
        DashboardTheme.Text(g, status, new RectangleF(85, 322, 470, 25), 9, error is null ? DashboardTheme.Muted : DashboardTheme.Accent, center: true);
        g.Restore(state);
    }
    private static double Ease(double value) { var t = Math.Clamp(value, 0, 1); return t * t * (3 - 2 * t); }
    internal static void ExportFrames(string directory)
    {
        Directory.CreateDirectory(directory);
        for (var i = 0; i < 50; i++)
        {
            using var bitmap = new Bitmap(640, 370, PixelFormat.Format32bppArgb); using var g = Graphics.FromImage(bitmap);
            Draw(g, new Rectangle(0, 0, 640, 370), i * .08, i > 7); bitmap.Save(Path.Combine(directory, $"frame-{i:000}.png"), ImageFormat.Png);
        }
    }
}
