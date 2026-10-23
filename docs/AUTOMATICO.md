# Guia do modo automático — 0.6

## Biblioteca de jogos

Não há cartões de jogos pré-carregados: é necessário encontrar o executável instalado. Steam, Epic e os registros de instalação do Windows são consultados para os três perfis conhecidos. A biblioteca é revisada aproximadamente a cada 30 segundos quando o app está livre ou aguardando um jogo; instalações não reconhecidas podem ser adicionadas pelo `.exe`. O ícone de cada jogo vem desse arquivo. Remover um cartão da biblioteca não desinstala o jogo; o identificador fica oculto até adicioná-lo novamente.

Use **+ Adicionar jogo** e selecione o `.exe` da partida. O cadastro é persistido em `%LOCALAPPDATA%\RotaGame\games.json`, com cópia `.bak` ao atualizar. A busca no topo filtra os cartões. Selecionar um cartão limita a detecção; o botão de detecção automática considera todos os perfis, inclusive os cadastrados.

Um observador consulta os processos do Windows a cada dois segundos e mostra **Em execução** ou **Fechado**. Com a otimização ativada, o app aguarda um jogo da biblioteca abrir, reconhece o processo e aguarda tráfego antes de comparar. Se o jogo fechar, restaura a preferência anterior e espera o próximo. Não abre executáveis automaticamente. Se cadastrar um launcher, pode reconhecer somente o launcher, e não o processo real da partida; prefira o `.exe` do jogo.

O painel inclui um diagrama com a origem, os caminhos testados e o destino observado. Os números são o RTT completo de cada caminho; não são medições separadas de cada trecho. Só há nomes de pontes e latências após existirem resultados reais para elas. O servidor/região continua sob controle do jogo.

No diagrama, **Em uso** indica o caminho aplicado e aparece em verde, com brilho e pontos animados após respostas confirmadas. **Em teste** indica uma etapa da comparação, em azul. **Média ao vivo** usa a mesma janela do ping médio atual; **Comparação** usa os blocos anteriores daquele caminho. **Sem resposta** ou **Aguardando sonda** não são zero milissegundos. Quando a ponte é interrompida, o destaque muda para a direta; ao desativar, vira **Última rota**, sem animação de conexão ativa. As partículas são ilustrativas e não informam quantidade de tráfego. O app pausa essa animação ao ocultar ou minimizar a janela.

## Medidor ao vivo

**Ping médio atual** é a média dos RTTs recebidos nas últimas 30 tentativas do caminho atual, atualizada a cada sonda. Timeouts não viram zero. Variação é a média das diferenças absolutas entre respostas consecutivas; p95 é o percentil nearest rank. O gráfico acompanha as últimas 180 sondas, com lacunas para falhas. As médias de direta e caminho escolhido abaixo dos indicadores são da comparação inicial.

As sondas são ICMP, não o protocolo do jogo. ICMP pode ser filtrado ou limitado e pode receber prioridade diferente. Falhas do teste não comprovam perda dos pacotes do jogo. Confirme qualquer melhora na telemetria interna do jogo.

## Preparação inicial

A engrenagem abre **Conta e conexão** dentro do RotaGame. A aba acompanha o status a cada três segundos enquanto estiver visível. **Entrar ou criar conta** instala o componente se necessário e inicia a autorização oficial. **Instalar conexão** permite instalar separadamente, e **Atualizar status** refaz a consulta. Os botões de convite e aprovação ficam disponíveis após a conexão da conta.

O fluxo inclui os seguintes passos:

1. Instalação de Tailscale via `winget`, pacote `Tailscale.Tailscale`, iniciada pelo usuário. Se não houver gerenciador ou a instalação falhar, indica o [instalador oficial](https://tailscale.com/download/windows).
2. Entrada na conta pelo fluxo oficial do componente, com autorização no navegador.
3. Em Cascavel, na aba **Minha conta**, clique **Criar convite para meu amigo**. Em Users, use Invite users, escolha membro e copie o link para enviar ao amigo.
4. No Acre, abra **Aceitar convite**, cole o link de usuário `/uinv/…` e clique **Abrir e aceitar convite**. Depois de aceitar na página oficial, use **Já aceitei · conectar este PC** e selecione a rede convidada. Essa ação abre uma nova escolha de rede mesmo se o PC já estiver conectado a outra conta/rede; aceitar o convite no navegador sozinho não troca a rede do cliente.
5. Em Cascavel, abra **Autorizar ponte** e clique **Preparar este PC como ponte**. Se a autorização continuar pendente, clique **Abrir autorização oficial**: encontre o PC pelo nome exibido, use Edit route settings / Edit exit node, marque Use as exit node e salve. A aba verifica a permissão real a cada três segundos e mostra quando a ponte está ativa. Se a rede já tiver aprovação automática configurada, a confirmação aparece sem essa etapa manual.

Login, convite e autorização exigem interação do dono da conta. O programa não cria nem compartilha senhas e não inventa autorização. Nenhum arquivo de chave WireGuard é pedido. O serviço de transporte cuida do túnel; não é exigida configuração manual de roteador no fluxo inicial.

A identidade RotaGame está na aba, sem substituir a identificação do Google/Tailscale. O login segue no navegador oficial; convite e autorização ainda abrem as páginas da conta. Não há serviço próprio que elimine contas Tailscale, nem convite gerado silenciosamente pela API nesta versão. A operação de entrada bloqueia novas ativações até terminar. Fechar o aplicativo cancela os processos auxiliares pendentes da sessão de preparação.

No PC de Cascavel, também é possível usar **Oferecer este PC como ponte → Ativar ponte** na tela principal. Se faltar aprovação, o aplicativo mostra a aba **Autorizar ponte**, com o acesso à etapa oficial. Deixe-o ligado, conectado e sem suspender. Após a aprovação, o outro PC deve conseguir descobrir a oferta. Desativar retira a oferta desta sessão. Durante a preparação/oferta, a troca de conta ou rede fica bloqueada. O programa não instala nada no PC remoto automaticamente; a preparação deve ocorrer nos dois computadores.

## Busca e seleção

No computador do amigo, abra o jogo, entre em treino e clique **Ativar**, mantendo **Vou jogar neste PC**. O programa:

1. Observa por aproximadamente 18 segundos os metadados de envios de processos dos perfis conhecidos.
2. Escolhe um destino IPv4 público com atividade sustentada, priorizando UDP. O IPv4 fica fixo nessa ativação.
3. Descobre até oito pontes autorizadas e online na rede privada.
4. Tenta estabelecer ligação direta entre os PCs. Se uma ponte depender de intermediário DERP/peer relay, ela fica fora desta seleção inicial.
5. Mede direta, dois blocos de cada ponte elegível, e direta novamente.
6. Escolhe a direta ou uma ponte com vantagem suficiente e começa a monitorar.

São 24 sondas por bloco, intervalo mínimo 1 segundo, timeout 1 segundo, payload 32 bytes e 2 aquecimentos fora das estatísticas. Uma sondagem lenta não causa rajadas para compensar o atraso.

Cada caminho precisa ter ao menos 20 respostas. A ponte deve melhorar a média e reduzir a mediana por max(5 ms, 5% da direta), sem piorar p95/falhas e sem elevar jitter mais de 3 ms. A decisão é exploratória, sem cálculo de significância estatística. Se a evidência não bastar, mantém a direta. Resultados de alvos/ajustes distintos e mudança da interface direta não produzem escolha de ponte.

O app compara somente caminhos disponíveis: não tem servidores públicos próprios, não controla as rotas internas da operadora e não muda o matchmaking. A região/servidor continua sendo escolhido no jogo.

## Durante a partida

As trocas iniciais podem reconectar a sessão; faça a ativação em treino, antes da partida competitiva. Após escolher, esta versão mantém o caminho e monitora. Não faz alternância periódica para “perseguir” cada oscilação de ping. Se a ponte ficar indisponível, mudar para um intermediário ou houver cinco falhas consecutivas de sonda, tenta voltar à direta. Esse retorno também pode reconectar a partida.

A ponte é uma saída geral de internet. Navegação e downloads do computador do amigo também podem passar pela sua conexão; isso pode ocupar upload e aumentar latência. Esta versão não aplica roteamento por processo.

## Descoberta e limitações

Usa ETW, recurso nativo do Windows, com Microsoft TraceEvent. Copia PID, IP/porta, protocolo e contagem; não grava payload. Não injeta código no jogo nem instala um driver próprio de captura. O executável pede administrador para essa observação.

Os nomes dos processos são heurísticos: `Discovery`, `TheFinals`, `RocketLeague`, `GTA5` e `GTA5_Enhanced`. Atualizações podem mudar esses nomes. Um destino observado pode ser voz, outro jogador ou um serviço auxiliar. Isso não garante identificar o servidor da partida, especialmente no GTA Online.

O app fixa um destino por ativação e não acompanha todas as mudanças de IP dentro da sessão. Ao mudar de sessão/região, desative e ative novamente em um momento apropriado. Se não encontrar o processo/destino ou não houver respostas ICMP, informa a limitação; não substitui o destino do jogo por um site genérico para afirmar melhoria.

## Desativar e recuperar

Desativar para as sondas e restaura a preferência anterior de ponto de saída. Antes de cada mudança, há um registro em `%LOCALAPPDATA%\RotaGame\connection-recovery.json`. Um erro/cancelamento usa um prazo próprio para tentar restaurar; se falhar, o registro permanece e o botão permite repetir. Uma alteração feita posteriormente fora do programa é preservada.

Fechar normalmente também tenta restaurar. Se o processo for encerrado à força, o serviço de conexão pode manter o último caminho até reabrir o app ou alterá-lo no componente. Não há watchdog independente. A recuperação persistente cobre a seleção de saída no modo jogar; uma oferta de ponte após encerramento forçado deve ser desativada no app/componente.

## Referências oficiais

- [Tailscale: pontos de saída no Windows](https://tailscale.com/docs/features/exit-nodes?tab=windows).
- [Conexões diretas e intermediárias](https://tailscale.com/docs/reference/connection-types).
- [CLI](https://tailscale.com/docs/reference/tailscale-cli).
- [Roteamento userspace e ICMP](https://tailscale.com/kb/1177/kernel-vs-userspace-routers).
- [Microsoft TraceEvent](https://github.com/microsoft/perfview/blob/main/documentation/TraceEvent/TraceEventProgrammersGuide.md).
