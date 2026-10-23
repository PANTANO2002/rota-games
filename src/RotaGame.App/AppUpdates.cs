using Velopack;
using Velopack.Sources;

namespace RotaGame.App;

internal sealed record AppRelease(string Version, string Notes);
internal interface IAppUpdates
{
    bool Supported { get; }
    Task<AppRelease?> CheckAsync(CancellationToken ct);
    Task DownloadAsync(AppRelease release, Action<int> progress, CancellationToken ct);
    void ScheduleRestart(AppRelease release);
}

internal sealed class AppUpdates : IAppUpdates
{
    // A missing/inaccessible release feed is never reported as "up to date".
    internal const string RepositoryUrl = "https://github.com/PANTANO2002/rota-games";
    private readonly UpdateManager _manager;
    private UpdateInfo? _pending;
    private Task<UpdateInfo?>? _checking;
    private readonly string[] _restartArgs;
    public bool Supported => _manager.IsInstalled;
    internal AppUpdates(UpdateManager? manager = null, string[]? restartArgs = null)
    {
        _manager = manager ?? new UpdateManager(new GithubSource(RepositoryUrl, null, false), new UpdateOptions { AllowVersionDowngrade = false, ExplicitChannel = "win" });
        _restartArgs = restartArgs ?? ["--updated"];
    }
    public async Task<AppRelease?> CheckAsync(CancellationToken ct)
    {
        if (!Supported) throw new InvalidOperationException("Use o instalador ou o pacote portátil completo para habilitar atualizações.");
        if (_checking is null || _checking.IsCompleted) _checking = _manager.CheckForUpdatesAsync();
        var info = await _checking.WaitAsync(TimeSpan.FromSeconds(10), ct);
        if (info is null) return null;
        if (_manager.CurrentVersion is null || info.TargetFullRelease.Version <= _manager.CurrentVersion) return null;
        if (info.TargetFullRelease.Size is <= 0 or > 536870912) throw new InvalidOperationException("Pacote de atualização com tamanho inesperado.");
        _pending = info;
        return new(info.TargetFullRelease.Version.ToString(), info.TargetFullRelease.NotesMarkdown ?? "Melhorias e correções do RotaGame.");
    }
    public Task DownloadAsync(AppRelease release, Action<int> progress, CancellationToken ct) => _manager.DownloadUpdatesAsync(Pending(release), progress, ct);
    public void ScheduleRestart(AppRelease release) => _manager.WaitExitThenApplyUpdates(Pending(release).TargetFullRelease, silent: true, restart: true, restartArgs: _restartArgs);
    private UpdateInfo Pending(AppRelease release) => _pending is not null && _pending.TargetFullRelease.Version.ToString() == release.Version ? _pending : throw new InvalidOperationException("Verifique a nova versão novamente antes de atualizar.");
}

internal enum UpdateState { Idle, Checking, Current, Available, Deferred, Downloading, Preparing, Restarting, Error, Unsupported }

internal sealed class UpdateWorkflow(IAppUpdates source, Func<Task<bool>> prepareRestart)
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private string? _deferred;
    internal AppRelease? Release { get; private set; }
    internal UpdateState State { get; private set; }
    internal string Message { get; private set; } = "Verificação automática ao abrir e a cada 15 minutos.";
    internal bool Busy => State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Preparing or UpdateState.Restarting;
    internal event EventHandler? Changed;

    internal async Task<bool> CheckAsync(bool startup, CancellationToken ct)
    {
        if (Busy) return false;
        if (!source.Supported) { Set(UpdateState.Unsupported, "Use o instalador ou o pacote portátil completo para receber atualizações automáticas."); return false; }
        Set(UpdateState.Checking, "Verificando atualizações…");
        try
        {
            var found = await source.CheckAsync(ct); ct.ThrowIfCancellationRequested();
            if (found is null) { Release = null; Set(UpdateState.Current, "Você está usando a versão mais recente do canal publicado."); return false; }
            Release = found;
            Set(!startup && _deferred == found.Version ? UpdateState.Deferred : UpdateState.Available, "Nova versão " + found.Version + " disponível.");
            return startup && await InstallAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { Set(UpdateState.Idle, "Verificação cancelada."); return false; }
        catch (Exception) { Set(UpdateState.Error, "Não foi possível verificar agora. O canal pode ainda não estar publicado ou a conexão está indisponível. Você pode continuar usando esta versão."); return false; }
    }
    internal void Defer()
    {
        if (Release is null || Busy) return; _deferred = Release.Version;
        Set(UpdateState.Deferred, "Atualização adiada. Você pode retomá-la aqui; ao abrir o app novamente, a verificação será automática.");
    }
    internal async Task<bool> InstallAsync(CancellationToken ct)
    {
        if (Release is not { } release || State is UpdateState.Downloading or UpdateState.Preparing or UpdateState.Restarting) return false;
        Set(UpdateState.Downloading, "Baixando a versão " + release.Version + "…");
        try
        {
            await source.DownloadAsync(release, percent => NotifyProgress(release, percent), ct); ct.ThrowIfCancellationRequested();
            Set(UpdateState.Preparing, "Preparando a conexão para reiniciar o RotaGame…");
            if (!await prepareRestart()) { Set(UpdateState.Error, "A atualização foi baixada, mas a conexão não pôde ser encerrada. Conclua a preparação da conta ou tente Desativar antes de atualizar."); return false; }
            ct.ThrowIfCancellationRequested(); source.ScheduleRestart(release); Set(UpdateState.Restarting, "Atualizando e reiniciando o RotaGame…"); return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { Set(UpdateState.Deferred, "Download cancelado. A versão atual foi mantida."); return false; }
        catch (Exception) { Set(UpdateState.Error, "Não foi possível concluir a atualização. A versão atual foi mantida; verifique sua conexão e tente novamente."); return false; }
    }
    private void NotifyProgress(AppRelease release, int percent)
    {
        void Update() { if (State == UpdateState.Downloading) Set(State, $"Baixando a versão {release.Version} · {Math.Clamp(percent, 0, 100)}%"); }
        if (_context is not null && SynchronizationContext.Current != _context) _context.Post(_ => Update(), null); else Update();
    }
    private void Set(UpdateState state, string text) { State = state; Message = text; Changed?.Invoke(this, EventArgs.Empty); }
}
