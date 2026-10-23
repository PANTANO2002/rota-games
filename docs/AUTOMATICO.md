# Guia do modo automático — 0.8

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

Escolha uma vez o papel deste PC na tela principal. **Este PC é a ponte** identifica Cascavel; **Vou jogar neste PC** identifica Acre. A escolha fica salva, sem tentar deduzir a cidade por IP.

**Ativar ponte** em Cascavel e **Ativar conexão** no Acre consultam o estado real. Conta já conectada não precisa de novo login. Quando faltar uma etapa, abre o assistente com apenas uma próxima ação. Após a entrada oficial no navegador, avança sozinho. Se os dois já têm acesso à ponte, não precisam repetir convite.

Para uma primeira conexão entre contas próprias, Cascavel prepara e aprova a ponte pela orientação mostrada; depois usa **Compartilhar minha ponte**. Na página oficial, encontre o nome exibido, use Share e marque **Allow use as exit node**. Envie o link. No Acre, **Colar convite → Aceitar convite e conectar** abre a aceitação oficial, sem trocar de rede. O programa acompanha a aparição da ponte. Convites antigos `/uinv/` continuam com uma etapa explícita de escolha da rede convidada.

A autorização inicial ainda é concedida pelo dono da conta na página oficial. Não é preciso configurar roteador, IP público ou chaves WireGuard. Tailscale já fornece o túnel. O app instala o componente oficial se necessário. As senhas ficam com o provedor; o RotaGame não cria cadastro próprio nem modifica a identidade do login Google/Tailscale.

Deixe os dois RotaGame na mesma versão, a partir de 0.8. O programa reconhece os papéis e salva o parceiro quando só há um candidato. Com mais de uma ponte, use **Escolher PC**. A ponte recebe os indicadores e o gráfico medidos no amigo. A tela identifica essa origem; amostras antigas não são apresentadas como ping atual. Desativar em Cascavel retira a oferta desta sessão. O PC da ponte deve permanecer ligado e conectado.

## Busca e seleção

No computador do amigo, abra o jogo, entre em treino e clique **Ativar**, mantendo **Vou jogar neste PC**. O programa:

1. Observa por aproximadamente 18 segundos os metadados de envios de processos dos perfis conhecidos.
2. Escolhe um destino IPv4 público com atividade sustentada, priorizando UDP. O IPv4 fica fixo nessa ativação.
3. Descobre até oito pontes autorizadas e online na rede privada.
4. Tenta estabelecer ligação direta entre os PCs. Pontes via DERP também são medidas e identificadas como intermediário; peer relay continua excluído.
5. Mede direta, dois blocos de cada ponte elegível, e direta novamente.
6. Escolhe a direta ou uma ponte com vantagem suficiente e começa a monitorar.

São 24 sondas por bloco, intervalo mínimo 1 segundo, timeout 1 segundo, payload 32 bytes e 2 aquecimentos fora das estatísticas. Uma sondagem lenta não causa rajadas para compensar o atraso.

Cada caminho precisa ter ao menos 20 respostas. A ponte deve melhorar a média e reduzir a mediana por max(5 ms, 5% da direta), sem piorar p95/falhas e sem elevar jitter mais de 3 ms. A decisão é exploratória, sem cálculo de significância estatística. Se a evidência não bastar, mantém a direta. Resultados de alvos/ajustes distintos e mudança da interface direta não produzem escolha de ponte.

O app compara somente caminhos disponíveis: não tem servidores públicos próprios, não controla as rotas internas da operadora e não muda o matchmaking. A região/servidor continua sendo escolhido no jogo.

## Durante a partida

As trocas iniciais podem reconectar a sessão; faça a ativação em treino, antes da partida competitiva. Após escolher, esta versão mantém o caminho e monitora. Não faz alternância periódica para “perseguir” cada oscilação de ping. Se a ponte ficar indisponível, mudar o tipo de túnel medido ou houver cinco falhas consecutivas de sonda, tenta voltar à direta. Esse retorno também pode reconectar a partida.

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
