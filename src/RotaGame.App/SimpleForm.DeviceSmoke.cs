using System.Net;
using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    internal async Task RunDeviceSmokeAsync(string directory)
    {
        Directory.CreateDirectory(directory); _gameWatcher.Stop(); TopMost = true;
        try
        {
            ClientSize = new Size(1340, 860); ShowDashboard(); SelectRole(DeviceRole.Player);
            Check(_playerChoice.Chosen && !_bridgeChoice.Chosen && _activate.Text.Contains("Ativar conexão"), "Escolha do jogador não refletida no botão.");
            await ActivateAsync(new("127.0.0.1", 0, "ICMP", Environment.ProcessId, "ICMP LOCAL REAL", 1), new(4, 100, 300, 32, 0), true);
            var frame = MakeFrame() with { PairRequested = true, BridgeUsable = true, Name = "JOGADOR · TESTE LOCAL" };
            var expected = Statistics.From(_rolling).AverageMs;
            Check(frame.Measurements?.AverageMs == expected && expected.HasValue && frame.Measurements!.Confirmed, "O envio não contém a média e a rota reais do medidor: " + System.Text.Json.JsonSerializer.Serialize(new { Expected = expected, frame.Measurements, Views = _routeMap.Snapshot(DateTimeOffset.UtcNow) }));
            Check(_activate.Text.Contains("Desativar") && _playerChoice.Chosen && !_playerChoice.Enabled, "O estado ativo perdeu a identificação.");
            Save("device-player-local.png");
            await DeactivateAsync(); SelectRole(DeviceRole.Bridge);
            Check(_bridgeChoice.Chosen && !_playerChoice.Chosen && _activate.Text.Contains("Ativar ponte"), "Escolha da ponte não refletida no botão.");
            var inbox = new DeviceInbox();
            await using (var server = new PeerPanelServer(IPAddress.Loopback, ip => ip == "127.0.0.1" ? "local-player-node" : null, MakeFrame, inbox, 0, true))
            {
                var response = await PeerPanelServer.ExchangeAsync(IPAddress.Loopback, frame, default, server.BoundPort, true);
                Check(response.Role == DeviceRole.Bridge && response.Measurements is null, "Resposta da ponte contém papel ou dados errados.");
                _partner = inbox.Fresh(DateTimeOffset.UtcNow, DeviceRole.Player).Single(); ApplyRemoteView(_partner); UpdateDeviceLabels();
                Check(_metrics[0].Text == N(expected) + " ms" && _metricTitles[0].Text.Contains("PC DO AMIGO") && _chart.RemotePoints?.Count == frame.Measurements!.Points.Length, "O painel da ponte não exibe os valores recebidos.");
                Check(_state.Text == "Amigo na conexão direta", "Amigo conectado foi confundido com ponte em uso.");
                Check(_routeMap.Snapshot(DateTimeOffset.UtcNow).Single(v => v.Selected).Ping == _metrics[0].Text && _table.Rows.Count > 0, "Diagrama ou comparação remota diverge do medidor.");
                _routeMap.PreviewLabel = "TESTE LOCAL · ICMP REAL · NÃO É A CONEXÃO ACRE–CASCAVEL";
                _state.Text = "TESTE LOCAL · painel da ponte"; Save("device-bridge-received.png");
                ClientSize = new Size(1120, 780); ArrangeDashboard(); await Task.Delay(100); Save("device-bridge-compact.png");
                var stale = frame with { Measurements = frame.Measurements! with { SampleAgeMs = 6001 } };
                ApplyRemoteView(_partner with { Frame = stale }); Check(_metrics[0].Text.Contains("—") && !_routeMap.Snapshot(DateTimeOffset.UtcNow).Any(v => v.Moving), "Dados antigos continuam apresentados como atuais.");
                ApplyRemoteView(_partner with { ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-6) }); Check(_metrics[0].Text.Contains("—"), "A interrupção das mensagens manteve ping antigo como atual.");
                _tailStatus = new("Running", "", true, "PONTE DE TESTE", true, []) { SelfIp = "100.64.0.10" };
                var usingBridge = frame with { Measurements = frame.Measurements! with { RouteId = "100.64.0.10", Comparing = false } };
                ApplyRemoteView(_partner with { Frame = usingBridge }); Check(_state.Text == "Esta ponte está em uso pelo amigo", "O painel não reconheceu sua ponte em uso.");
                ApplyRemoteView(_partner with { Frame = usingBridge with { Measurements = usingBridge.Measurements! with { Comparing = true } } }); Check(_state.Text == "Amigo testando esta ponte", "Teste foi confundido com uso após escolha.");
                Check(inbox.Fresh(DateTimeOffset.UtcNow.AddSeconds(11), DeviceRole.Player).Length == 0, "PC desconectado continua recente.");
            }
            ClearRemoteView(); Check(_metrics.All(m => m.Text == "—") && _chart.RemotePoints is null && _routeMap.Remote is null, "Desconexão manteve valores antigos.");
            _partner = null;
            var offers = (SmokeBridgeOffers)_bridgeOffers; offers.Advertising = offers.Approved = true;
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await ActivateAsync(); Check(_state.Text == "Ponte disponível" && _pageScroll.Visible && _connectionPage?.Visible != true, "Uma ponte já autorizada abriu novamente a autorização.");
                await DeactivateAsync(); Check(offers.Advertising && offers.Writes == 0, "Parar retirou uma oferta que já pertencia ao usuário.");
            }
            offers.Advertising = offers.Approved = false; await ActivateAsync();
            Check(_bridgeReadiness == BridgeReadiness.Confirming && _connectionPage?.Visible != true, "A propagação abriu a autorização automaticamente.");
            _bridgeStartedAt = DateTimeOffset.UtcNow.AddSeconds(-9); UpdateBridgeAvailability(true, false);
            Check(_bridgeReadiness == BridgeReadiness.PermissionUnconfirmed && _pageScroll.Visible, "A permissão pendente forçou outra tela.");
            offers.Approved = true; UpdateBridgeAvailability(true, true); Check(_state.Text == "Ponte disponível", "A confirmação tardia não foi reconhecida.");
            await DeactivateAsync(); Check(!offers.Advertising && offers.Writes == 2, "A oferta introduzida pela sessão não foi desfeita.");
            SaveDevice(_device with { PartnerNodeId = "remembered-partner" });
            using (var reopened = new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), new GameLibrary(Path.Combine(directory, "reopened-games.json"), () => []), deviceStore: _deviceStore))
                Check(reopened._device.Role == DeviceRole.Bridge && reopened._bridgeChoice.Chosen && reopened._device.PartnerNodeId == "remembered-partner", "Uma nova janela esqueceu a função e o outro PC.");
            SelectRole(DeviceRole.Player); Check(_device.PartnerNodeId is null && !_remoteShown, "Trocar de função reaproveitou a identificação antiga.");
            File.WriteAllText(Path.Combine(directory, "devices-smoke.txt"), "PASS: função e parceiro persistem numa nova janela; botões refletem escolha e ativação; ICMP local real enviado por TCP ao painel da ponte, média/gráfico/diagrama/tabela intactos e com origem identificada; dados antigos deixam de aparecer como atuais; desconexão limpa os valores. Três ativações/desativações da ponte pré-existente sem reescrita nem tela de autorização; confirmação tardia avança sem repetir permissão; só a oferta introduzida pela sessão é desfeita. Teste em loopback e transporte controlado, sem conta, firewall ou rotas reais do usuário alterados.\n");
        }
        finally { _routeMap.PreviewLabel = null; TopMost = false; }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Save(string name) { using var image = new Bitmap(Width, Height); DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save(Path.Combine(directory, name)); }
    }
}
