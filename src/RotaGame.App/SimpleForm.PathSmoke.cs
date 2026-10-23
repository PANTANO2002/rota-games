using System.Diagnostics;
using RotaGame.Core;

namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    internal async Task RunPathSmokeAsync(string directory, PathSmokeRoutes routes, PathSmokeMeter meter)
    {
        Directory.CreateDirectory(directory); _gameWatcher.Stop(); _tips.Active = false; TopMost = true;
        try
        {
            SelectRole(DeviceRole.Player); ShowDashboard(); ClientSize = new Size(1340, 860);
            _routeMap.PreviewLabel = "TESTE CONTROLADO · ROTAS E PINGS SIMULADOS · SEM ALTERAR A REDE";
            var destination = new ObservedDestination("127.0.0.1", 0, "ICMP", Environment.ProcessId, Process.GetCurrentProcess().ProcessName, 1);
            _operation = ActivateAsync(destination, new(24, 100, 200, 32, 0));
            await meter.FirstProbe.Task.WaitAsync(TimeSpan.FromSeconds(5)); Check(_comparingPaths, "O teste não começou na comparação automática.");
            using (var picker = new PathChoiceWindow([RouteOption.Direct, PathSmokeRoutes.Bridge], null, RouteOption.Direct, RequestPath))
            {
                picker.Show(this); await Task.Delay(50);
                using var bitmap = new Bitmap(picker.Width, picker.Height); picker.DrawToBitmap(bitmap, picker.ClientRectangle); bitmap.Save(Path.Combine(directory, "escolher-caminho.png"));
                Descendants(picker).OfType<DashboardButton>().Single(b => b.AccessibleName == PathSmokeRoutes.Bridge.Name).PerformClick();
            }
            await Until(() => _report?.ManualSelection == true && _selected.Id == PathSmokeRoutes.Bridge.Id && _rolling.Count > 0, "Escolha da ponte durante a comparação não foi aplicada.");
            Check(_active && !_cts!.IsCancellationRequested && !_comparingPaths && routes.Selected == PathSmokeRoutes.Bridge.Id, "Trocar caminho encerrou a ativação ou não alterou a seleção.");
            Check(_report!.Choice is null && _report.CurrentRoute?.Id == PathSmokeRoutes.Bridge.Id && _report.Method.Contains("manual"), "Escolha manual foi apresentada como vencedor de comparação.");
            Check(_routeMap.Snapshot(DateTimeOffset.UtcNow).Single(v => v.Selected).Badge == "MANUAL · EM USO", "Diagrama não identifica a escolha manual.");
            using (var image = new Bitmap(Width, Height)) { DrawToBitmap(image, ClientRectangle); image.Save(Path.Combine(directory, "manual-ponte-simulado.png")); }
            meter.Timeouts = true;
            await Until(() => meter.Failed >= 5, "O teste de ICMP sem resposta não completou.", 9);
            Check(_selected.Id == PathSmokeRoutes.Bridge.Id && _active, "Modo manual foi abandonado apenas porque ICMP não respondeu."); meter.Timeouts = false;
            RequestPath(RouteOption.Direct);
            await Until(() => !_switchingPath && _selected.IsDirect && _rolling.Count > 0 && _state.Text.StartsWith("Manual"), "Escolha direta não foi aplicada durante o monitoramento.");
            RequestPath(null);
            await Until(() => _report?.ManualSelection == false && _report.Choice is not null && !_comparingPaths && _rolling.Count > 0, "Voltar ao automático não refez a comparação.");
            Check(_report!.Choice!.Winner.Id == PathSmokeRoutes.Bridge.Id, "A fixture automática não escolheu a ponte prevista.");
            meter.DirectRtt = 99; meter.BridgeRtt = 134;
            var previous = _report; RequestPath(null);
            await Until(() => !ReferenceEquals(_report, previous) && _report?.Choice is not null && !_comparingPaths && _rolling.Count > 0, "Reavaliar no automático não concluiu o novo teste.");
            Check(_selected.IsDirect && routes.Selected == "" && _report!.Choice!.Winner.IsDirect && _report.Choice.Assessments.Single().Reason.Contains("não foi menor"), "A ponte pior deveria manter a direta, com explicação.");
            Check(_routeMap.Snapshot(DateTimeOffset.UtcNow).Any(v => !v.Route.IsDirect && v.Detail.StartsWith("Último teste")), "O ping da rota não ativa parece uma medição ao vivo.");
            using (var image = new Bitmap(Width, Height)) { DrawToBitmap(image, ClientRectangle); image.Save(Path.Combine(directory, "automatico-direta-simulado.png")); }
            meter.BridgeRtt = 96; previous = _report; RequestPath(null);
            await Until(() => !ReferenceEquals(_report, previous) && _report?.Choice is not null && !_comparingPaths && _rolling.Count > 0, "Novo teste de ganho pequeno não terminou.");
            Check(_selected.Id == PathSmokeRoutes.Bridge.Id && _report!.Choice!.WinnerStats.AverageMs == 96, "Ganho pequeno e repetido deveria escolher a ponte de menor ping.");
            routes.FailNextBridge = true; RequestPath(PathSmokeRoutes.Bridge);
            await Until(() => !_active, "Falha de aplicação não encerrou a operação com recuperação.");
            Check(routes.Selected == "" && !HasRecovery(), "Falha manual não restaurou a preferência anterior.");
            await DeactivateAsync();
            File.WriteAllText(Path.Combine(directory, "paths-smoke.txt"), "PASS: seleção manual de ponte/direta durante a comparação e monitoramento; ICMP sem resposta mantém a escolha manual; retorno ao automático refaz os testes; direta 99 / ponte 134 escolhe direta; direta 99 / ponte 96 escolhe ponte; valores de rotas não ativas identificados como último teste; falha parcial restaura a preferência anterior. Rotas e pings simulados e identificados; nenhuma rede real alterada.\n");
        }
        finally { _routeMap.PreviewLabel = null; await DeactivateAsync(); TopMost = false; }
        async Task Until(Func<bool> condition, string error, int seconds = 6)
        {
            var watch = Stopwatch.StartNew(); while (!condition() && watch.Elapsed.TotalSeconds < seconds) await Task.Delay(25);
            Check(condition(), error + " Estado: " + _state.Text + ". " + _message.Text);
        }
        static void Check(bool value, string text) { if (!value) throw new InvalidOperationException(text); }
    }
}
internal sealed class PathSmokeRoutes : IAutomaticRoutes
{
    internal static RouteOption Bridge { get; } = new("100.64.0.25", "Ponte de teste · Cascavel");
    internal string Selected = ""; internal bool FailNextBridge;
    public Task<RouteReading> ReadAsync(CancellationToken ct) => Task.FromResult(new RouteReading(Selected, true, "Controlled fixture"));
    public Task<IReadOnlyList<RouteOption>> DiscoverAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RouteOption>>([Bridge]);
    public Task SelectAsync(RouteOption route, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Selected = route.Id; if (!route.IsDirect && FailNextBridge) { FailNextBridge = false; throw new IOException("Falha parcial controlada na escolha manual."); } return Task.CompletedTask; }
}
internal sealed class PathSmokeMeter : IAutomaticMeter
{
    internal TaskCompletionSource FirstProbe { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool Timeouts; internal int Failed;
    internal double DirectRtt = 110, BridgeRtt = 70;
    public async Task<MeasurementBlock> MeasureAsync(string target, RouteOption route, ProbeSettings settings, IProgress<MeasurementProgress>? progress, CancellationToken ct)
    {
        var block = new MeasurementBlock { TargetIp = target, Path = PathKind.Diagnostic, Settings = settings };
        for (var i = 1; i <= settings.Count; i++)
        {
            await Task.Delay(25, ct); var now = DateTimeOffset.UtcNow;
            var snapshot = new RouteSnapshot(77, route.IsDirect ? "fixture-physical" : "fixture-tunnel", route.IsDirect ? "Interface física de teste" : "Tailscale de teste", "Dados simulados", "", "0.0.0.0/0", !route.IsDirect, route.IsDirect, now);
            var sample = new Sample(i, now, Timeouts ? ProbeOutcome.Timeout : ProbeOutcome.Success, Timeouts ? null : route.IsDirect ? DirectRtt : BridgeRtt, "Dado simulado para testar seleção", snapshot, snapshot);
            if (Timeouts) Failed++; block.Samples.Add(sample); progress?.Report(new(block, sample, "Teste controlado")); FirstProbe.TrySetResult();
        }
        block.Status = "complete"; block.EndedAt = DateTimeOffset.UtcNow; return block;
    }
}
