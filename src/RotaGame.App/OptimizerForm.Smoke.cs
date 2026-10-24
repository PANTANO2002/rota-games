using System.Diagnostics;
using System.Net;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class OptimizerForm
{
    internal async Task SmokeAsync(string directory, NetworkSmokeRoutes routes, NetworkSmokeMeter meter)
    {
        Directory.CreateDirectory(directory); _libraryTimer.Stop(); _tips.Active = false; TopMost = true; ClientSize = new Size(1440, 860);
        _map.PreviewLabel = "TESTE CONTROLADO · ROTAS, SALTOS E PINGS SIMULADOS · SEM ALTERAR A REDE";
        try
        {
            if (_profiles.Count != 0 || _cards.Count != 1 || _cards[0].Profile is not null) throw new Exception("Biblioteca vazia inventou um jogo.");
            Capture("inicio-biblioteca-vazia.png");
            using (var consent = new WarpConsentWindow())
            {
                consent.Show(this); await Task.Delay(50);
                if (AllControls(consent).OfType<CheckBox>().Single().Checked || AllControls(consent).OfType<DashboardButton>().Single(b=>b.Text=="Aceitar e preparar").Enabled) throw new Exception("Termos foram aceitos sem escolha do usuário.");
                using var image=new Bitmap(consent.Width,consent.Height);consent.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(directory,"preparar-cloudflare-sem-aceite.png"));consent.Close();
            }
            var target = new ObservedDestination("127.0.0.1", 7777, "UDP", Environment.ProcessId, Process.GetCurrentProcess().ProcessName, 24);
            _runTask = ActivateAsync(target, new(24, 100, 200, 32, 0));
            await Until(() => _report?.Choice is not null && !_comparing && _map.Trace?.Reached == true && _rolling.Count > 0, "Comparação WARP ou mapa não terminou.");
            if (_selected.Id != "warp" || _map.Trace!.Hops.Count != 4 || _map.Trace.Hops[1].Address is not null) throw new Exception("Escolha ou salto silencioso incorreto.");
            _report!.Notes.Add("TESTE: dados simulados, sem usar rede WARP real.");
            foreach (var width in new[] { 1440, 1080, 1600 }) { ClientSize = new Size(width, 860); Arrange(); await Task.Delay(60); Capture("caminho-warp-simulado-" + width + ".png"); }
            ClientSize = new Size(1440, 860); var previous = _report; meter.WarpRtt = 140; _refresh.PerformClick();
            await Until(() => !ReferenceEquals(previous, _report) && _report?.Choice is not null && !_comparing && _map.Trace?.Reached == true, "Recomparação não terminou.");
            if (!_selected.IsDirect || routes.Selected != "") throw new Exception("WARP pior não manteve a direta."); Capture("caminho-direto-simulado.png");
            await StopAsync(); if (routes.Selected != "" || _lease.Active || _metrics.Any(m => m.Text != "—")) throw new Exception("Desativar não restaurou a conexão e limpou o estado ao vivo.");
            _profiles=Enumerable.Range(1,6).Select(i=>new GameProfile("fixture-"+i,"Jogo simulado "+i,[],Environment.ProcessPath)).ToArray();RebuildGames();ClientSize=new Size(1080,860);Arrange();await Task.Delay(60);
            if (_games.RowCount<2 || _cards.Any(c=>c.Height<75)) throw new Exception("Cartões excedentes foram cortados em vez de criar outra linha.");Capture("biblioteca-varias-linhas-simulada.png");
            _profiles=[];RebuildGames();
            _map.Destination=target;_map.Route=RouteOption.Direct;
            _map.Trace=new(target.Ip,routes.Snapshot(),Enumerable.Range(1,30).Select(i=>new TraceHop(i,TraceOutcome.Timeout,null,null,"Teste simulado")).ToArray(),"partial",DateTimeOffset.UtcNow);
            Arrange();await Task.Delay(60); if(_map.Height<_map.ContentHeight) throw new Exception("Trajeto longo foi cortado.");Capture("trajeto-longo-simulado.png");
            _map.Destination=null;ClientSize=new Size(1440,860);Arrange();
            var source = new SmokeUpdates {Release=new("0.9.1","Dados de teste do aviso.")}; var flow = new UpdateWorkflow(source, PrepareForUpdateAsync); await flow.CheckAsync(false, default); ConfigureUpdates(source, flow);
            if (!_updatePanel.Visible || _updates?.Release is null) throw new Exception("Nova interface perdeu uma atualização já descoberta."); Capture("atualizacao-visivel.png");
            _updates!.Defer(); if (_updatePanel.Visible) throw new Exception("Depois não recolheu o aviso.");
            var failed = new SmokeUpdates { FailCheck = true }; var failedFlow = new UpdateWorkflow(failed, PrepareForUpdateAsync); await failedFlow.CheckAsync(false,default); ConfigureUpdates(failed,failedFlow);
            if (!_updatePanel.Visible || _updates!.State != UpdateState.Error) throw new Exception("Erro na abertura ficou invisível.");
            File.WriteAllText(Path.Combine(directory,"network-smoke.txt"),"PASS: nova tela sem ponte/conta Tailscale; biblioteca vazia; comparação simulada direta 110 / WARP 75 escolhe WARP e 110 / 140 escolhe direta; quatro saltos, um silencioso sem IP/ping inventado; janela em 1080/1440/1600; recomp comparação pelo botão; desativar restaura e limpa valores atuais; atualização já encontrada e erro da abertura preservados no painel. Rotas, saltos e pings são simulados e identificados.\n");
        }
        finally { await StopAsync(); TopMost = false; }
        async Task Until(Func<bool> condition, string message)
        {
            var watch=Stopwatch.StartNew(); while(!condition() && watch.Elapsed.TotalSeconds<15) await Task.Delay(25);
            if(!condition()) throw new Exception(message+" Estado="+_status.Text+"; "+_hint.Text);
        }
        void Capture(string name) { using var image=new Bitmap(Width,Height);DrawToBitmap(image,ClientRectangle);image.Save(Path.Combine(directory,name)); }
        static IEnumerable<Control> AllControls(Control root) {foreach(Control child in root.Controls) {yield return child;foreach(var nested in AllControls(child))yield return nested;}}
    }
}
internal sealed class NetworkSmokeRoutes : IAutomaticRoutes
{
    internal string Selected="";
    public Task<RouteReading> ReadAsync(CancellationToken ct)=>Task.FromResult(new RouteReading(Selected,true,"simulation") {Identity="fixture-account"});
    public Task<IReadOnlyList<RouteOption>> DiscoverAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<RouteOption>>([WarpClient.Warp]);
    public Task SelectAsync(RouteOption option,CancellationToken ct) {ct.ThrowIfCancellationRequested();Selected=option.Id;return Task.CompletedTask;}
    internal RouteSnapshot Snapshot()=>new(9,Selected.Length==0?"physical-fixture":"warp-fixture",Selected.Length==0?"Ethernet de teste":"Cloudflare WARP de teste","Dados simulados","192.0.2.1","0.0.0.0/0",false,Selected.Length==0,DateTimeOffset.UtcNow);
}
internal sealed class NetworkSmokeObserver(NetworkSmokeRoutes routes) : IRouteObserver { public RouteSnapshot Observe(IPAddress target)=>routes.Snapshot(); }
internal sealed class NetworkSmokeMeter(NetworkSmokeRoutes routes):IAutomaticMeter
{
    internal double WarpRtt=75;
    public async Task<MeasurementBlock> MeasureAsync(string target,RouteOption route,ProbeSettings settings,IProgress<MeasurementProgress>? progress,CancellationToken ct)
    {
        var block=new MeasurementBlock {TargetIp=target,Settings=settings,Path=PathKind.Diagnostic};
        for(var i=1;i<=settings.Count;i++) {await Task.Delay(20,ct);var snapshot=routes.Snapshot();var sample=new Sample(i,DateTimeOffset.UtcNow,ProbeOutcome.Success,route.IsDirect?110:WarpRtt,"Simulado",snapshot,snapshot);block.Samples.Add(sample);progress?.Report(new(block,sample,"simulation"));}
        block.Status="complete";block.EndedAt=DateTimeOffset.UtcNow;return block;
    }
}
internal sealed class NetworkSmokeProbe:ITraceProbe
{
    public Task<TraceReply> SendAsync(IPAddress target,int ttl,int timeoutMs,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();return Task.FromResult(ttl switch {1=>new TraceReply(TraceOutcome.Router,"192.0.2.1",5,"Simulado"),2=>new(TraceOutcome.Timeout,null,null,"Simulado"),3=>new(TraceOutcome.Router,"198.51.100.1",42,"Simulado"),_=>new(TraceOutcome.Destination,target.ToString(),75,"Simulado")});
    }
}
