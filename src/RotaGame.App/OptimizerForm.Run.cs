using System.Diagnostics;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class OptimizerForm
{
    private CancellationTokenSource? _targetCts;
    private bool _repeatRequested;
    private async Task ActivateAsync(ObservedDestination? forced = null, ProbeSettings? settings = null)
    {
        _runCts?.Dispose(); _runCts = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token); var ct = _runCts.Token;
        _report = null; _table.Rows.Clear(); _destination = null; _map.Destination = null; ClearLive(); SetControls();
        try
        {
            if (!_controlled) await LegacyRecovery.RestoreAsync(_dataDirectory, ct);
            await _lease.RestoreAsync(ct);
            while (true)
            {
                ct.ThrowIfCancellationRequested(); GameProfile? game = null; ObservedDestination? destination;
                if (forced is not null) destination = forced;
                else
                {
                    while (true)
                    {
                        var running = GameLibrary.Running(_profiles);
                        game = _profiles.FirstOrDefault(p => (_chosenGame is null || p.Id == _chosenGame) && running.GetValueOrDefault(p.Id)?.Length > 0);
                        if (game is not null) break;
                        _status.Text = "Ativo · aguardando um jogo"; _hint.Text = "Abra um jogo da biblioteca. O destino será detectado pelo tráfego do processo."; await Task.Delay(1500, ct);
                    }
                    _status.Text = game.Name + " reconhecido";
                    try { destination = await new GameDiscovery().FindAsync(game.Name, new UiProgress<string>(s => _hint.Text = s), ct, customProcessNames: game.ProcessNames); }
                    catch (GameNotReadyException ex) { _hint.Text = ex.Message; await Task.Delay(1500, ct); continue; }
                }
                while (destination is not null)
                {
                    _destination = destination; _destinationLabel.Text = $"Destino observado · {destination.Ip}:{destination.Port} · {destination.Protocol} · {destination.ProcessName}";
                    _tips.SetToolTip(_destinationLabel, "Destino observado no processo, priorizando tráfego UDP sustentado. Pode ser voz ou outro jogador, principalmente em GTA Online. Não há cidade ou servidor inventado.");
                    _map.Destination = destination; _report = null; _table.Rows.Clear(); ClearLive();
                    using var target = CancellationTokenSource.CreateLinkedTokenSource(ct); _targetCts = target;
                    var watch = forced is null ? WatchDestinationAsync(game!, destination, target.Token) : Task.Delay(Timeout.Infinite, target.Token).ContinueWith<ObservedDestination?>(_ => null, TaskScheduler.Default);
                    Task? comparison = null, monitoring = null;
                    try
                    {
                        _comparing = true; _status.Text = "Comparando os caminhos…";
                        _hint.Text = "Compare em treino antes da partida: alternar a saída pode reconectar o jogo. Não há garantia de menor ping."; SetControls();
                        var compare = new AutomaticOptimizer(_routes, _meter, _lease).ActivateAsync(game?.Name ?? destination.ProcessName, destination,
                            settings ?? new ProbeSettings(24, 1000, 1000, 32, 2), new UiProgress<string>(s => _status.Text = s), new UiProgress<MeasurementProgress>(ShowSample), target.Token, new UiProgress<string>(s => _hint.Text = s));
                        comparison = compare;
                        if (await Task.WhenAny(compare, watch) == watch) { destination = await watch; continue; }
                        _report = await compare; _selected = _report.Choice!.Winner; _comparing = false;
                        _map.Route = _selected; ClearLive(); RefreshResults(); _status.Text = "Ativo · " + _selected.Name; _hint.Text = _report.Choice.Reason;
                        await StartTraceAsync(target.Token); SetControls();
                        monitoring = MonitorAsync(destination, forced is not null, target.Token);
                        if (await Task.WhenAny(monitoring, watch) == watch) destination = await watch;
                        else { await monitoring; destination = null; }
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested && _repeatRequested) { _repeatRequested = false; }
                    finally
                    {
                        _comparing = false; target.Cancel(); _traceCts?.Cancel();
                        foreach (var task in new[] { comparison, monitoring, watch, _traceTask }.Where(t => t is not null))
                            try { await task!; } catch (OperationCanceledException) { } catch (GameNotReadyException) { }
                        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(25)); await _lease.RestoreAsync(cleanup.Token);
                        _targetCts = null;
                    }
                }
                _destination = null; _map.Destination = null; ClearLive(); _status.Text = "Jogo fechado · aguardando outro jogo"; SetControls();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { _status.Text = "Desativado"; }
        catch (Exception ex) { _status.Text = "Não foi possível concluir"; _hint.Text = ex.Message; }
        finally
        {
            _traceCts?.Cancel(); if (_traceTask is not null) try { await _traceTask; } catch (OperationCanceledException) { }
            try { using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(25)); await _lease.RestoreAsync(cleanup.Token); }
            catch (Exception ex) { _hint.Text = "Restauração pendente: " + ex.Message; }
            _runCts.Cancel(); ClearLive(); _comparing = false; RefreshResults(); SetControls();
        }
    }
    private async Task<ObservedDestination?> WatchDestinationAsync(GameProfile game, ObservedDestination current, CancellationToken ct)
    {
        string? pending = null;
        while (true)
        {
            for (var i = 0; i < (pending is null ? 20 : 1); i++) { if (!Alive(current)) return null; await Task.Delay(1000, ct); }
            try
            {
                var found = await new GameDiscovery().FindAsync(game.Name, null, ct, TimeSpan.FromSeconds(8), customProcessNames: game.ProcessNames);
                if (found.Ip == current.Ip) { pending = null; continue; }
                if (pending == found.Ip) return found; pending = found.Ip;
            }
            catch (GameNotReadyException) { pending = null; }
        }
    }
    private static bool Alive(ObservedDestination target)
    {
        try { using var p = Process.GetProcessById(target.ProcessId); return !p.HasExited && p.ProcessName.Equals(target.ProcessName, StringComparison.OrdinalIgnoreCase); }
        catch (ArgumentException) { return false; }
    }
    private async Task MonitorAsync(ObservedDestination target, bool forced, CancellationToken ct)
    {
        var failures = 0;
        while (forced || Alive(target))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var block = await _meter.MeasureAsync(target.Ip, _selected, new(5, 1000, 1000, 32, 0), new UiProgress<MeasurementProgress>(ShowSample), ct);
                failures = block.Stats.Received == 0 ? failures + block.Samples.Count : 0;
                if (failures >= 5 && !_selected.IsDirect) await FallbackAsync("O destino não respondeu às sondas pela alternativa. Voltei à direta; ICMP pode ser filtrado.", ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (!_selected.IsDirect) { await FallbackAsync("O caminho WARP perdeu a confirmação. " + ex.Message, ct); }
        }
    }
    private async Task FallbackAsync(string reason, CancellationToken ct)
    {
        await _lease.ApplyAsync(RouteOption.Direct, ct); _selected = RouteOption.Direct;
        if (_report is not null) { _report.CurrentRoute = _selected; _report.Notes.Add(reason); }
        _map.Route = _selected; ClearLive(); _status.Text = "Ativo · conexão direta"; _hint.Text = reason; RefreshResults(); await StartTraceAsync(ct);
    }
    private async Task StartTraceAsync(CancellationToken ct)
    {
        if (_destination is null || _comparing) return;
        _traceCts?.Cancel(); if (_traceTask is not null) try { await _traceTask; } catch (OperationCanceledException) { }
        _traceCts?.Dispose(); _traceCts = CancellationTokenSource.CreateLinkedTokenSource(ct); var token = _traceCts.Token; var destination = _destination;
        _traceTask = TraceAsync();
        async Task TraceAsync()
        {
            try
            {
                _map.Trace = await _tracer.RunAsync(destination.Ip, new UiProgress<NetworkTrace>(t => { if (_destination == destination && !token.IsCancellationRequested) _map.Trace = t; }), token);
                if (_map.Trace.Status == "route-changed") _hint.Text = "A interface mudou durante o trajeto. Clique no mapa para medir novamente; os saltos antigos não são apresentados como atuais.";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { if (!token.IsCancellationRequested) _hint.Text = "Não consegui revelar o trajeto: " + ex.Message; }
        }
    }
    private async Task StopAsync()
    {
        if (_stopping) return; _stopping = true; SetControls(); _runCts?.Cancel(); _targetCts?.Cancel(); _traceCts?.Cancel();
        try
        {
            if (_runTask is not null) await _runTask;
            using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(25)); await _lease.RestoreAsync(ct.Token);
            if (!_controlled) await LegacyRecovery.RestoreAsync(_dataDirectory, ct.Token);
            _status.Text = "Desativado"; _hint.Text = "Análise encerrada. A preferência anterior do WARP foi restaurada.";
        }
        catch (Exception ex) { _hint.Text = "Restauração pendente: " + ex.Message; }
        finally { _stopping = false; ClearLive(); RefreshResults(); SetControls(); }
    }
    private void ClearLive()
    {
        _rolling.Clear(); _chart.Samples.Clear(); _chart.Invalidate(); foreach (var value in _metrics) value.Text = "—"; _map.SetSample(null);
    }
    private void ShowSample(MeasurementProgress progress)
    {
        if (progress.Sample is not { } sample) return;
        _rolling.Enqueue(sample); while (_rolling.Count > 30) _rolling.Dequeue();
        var stats = Statistics.From(_rolling.Select((s, i) => s with { Sequence = i + 1 }));
        _metrics[0].Text = Number(stats.AverageMs) + " ms"; _metrics[1].Text = Number(stats.JitterMs) + " ms";
        _metrics[2].Text = Number(stats.P95Ms) + " ms"; _metrics[3].Text = Number(stats.FailurePercent) + " %";
        _chart.Samples.Add(sample); while (_chart.Samples.Count > 180) _chart.Samples.RemoveAt(0); _chart.Invalidate(); if (!_comparing) _map.SetSample(sample);
    }
    private void RefreshResults()
    {
        _table.Rows.Clear(); if (_report?.Choice is not { } choice) return;
        _summary.Text = $"Teste {(_report.CompletedAt ?? _report.CreatedAt).ToLocalTime():HH:mm} · direto {Number(choice.DirectStats.AverageMs)} ms · escolhido {Number(choice.WinnerStats.AverageMs)} ms";
        foreach (var group in _report.Trials.GroupBy(t => t.Route.Id))
        {
            var route = group.First().Route; var stats = Statistics.Combine(group.Select(t => t.Block)); var selected = route.Id == _selected.Id;
            var row = _table.Rows.Add(route.Name, Number(stats.AverageMs), Number(stats.P95Ms), Number(stats.JitterMs), Number(stats.FailurePercent) + "%", selected ? _runCts is { IsCancellationRequested: false } ? "Em uso" : "Último escolhido" : "Último teste");
            _table.Rows[row].Cells[5].ToolTipText = choice.Assessments.FirstOrDefault(a => a.Route.Id == route.Id)?.Reason ?? choice.Reason;
        }
        SetControls();
    }
    private static string Number(double? value) => value?.ToString("0.0") ?? "—";
}
