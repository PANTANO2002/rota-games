using System.Diagnostics;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    private async Task RunPlayerLoopAsync(ObservedDestination? forced, ProbeSettings? settings, bool smoke)
    {
        var ct = _cts!.Token;
        var status = new Inline<string>(s => _state.Text = s);
        var samples = new Inline<MeasurementProgress>(ShowProgress);
        while (!ct.IsCancellationRequested)
        {
            ObservedDestination destination;
            if (forced is not null) destination = forced;
            else
            {
                var profile = await WaitForGameAsync(ct);
                _activeProfileId = profile.Id; UpdateCardStates();
                _state.Text = profile.Name + " reconhecido";
                _message.Text = "Detectando a conexão. A comparação pode reconectar o jogo; use treino antes da partida.";
                try { destination = await new GameDiscovery().FindAsync(profile.Name, status, ct, customProcessNames: profile.ProcessNames); }
                catch (GameNotReadyException ex)
                {
                    _state.Text = profile.Name + " reconhecido · aguardando conexão"; _message.Text = ex.Message;
                    await Task.Delay(1500, ct); continue;
                }
            }
            _report = null; _table.Rows.Clear(); _emptyResults.Visible = true; ClearLive();
            _routeMap.Destination = destination; _routeMap.Report = null; SyncDashboard();
            _destination.Text = smoke ? "Validação local: estes valores não representam a conexão do Acre." : $"Destino observado em {destination.ProcessName}. A região é escolhida no jogo.";
            _report = await new AutomaticOptimizer(_routes, _meter, _lease).ActivateAsync(destination.ProcessName, destination,
                settings ?? new ProbeSettings(24, 1000, 1000, 32, 2), status, samples, ct, new Inline<string>(s => _message.Text = s));
            _selected = _report.Choice!.Winner; RefreshResult(); SetControls();
            _state.Text = smoke ? "Validação local · conexão direta" : "Ativo · " + _selected.Name;
            _path.Text = "Caminho: " + _selected.Name; _message.Text = _report.Choice.Reason; ClearLive();
            if (smoke)
            {
                await _meter.MeasureAsync(destination.Ip, _selected, new(5, 100, 300, 32, 0), samples, ct);
                return;
            }
            await MonitorDestinationAsync(destination, samples, ct);
            _state.Text = "Jogo fechado · restaurando a conexão…";
            using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20))) await _lease.RestoreAsync(cleanup.Token);
            _activeProfileId = null; _routeMap.Destination = null; _routeMap.Report = null; _routeMap.Invalidate();
            ClearLive(); UpdateCardStates();
        }
        ct.ThrowIfCancellationRequested();
    }

    private async Task<GameProfile> WaitForGameAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested(); RefreshRunningGames();
            var eligible = _game.SelectedIndex > 0 ? new[] { _profiles[_game.SelectedIndex - 1] }.AsEnumerable() : _profiles;
            var found = eligible.FirstOrDefault(p => _runningGames.GetValueOrDefault(p.Id)?.Length > 0);
            if (found is not null) return found;
            _state.Text = "Ativo · aguardando um jogo";
            _message.Text = _game.SelectedIndex > 0 ? "Abra " + _game.Text + ". Ele será reconhecido automaticamente." : "Abra um jogo da biblioteca. O reconhecimento está ligado.";
            await Task.Delay(1500, ct);
        }
    }

    private async Task MonitorDestinationAsync(ObservedDestination destination, IProgress<MeasurementProgress> samples, CancellationToken ct)
    {
        var failed = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var process = Process.GetProcessById(destination.ProcessId);
                if (process.HasExited || !process.ProcessName.Equals(destination.ProcessName, StringComparison.OrdinalIgnoreCase)) return;
            }
            catch (ArgumentException) { return; }
            var watch = Stopwatch.StartNew();
            try
            {
                var block = await _meter.MeasureAsync(destination.Ip, _selected, new(1, 1000, 1000, 32, 0), samples, ct);
                failed = block.Stats.Received == 0 ? failed + 1 : 0;
                if (failed >= 5 && !_selected.IsDirect) await FallbackAsync();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (!_selected.IsDirect)
            {
                var current = await _routes.ReadAsync(ct);
                if (current.Selector != _selected.Id) throw new InvalidOperationException("A conexão foi alterada fora do programa. A alteração será preservada.");
                _report!.Notes.Add(ex.Message); await FallbackAsync();
            }
            await Task.Delay(Math.Max(0, 1000 - (int)watch.ElapsedMilliseconds), ct);
        }
        ct.ThrowIfCancellationRequested();

        async Task FallbackAsync()
        {
            await _lease.ApplyAsync(RouteOption.Direct, ct); _selected = RouteOption.Direct; failed = 0; ClearLive();
            _state.Text = "Ativo · conexão direta"; _path.Text = "Caminho: conexão direta";
            _message.Text = "A ponte falhou ou ficou instável. O programa voltou à conexão direta.";
            _baseline.Text = "Ponte interrompida; acompanhando a conexão direta.";
            SyncDashboard();
        }
    }
}
