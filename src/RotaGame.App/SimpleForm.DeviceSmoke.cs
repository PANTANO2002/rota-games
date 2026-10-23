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
            Check(frame.Measurements?.AverageMs == expected && expected.HasValue && frame.Measurements!.Confirmed, "O envio não contém a média e a rota reais do medidor.");
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
                Check(_routeMap.Snapshot(DateTimeOffset.UtcNow).Single(v => v.Selected).Ping == _metrics[0].Text && _table.Rows.Count > 0, "Diagrama ou comparação remota diverge do medidor.");
                _routeMap.PreviewLabel = "TESTE LOCAL · ICMP REAL · NÃO É A CONEXÃO ACRE–CASCAVEL";
                _state.Text = "TESTE LOCAL · painel da ponte"; Save("device-bridge-received.png");
                ClientSize = new Size(1120, 780); ArrangeDashboard(); await Task.Delay(100); Save("device-bridge-compact.png");
                var stale = frame with { Measurements = frame.Measurements! with { SampleAgeMs = 6001 } };
                ApplyRemoteView(_partner with { Frame = stale }); Check(_metrics[0].Text.Contains("—") && !_routeMap.Snapshot(DateTimeOffset.UtcNow).Any(v => v.Moving), "Dados antigos continuam apresentados como atuais.");
                Check(inbox.Fresh(DateTimeOffset.UtcNow.AddSeconds(11), DeviceRole.Player).Length == 0, "PC desconectado continua recente.");
            }
            ClearRemoteView(); Check(_metrics.All(m => m.Text == "—") && _chart.RemotePoints is null && _routeMap.Remote is null, "Desconexão manteve valores antigos.");
            SaveDevice(_device with { PartnerNodeId = "remembered-partner" });
            using (var reopened = new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), new GameLibrary(Path.Combine(directory, "reopened-games.json"), () => []), deviceStore: _deviceStore))
                Check(reopened._device.Role == DeviceRole.Bridge && reopened._bridgeChoice.Chosen && reopened._device.PartnerNodeId == "remembered-partner", "Uma nova janela esqueceu a função e o outro PC.");
            SelectRole(DeviceRole.Player); Check(_device.PartnerNodeId is null && !_remoteShown, "Trocar de função reaproveitou a identificação antiga.");
            File.WriteAllText(Path.Combine(directory, "devices-smoke.txt"), "PASS: função e parceiro persistem numa nova janela; botões refletem escolha e ativação; ICMP local real enviado por TCP ao painel da ponte, média/gráfico/diagrama/tabela intactos e com origem identificada; dados antigos deixam de aparecer como atuais; desconexão limpa os valores. Teste em loopback, sem conta, firewall ou rotas reais do usuário alterados.\n");
        }
        finally { _routeMap.PreviewLabel = null; TopMost = false; }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Save(string name) { using var image = new Bitmap(Width, Height); DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save(Path.Combine(directory, name)); }
    }
}
