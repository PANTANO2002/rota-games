# Arquitetura — RotaGame 0.1

> Este arquivo descreve o modo avançado preservado. A arquitetura atual do modo automático está em docs/ARQUITETURA-AUTO.md.

## Objetivo

Construir primeiro uma ferramenta de medição honesta. Uma rota WireGuard funcional pode transportar o tráfego destinado aos IPs configurados, mas só medições no Acre podem indicar benefício. O aplicativo não implementa criptografia ou driver próprios.

```text
Windows do jogador
  RotaGame.App (WinForms; usuário comum)
       │ destino resolvido e fixado em IPv4
       ▼
  RotaGame.Core
       ├── MeasurementRunner / plano A–B–B–A / validação de blocos
       ├── Statistics / Comparison
       └── Export JSON e CSV
       │ interfaces IProbe e IRouteObserver
       ▼
  RotaGame.Windows
       ├── System.Net.NetworkInformation.Ping (ICMP)
       └── IP Helper GetBestRoute + NetworkInterface
       │
       ├── A: Ethernet/Wi-Fi → operadora → destino público fixo
       └── B: interface WireGuard → operadora → VPS → mesmo destino

WireGuard oficial (separado; instalação/ativação próprias)
  Cliente Windows ↔ UDP/51820 ↔ Linux VPS
  Configuração dividida por IP; encaminhamento/NAT no relay
```

## Componentes e responsabilidades

| Projeto | Responsabilidade |
|---|---|
| `RotaGame.Core` | Modelos, execução assíncrona sequencial, estatísticas, comparação e exportação. Não sabe controlar o Windows. |
| `RotaGame.Windows` | Adaptadores ICMP e leitura da melhor rota IPv4 via P/Invoke. Resolve hostnames e descarta IPv6 nesta versão. |
| `RotaGame.App` | Interface, gráfico, estado do experimento, cancelamento e uso limitado do `tracert.exe`. |
| `RotaGame.Tests` | Cálculos, integridade de sessões, bloqueio de rotas incorretas, cancelamento, exportação e integração opcional. |
| `relay/` | Modelo de cliente e preparação documentada de uma VPS Linux dedicada. Nenhum segredo embutido. |

## Integridade da medição

- A resolução de destino ocorre uma vez. O app escolhe deterministicamente um IPv4 do DNS e o exibe; a escolha não significa que esse seja o endpoint da partida.
- Cada bloco observa uma rota de referência. Antes/depois de cada envio, confere índice, ID do adaptador, próximo salto e prefixo. Qualquer diferença invalida o bloco inteiro para comparação.
- Blocos A aceitam Ethernet/Wi-Fi sem WireGuard/túnel conhecido pela descrição; B exigem descrição de adaptador contendo WireGuard. Nome digitado ou botão escolhido não bastam para classificar o caminho.
- Rota A1 deve coincidir com A2; B1/B2 devem usar o mesmo ID de adaptador e prefixo. Isso não identifica a VPS criptograficamente nem detecta mudança de peer dentro de uma configuração WireGuard. Não altere a configuração do túnel entre blocos.
- Erro local de envio não entra como perda de pacote; o bloco é invalidado. Timeout/inalcançável entram como falhas ICMP, não como prova de perda UDP.
- Dados parciais são preservados. Ao repetir um bloco inválido no assistente, ele é substituído; exporte antes se quiser preservar a tentativa anterior.
- O runner não sobrepõe sondas. O intervalo é mínimo entre inícios; não tenta compensar atraso com rajadas. Aquecimento excluído das estatísticas.
- JSON guarda timestamps UTC, ajustes e rota antes/depois de cada sonda. CSV resume a rota anterior e inclui o estado final do bloco. O relatório JSON inclui estatísticas e conclusão; sessões diagnósticas não geram comparação A/B.

## Estado da interface

```text
Pronto → Diagnóstico em execução → Concluído / Parcial / Inválido
Pronto → Comparação preparada
       → aguarda A1 → mede A1
       → aguarda B1 → mede B1
       → aguarda B2 → mede B2
       → aguarda A2 → mede A2 → avaliação

Falha de bloco → mantém a etapa para repetir
Parar durante envio → cancelamento → bloco parcial
Encerrar entre blocos → mantém os dados; sem conclusão completa
```

Os parâmetros e o destino ficam bloqueados durante o assistente. O aplicativo não armazena configurações de WireGuard, chaves privadas nem credenciais. Só faz rede para resolver o destino e enviar as sondas solicitadas. Não há serviço remoto de telemetria próprio.

## Limitações explícitas

- IPv4 e ICMP; não mede diretamente pacotes UDP do jogo, MOS, jitter unidirecional ou tempo do servidor.
- Não faz multipath, redundância, seleção automática de relay, captura por processo, aceleração TCP ou manipulação do registro.
- A observação da tabela é uma fotografia. Uma mudança transitória entre duas consultas pode escapar. Não inspeciona redirecionamentos de WFP, proxies ou VPNs não identificadas; desative outras VPNs para um experimento controlado.
- A indicação WireGuard prova apenas que a melhor rota local usa esse adaptador. Confirme handshake/transferências no cliente e na VPS; o app não consulta `wg show` com privilégios.
- O túnel é dividido por destinos `/32`, e não por processo. Qualquer processo que envie para aquele IP também usa o túnel. Mudanças de IP do jogo exigem atualização manual da lista e da ACL no relay.
- O guia do relay é um ponto de partida para uma VPS dedicada. Não foi executado num servidor real nesta entrega.
- Sem intervalo de confiança ou inferência estatística formal. O destaque de 5 ms é uma regra exploratória documentada.
- Executável sem assinatura digital. A distribuição x64 não foi testada em máquinas ARM64 ou em todos os ambientes Windows.

## Próximas etapas após os dados do Acre

1. Identificar os endpoints efetivos de cada jogo e coletar baselines pela operadora do amigo.
2. Disponibilizar um relay temporário e verificar encaminhamento, NAT, handshake, MTU e estabilidade.
3. Executar A–B–B–A real e validação dentro dos jogos. Escolher ou descartar relay com esses dados.
4. Só depois avaliar automação privilegiada de WireGuard, mais relays, destinos dinâmicos e métricas de UDP. Alterações privilegiadas ficam em um serviço separado, de superfície pequena, se essa etapa for necessária.
