using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace RotaGame.App;

internal static class UpdatesSmoke
{
    internal static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>();
        void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); log.Add("PASS: " + description); }
        var source = new SmokeUpdates(); var prepared = false;
        var flow = new UpdateWorkflow(source, () => { prepared = true; return Task.FromResult(true); });
        Check(!await flow.CheckAsync(false, default) && flow.State == UpdateState.Available && source.Downloads == 0, "App aberto avisa sem baixar ou reiniciar.");
        flow.Defer(); await flow.CheckAsync(false, default);
        Check(flow.State == UpdateState.Deferred && source.Downloads == 0, "Depois suprime o aviso repetido da mesma versão nesta sessão.");
        source.Release = new("0.8.1", "Teste"); await flow.CheckAsync(false, default);
        Check(flow.State == UpdateState.Available, "Uma versão posterior volta a avisar após adiamento.");
        source.BeforeRestart = () => { if (!prepared) throw new InvalidOperationException("Restart antes de preparar conexão."); };
        Check(await flow.InstallAsync(default) && source.Downloads == 1 && source.Restarts == 1, "Atualizar agora prepara a conexão antes de agendar a reinicialização.");
        source = new(); flow = new(source, () => Task.FromResult(true));
        Check(await flow.CheckAsync(true, default) && source.Downloads == 1 && source.Restarts == 1, "Ao abrir, a nova versão é baixada e aplicada automaticamente.");
        source = new() { FailDownload = true }; flow = new(source, () => Task.FromResult(true));
        Check(!await flow.CheckAsync(true, default) && flow.State == UpdateState.Error && source.Restarts == 0, "Falha no download preserva a versão e não reinicia.");
        source = new(); flow = new(source, () => Task.FromResult(false));
        Check(!await flow.CheckAsync(true, default) && flow.State == UpdateState.Error && source.Restarts == 0, "Falha ao restaurar a conexão impede reinicialização.");
        source = new() { FailCheck = true }; flow = new(source, () => Task.FromResult(true)); await flow.CheckAsync(false, default);
        Check(flow.State == UpdateState.Error, "Canal ausente ou offline nunca é apresentado como versão mais recente.");
        source = new() { Supported = false }; flow = new(source, () => Task.FromResult(true)); await flow.CheckAsync(true, default);
        Check(flow.State == UpdateState.Unsupported && source.Downloads == 0, "Executável solto informa que é necessário o pacote completo.");
        source = new() { Release = null }; flow = new(source, () => Task.FromResult(true)); await flow.CheckAsync(true, default);
        Check(flow.State == UpdateState.Current && source.Restarts == 0, "Sem versão posterior, continua sem reiniciar.");
        using var cancelled = new CancellationTokenSource();
        source = new() { OnDownload = cancelled.Cancel }; flow = new(source, () => Task.FromResult(true));
        await flow.CheckAsync(true, cancelled.Token);
        Check(flow.State == UpdateState.Deferred && source.Restarts == 0, "Download cancelado não agenda aplicação.");
        File.WriteAllLines(Path.Combine(directory, "updates-workflow.txt"), log);
    }

    internal static async Task PackageAsync(string feed, string directory)
    {
        Directory.CreateDirectory(directory);
        AppUpdates Make(string folder, string cache) => new(new UpdateManager(new SimpleFileSource(new DirectoryInfo(folder)), new UpdateOptions { ExplicitChannel = "win" }, new TestVelopackLocator("RotaGame", "0.6.0", cache)));
        var source = Make(feed, Path.Combine(directory, "cache-valid"));
        var release = await source.CheckAsync(default) ?? throw new InvalidOperationException("Feed sem atualização.");
        await source.DownloadAsync(release, _ => { }, default);
        var corrupt = Path.Combine(directory, "corrupt-feed"); Directory.CreateDirectory(corrupt);
        foreach (var path in Directory.GetFiles(feed, "*.json")) File.Copy(path, Path.Combine(corrupt, Path.GetFileName(path)), true);
        foreach (var path in Directory.GetFiles(feed, "*.nupkg"))
        {
            var target = Path.Combine(corrupt, Path.GetFileName(path)); File.Copy(path, target, true);
            using var stream = new FileStream(target, FileMode.Open, FileAccess.ReadWrite); stream.Position = Math.Min(100, stream.Length - 1); var value = stream.ReadByte(); stream.Position--; stream.WriteByte((byte)(value ^ 0xff));
        }
        var bad = Make(corrupt, Path.Combine(directory, "cache-corrupt"));
        var badRelease = await bad.CheckAsync(default) ?? throw new InvalidOperationException("Feed corrompido não retornou metadados.");
        try { await bad.DownloadAsync(badRelease, _ => { }, default); }
        catch (Velopack.Exceptions.ChecksumFailedException)
        {
            File.WriteAllText(Path.Combine(directory, "updates-package.txt"), "PASS: pacote " + release.Version + " descoberto e baixado com Velopack; cópia adulterada rejeitada por checksum. Nenhuma instalação ou rota do usuário alterada.\n"); return;
        }
        throw new InvalidOperationException("Pacote adulterado não foi rejeitado por checksum.");
    }
}

internal sealed class SmokeUpdates : IAppUpdates
{
    public bool Supported { get; set; } = true;
    public AppRelease? Release { get; set; } = new("0.8.0", "Dados controlados para teste do atualizador.");
    public bool FailCheck { get; set; }
    public bool FailDownload { get; set; }
    public Action? OnDownload { get; set; }
    public int Downloads { get; private set; }
    public int Restarts { get; private set; }
    public Action? BeforeRestart { get; set; }
    public Task<AppRelease?> CheckAsync(CancellationToken ct) => FailCheck ? Task.FromException<AppRelease?>(new IOException("Offline (teste)")) : Task.FromResult(Release);
    public Task DownloadAsync(AppRelease release, Action<int> progress, CancellationToken ct)
    {
        Downloads++; OnDownload?.Invoke(); ct.ThrowIfCancellationRequested(); progress(65);
        if (FailDownload) throw new IOException("Falha controlada");
        return Task.CompletedTask;
    }
    public void ScheduleRestart(AppRelease release) { BeforeRestart?.Invoke(); Restarts++; }
}

internal sealed partial class SimpleForm
{
    internal async Task RunUpdateSmokeAsync(string directory)
    {
        await UpdatesSmoke.RunAsync(directory); ConfigureUpdates(new SmokeUpdates()); await CheckUpdatesAsync();
        if (!_updateBanner.Visible || _shell!.RowStyles[1].Height <= 0) throw new InvalidOperationException("Aviso não visível.");
        ClientSize = new Size(1340, 860); ShowUpdates(); await Task.Delay(100);
        using (var image = new Bitmap(_updateWindow!.Width, _updateWindow.Height)) { _updateWindow.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(directory, "updates-dialog-test.png")); }
        _updateWindow.Close(); await Task.Delay(40);
        using (var image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(directory, "updates-banner-test.png")); }
        _updateLater!.PerformClick();
        if (_updateBanner.Visible || _updates!.State != UpdateState.Deferred) throw new InvalidOperationException("Depois não recolheu aviso.");
        File.AppendAllText(Path.Combine(directory, "updates-workflow.txt"), "PASS: aviso e diálogo com Atualizar agora / Depois; Depois recolhe o aviso.\n");
    }
}
