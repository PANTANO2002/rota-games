using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace RotaGame.App;

internal static class BrandAssets
{
    public static Icon LoadIcon()
    {
        using var stream = typeof(BrandAssets).Assembly.GetManifestResourceStream("RotaGame.Assets.RotaGame.ico")
            ?? throw new InvalidOperationException("Ícone do RotaGame ausente.");
        using var icon = new Icon(stream, new Size(48, 48)); return (Icon)icon.Clone();
    }
    public static void Draw(Graphics g, RectangleF bounds, float reveal = 1, float pulse = 0)
    {
        var state = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(bounds.X, bounds.Y); g.ScaleTransform(bounds.Width / 100, bounds.Height / 100);
        using var tile = new GraphicsPath(); tile.AddArc(5, 5, 42, 42, 180, 90); tile.AddArc(53, 5, 42, 42, 270, 90); tile.AddArc(53, 53, 42, 42, 0, 90); tile.AddArc(5, 53, 42, 42, 90, 90); tile.CloseFigure();
        using var ground = new LinearGradientBrush(new RectangleF(5, 5, 90, 90), Color.FromArgb(37, 46, 62), Color.FromArgb(12, 16, 25), 55f);
        using var edge = new Pen(Color.FromArgb(69, 78, 99), 1.3f); g.FillPath(ground, tile); g.DrawPath(edge, tile);
        using var route = new GraphicsPath(); route.StartFigure(); route.AddLine(30, 74, 30, 28); route.AddLine(30, 28, 52, 28);
        route.AddBezier(52, 28, 66, 28, 73, 34, 73, 44); route.AddBezier(73, 44, 73, 54, 65, 59, 52, 59); route.AddLine(52, 59, 30, 59);
        route.StartFigure(); route.AddLine(51, 59, 73, 75);
        using var visibleRoute = PartialPath(route, reveal);
        for (var i = 5; i >= 1; i--) { using var glow = new Pen(Color.FromArgb(6 + (5 - i) * 3, 255, 62, 107), 9 + i * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; if (visibleRoute.PointCount > 1) g.DrawPath(glow, visibleRoute); }
        using var gradient = new LinearGradientBrush(new RectangleF(25, 23, 53, 56), Color.FromArgb(255, 154, 171), Color.FromArgb(255, 46, 94), 55f);
        using var stroke = new Pen(gradient, 8.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; if (visibleRoute.PointCount > 1) g.DrawPath(stroke, visibleRoute);
        if (pulse > 0) { var spread = 10 + 7 * pulse; using var ring = new Pen(Color.FromArgb((int)(80 * (1 - pulse)), 39, 221, 172), 1.5f); g.DrawEllipse(ring, 30 - spread, 74 - spread, spread * 2, spread * 2); }
        using var nodeGlow = new SolidBrush(Color.FromArgb(35, 39, 221, 172)); g.FillEllipse(nodeGlow, 19, 63, 22, 22);
        using var node = new LinearGradientBrush(new RectangleF(24, 68, 12, 12), Color.FromArgb(159, 255, 218), Color.FromArgb(20, 204, 157), 90f); g.FillEllipse(node, 24, 68, 12, 12);
        g.Restore(state);
    }
    private static GraphicsPath PartialPath(GraphicsPath source, float progress)
    {
        if (progress >= 1) return (GraphicsPath)source.Clone();
        using var flat = (GraphicsPath)source.Clone(); flat.Flatten(null, .15f);
        var points = flat.PathPoints; var types = flat.PathTypes; var total = 0f;
        for (var i = 1; i < points.Length; i++) if ((types[i] & 7) != 0) total += Distance(points[i - 1], points[i]);
        var remaining = total * Math.Clamp(progress, 0, 1); var result = new GraphicsPath();
        for (var i = 1; i < points.Length && remaining > 0; i++)
        {
            if ((types[i] & 7) == 0) { result.StartFigure(); continue; }
            var length = Distance(points[i - 1], points[i]); var fraction = Math.Min(1, remaining / Math.Max(.001f, length));
            result.AddLine(points[i - 1], new PointF(points[i - 1].X + (points[i].X - points[i - 1].X) * fraction, points[i - 1].Y + (points[i].Y - points[i - 1].Y) * fraction)); remaining -= length;
        }
        return result;
        static float Distance(PointF a, PointF b) => MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
    }
    private const string SvgMark = """
<defs><linearGradient id="tile" x2="1" y2="1"><stop stop-color="#252e3e"/><stop offset="1" stop-color="#0c1019"/></linearGradient><linearGradient id="route" x2="1" y2="1"><stop stop-color="#ff9aab"/><stop offset="1" stop-color="#ff2e5e"/></linearGradient><linearGradient id="node" x2="0" y2="1"><stop stop-color="#9fffda"/><stop offset="1" stop-color="#14cc9d"/></linearGradient><filter id="glow" x="-35%" y="-35%" width="170%" height="170%"><feGaussianBlur stdDeviation="3"/></filter></defs>
<rect x="5" y="5" width="90" height="90" rx="21" fill="url(#tile)" stroke="#454e63" stroke-width="1.3"/>
<path id="r" d="M30 74V28H52C66 28 73 34 73 44S65 59 52 59H30M51 59L73 75" fill="none" stroke="url(#route)" stroke-width="8.5" stroke-linecap="round" stroke-linejoin="round"/>
<use href="#r" opacity=".24" filter="url(#glow)"/><circle cx="30" cy="74" r="10" fill="#27ddac" opacity=".13"/><circle cx="30" cy="74" r="6" fill="url(#node)"/>
""";
    public static void Export(string directory)
    {
        Directory.CreateDirectory(directory);
        var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 }; var frames = new List<byte[]>();
        foreach (var size in sizes)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb); using var g = Graphics.FromImage(bitmap); g.Clear(Color.Transparent); Draw(g, new RectangleF(0, 0, size, size));
            using var data = new MemoryStream(); bitmap.Save(data, ImageFormat.Png); frames.Add(data.ToArray());
            if (size == 256) bitmap.Save(Path.Combine(directory, "RotaGame-icon.png"), ImageFormat.Png);
        }
        using (var file = File.Create(Path.Combine(directory, "RotaGame.ico")))
        using (var writer = new BinaryWriter(file))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            var offset = 6 + sizes.Length * 16;
            for (var i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
            }
            foreach (var frame in frames) writer.Write(frame);
        }
        File.WriteAllText(Path.Combine(directory, "RotaGame-mark.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">" + SvgMark + "</svg>");
        File.WriteAllText(Path.Combine(directory, "RotaGame-logo.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 400 100\">" + SvgMark + SvgWordmark("Rota", 113, "#eff3f9") + SvgWordmark("Game", 196, "#ff4759") + "</svg>");
        using var logo = new Bitmap(800, 200, PixelFormat.Format32bppArgb); using var graphics = Graphics.FromImage(logo); graphics.Clear(Color.FromArgb(9, 12, 17));
        Draw(graphics, new RectangleF(0, 0, 200, 200)); using var font = FontAssets.Title(53); using var white = new SolidBrush(Color.FromArgb(239, 243, 249)); using var red = new SolidBrush(Color.FromArgb(255, 71, 89));
        graphics.DrawString("Rota", font, white, 219, 49); graphics.DrawString("Game", font, red, 219 + graphics.MeasureString("Rota", font).Width - 13, 49); logo.Save(Path.Combine(directory, "RotaGame-logo.png"), ImageFormat.Png);
    }

    private static string SvgWordmark(string text, float x, string color)
    {
        using var path = new GraphicsPath(); path.AddString(text, FontAssets.DisplayFamily, (int)FontStyle.Bold, 47, new PointF(x, 22), StringFormat.GenericTypographic);
        var points = path.PathPoints; var types = path.PathTypes; var data = new System.Text.StringBuilder();
        static string Number(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        string Point(int i) => Number(points[i].X) + " " + Number(points[i].Y);
        for (var i = 0; i < points.Length; i++)
        {
            var kind = types[i] & 7;
            if (kind == 0) data.Append("M" + Point(i));
            else if (kind == 1) data.Append("L" + Point(i));
            else if (kind == 3 && i + 2 < points.Length) { data.Append("C" + Point(i) + " " + Point(i + 1) + " " + Point(i + 2)); i += 2; }
            if ((types[i] & 128) != 0) data.Append('Z');
        }
        return "<path fill=\"" + color + "\" d=\"" + data + "\"/>";
    }
}

internal sealed class DashboardBrand : Control
{
    public DashboardBrand() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(BackColor); var size = Math.Min(Width, Height); BrandAssets.Draw(e.Graphics, new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size)); }
}
