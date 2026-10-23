using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace RotaGame.App;

internal static class FontAssets
{
    // GDI (native controls) and GDI+ (drawn artwork) both need the embedded fonts.
    // Extract to a versioned cache and register only for this process. AddFontFile
    // also supports the Indic tables in Rajdhani that GDI+ AddMemoryFont drops.
    private static readonly PrivateFontCollection Collection = new();
    internal static readonly FontFamily BodyFamily;
    internal static readonly FontFamily DisplayFamily;

    static FontAssets()
    {
        foreach (var name in new[] { "Barlow-Regular", "Barlow-Bold", "Rajdhani-Regular", "Rajdhani-Bold" })
        {
            using var resource = typeof(FontAssets).Assembly.GetManifestResourceStream("RotaGame.Assets.Fonts." + name + ".ttf")
                ?? throw new InvalidOperationException("Fonte incluída não encontrada: " + name);
            using var buffer = new MemoryStream(); resource.CopyTo(buffer); var bytes = buffer.ToArray();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame", "Fonts"); Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name + "-" + hash[..12] + ".ttf");
            if (!File.Exists(path))
            {
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, false); }
                catch (IOException) when (File.Exists(path)) { }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            Collection.AddFontFile(path);
            if (AddFontResourceEx(path, 0x10, IntPtr.Zero) == 0)
                throw new InvalidOperationException("Não foi possível carregar a fonte do RotaGame.");
        }
        var families = Collection.Families;
        BodyFamily = families.FirstOrDefault(f => f.GetName(1033) == "Barlow") ?? throw new InvalidOperationException("Barlow: " + string.Join(", ", families.Select(f => f.Name)));
        DisplayFamily = families.FirstOrDefault(f => f.GetName(1033) == "Rajdhani") ?? throw new InvalidOperationException("Rajdhani: " + string.Join(", ", families.Select(f => f.Name)));
    }

    internal static Font Ui(float size, FontStyle style = FontStyle.Regular) => new(BodyFamily, size, style);
    internal static Font Title(float size, FontStyle style = FontStyle.Bold) => new(DisplayFamily, size, style);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);
}
