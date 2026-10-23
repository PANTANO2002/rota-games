using RotaGame.Core;

namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    internal async Task RunDiagramSmokeAsync(string directory)
    {
        Directory.CreateDirectory(directory); _gameWatcher.Stop(); TopMost = true;
        var log = new List<string>();
        try
        {
            WindowState = FormWindowState.Normal; ClientSize = new Size(1340, 860); Location = Screen.FromControl(this).WorkingArea.Location; ShowDashboard();
            await ActivateAsync(new("127.0.0.1", 0, "ICMP", Environment.ProcessId, "Teste ICMP local", 1), new(4, 100, 300, 32, 0), true);
            var live = _routeMap.Snapshot(DateTimeOffset.UtcNow).Single();
            var measured = Statistics.From(_rolling).AverageMs;
            Check(measured.HasValue && live.Ping == N(measured) + " ms" && live.Selected && live.Moving, "O diagrama não mostra a média real do monitor local.");
            Check(_routeMap.AnimationRunning, "O temporizador não iniciou na rota confirmada.");
            SaveForm("interface-diagram-local.png");
            using (var first = MapImage())
            {
                await Task.Delay(160); using var next = MapImage();
                Check(PixelDifference(first, next) > .0001, "Os pixels da animação não mudaram.");
            }
            _routeMap.Visible = false; Check(!_routeMap.AnimationRunning, "A animação continua oculta.");
            _routeMap.Visible = true; Check(_routeMap.AnimationRunning, "A animação não voltou após exibir o painel.");
            WindowState = FormWindowState.Minimized; await Task.Delay(60); Check(!_routeMap.AnimationRunning, "A animação continua minimizada.");
            WindowState = FormWindowState.Normal; await Task.Delay(60); Check(_routeMap.AnimationRunning, "A animação não voltou após restaurar.");
            var expired = _routeMap.Snapshot(_rolling.Last().At.AddSeconds(6)).Single();
            Check(!expired.Moving && expired.Ping == "—" && expired.Detail == "Aguardando sonda", "Uma amostra antiga está sendo mostrada como atual.");
            await DeactivateAsync(); Check(!_routeMap.AnimationRunning, "Desativar não parou a animação.");
            log.Add("PASS: ICMP local real 4/4 na comparação e 5/5 no monitor; média do diagrama idêntica à média atual; pixels animados mudam; animação para ao ocultar, minimizar e desativar; amostra antiga não vira ping ao vivo.");

            var now = DateTimeOffset.UtcNow;
            var destination = new ObservedDestination("192.0.2.123", 27015, "UDP", Environment.ProcessId, "Demonstração visual", 50);
            var bridge = new RouteOption("100.64.0.2", "Ponte de teste · Cascavel");
            var other = new RouteOption("100.64.0.3", "Outra ponte de teste");
            var report = new AutomaticReport { Game = "Demonstração visual", Destination = destination };
            foreach (var option in new[] { RouteOption.Direct, bridge, other })
                for (var block = 0; block < 2; block++) report.Trials.Add(new(option, FixtureBlock(option, option.IsDirect ? 134 : option == bridge ? 87 : 109)));
            report.Choice = AutomaticPolicy.Choose(report.Trials);
            Check(report.Choice.Winner.Id == bridge.Id, "A fixture de comparação não selecionou a ponte prevista.");
            _cts?.Dispose(); _cts = new();
            _routeMap.Destination = destination; _report = report; _selected = bridge; _active = true; SyncDashboard(); RefreshResult();
            _routeMap.PreviewLabel = "PRÉVIA VISUAL · DADOS SIMULADOS · NÃO É O PING DO ACRE";
            Feed(bridge, FixtureBlock(bridge, 86));
            Check(_routeMap.Snapshot(now).Single(r => r.Route.Id == bridge.Id).Selected, "A ponte escolhida não foi destacada.");
            var preferred = _routeMap.PreferredContentHeight;
            Check(preferred >= 250, "Três caminhos não reservaram altura suficiente.");
            _routeMap.FreezeAnimation = true;
            foreach (var size in new[] { new Size(1600, 860), new Size(1340, 860), new Size(1080, 720) })
            {
                ClientSize = size; ArrangeDashboard(); await Task.Delay(80);
                Check(_routeMap.Height / (DeviceDpi / 96f) >= preferred - 2, $"Os caminhos foram comprimidos: {_routeMap.Height}px, mínimo {preferred} lógicos em {size}.");
                SaveForm($"interface-diagram-{size.Width}.png");
                using var presented = CapturePresented(); Invalidate(true); Refresh(); await Task.Delay(90);
                using var refreshed = CapturePresented();
                var difference = PixelDifference(presented, refreshed);
                Check(difference <= .003, "O diagrama com três caminhos deixou pixels antigos após redimensionar.");
                log.Add($"PASS: três rotas em {size.Width}×{size.Height}; pixels apresentados vs. repintura completa: {difference:P4} (animação congelada para comparar o mesmo quadro).");
            }
            ClientSize = new Size(1340, 860); ArrangeDashboard(); await Task.Delay(80);
            var frames = Path.Combine(directory, "diagram-frames"); Directory.CreateDirectory(frames);
            for (var i = 0; i < 48; i++)
            {
                using var bitmap = new Bitmap(900, 280); using var g = Graphics.FromImage(bitmap);
                _routeMap.Draw(g, bitmap.Size, i / 48d, now); bitmap.Save(Path.Combine(frames, $"frame-{i:000}.png"));
                if (i == 12) bitmap.Save(Path.Combine(directory, "diagram-preview.png"));
            }

            var failed = FixtureBlock(bridge, 86); failed.Samples.Clear();
            var route = FixtureRoute(bridge); var timeout = new Sample(1, now, ProbeOutcome.Timeout, null, "Teste controlado sem resposta", route, route);
            failed.Samples.Add(timeout); _routeMap.Observe(bridge, new(failed, timeout, ""), Statistics.From(failed.Samples));
            var missing = _routeMap.Snapshot(now).Single(r => r.Route.Id == bridge.Id);
            Check(missing.Ping == "—" && missing.Detail == "Sem resposta" && !missing.Moving, "Falha ICMP foi mostrada como ping ou movimento.");
            var wrong = timeout with { Outcome = ProbeOutcome.Success, RttMs = 1, RouteAfter = route with { InterfaceId = "changed" } };
            _routeMap.Observe(bridge, new(failed, wrong, ""), Statistics.From([wrong]));
            Check(_routeMap.Snapshot(now).Single(r => r.Route.Id == bridge.Id).Detail == "Rota sem confirmação", "Uma rota divergente foi apresentada como confirmada.");
            var tailscale = route with { Name = "Tailscale", Description = "Tailscale Tunnel", IsWireGuard = false };
            var confirmed = wrong with { RouteBefore = tailscale, RouteAfter = tailscale };
            _routeMap.Observe(bridge, new(failed, confirmed, ""), Statistics.From([confirmed]));
            Check(_routeMap.Snapshot(now).Single(r => r.Route.Id == bridge.Id).Moving, "O adaptador Tailscale sem o nome WireGuard foi rejeitado.");
            _selected = RouteOption.Direct; SyncDashboard(); Feed(RouteOption.Direct, FixtureBlock(RouteOption.Direct, 136));
            var fallback = _routeMap.Snapshot(now);
            Check(fallback.Single(r => r.Selected).Route.IsDirect && !fallback.Single(r => r.Route.Id == bridge.Id).Moving, "Retorno à direta manteve a ponte antiga destacada.");
            SaveForm("interface-diagram-fallback-test.png");
            _active = false; SyncDashboard(); Check(_routeMap.Snapshot(now).All(r => !r.Moving && r.Badge != "EM USO"), "Desativado mostra uma rota em uso.");
            _routeMap.Destination = null; _report = null; SyncDashboard();
            Check(_routeMap.Snapshot(now).Count == 1 && _routeMap.Snapshot(now)[0].Ping == "—", "Reiniciar mantém uma ponte ou ping de outra sessão.");
            log.Add("PASS: dados controlados explicitamente identificados; três rotas sem cortes em 1600/1340/1080 px; 48 quadros de prévia; timeout, rota divergente, retorno à direta e limpeza da sessão conferidos.");
            File.WriteAllLines(Path.Combine(directory, "diagram-smoke.txt"), log);

            MeasurementBlock FixtureBlock(RouteOption option, double mean)
            {
                var block = new MeasurementBlock { Path = PathKind.Diagnostic, TargetIp = destination.Ip, Settings = new(24, 1000, 1000, 32, 0), Status = "complete", EndedAt = now };
                for (var i = 0; i < 24; i++)
                {
                    var snapshot = FixtureRoute(option);
                    block.Samples.Add(new(i + 1, now.AddMilliseconds((i - 23) * 10), ProbeOutcome.Success, mean + (i % 3 - 1), "Dado simulado de teste visual", snapshot, snapshot));
                }
                return block;
            }
            RouteSnapshot FixtureRoute(RouteOption option) => new(7, option.IsDirect ? "test-physical" : "test-tunnel", "Interface de teste", "Demonstração", "", "0.0.0.0/0", !option.IsDirect, option.IsDirect, now);
            void Feed(RouteOption option, MeasurementBlock block)
            {
                ClearLive(); _routeMap.BeginMeasurement(option);
                foreach (var sample in block.Samples) { var progress = new MeasurementProgress(block, sample, ""); ShowProgress(progress); _routeMap.Observe(option, progress, Statistics.From(_rolling)); }
            }
        }
        finally { _routeMap.PreviewLabel = null; _routeMap.FreezeAnimation = false; TopMost = false; }
        void SaveForm(string name) { using var image = new Bitmap(Width, Height); DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save(Path.Combine(directory, name)); }
        Bitmap MapImage() { var image = new Bitmap(_routeMap.Width, _routeMap.Height); _routeMap.DrawToBitmap(image, _routeMap.ClientRectangle); return image; }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
