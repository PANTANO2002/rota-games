using System.Diagnostics;

namespace RotaGame.Windows;

public static class OfficialWarpSetup
{
    public const string Documentation = "https://developers.cloudflare.com/warp-client/get-started/windows/";
    public static async Task LaunchAsync(IProgress<string>? progress, CancellationToken ct)
    {
        if (WarpClient.FindExecutable() is { } cli)
        {
            var state = await new WarpClient(new CommandRunner()).PrepareRegistrationAsync(ct);
            if (state.Ready && state.Traffic) { progress?.Report("WARP gratuito preparado. Abra o jogo e clique em Ativar para comparar os caminhos."); return; }
            var gui = Path.Combine(Path.GetDirectoryName(cli)!, "Cloudflare WARP.exe");
            if (!File.Exists(gui)) throw new FileNotFoundException("Abra o aplicativo oficial Cloudflare WARP pelo menu Iniciar para concluir a preparação.");
            Process.Start(new ProcessStartInfo(gui) { UseShellExecute = true }); return;
        }
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame", "vendor-setup"); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "Cloudflare-WARP.msi");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(4) };
        progress?.Report("Baixando o componente oficial da Cloudflare…");
        using var response = await http.GetAsync("https://downloads.cloudflareclient.com/v1/download/windows/ga", HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is <= 0 or > 268435456) throw new InvalidDataException("Instalador da Cloudflare com tamanho inesperado.");
        await using (var input = await response.Content.ReadAsStreamAsync(ct))
        await using (var output = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
        {
            var buffer = new byte[65536]; long count = 0; int read;
            while ((read = await input.ReadAsync(buffer, ct)) != 0)
            {
                count += read; if (count > 268435456) throw new InvalidDataException("Instalador excedeu o limite.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        progress?.Report("Verificando a assinatura oficial da Cloudflare…");
        // Static script, fixed Windows binary and environment argument: no path
        // is interpolated into shell code. Neither EULA nor policy is bypassed.
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", "$s=Get-AuthenticodeSignature -LiteralPath $env:ROTAGAME_VENDOR_FILE; if($s.Status -eq 'Valid' -and $s.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName,$false) -eq 'Cloudflare, Inc.') {exit 0}; exit 1" }) start.ArgumentList.Add(arg);
        start.Environment["ROTAGAME_VENDOR_FILE"] = file;
        using var check = Process.Start(start) ?? throw new IOException("Não foi possível verificar o componente.");
        try { await check.WaitForExitAsync(ct); } catch (OperationCanceledException) { try { check.Kill(true); } catch (InvalidOperationException) { } throw; }
        if (check.ExitCode != 0) throw new InvalidDataException("A assinatura da Cloudflare não foi confirmada. O instalador não foi aberto.");
        ct.ThrowIfCancellationRequested();
        progress?.Report("Conclua o instalador oficial. Ao terminar, o RotaGame tentará preparar a conexão automaticamente. Não é preciso contratar WARP+.");
        using var installer = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        if (installer is not null) await installer.WaitForExitAsync(ct);
        // Some MSI launchers detach. Wait briefly for the installed CLI; never
        // terminate the vendor installer if the user closes RotaGame.
        for (var i = 0; i < 15 && WarpClient.FindExecutable() is null; i++) await Task.Delay(1000,ct);
        if (WarpClient.FindExecutable() is null) { progress?.Report("O componente ainda não foi instalado. Conclua a instalação e clique em Preparar rota gratuita novamente."); return; }
        await Task.Delay(2500,ct);
        await LaunchAsync(progress,ct);
    }
}
