using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RotaGame.Windows;

public sealed record GameProfile(string Id, string Name, string[] ProcessNames, string? ExecutablePath = null)
{
    public bool Matches(string processName) => ProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase);
}

public sealed class GameLibrary(string path, Func<IReadOnlyList<GameProfile>>? discoverInstalled = null)
{
    public static readonly GameProfile[] BuiltIn =
    [
        new("the-finals", "The Finals", ["Discovery", "TheFinals"]),
        new("rocket-league", "Rocket League", ["RocketLeague"]),
        new("gta-online", "GTA Online", ["GTA5", "GTA5_Enhanced"])
    ];

    public IReadOnlyList<GameProfile> Load()
    {
        var hidden = ReadHidden();
        var installed = discoverInstalled?.Invoke() ?? InstalledGameScanner.Find();
        return ReadSaved().Concat(installed).Where(p => !hidden.Contains(p.Id) && !string.IsNullOrWhiteSpace(p.Id) && !string.IsNullOrWhiteSpace(p.Name) &&
                p.ProcessNames is { Length: > 0 } && p.ExecutablePath is { } exe && File.Exists(exe))
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    private List<GameProfile> ReadSaved() => File.Exists(path) ? JsonSerializer.Deserialize<List<GameProfile>>(File.ReadAllText(path))
        ?? throw new InvalidDataException("A biblioteca de jogos está vazia ou inválida.") : [];
    private HashSet<string> ReadHidden() => File.Exists(path + ".hidden.json")
        ? new(JsonSerializer.Deserialize<string[]>(File.ReadAllText(path + ".hidden.json")) ?? [], StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);

    internal static GameProfile FromExecutable(string full, string label, string[]? aliases = null)
    {
        var id = "custom-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full.ToUpperInvariant())))[..16];
        return new(id, label, aliases ?? [Path.GetFileNameWithoutExtension(full)], full);
    }

    public GameProfile Add(string executablePath, string? name = null)
    {
        var full = Path.GetFullPath(executablePath);
        if (!File.Exists(full) || !Path.GetExtension(full).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selecione o arquivo .exe do jogo instalado.");
        var process = Path.GetFileNameWithoutExtension(full);
        var label = string.IsNullOrWhiteSpace(name) ? process : name.Trim();
        if (label.Length > 80) label = label[..80];
        var profile = FromExecutable(full, label);
        var custom = ReadSaved().Where(p => p.Id != profile.Id).Append(profile).ToList();
        Save(custom);
        var hidden = ReadHidden(); if (hidden.Remove(profile.Id)) SaveHidden(hidden);
        return profile;
    }

    public void Remove(string id)
    {
        Save(ReadSaved().Where(p => p.Id != id).ToList());
        var hidden = ReadHidden(); hidden.Add(id); SaveHidden(hidden);
    }

    private void SaveHidden(HashSet<string> ids) => File.WriteAllText(path + ".hidden.json", JsonSerializer.Serialize(ids));

    private void Save(List<GameProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        File.Move(path + ".tmp", path, true);
    }

    public static Dictionary<string, int[]> Running(IEnumerable<GameProfile> profiles)
    {
        var entries = new List<(int Pid, string Name)>();
        foreach (var process in Process.GetProcesses())
            using (process)
                try { entries.Add((process.Id, process.ProcessName)); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
        return profiles.ToDictionary(p => p.Id, p => entries.Where(e => p.Matches(e.Name)).Select(e => e.Pid).ToArray());
    }
}

public sealed class GameNotReadyException(string message) : InvalidOperationException(message);
