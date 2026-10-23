using System.Diagnostics;
using System.Text.RegularExpressions;
using RotaGame.Windows;

namespace RotaGame.App;

internal static class SetupForm
{
    internal static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host is not ("login.tailscale.com" or "console.tailscale.com"))
            throw new InvalidOperationException("Endereço de autorização inesperado.");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}

internal interface IConnectionSession
{
    bool Installed { get; }
    Task<TailStatus?> ReadAsync(CancellationToken ct);
    Task InstallAsync(IProgress<string> progress, CancellationToken ct);
    Task LoginAsync(IProgress<string> progress, IProgress<string> openUrl, CancellationToken ct, bool chooseNetwork = false);
}

internal sealed class ConnectionSession : IConnectionSession
{
    public bool Installed => TailscaleClient.FindExecutable() is not null;
    public async Task<TailStatus?> ReadAsync(CancellationToken ct) => Installed ? await new TailscaleClient(new CommandRunner()).StatusAsync(ct) : null;
    public async Task InstallAsync(IProgress<string> progress, CancellationToken ct)
    {
        if (Installed) return;
        var winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        if (!File.Exists(winget)) throw new InvalidOperationException("Instalador automático indisponível. Instale Tailscale de tailscale.com/download/windows e retorne a esta aba.");
        progress.Report("Instalando o componente de conexão. Aguarde…");
        var result = await new CommandRunner().RunAsync(winget, ["install", "--id", "Tailscale.Tailscale", "--exact", "--source", "winget", "--silent", "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity"], TimeSpan.FromMinutes(6), ct);
        if (result.ExitCode != 0 || !Installed) throw new InvalidOperationException("A instalação não terminou. Tente novamente ou use o instalador oficial do Tailscale.");
        progress.Report("Componente instalado.");
    }
    public async Task LoginAsync(IProgress<string> progress, IProgress<string> openUrl, CancellationToken ct, bool chooseNetwork = false)
    {
        await InstallAsync(progress, ct);
        if (!chooseNetwork && (await ReadAsync(ct))?.State == "Running") { progress.Report("Conta já conectada neste PC."); return; }
        progress.Report("Conclua a entrada oficial no navegador. O estado será atualizado aqui.");
        var start = new ProcessStartInfo(TailscaleClient.FindExecutable()!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(chooseNetwork ? "login" : "up"); using var process = new Process { StartInfo = start }; var opened = 0;
        void ReadLine(object _, DataReceivedEventArgs e)
        {
            if (e.Data is null) return;
            var match = Regex.Match(e.Data, @"https://login\.tailscale\.com/[a-zA-Z0-9/_?=.&%-]+");
            if (match.Success && Interlocked.Exchange(ref opened, 1) == 0) openUrl.Report(match.Value);
        }
        process.OutputDataReceived += ReadLine; process.ErrorDataReceived += ReadLine;
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(4));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); ct.ThrowIfCancellationRequested(); throw new InvalidOperationException("A autorização ainda não terminou. Você pode tentar entrar novamente."); }
        if (process.ExitCode != 0 || (await ReadAsync(ct))?.State != "Running") throw new InvalidOperationException("A conta não ficou conectada. Conclua a autorização no navegador e tente novamente.");
        progress.Report("Conta conectada. Continue pelo RotaGame.");
    }
}

