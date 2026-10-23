using System.Diagnostics;
using RotaGame.Windows;

internal static class GameLibraryTests
{
    public static List<(string Name, Func<Task> Run)> All()
    {
        var tests = new List<(string, Func<Task>)>();
        void Test(string name, Action action) => tests.Add((name, () => { action(); return Task.CompletedTask; }));
        void Check(bool value) { if (!value) throw new Exception("Game library assertion failed"); }
        (GameLibrary Library, string Root) Store()
        {
            var root = Path.Combine(Path.GetTempPath(), "RotaGame-library-" + Guid.NewGuid().ToString("N"));
            return (new GameLibrary(Path.Combine(root, "games.json"), () => []), root);
        }
        Test("LIBRARY: no games appear without installed executables", () =>
        {
            var (library, _) = Store(); var profiles = library.Load();
            Check(profiles.Count == 0);
        });
        Test("LIBRARY: executable registration survives a fresh load", () =>
        {
            var (library, root) = Store();
            try { var profile = library.Add(Environment.ProcessPath!, "My game"); Check(library.Load().Single(p => p.Id == profile.Id).ExecutablePath == Environment.ProcessPath && profile.Matches(Process.GetCurrentProcess().ProcessName)); }
            finally { Directory.Delete(root, true); }
        });
        Test("LIBRARY: adding the same executable updates instead of duplicating", () =>
        {
            var (library, root) = Store();
            try { var a = library.Add(Environment.ProcessPath!, "First"); var b = library.Add(Environment.ProcessPath!, "Renamed"); Check(a.Id == b.Id && library.Load().Count == 1 && library.Load().Single(p => p.Id == a.Id).Name == "Renamed"); }
            finally { Directory.Delete(root, true); }
        });
        Test("LIBRARY: removing the only game returns to an empty library", () =>
        {
            var (library, root) = Store();
            try { var added = library.Add(Environment.ProcessPath!); library.Remove(added.Id); Check(library.Load().Count == 0); }
            finally { Directory.Delete(root, true); }
        });
        Test("LIBRARY: a removed executable is hidden until it exists again", () =>
        {
            var (library, root) = Store(); Directory.CreateDirectory(root); var executable = Path.Combine(root, "game.exe"); File.WriteAllText(executable, "fixture");
            try { library.Add(executable); File.Delete(executable); Check(library.Load().Count == 0); File.WriteAllText(executable, "fixture"); Check(library.Load().Count == 1); }
            finally { Directory.Delete(root, true); }
        });
        Test("LIBRARY: a launcher manifest alone cannot create a game card", () =>
        {
            var (_, root) = Store(); var apps = Path.Combine(root, "steamapps"); Directory.CreateDirectory(apps);
            try { File.WriteAllText(Path.Combine(apps, "appmanifest_1.acf"), "\"AppState\" { \"installdir\" \"Rocket League\" }"); Check(InstalledGameScanner.FindIn([root], "").Count == 0); }
            finally { Directory.Delete(root, true); }
        });
        Test("LIBRARY: Steam libraries discover real executable paths and aliases", () =>
        {
            var (_, root) = Store(); var steam = Path.Combine(root, "Steam"); var library = Path.Combine(root, "Second library");
            Directory.CreateDirectory(Path.Combine(steam, "steamapps")); Directory.CreateDirectory(Path.Combine(library, "steamapps"));
            var executable = Path.Combine(library, @"steamapps\common\Finals\Discovery\Binaries\Win64\Discovery.exe"); Directory.CreateDirectory(Path.GetDirectoryName(executable)!); File.WriteAllText(executable, "fixture");
            try
            {
                File.WriteAllText(Path.Combine(steam, @"steamapps\libraryfolders.vdf"), "\"libraryfolders\" { \"1\" { \"path\" \"" + library.Replace(@"\", @"\\") + "\" } }");
                File.WriteAllText(Path.Combine(library, @"steamapps\appmanifest_1.acf"), "\"AppState\" { \"installdir\" \"Finals\" }");
                var found = InstalledGameScanner.FindIn([steam], ""); Check(found.Count == 1 && found[0].Name == "The Finals" && found[0].ExecutablePath == executable && found[0].Matches("Discovery"));
            }
            finally { Directory.Delete(root, true); }
        });
        Test("LIBRARY: Epic discovery requires files and remembers manual removal", () =>
        {
            var (_, root) = Store(); var manifests = Path.Combine(root, "Manifests"); var game = Path.Combine(root, "RocketLeague"); Directory.CreateDirectory(manifests);
            var executable = Path.Combine(game, @"Binaries\Win64\RocketLeague.exe"); Directory.CreateDirectory(Path.GetDirectoryName(executable)!); File.WriteAllText(executable, "fixture");
            try
            {
                File.WriteAllText(Path.Combine(manifests, "rocket.item"), System.Text.Json.JsonSerializer.Serialize(new { InstallLocation = game }));
                var library = new GameLibrary(Path.Combine(root, "games.json"), () => InstalledGameScanner.FindIn([], manifests));
                var found = library.Load().Single(); Check(found.Name == "Rocket League"); library.Remove(found.Id); Check(library.Load().Count == 0);
                library.Add(executable); Check(library.Load().Count == 1); File.Delete(executable); Check(library.Load().Count == 0);
            }
            finally { Directory.Delete(root, true); }
        });
        Test("LIBRARY: rejects missing files and non-executables", () =>
        {
            var (library, root) = Store(); Directory.CreateDirectory(root); var text = Path.Combine(root, "wrong.txt"); File.WriteAllText(text, "fixture");
            try
            {
                foreach (var file in new[] { text, Path.Combine(root, "missing.exe") })
                { try { library.Add(file); throw new Exception("Accepted invalid executable"); } catch (ArgumentException) { } }
            }
            finally { Directory.Delete(root, true); }
        });
        tests.Add(("REAL LIBRARY: recognizes a registered process start and exit", async () =>
        {
            var (library, root) = Store();
            try
            {
                var profile = library.Add(Environment.ProcessPath!, "Controlled process");
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true }; start.ArgumentList.Add("--game-child");
                using var child = Process.Start(start) ?? throw new Exception("Cannot start controlled process");
                Check(GameLibrary.Running([profile])[profile.Id].Contains(child.Id));
                await child.WaitForExitAsync();
                Check(!GameLibrary.Running([profile])[profile.Id].Contains(child.Id));
            }
            finally { Directory.Delete(root, true); }
        }));
        return tests;
    }
}
