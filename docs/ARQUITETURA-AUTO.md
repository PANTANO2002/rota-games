# Arquitetura automática — RotaGame 0.6

## Biblioteca e ciclo dos jogos

GameLibrary persiste perfis com nome, caminho do executável e nomes de processo. Os três perfis conhecidos são apenas metadados de reconhecimento: nenhum é exibido sem encontrar o executável. InstalledGameScanner consulta bibliotecas Steam, manifestos locais Epic e locais de instalação no registro do Windows. As gravações usam arquivo temporário/rename e cópia `.bak`; uma lista separada guarda IDs ocultos pelo usuário. Adicionar o mesmo executável atualiza seu perfil, evitando duplicata, e retira seu ID da lista de ocultos. Arquivos removidos deixam de aparecer sem apagar o cadastro, permitindo reencontrá-los após reinstalação no mesmo caminho.

O painel consulta processos a cada dois segundos para os estados dos cartões. A seleção automática é armada por Ativar: aguarda um perfil aberto, passa seus nomes de processo à observação ETW, compara e monitora. Ao terminar o processo, restaura a conexão e volta ao estado de espera. Não inicia os executáveis cadastrados. O reconhecimento por nome também pode corresponder a outro executável de mesmo nome; não é verificação criptográfica da instalação do jogo.

DashboardControls concentra os controles desenhados para o novo visual, com superfícies arredondadas, cartões, chave ON/OFF e diagrama. O mapa mantém a direta e a ponte escolhida visíveis, mostrando no máximo três caminhos; a tabela contém os resultados completos. Não inventa infraestrutura, cidades, tráfego em bytes ou latências de segmentos.

RouteDiagram agora é um controle separado, com geometria vetorial, preenchimentos em degradê, halo nos nós/arestas e partículas em curvas Bézier. DiagramMeter decora o mesmo IAutomaticMeter usado pelo comparador e monitor: encaminha os eventos originais, identifica a RouteOption real e passa ao diagrama as estatísticas da mesma fila de 30 sondas do indicador principal. O desenho diferencia média atual e blocos concluídos de comparação. Blocos do monitor não são acumulados indefinidamente no desenho. O modo normal não tem valores de demonstração.

A seleção visual vem da rota efetivamente aplicada pela janela, incluindo retorno à direta, independentemente do vencedor histórico do relatório. Anima apenas o caminho atual com resposta recente (até cinco segundos), sem erro local, com fingerprint estável e adaptador compatível com direta ou túnel Tailscale/WireGuard. Timeout, rota divergente ou leitura antiga não produzem ping atual fictício. As partículas não representam captura de pacotes. O temporizador de 33 ms só opera quando há um caminho animável, o controle está visível e a janela não está minimizada; é encerrado ao descartar o controle. A altura do painel aumenta para duas/três rotas, preservando os cartões.

Painéis, tabelas de layout e controles pintados usam ResizeRedraw e buffer duplo. O formulário invalida a árvore de controles em mudanças de tamanho, ao terminar o redimensionamento e ao trocar DPI, com uma invalidação adicional enfileirada após o layout. O scroller invalida o conteúdo e o viewport quando muda o deslocamento ou os limites. Os botões da janela desenham ícones vetoriais para minimizar, maximizar/restaurar e fechar, sem depender da fonte. A validação compara os pixels capturados da tela com o mesmo estado após repintura completa; DrawToBitmap permanece para capturas de layout, mas não serve como evidência isolada contra pixels antigos.

ConnectionPage ocupa a mesma área central da biblioteca. IConnectionSession separa a interface das operações de instalação/entrada/status; ConnectionSession implementa o fluxo real e SmokeConnectionSession é usado apenas pelos modos explícitos de validação. A instalação reutiliza o pacote oficial; a entrada inicia o cliente, aceita somente URLs oficiais para abrir o navegador, observa a saída do processo e verifica BackendState após concluir. Cancelamento encerra o auxiliar e a vida útil acompanha a aba/aplicativo. O estado é consultado a cada 1,5 segundo apenas enquanto a aba está visível. Não há banco de contas RotaGame, navegador embutido, coleta de senhas ou tokens administrativos distribuídos no executável. Convite/aprovação continuam nas páginas oficiais. O Laboratório sai da navegação; o código manual anterior permanece apenas no modo --advanced para desenvolvimento.

DashboardScrollHost recorta o conteúdo e usa DashboardScrollBar, desenhada com degradê, brilho e thumb arredondado. A página e os resultados usam rolagem vertical quando necessária. A biblioteca é uma grade adaptável: calcula colunas com largura mínima de 240 pixels lógicos e coloca só os cartões excedentes na próxima linha; a última linha mantém a largura das anteriores. A altura da biblioteca cresce conforme as linhas, sem barras internas. O layout evita um estado de visibilidade antigo durante redimensionamentos encadeados. Arraste, clique no trilho, roda, Home/End e setas alteram o deslocamento. A navegação por Tab revela o cartão focado. O corpo da tela usa alturas mínimas e divide o espaço restante entre diagrama/gráfico e resultados.

ConnectionPage usa ConnectionSteps para derivar uma única próxima ação da função persistida e do estado real. Consulta o serviço a cada 1,5 s enquanto visível, inclusive durante a entrada. LoginCompletion confirma BackendState Running sem depender do encerramento do auxiliar. Uma troca explícita de rede antiga ainda espera o processo. Os links HTTPS oficiais /admin/invite/ e /uinv/ são reconhecidos; não se persiste o convite. Aprovação usa Self.ExitNodeOption; a ponte fica pronta ao identificar o jogador com permissão de uso.

DeviceProfileStore persiste um GUID de instalação, função, nome e NodeId do parceiro em LocalAppData, fora da instalação. Descoberta envia apenas identificação. Quando há um único RotaGame com papel oposto, salva o parceiro; múltiplos exigem escolha. PeerPanelServer escuta TCP 52941 apenas no IPv4 real do adaptador Tailscale. A regra de firewall é específica do executável, IP local e IPs privados de peers conhecidos. O endereço TCP de origem é resolvido no mapa autenticado do Tailscale; não se aceita identidade de origem alegada no corpo. Só jogador envia medições; não há comandos remotos, arquivos ou ações de rede no protocolo.

O painel envia até 180 pontos e nove resumos de caminhos ao parceiro selecionado a cada dois segundos. Limita mensagem a 64 KiB, profundidade JSON 12, quatro clientes e timeout curto. Verifica papéis, protocolo, números finitos, sequência e idade pelo relógio de recebimento. Mensagens repetidas são rejeitadas. A ponte mostra a origem dos dados; sondas com mais de cinco segundos deixam de ser atuais e peers sem dados por dez segundos expiram. O canal de telemetria fica dentro do túnel WireGuard, separado da seleção da rota do jogo.

IntroForm exibe a marca vetorial sendo traçada, pulso do nó, nome com degradê e pontos de rota durante quatro segundos. A leitura da biblioteca ocorre numa tarefa enquanto a introdução permanece visível; o formulário principal é preparado no contexto de interface antes de fechar a introdução. Pode levar mais tempo se a inicialização real demorar; em erro apresenta a mensagem e permite fechar. Não simula percentual de carregamento. FontAssets inclui Barlow e Rajdhani estáticas, extrai para cache com hash e registra os arquivos somente para o processo em GDI e GDI+. Títulos/indicadores e texto corrente usam famílias distintas; os controles de janela são vetoriais. A marca SVG exportada usa contornos, sem depender de uma fonte instalada.

Os cartões extraem o ícone do executável instalado. BrandAssets desenha a marca vetorial e exporta SVG/PNG/ICO; o ICO inclui 16/24/32/48/64/128/256 pixels, é embutido como ícone da aplicação e como recurso das janelas.

```text
SimpleForm / usuário Windows
  ├── Jogar: Ativar → GameDiscovery → AutomaticOptimizer → monitor
  └── Ponte: Ativar → oferta Tailscale → autorização inicial na conta

GameDiscovery
  ETW kernel NetworkTCPIP → PID dos perfis → EndpointAccumulator
  → destino IPv4 público observado; fixado por ativação

AutomaticOptimizer
  RouteLease → registro antes de cada mutação
  TailscaleClient → pontes online/aprovadas, verificação de ligação direta ou por DERP
  AutomaticMeter → preferência + interface confirmadas
  MeasurementRunner → sondas com checagem de rota antes/depois de cada envio
  AutomaticPolicy → direta ou ponte com vantagem medida

Desativar / fechamento normal → cancela → restaura preferência → limpa registro
Queda do processo → recuperação pelo registro na próxima abertura
```

| Projeto | Responsabilidades |
|---|---|
| Core | Modelos, sondas abstratas, estatísticas, política de escolha, controlador automático e lease de configuração. |
| Windows | ICMP, GetBestRoute, descoberta ETW, comandos sem shell, adaptador Tailscale, persistência do registro. |
| App | Tela simples, assistente inicial, medidor em tempo real, exportação JSON e tela avançada preservada. |
| Tests | Fixtures de decisão/recuperação/CLI e integrações explícitas ICMP, Windows e ETW. |

## Integridade e restauração

O executor usa `ProcessStartInfo.ArgumentList` sem shell e preserva o seletor vazio `--exit-node=`. O caminho do componente real é descoberto em Program Files, não fornecido por peers. O modo jogar só altera a seleção de ponto de saída; o modo ponte anuncia/retira sua oferta. O app não altera manualmente DNS, registro, firewall ou rotas do Windows.

O registro salva a preferência original, a aplicada anteriormente e a pretendida antes da mutação. Assim, uma falha parcial permite restaurar tanto se a mudança chegou ao serviço quanto se não chegou. A troca do arquivo é feita por rename de temporário. Um estado externo diferente é preservado. A restauração usa token próprio com prazo limitado mesmo se a operação principal foi cancelada. Falha de restauração mantém o registro para repetir.

O estado da seleção/peer é conferido antes/depois do bloco. A rota IPv4 local e o ID do adaptador são conferidos antes/depois de cada sonda. Direta exige Ethernet/Wi-Fi sem túnel reconhecido; ponte exige seleção confirmada e interface Tailscale. Mudanças transitórias entre consultas e redirecionamentos de outras VPNs/WFP podem escapar; desative outras VPNs para testar.

## Estatísticas

A média atual usa as últimas 30 tentativas do caminho. Sem resposta não vira zero; falhas ficam no cálculo de falhas aparentes. A janela é limpa ao mudar de bloco/caminho. O jitter combinado não cria pares entre blocos. A tabela guarda média, p95, jitter e falhas; a faixa de comparação mostra as médias iniciais.

A política 0.8.4 ordena os caminhos elegíveis pela menor média ICMP, sem o corte anterior de 5 ms / 5% e sem pontuação composta. A ponte precisa ter pelo menos dois blocos distintos, ao menos 10 respostas em cada bloco e 20 no total, com média de cada bloco menor que a menor média dos blocos diretos. Isso evita chamar um único período favorável de melhora repetida. Exige p95/falhas sem piora e tolera até +3 ms de jitter. Empates mantêm a direta. O destino e os ajustes precisam coincidir, e a interface direta deve permanecer consistente. Mudança do tipo de túnel entre blocos exclui a ponte. Não há significância estatística formal nem garantia de que a rota continuará melhor.

O relatório guarda o horário de conclusão e uma avaliação por ponte com o motivo da elegibilidade. O painel marca as rotas inativas como último teste, separado do monitor ao vivo. Selecionar Automático novamente refaz a comparação completa, com restauração da sessão anterior antes das novas medições.

Pontos de saída Windows Tailscale usam roteamento userspace. Para ICMP, o peer pode emitir sua própria sonda ao destino e responder à original quando sua sonda sucede. É diagnóstico aproximado do caminho completo, não RTT/perda dos datagramas UDP do jogo. A validação na telemetria interna do jogo é necessária.

## Observação de destinos

A sessão ETW tem nome único e é encerrada ao fim/cancelamento da observação. O provedor kernel é habilitado antes de acessar o consumidor. Os callbacks copiam somente metadados e não retêm objetos TraceEvent reutilizados. O acumulador e o mapa de PIDs são sincronizados; progresso da thread de observação é encaminhado ao contexto da interface.

Os perfis de nomes de processos são heurísticos. Destinos com pelo menos oito envios durante 18 segundos são priorizados por UDP e contagem. Voz e peers podem ser confundidos com servidor de partida. Esta versão não muda matchmaking nem segue todas as mudanças de IP sem nova ativação.

## Preparação e limites

Instalação é iniciada pelo botão do usuário via pacote `Tailscale.Tailscale` do winget. Login usa o fluxo oficial, abrindo apenas URLs HTTPS de `login.tailscale.com`. Convite e autorização inicial pertencem à conta do usuário; o app não fabrica credenciais nem compartilha senha. O manifesto pede elevação para ETW. Encerrar o processo à força não para o serviço de transporte: não há watchdog independente.

O modo automático usa saída geral de internet, sem roteamento por processo. Só testa direta e até oito pontes cadastradas/autorizadas; não possui relays públicos. Não escolhe região/servidor de jogo e só trata IPv4. A instalação/login reais e a ponte Cascavel/Acre ainda precisam de validação nos PCs de casa.

O diagnóstico manual e o comparador WireGuard da versão 0.1 permanecem em Avançado. A arquitetura desse modo está em ../ARCHITECTURE.md.

### Estado e preservação da ponte na 0.8.2

A oferta original é consultada antes de ativar. Se o usuário já a configurou no Tailscale, iniciar/parar o RotaGame preserva essa oferta e informa isso ao encerrar. Se o RotaGame introduziu a oferta, ele a retira ao parar e mantém registro para recuperação. Não se alteram as preferências de uma nova conta/identidade por causa de um registro antigo.

A confirmação pode levar alguns segundos. A tela principal permanece aberta: Confirmando a ponte, Ponte disponível e Permissão não confirmada são estados distintos. Verificar ponte abre a orientação oficial apenas quando o usuário escolhe. O painel não trata clicar em autorizar como prova de aprovação.

No host, amigo conectado não significa ponte em uso. O painel distingue conexão direta, teste da ponte e uso da ponte após escolha e sonda confirmada. A explicação da escolha é recebida do jogador, e a idade dos dados considera também o tempo desde a última mensagem. Conta conectada, aprovação, alcance do túnel e desempenho são verificações separadas.
## Correções e escolha de caminho na 0.8.3

O aviso de painel indisponível podia ser causado pela primeira criação da regra: Windows retorna HRESULT 0x80070002 quando a regra ainda não existe, traduzido pelo .NET para FileNotFoundException. O código tratava apenas COMException. A busca agora considera ausente apenas esse HRESULT; acesso negado e outros erros não são ocultados. A limpeza é idempotente. A regra segue limitada ao executável, IPv4 local do adaptador Tailscale, TCP 52941 e peers privados conhecidos; não se desativa o firewall nem se abrem portas no roteador.

No Acre, clique em qualquer parte do diagrama e escolha Direta, a ponte desejada ou Automático. Manual aplica o caminho escolhido sem exigir que ele vença a comparação. O diagrama identifica MANUAL depois de a seleção ser confirmada. O relatório distingue preferência manual, rota atual e ensaios anteriores. ICMP sem resposta sozinho não desfaz uma escolha manual; indisponibilidade do túnel pode retornar à direta. Automático refaz os testes. Trocas podem reconectar uma partida.

O seletor altera o caminho no computador que vai jogar; o host recebe a identificação do modo e acompanha os resultados. Não há controle remoto de rotas no protocolo de telemetria. Um parceiro único reconhecido no canal privado é salvo sem escolha manual, preservando a privacidade da descoberta. Use a mesma versão nos dois PCs.
