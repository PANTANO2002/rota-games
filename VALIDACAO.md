# Validação da entrega 0.6 — 23/10/2026

## Diagrama animado e ping atual

**56/56 testes locais passaram** em `validation/release-06-tests.txt`. A publicação independente está registrada em `release-06-publish.txt`. O binário publicado passou pelos cinco modos de validação: `--diagram-smoke`, `--simple-smoke`, `--render-smoke`, `--account-smoke` e `--intro-smoke`.

O novo teste do diagrama executou ICMP real de loopback: dois blocos de quatro sondas e monitor de cinco sondas. O valor mostrado no nó foi comparado à média da fila do indicador atual. A captura de dois quadros confirmou mudança real dos pixels da animação. Ocultar, minimizar e desativar pararam seu temporizador; mostrar/restaurar retomaram quando havia resposta recente. Amostra antiga não foi tratada como ping ao vivo.

Os cenários controlados do desenho, identificados como dados simulados, verificaram três rotas, seleção da ponte, falha ICMP sem zero fictício, fingerprint divergente, adaptador Tailscale sem a palavra WireGuard, retorno à direta sem manter a ponte anterior destacada e limpeza ao mudar de sessão. Em 1600×860, 1340×860 e 1080×720, os caminhos mantiveram a altura necessária. A comparação dos pixels apresentados pelo Windows com uma repintura completa resultou em **0,0000% de diferença** nos três tamanhos, com a animação congelada para comparar o mesmo quadro. Evidência: `diagram-smoke.txt`, `interface-diagram-local.png`, `interface-diagram-*.png`.

A prévia `branding/RotaGame-diagrama.gif` contém 48 quadros, total de 2,4 segundos, em repetição. Seus valores são simulados e identificados no rodapé. Os dados de demonstração só são criados pelo modo explícito de teste; o uso normal recebe as rotas e estatísticas do medidor. O ping é ICMP do caminho completo, sem estimativas separadas de cada trecho; os pontos animados não são captura de pacotes do jogo.

A verificação geral de repintura repetiu 54 mudanças de tamanho e seis ciclos de maximizar/restaurar nas seis telas/cenários, todos com 0,0000% de diferença. A biblioteca vazia, quebra de cartões, ícones, recuperação, três abas de conta e controles da janela continuam passando. O carregamento com introdução levou 4,137 segundos, dentro de 3–5 segundos. Os logs e imagens sem versão no nome foram atualizados para esta entrega; as seções seguintes e os logs versionados preservam os registros históricos.

A ligação real entre Cascavel e Acre e o consentimento em contas reais continuam pendentes. Nenhum transporte ou conta real foi instalado/conectado por esses testes.

## Histórico da versão 0.5

## Repintura, controles da janela e conta

**56/56 testes passaram** com integração local em `validation/release-05-tests.txt`. O executável publicado passou pelos modos `--simple-smoke`, `--render-smoke`, `--account-smoke` e `--intro-smoke`. O log da publicação está em `release-05-publish.txt`.

A validação de apresentação captura os pixels da janela real pelo Windows, sem usar DrawToBitmap como única prova. Exercita 54 mudanças de tamanho, seis ciclos de maximizar/restaurar, as três abas de conta, biblioteca e rolagem. Compara o estado apresentado com uma repintura completa do mesmo estado, admitindo até 0,3% de diferença para ruído visual; os seis cenários finais apresentaram **0,0000%** de diferença. Os controles reais de minimizar, maximizar e restaurar foram acionados e os estados da janela conferidos. Resultados e imagens estão em `rendering-smoke.txt` e `*-resize-presented.png` / `*-resize-refreshed.png`. Os layouts, tipografia e ícones foram inspecionados. Não é uma certificação de todos os drivers, DPIs ou sessões remotas.

As três abas de conta foram exercitadas com serviço controlado: sem componente, instalação concluída, entrada, conta conectada, computador do amigo, validação do link de convite de usuário, escolha explícita da rede mesmo já conectado, ponte encontrada, autorização pendente e aprovação confirmada pelo serviço. O teste confere que abrir a página oficial não marca aprovação e que oferecer a ponte bloqueia a troca de conta/rede. Intercepta os cinco links de suas etapas: não instala transporte, não abre sites e não cria contas. `account-smoke.txt` registra o resultado. As imagens `interface-account-test.png`, `interface-invite-test.png` e `interface-authorization-*-test.png` identificam os dados de teste. Instalação/login reais, consentimento do provedor, criação/aceite de convite e aprovação administrativa ainda precisam ser concluídos pelos usuários; não foram validados numa conta real.

A introdução foi executada no binário publicado com carregamento controlado do painel em paralelo. O formulário principal estava preparado ao terminar e a duração observada foi **4,090 segundos**, dentro do intervalo solicitado de 3–5 segundos. `intro-smoke.txt` registra a duração. A prévia `branding/RotaGame-intro.gif` contém 50 quadros de 80 ms, total de quatro segundos. Barlow e Rajdhani são recursos incorporados e foram carregadas no processo sem instalação de fontes no Windows; os SVGs de marca usam contornos.

Foi mantida a biblioteca sem jogos pré-carregados, a quebra dos cartões em novas linhas e a medição real de loopback. O Laboratório foi removido da navegação, mantendo os indicadores na tela principal. As imagens de nomes compartilhados foram atualizadas pela validação 0.5; os registros versionados abaixo são históricos. A ponte Cascavel/Acre continua pendente.

## Histórico da versão 0.4

## Instalações, barras e identidade visual

**59/59 testes passaram** em `validation/release-04-tests.txt`, incluindo ICMP local/externo e descoberta ETW real. A execução com integração local passou 56/56 antes dos testes externos. As novas verificações cobrem biblioteca vazia, ausência de executável apesar de existir manifesto, biblioteca Steam em outro caminho, instalação Epic, remoção manual, retorno após reinstalação e ícones provenientes dos executáveis.

O executável publicado foi validado em 1600×860, 1340×860, 1120×780 e 1080×720. Biblioteca vazia e quatro cartões que cabem usam uma linha. Com cinco cartões, somente o quinto vai para a linha seguinte, com a mesma largura dos anteriores; a biblioteca maior cria várias linhas. Não há barra horizontal ou vertical interna na biblioteca. Foi verificada também a visibilidade real dos controles após redimensionamentos sucessivos. O painel reduz os espaços até seu limite mínimo e só então exibe rolagem vertical, incluindo o espaço necessário às novas linhas de cartões. Arraste, roda e Home foram exercitados na barra do painel.

Evidências atuais: `interface-library.png` mostra a biblioteca vazia e a marca; `interface-style.png` mostra reconhecimento do executável controlado e extração do ícone; `interface-wrapped.png` mostra só o cartão excedente na segunda linha; `interface-scroll.png` usa perfis de teste para mostrar várias linhas em uma janela pequena; `interface-compact.png` mostra o painel compacto sem barras desnecessárias. `interface-automatic.png` e o compacto contêm ICMP real de loopback (0 ms locais, sem relação com a internet do Acre). `simple-smoke.txt` registra o resultado. Os nomes “Teste de layout” são fixtures exclusivas do modo explícito de validação e não aparecem em uso normal.

As instalações Steam/Epic foram verificadas com metadados e arquivos controlados; não foram instalados jogos nesta máquina. O cadastro não é uma certificação de integridade do executável. A marca própria é fornecida em `branding/` como SVG/PNG/ICO; o ícone foi incorporado ao executável e às janelas. A ponte Cascavel/Acre e a compatibilidade com partidas reais continuam pendentes.

## Histórico da versão 0.3

Os arquivos de imagem e `simple-smoke.txt` com nomes compartilhados foram atualizados pela validação 0.4 descrita acima; os logs com versão no nome preservam os testes anteriores.

## Biblioteca e novo painel

**55/55 testes passaram** na execução completa com descoberta ETW e sondas externas. O log está em `validation/release-03-tests.txt`. A execução padrão com integração local passou 52/52 em `library-tests.txt`.

Novas verificações: persistência do cadastro de executável, recarga, atualização sem duplicata, remoção preservando perfis iniciais, rejeição de arquivos inválidos e reconhecimento real do início/fim de um processo controlado. A integração ETW agora usa um nome de processo personalizado, como os jogos cadastrados, para identificar 12 envios UDP reais associados ao PID correto.

O executável publicado passou pelo fluxo da interface: cadastro do próprio executável em uma biblioteca isolada, reconhecimento de seu PID, ativação antes de um processo iniciar, estado de espera e cancelamento, duas medições locais 4/4, monitor 5/5, ping médio e desativação/restauração. O teste não altera a rede. Evidência: `simple-smoke.txt`.

O painel foi renderizado e inspecionado nos formatos normal e compacto, incluindo degradês e brilho dos controles. `interface-library.png` mostra a tela sem medições, `interface-style.png` mostra um jogo selecionado, `interface-automatic.png` mostra ICMP real de loopback com a chave ligada, e `interface-compact.png` mostra a mesma tela em janela menor. Os 0 ms do teste local não representam internet, jogo ou Acre. O diagrama mostra apenas a direta porque não há uma ponte real cadastrada neste ambiente.

Continuam pendentes a instalação/login real do transporte e a ponte Cascavel/Acre. O reconhecimento de um executável não garante que todo destino observado seja o servidor da partida. Não há certificação de compatibilidade com todos os jogos, launchers ou anti-cheats.

## Histórico da versão 0.2

## Versão automática atual

**49/49 testes passaram** na execução com `--integration --discovery-integration --public-probe`. O log está em `validation/automatic-tests.txt`.

Foram verificados: escolha de ponte mais rápida, rejeição de piora de média/p95/jitter/falhas, retenção da direta sem evidência, vários candidatos, cancelamento, restauração após falha parcial, preservação de alterações externas, recuperação persistente, contrato dos comandos e parsing do estado do transporte.

Integrações reais neste ambiente: ICMP local e externo, leitura da rota, execução do medidor automático direto e **descoberta ETW de 12 envios UDP controlados do PID correto**. A descoberta registra somente metadados; não foi usada para capturar uma partida real.

O executável publicado passou por `--simple-smoke`: abriu a interface automática, fez dois blocos locais de 4/4 respostas e cinco sondas de monitor, atualizou o ping médio, salvou resultado e executou Desativar/restauração. `validation/interface-automatic.png` é a renderização da janela real em um teste local; os 0 ms mostrados são de loopback, não da internet do Acre. Evidências: `simple-smoke.txt`, `simple-loopback.json` e `automatic-publish.txt`.

**Não validado:** instalação real do Tailscale, login/convite/autorização em uma conta, controle de uma ponte real, rede Cascavel/Acre, jogos em execução ou redução de ping. As operações de seleção/anúncio do transporte foram implementadas conforme documentação oficial; o contrato de seleção foi exercitado com fixtures. O componente não foi instalado/conectado nesta máquina de trabalho, pois não é o PC de casa do usuário. A primeira preparação e o teste ponta a ponta precisam ocorrer nos PCs reais.

O teste da interface usa adaptadores de seleção controlados e ICMP real de loopback. Não instala, anuncia ou seleciona uma ponte real. A integração ETW explícita envia 12 datagramas UDP de 2 bytes a `1.1.1.1:9`; esse tráfego só valida a associação de PID/destino.

## Histórico da versão 0.1, modo avançado

## Resultado

Protótipo Windows x64 compilado em Release e publicado como executável independente com runtime. **Compilação sem erros e sem avisos.** Os 18 testes determinísticos e a integração local passaram; o teste externo adicional também passou (**20/20** na execução com ambos os testes de integração).

O fluxo real da interface foi executado com o binário publicado: janela criada, oito sondas ICMP respondidas em loopback, gráfico/tabela atualizados, JSON e CSV exportados e imagem salva. A imagem foi inspecionada e os problemas de contraste do seletor e atualização final de estado foram corrigidos antes da publicação final.

## Evidências incluídas

| Arquivo em `validation/` | Evidência |
|---|---|
| `build-and-tests.txt` | Compilação e execução final dos 18 testes determinísticos + 1 integração local. |
| `ui-smoke.txt` | Resultado do fluxo da interface com o executável publicado. |
| `ui-loopback.json` / `.csv` | Oito sondas reais ao loopback, com configurações de validação: intervalo 100 ms, timeout 300 ms e 1 aquecimento. Esses valores diferem dos padrões normais da interface. |
| `interface.png` | Janela real renderizada pelo aplicativo após o diagnóstico local; não é mockup. |
| `external-diagnostic.json` / `.csv` | Doze sondas reais a `1.1.1.1` desta máquina, após 1 aquecimento. |
| `independent-route-check.json` | Consulta separada com `Find-NetRoute`, confirmando interface 10 / Ethernet, prefixo padrão e próximo salto `192.168.0.1`, coerentes com a evidência nativa do app. |

No teste externo: **12/12 respostas, mediana 27 ms, p95 27 ms e 0% de falhas ICMP**. É uma observação curta deste ambiente, **não do Acre, dos jogos ou de uma rota por relay**. Não deve ser usada como referência de melhoria.

## Cenários cobertos pelos testes

- Média, mediana, p95 nearest rank e jitter de RTT.
- Lacunas de resposta não viram 0 ms e não unem pares de jitter.
- Erros locais separados de perda aparente; bloco localmente inválido não produz conclusão.
- 100% de falhas com latência desconhecida.
- Jitter combinado sem atravessar fronteiras de blocos.
- A–B–B–A válido com sinais corretos nos deltas.
- Sessões incompletas, canceladas, com alvos distintos ou respostas insuficientes.
- Rota direta indevidamente marcada como relay.
- Aquecimento fora das estatísticas.
- Recusa de relay antes de enviar se não houver rota WireGuard.
- Mudança de rota durante a sonda invalida o bloco.
- Cancelamento preserva resultados parciais.
- Entradas de destino inválidas, IPv6 e multicast rejeitados.
- Exportação JSON com sondas e rota; CSV com aspas e proteção contra fórmulas.
- Consulta real à rota Windows e ICMP de loopback.
- ICMP externo com exportação da evidência.

## Não validado nesta entrega

- **Relay WireGuard ponta a ponta:** não há VPS fornecida. Os cenários WireGuard dos testes usam fixtures sintéticas; não são prova de um túnel real.
- O script Linux foi preparado e revisado, mas não executado numa VPS. Encaminhamento, NAT, regras do provedor/firewall, MTU e teardown precisam ser verificados no servidor escolhido.
- Rede do amigo no Acre, jogos em execução, servidores reais e qualquer redução de ping/perda.
- Todas as versões/configurações de Windows, DPI, ambientes ARM64, anti-cheats e assinatura/distribuição pública do executável.

O próximo teste objetivo é coletar baselines do computador no Acre, identificar os destinos e só então medir um relay temporário, seguindo o roteiro do README.


## Versão 0.7.0 · PANT_CORE

- 56/56 testes de conexão, estatísticas, recuperação e biblioteca passaram, incluindo ICMP local real.
- 12 verificações do atualizador: abertura automática, aviso durante uso, Depois, versão posterior, erros, cancelamento, preparação e interface.
- Velopack descobriu e baixou o pacote 0.7.0 real; uma cópia adulterada foi rejeitada por checksum.
- Uma instalação portátil isolada de teste 0.6.9 aplicou 0.7.0, reiniciou e confirmou versão 0.7.0.0 e CompanyName PANT_CORE. Nenhuma rota ou instalação normal do usuário foi alterada nesse teste.
- O instalador administrativo verificou o payload incorporado sem instalar no perfil do usuário. O manifesto pede confirmação UAC; não há certificado Authenticode.
- Quatro abas de conta com medição do texto e ações; convite com clipboard isolado, reconhecimento e aprovação controlados.
- Renderização: zero diferença nos sete cenários depois de 63 mudanças de tamanho e sete ciclos maximizar/restaurar. Balões de ajuda são desabilitados somente durante a comparação de pixels.
- Smoke da tela principal, diagrama com ICMP local e introdução passaram. A assinatura PANT_CORE aparece no rodapé, na introdução e nos metadados do EXE.

A publicação online e a consulta ao feed GitHub são verificadas separadamente após criar a Release. Os testes não comprovam melhora nos jogos ou funcionamento da ponte entre os PCs reais.

Publicação concluída: https://github.com/PANTANO2002/rota-games/releases/tag/v0.7.0. A rotina do GitHub terminou com sucesso; GithubSource sem credenciais descobriu e baixou 0.7.0, validando o pacote. O link fixo do instalador respondeu HTTP 200. Fonte pública expandida em src/, tests/, docs/, installer/, branding/ e licenses/.


## Versão 0.8.0 · conexão e painel dos dois PCs

- 74/74 testes passaram: papel e parceiro persistidos, próximos passos derivados do estado, links de compartilhamento e convites antigos, login concluído pelo serviço mesmo com CLI pendente, limites/rejeições do protocolo privado, dados reais ICMP transportados por TCP local, seleção de ponte DERP com os mesmos limites e invalidação ao mudar de tipo de túnel.
- `--devices-smoke`: painel de jogador produz medições ICMP reais locais; a ponte recebe por TCP e exibe exatamente a média, gráfico, diagrama e comparação recebidos. Indicadores identificam PC do amigo. Dados antigos perdem o estado atual, e a desconexão limpa os valores. Nova janela preserva papel e parceiro.
- `--account-smoke`: assistente avança após login, permissão e descoberta; aceita /admin/invite/ sem trocar rede e rejeita hosts estranhos. Sem conta ou rede real alterada.
- `--render-smoke`: sete cenários, 63 mudanças de tamanho e sete ciclos maximizar/restaurar; incluiu escolha de papel, compartilhar, aceitar e permissão. Pixels apresentados iguais à repintura, sem rastros antigos. Botões nativos de minimizar/maximizar/restaurar passaram.
- `--simple-smoke` e `--diagram-smoke`: ICMP local real, biblioteca vazia, cartões distribuídos e três rotas em 1600/1340/1080 px. Comparação de pixels do diagrama: 0,0000% nos três tamanhos (animação congelada e tooltips desativados só no teste para comparar o mesmo quadro).
- `--update-smoke`: aviso no app aberto, adiamento, aplicação automática na abertura, cancelamento, erros e restauração prévia continuam passando.

O diagnóstico somente de leitura encontrou um peer aprovado online acessível por DERP, que a 0.7 excluía. A 0.8 inclui esse caminho na comparação; isso não demonstra melhora de ping. A nova versão ainda não foi testada entre os PCs reais de Acre e Cascavel. Não se alterou a configuração Tailscale nem se abriu firewall real durante os testes locais de telemetria. Os testes de rede usaram loopback explicitamente habilitado, proibido no modo de produção.
O instalador 0.8.0 passou na verificação do payload incorporado. O pacote real foi descoberto/baixado pelo atualizador e uma cópia adulterada foi rejeitada por checksum. Uma cópia portátil isolada de 0.7.0 aplicou 0.8.0 e reiniciou confirmando versão 0.8.0, sem alterar a instalação normal ou rotas do usuário.
## Versão 0.8.1 · verificação de túnel intermediário

A CLI Tailscale usa por padrão until-direct=true e pode retornar erro após respostas DERP. A descoberta agora envia --until-direct=false, para aceitar o caminho alcançável e medi-lo. A fixture reproduz o retorno de erro sem a opção e exige a opção para a ponte DERP entrar nos candidatos. Os 74 testes passaram novamente. Verificação somente de leitura com um peer real aprovado retornou resposta e código zero com a opção; naquele momento o link já era direto. Não se afirma teste de jogo real ou comparação real por ponte a partir desse ping.
## Versão 0.8.2 · oferta, confirmação e uso da ponte

81/81 testes passaram. Novos cenários cobrem três ciclos de ativar/parar preservando uma oferta manual, recuperação após alteração parcial, proteção contra mudança de identidade e manutenção do registro quando não há conexão suficiente para restaurar.

O teste integrado da interface ativou/parou a ponte controlada três vezes sem reescrever a oferta nem abrir autorização. Verificou espera inicial, confirmação tardia, permissão não confirmada sem navegação forçada e retirada apenas da oferta criada pela sessão. A interface diferenciou amigo na direta, testando esta ponte e usando esta ponte. Os dois últimos estados usam fixtures explicitamente controladas; o transporte TCP de medições e o ICMP locais foram reais. Não é uma prova de jogo real entre Acre e Cascavel.

Os cinco modos simple/devices/account/render/update passaram. A interface permaneceu sem rastros após 63 redimensionamentos e sete ciclos maximizar/restaurar; as quatro etapas do assistente mantiveram 0,0000% de diferença entre pixels apresentados e repintura completa. Os números locais não são o ping do Acre.

## Versão 0.8.3 · reconhecimento, painel e escolha de caminho

- 86/86 testes locais passaram, incluindo integração ICMP e o teste opcional de firewall Windows. A execução padrão de CI, sem esse teste que exige Tailscale e administrador, contém 85 testes.
- O Windows desta máquina reproduziu a primeira consulta a uma regra ausente: HRESULT 0x80070002 convertido em FileNotFoundException. A correção reconhece somente esse código como ausência; acesso negado e outros erros continuam visíveis.
- Uma regra temporária exclusiva do teste foi criada, relida, reutilizada e removida. Ficou limitada ao executável de teste, TCP 52941 e ao próprio endereço privado do adaptador Tailscale como origem e destino. Um servidor TCP real recebeu nesse endereço. A limpeza confirmou a ausência da regra. Nenhuma preferência ou rota Tailscale foi alterada.
- Dois perfis novos, sem parceiro escolhido, reconheceram os papéis jogador/ponte por TCP local real e transportaram uma medição ICMP local real. Mais de uma ponte elegível exige escolha, e um parceiro já salvo é preservado. Isso não comprova recebimento entre duas máquinas físicas.
- Os sete modos de interface simple/devices/paths/account/render/diagram/update passaram. O teste do seletor clicou na ponte durante a comparação, manteve a ativação, exibiu escolha manual sem declarar vencedor automático, preservou a escolha após cinco timeouts ICMP, trocou para direta e voltou ao automático. Também verificou restauração após alteração parcial. Esses caminhos e pings do teste de seleção são simulados e identificados como tal nas imagens.
- O instalador administrativo verificou seu payload incorporado. O pacote 0.8.3 real foi descoberto e baixado; a cópia adulterada foi rejeitada por checksum. Uma cópia portátil isolada de 0.8.2 aplicou 0.8.3 e reiniciou confirmando a versão, sem modificar a instalação normal. Esse teste usa preparação controlada e não valida a rotina antiga de limpeza do painel na instalação comum de 0.8.2; para esta correção recomenda-se usar o instalador nos dois PCs.

Ainda falta testar a versão nos PCs reais de Acre e Cascavel, com seus firewalls, rede compartilhada e um jogo em execução. Nenhum resultado local demonstra redução de ping nesses computadores.
