using System.Text.Json;

namespace RotaGame.Windows;

public static class WarpConsent
{
    public const string TermsUrl = "https://www.cloudflare.com/application/terms/";
    public const string PrivacyUrl = "https://www.cloudflare.com/application/privacypolicy/";
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame", "warp-consent.json");
    public static bool Granted()
    {
        try { return File.Exists(FilePath) && JsonSerializer.Deserialize<Consent>(File.ReadAllText(FilePath)) is { Version: 1, Accepted: true }; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return false; }
    }
    public static void AcceptByUser()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(new Consent(1, true, DateTimeOffset.UtcNow, TermsUrl, PrivacyUrl)));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
    private sealed record Consent(int Version, bool Accepted, DateTimeOffset At, string Terms, string Privacy);
}
