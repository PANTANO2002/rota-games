# Arquitetura atual · 0.9.0 · PANT_CORE

OptimizerForm → GameLibrary / GameDiscovery → AutomaticOptimizer → WarpClient / WarpMeter → NetworkTracer / NetworkMap.

A abertura normal dispensa Tailscale, papéis, convites e ponte. LegacyRecovery usa o componente antigo somente quando existe diário de alteração pendente de uma versão anterior. Não instala nem autoriza uma nova conta.

## Detecção e comparação

A biblioteca exige executável existente; ícones vêm do arquivo. GameDiscovery usa metadados ETW por processo e prioriza UDP sustentado. Não lê credenciais ou conteúdo da partida. O IPv4 fica fixo durante a comparação.

AutomaticOptimizer mede direta / alternativa / alternativa / direta nas mesmas condições. AutomaticPolicy exige dois blocos distintos completos com dez respostas por bloco e melhora repetida na média, sem piorar p95 ou falhas, tolerando até +3 ms de jitter. Destino ou interface direta divergentes invalidam a escolha.

WarpMeter confirma o estado oficial antes/depois do bloco e consulta a interface para o próprio destino. Um rótulo WARP com interface física não é aceito. Uma alternativa que perde confirmação ou completa cinco sondas sem resposta tenta retornar à direta; ICMP filtrado permanece uma hipótese explícita.

O observador do destino acompanha o processo durante comparação e monitoramento. Outro IP precisa aparecer em duas novas janelas de tráfego para cancelar, restaurar e comparar o novo destino. Fechar o jogo restaura a conexão e volta à espera.

## Integração WARP

WarpClient encontra o CLI no diretório oficial Program Files e passa argumentos estruturados sem shell. Lê status, settings e registration show. Saída desconhecida, conta paga ou corporativa falham sem mudar a configuração. O registro é processado em memória; a identidade persistida é SHA256 do GUID, sem licença/chave.

O controle normal só executa connect/disconnect. Não altera modo, protocolo, DNS, organização ou licença. DNS-only não conta como túnel. A preparação explícita após aceitação do usuário pode executar registration new somente para uma resposta reconhecida de registro inexistente. Erro de serviço nunca autoriza apagar/recriar uma conta.

OfficialWarpSetup baixa o MSI por HTTPS do endpoint oficial e exige Authenticode válido da Cloudflare, Inc. antes de abrir a interface normal do instalador. O componente não é redistribuído. WarpConsentWindow identifica a Cloudflare, explica o efeito sobre todo o tráfego e oferece termos/privacidade com opção desmarcada. --accept-tos só é usado depois dessa escolha real.

Não seleciona servidores ou POPs. Usa o protocolo existente no componente, sem forçar MASQUE ou supor WireGuard.

## Recuperação

RouteLease grava warp-recovery.json antes da mutação: estado original, último aplicado, anterior e identidade do registro. Desativar, sair, mudar destino e atualizar restauram o estado original. Mudança externa é preservada; identidade diferente bloqueia reconectar outra conta e conserva o diário para revisão.

Diários antigos ficam separados. Falha/cancelamento não apaga recuperação. Atualização e fechamento normal aguardam restauração; encerramento forçado exige recuperação na próxima abertura.

## Diagrama e interface

NetworkTracer envia ICMP TTL 1..30 ao destino original, timeout 800 ms, conferindo a interface antes/depois. TTLExpired usa cronômetro, pois PingReply pode informar RTT zero. Nunca envia novas sondas aos endereços recebidos como saltos.

Timeout conserva posição, sem IP/RTT inventados. Só confirma chegada quando a resposta vem do destino. Troca de interface invalida saltos antigos. O trajeto ICMP pode diferir de UDP, omitir roteadores ou esconder o túnel. RTT acumulado não é atraso por segmento; não há geolocalização inferida.

NetworkMap distribui nós em linhas e aumenta o painel; o fluxo luminoso é ilustrativo e depende de amostra recente. Média e gráfico separam blocos de saídas diferentes.

## Atualizações

Velopack consulta a release estável pública de PANTANO2002/rota-games. O mesmo UpdateWorkflow da introdução chega à janela principal, preservando aviso e erro. Verifica a cada 15 minutos e pelo botão Atualizações; ao abrir pode aplicar automaticamente. Cancela tarefas e aguarda recuperação antes do reinício. O instalador inclui bootstrap administrativo e runtime.
