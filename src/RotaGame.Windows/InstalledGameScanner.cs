using Microsoft.Win32;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RotaGame.Windows;

// Read launcher metadata and check actual files. No drive-wide scan or placeholder games.
public static class InstalledGameScanner
{
    private static readonly (GameProfile Profile, string[] Executables)[] Known =
    [
        (GameLibrary.BuiltIn[0], [@"Discovery\Binaries\Win64\Discovery.exe", @"Discovery\Binaries\Win64\Discovery-Win64-Shipping.exe", "TheFinals.exe"]),
        (GameLibrary.BuiltIn[1], [@"Binaries\Win64\RocketLeague.exe", @"Binaries\Win32\RocketLeague.exe"]),
        (GameLibrary.BuiltIn[2], ["GTA5.exe", "GTA5_Enhanced.exe"])
    ];

    public static IReadOnlyList<GameProfile> Find()
    {
        var steam = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(steam, Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string);
        Add(steam, Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string);
        Add(steam, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        var folders = new List<string>();
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            foreach (var keyPath in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                try
                {
                    using var key = hive.OpenSubKey(keyPath);
                    foreach (var name in key?.GetSubKeyNames() ?? [])
                    {
                        using var app = key?.OpenSubKey(name);
                        var label = app?.GetValue("DisplayName") as string ?? "";
                        if (label.Contains("THE FINALS", StringComparison.OrdinalIgnoreCase) || label.Contains("Rocket League", StringComparison.OrdinalIgnoreCase) || label.Contains("Grand Theft Auto V", StringComparison.OrdinalIgnoreCase))
                            if (app?.GetValue("InstallLocation") is string folder) folders.Add(folder);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        var epic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
        return FindIn(steam, epic, folders);
    }

    public static IReadOnlyList<GameProfile> FindIn(IEnumerable<string> steamRoots, string epicManifests, IEnumerable<string>? installFolders = null)
    {
        var folders = new HashSet<string>(installFolders ?? [], StringComparer.OrdinalIgnoreCase);
        var libraries = new HashSet<string>(steamRoots.Where(Directory.Exists), StringComparer.OrdinalIgnoreCase);
        foreach (var root in libraries.ToArray())
        {
            var vdf = SafeRead(Path.Combine(root, "steamapps", "libraryfolders.vdf"));
            foreach (Match match in Regex.Matches(vdf, "\"path\"\\s*\"((?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
                Add(libraries, match.Groups[1].Value.Replace(@"\\", @"\"));
        }
        foreach (var library in libraries)
            foreach (var manifest in Files(Path.Combine(library, "steamapps"), "appmanifest_*.acf"))
            {
                var match = Regex.Match(SafeRead(manifest), "\"installdir\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (match.Success) try
                {
                    var common = Path.GetFullPath(Path.Combine(library, "steamapps", "common")) + Path.DirectorySeparatorChar;
                    var folder = Path.GetFullPath(Path.Combine(common, match.Groups[1].Value));
                    if (folder.StartsWith(common, StringComparison.OrdinalIgnoreCase)) folders.Add(folder);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
            }
        foreach (var manifest in Files(epicManifests, "*.item"))
            try
            {
                using var json = JsonDocument.Parse(SafeRead(manifest));
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("InstallLocation", out var location) && location.ValueKind == JsonValueKind.String && location.GetString() is { } folder) Add(folders, folder);
            }
            catch (JsonException) { }
        var result = new List<GameProfile>();
        foreach (var folder in folders.Where(Directory.Exists))
            foreach (var (profile, executables) in Known)
                foreach (var relative in executables)
                {
                    var executable = Path.GetFullPath(Path.Combine(folder, relative));
                    if (!File.Exists(executable)) continue;
                    result.Add(GameLibrary.FromExecutable(executable, profile.Name, profile.ProcessNames.Append(Path.GetFileNameWithoutExtension(executable)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
                    break;
                }
        return result.GroupBy(p => p.Id).Select(g => g.First()).ToList();
    }

    private static void Add(HashSet<string> paths, string? path) { if (!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path)) paths.Add(path); }
    private static string SafeRead(string path) { try { return File.Exists(path) ? File.ReadAllText(path) : ""; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ""; } }
    private static string[] Files(string path, string pattern) { try { return Directory.Exists(path) ? Directory.GetFiles(path, pattern) : []; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; } }
}
