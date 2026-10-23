# RotaGame 0.1 — laboratório de conexão para Windows

> Guia histórico da tela Avançado, preservada na versão 0.2. Execute os comandos a partir da raiz RotaGame. A tela padrão atual e sua preparação estão descritas no README da raiz.

Protótipo para investigar a conexão de um jogador no Acre em **The Finals, Rocket League, GTA Online e outros jogos**. Mede a conexão existente e permite comparar o mesmo destino diretamente e através de um relay WireGuard. **Não promete reduzir ping.**

## Abrir e medir agora

1. Abra `app/RotaGame.exe` no Windows x64. A distribuição inclui o runtime; não exige Python nem SDK .NET no computador do amigo.
2. Escolha o jogo e anote a região observada. Salvador, São Paulo e Porto Alegre são referências informadas pelo usuário, **não localizações verificadas de servidores**. O perfil do jogo só identifica o relatório.
3. Informe o **IPv4 do destino real da sessão do jogo**. Para conferir o aplicativo sem depender da internet, use **Usar teste local**, depois **Iniciar diagnóstico**.
4. Faça um diagnóstico com 60 ou mais sondas. O gráfico e os indicadores mudam durante a medição. Para interromper, clique **Parar**.
5. Use **Exportar** para salvar JSON com os blocos, resultados e evidência de rota, ou CSV com as sondas individuais.

O teste local só valida o aplicativo; não avalia a internet. Um destino genérico como `1.1.1.1` pode ajudar a investigar a conexão, mas não representa os servidores dos jogos. Não há endereços de servidores inventados nos perfis.

## O que está funcional

- Aplicativo nativo Windows com interface em português, sondas ICMP IPv4 e gráfico em tempo real.
- Ping médio, mediana nos blocos, p95, jitter de RTT e proporção de falhas ICMP.
- Resolução do hostname uma vez por sessão: todos os blocos usam o **mesmo IPv4**, sem alternar destinos por DNS.
- Observação da melhor rota IPv4 do Windows e do adaptador antes e depois de **cada** sonda, inclusive no aquecimento. O bloco é invalidado se a rota muda.
- Comparação guiada **A–B–B–A**, com ativação manual no cliente oficial WireGuard e verificação do caminho.
- Cancelamento, exportação de dados parciais, distinção entre erros locais e falhas de resposta.
- Traçado de rota com `tracert`, sem resolução DNS nos saltos.
- Código-fonte, testes reproduzíveis e arquivos para preparar uma VPS como relay.

## Como interpretar

| Indicador | Definição nesta versão |
|---|---|
| Ping médio | Média do RTT apenas das respostas ICMP recebidas; sem resposta não vira zero. |
| Mediana | Valor central dos RTTs; com número par, média dos dois centrais. |
| p95 | RTT no percentil 95 pelo método nearest rank; mostra parte da cauda. |
| Jitter de RTT | Média de `abs(RTT[n] - RTT[n-1])` entre sondas consecutivas respondidas. Não atravessa lacunas nem fronteiras de blocos. Não é jitter unidirecional RFC 3550. |
| Falhas ICMP / perda aparente | `(timeouts + ICMP inalcançável) / envios válidos × 100`. Erros locais são separados e invalidam o bloco. |

ICMP pode receber tratamento diferente dos pacotes do jogo. **Falhas de ping não provam perda do tráfego do jogo.** Um servidor sem resposta pode estar filtrando ICMP. Asteriscos em saltos intermediários também não provam perda no destino. GTA Online pode envolver outros jogadores e serviços; um único IP não caracteriza toda a sessão.

Use o diagnóstico junto da telemetria do jogo, em sessões e horários comparáveis. A precisão do RTT reportado por `Ping` é de milissegundos; RTT local de 0 ms é possível por arredondamento.

## Comparar quando houver VPS

Ainda não há VPS fornecida para este projeto. O aplicativo funciona para diagnóstico; **não vem conectado a um relay e não há comparação real de relay validada**. O guia em [relay/GUIA.md](relay/GUIA.md) descreve a configuração.

1. Configure cliente e VPS com WireGuard. Use `AllowedIPs` restrito ao IPv4 público de teste (`/32`). O app não recebe nem armazena chaves.
2. Preencha o mesmo destino na interface e clique **Comparar A–B–B–A**.
3. **A1:** desative WireGuard e outras VPNs; clique **Medir 1/4 · direta**.
4. **B1:** ative o relay no cliente WireGuard; clique **Medir 2/4 · relay**.
5. **B2:** mantenha o mesmo túnel e clique **Medir 3/4 · relay**.
6. **A2:** desative o túnel; clique **Medir 4/4 · direta**.
7. Exporte o JSON. O resultado inclui `relay − direta` para mediana, p95 e falhas em pontos percentuais: valores negativos favorecem o relay.

Cada bloco usa os mesmos parâmetros: 3 aquecimentos fora das estatísticas, 60 sondas por padrão, 32 bytes de payload, timeout 1 s e intervalo mínimo 1 s entre inícios de envios. Pode ajustar a quantidade antes de começar. Uma sondagem lenta não gera rajadas para compensar atrasos. O botão de comparação só prepara a sessão; os envios começam em **Medir**.

O critério de destaque é exploratório: diferença de mediana maior que 5 ms, acompanhada de p95 e falhas que não piorem. Não há cálculo de significância estatística nem decisão automática de roteamento. Com menos de 20 respostas por caminho, o resultado é inconclusivo. A ordem A–B–B–A ajuda a observar variação temporal, mas não a elimina.

**A verificação identifica a interface/rota local, não comprova sozinha o handshake ou a saída na VPS.** Confirme handshake e contadores no WireGuard e na VPS. O diagnóstico não altera rotas, firewall, DNS, registro ou configurações do Windows. A ativação do WireGuard afeta o tráfego para os IPs configurados no túnel, inclusive o jogo; alternar durante uma partida pode derrubar a sessão. Faça os blocos fora de partidas competitivas e mantenha o caminho escolhido durante a partida de validação.

## Encontrar o destino

Comece pelo Monitor de Recursos do Windows (`resmon` → Rede), pelo processo do jogo e pelas informações de rede disponíveis no próprio jogo. O Monitor de Recursos pode não mostrar de forma suficiente os destinos de jogos UDP. Nesses casos, a etapa seguinte é uma captura com ferramenta de rede para identificar os endpoints usados naquele momento. Esta versão **não captura pacotes nem descobre automaticamente servidores**.

Anote o IP remoto, horário, jogo, região selecionada e operadora. Um IP de site, login, CDN, voz ou launcher pode não ser o servidor da partida. Não há perfis fixos de IP porque os destinos podem mudar entre sessões. O campo de destino aceita IPv4 ou hostname sem porta/caminho.

## Plano de campo no Acre

1. Teste no cabo, sem downloads, e anote operadora, cidade, horário e região escolhida no jogo.
2. Identifique um destino que responda a ICMP. Comece com um diagnóstico de 300 sondas; guarde JSON.
3. Quando houver relay, faça pelo menos três sessões A–B–B–A em horários diferentes, preferencialmente com 120 sondas por bloco.
4. Valide no jogo com o mesmo servidor/região quando possível, anotando ping, perda e travamentos fornecidos pelo próprio jogo. Para GTA Online, registre também as condições da sessão.
5. Mantenha um relay só se a melhora se repetir, sem piorar falhas/p95 e com custo aceitável. Uma rota alternativa pode ficar igual ou pior; a distância física continua existindo.

Não é possível escolher a melhor cidade da VPS a partir deste computador. Primeiro obtenha as medições da conexão dele; depois teste um relay temporário próximo do destino e compare com o caminho direto antes de assumir um custo recorrente.

## Compilar e executar os testes

Requisito para desenvolvimento: SDK **.NET 10** e Windows x64. Não há pacotes NuGet de terceiros no código de aplicação/testes.

```powershell
.\build.ps1
```

O script compila, executa os testes e publica o executável com runtime na pasta `app/`. A primeira publicação baixa os pacotes oficiais do runtime.

```powershell
dotnet run --project tests/RotaGame.Tests -c Release -- --integration
dotnet run --project tests/RotaGame.Tests -c Release -- --integration --public-probe
```

O segundo comando envia 13 sondas (1 aquecimento + 12 medidas) a `1.1.1.1`. O primeiro só usa loopback como integração real; os demais cenários são fixtures sintéticas. Os testes são um executável de asserções, sem dependência de framework externo: falha retorna exit code 1.

```powershell
.\app\RotaGame.exe --ui-smoke .\validation
```

O smoke da interface abre a janela, faz oito medições reais de loopback, exporta JSON/CSV, salva uma imagem e fecha. Só cria evidências de validação; não representa a conexão do amigo.

Leia [ARCHITECTURE.md](ARCHITECTURE.md) para os componentes e limites; [VALIDACAO.md](VALIDACAO.md) para o que foi efetivamente verificado nesta entrega.

## Fontes técnicas

- [Microsoft: Ping.SendPingAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping.sendpingasync?view=net-10.0).
- [Microsoft: GetBestRoute](https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getbestroute) e [MIB_IPFORWARDROW](https://learn.microsoft.com/en-us/windows/win32/api/ipmib/ns-ipmib-mib_ipforwardrow).
- [WireGuard: quick start e geração de chaves](https://www.wireguard.com/quickstart/).
- [WireGuard para Windows: rotas, AllowedIPs e interfaces](https://git.zx2c4.com/wireguard-windows/about/docs/netquirk.md).

Projeto independente, sem afiliação ao ExitLag ou aos jogos citados.
