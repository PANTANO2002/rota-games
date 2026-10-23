# RotaGame 0.8 — PANT_CORE

**Autoria e assinatura: PANT_CORE.** Otimizador experimental de caminhos e diagnóstico para jogos no Windows 11 x64. Não garante redução de ping.

Use **RotaGame-Setup.exe** para instalar. O instalador pede a autorização de administrador pelo Windows e cria atalhos; o runtime está incluído. Também existe **RotaGame-win-Portable.zip**: extraia todo o conteúdo e abra RotaGame.exe na pasta extraída. A autorização de administrador do Windows é necessária à descoberta automática dos destinos do jogo; não substitui a autorização da conta Tailscale.

Canal de versões: [Releases](https://github.com/PANTANO2002/rota-games/releases). Endereço fixo do instalador: [RotaGame-Setup.exe](https://github.com/PANTANO2002/rota-games/releases/latest/download/RotaGame-Setup.exe). As novas versões são distribuídas pelos assets das Releases.

## Atualizações automáticas

O atualizador está embutido. **Ao abrir**, procura uma versão posterior no canal GitHub e, se encontrada, baixa, confere o pacote, encerra a conexão do RotaGame e reinicia na nova versão. **Com o app aberto**, verifica a cada 15 minutos e mostra **Atualizar agora / Depois**, sem reiniciar até a escolha do usuário. A seta circular lateral também permite verificar e retomar uma atualização adiada.

Sem internet ou sem canal publicado, continua na versão atual e informa a falha de verificação. Não informa “versão mais recente” quando a consulta falha. A biblioteca e as configurações ficam fora da pasta do aplicativo. Se não conseguir restaurar uma rota alterada pelo app, não agenda a reinicialização.

Quem usa 0.6 ou anterior precisa instalar esta versão uma vez. O executável solto fora da instalação/pacote portátil completo não recebe atualizações. A abertura normal mantém a introdução de cerca de 4 segundos; a busca/download de uma atualização pode prolongar a espera, com o estado mostrado na tela.

Veja [publicação e atualização](docs/ATUALIZACOES.md). PANT_CORE é a assinatura de autoria; o instalador ainda não tem certificado digital Authenticode.

A tela mostra **ping médio atual**, atualizado a cada sonda, variação, p95, falhas do teste e gráfico. A média considera as últimas 30 sondas do caminho atual; sondas sem resposta não viram zero. Os valores são diagnósticos ICMP e podem diferir do contador do jogo.

O novo painel usa cartões de jogos, navegação lateral, chave ON/OFF, busca da biblioteca e diagrama dos caminhos realmente medidos. Os botões têm degradê e brilho suave, com destaque ao passar o mouse e ao selecionar; a chave ligada ganha brilho verde. O visual foi inspirado nas referências fornecidas; o produto mantém identidade própria RotaGame e não incorpora a rede de servidores do ExitLag.

Na versão **0.6**, o diagrama ganhou cartões com degradê, brilho nos nós e conexões curvas com pontos animados. A rota **Em uso** fica verde; durante a comparação, a rota **Em teste** recebe destaque azul. As outras mostram a média da **Comparação**, sem fingir que todas são medidas simultaneamente. O caminho em uso mostra a mesma média ICMP atual do indicador principal; timeout aparece como **Sem resposta**, sem substituir por zero. Se a ponte falhar e o app voltar à direta, o destaque acompanha essa mudança. Ao desativar, o diagrama identifica a última rota e os resultados anteriores, sem continuar mostrando uma conexão em uso.

Até três caminhos ficam visíveis, priorizando a direta e o caminho atual; a tabela mantém os demais resultados. O painel reserva altura para os caminhos adicionais e usa a barra do app quando necessário. As luzes ilustram o fluxo: não são pacotes capturados nem latências de trechos separados. A animação para ao ocultar/minimizar, desativar ou ficar sem uma sonda recente e confirmada. A prévia `branding/RotaGame-diagrama.gif` usa dados simulados identificados na própria imagem; o uso normal só exibe dados recebidos do medidor.

A abertura mostra a logo sendo desenhada, o nome e pontos de uma conexão animada por cerca de **4 segundos**, enquanto prepara a biblioteca em segundo plano. Pode pular quando o painel estiver pronto. Os títulos e números usam Rajdhani; os textos e botões usam Barlow. As fontes acompanham o executável e são carregadas apenas pelo processo, sem instalação no Windows. A prévia está em `branding/RotaGame-intro.gif`.

## Adicionar e reconhecer jogos

A biblioteca começa vazia quando não há instalações encontradas. Os três perfis conhecidos (The Finals, Rocket League e GTA V/Online) só aparecem se o aplicativo encontrar seu executável instalado, usando registros locais de Steam, Epic ou Windows. A lista é atualizada aproximadamente a cada 30 segundos enquanto não há uma partida sendo analisada. Outras instalações podem ser adicionadas manualmente; a detecção não cobre todos os jogos e launchers. Um jogo removido do disco deixa de aparecer na atualização seguinte.

Clique em **+ Adicionar jogo** ou no cartão **Adicionar jogo** e escolha o arquivo `.exe` da partida. O cadastro fica salvo para as próximas aberturas. Se possível, selecione o executável do jogo, evitando launchers que abrem outro processo.

Cada cartão usa o ícone extraído do executável. A marca e o ícone próprios do RotaGame estão em `branding/`, com SVG, PNG e ICO de vários tamanhos, e o ícone está incorporado no executável e nas janelas.

Os cartões distribuem a largura disponível. Quando não couberem numa linha, somente os cartões excedentes vão para a linha de baixo, preservando a largura dos demais. Não há rolagem horizontal na biblioteca. A tela se ajusta à altura disponível e só mostra sua barra vertical quando atinge o limite de redução ou precisa acomodar mais linhas de jogos. As barras são controles próprios com trilho arredondado, degradê e brilho nos estados de interação, operáveis por arraste, clique, roda e teclado.

Os cartões mostram **Em execução** ou **Fechado**. O programa verifica os processos a cada dois segundos, inclusive quando a otimização está desligada. A busca no topo filtra os jogos cadastrados. Clique em um cartão para limitar a detecção àquele jogo ou em **Automático · todos os jogos** para observar a biblioteca toda. Jogos adicionados podem ser removidos pelo menu de botão direito no cartão, com a otimização desligada.

Com **Ativar** ligado, o programa pode ficar aguardando o jogo iniciar: quando o reconhece, espera tráfego e começa a análise. Quando o jogo fecha, restaura a conexão anterior e volta a aguardar outro jogo da biblioteca. **Adicionar o executável não inicia o jogo nem cria uma conexão especial com o servidor**; o jogo deve ser aberto normalmente.

## Conectar os dois PCs

Instale a mesma versão nos dois PCs. Escolha uma vez na tela principal:

1. **Cascavel: Este PC é a ponte → Ativar ponte.** Se faltar login ou permissão, o assistente mostra apenas a próxima ação e avança quando o serviço confirma.
2. **Acre: Vou jogar neste PC → Ativar conexão.** Entre na própria conta. Se a ponte já estiver aprovada e visível, não precisa repetir convite.
3. Se ainda não houver acesso, em Cascavel siga **Compartilhar minha ponte**. Na página oficial, localize o nome exibido, escolha Share e marque **Allow use as exit node**. Envie o link. No Acre, use **Colar convite → Aceitar convite e conectar** e aceite na página oficial. O app procura a ponte automaticamente.

O novo fluxo compartilha a máquina, sem pedir ao amigo que troque de rede. Links antigos de convite de usuário continuam aceitos e têm uma etapa explícita de conexão à rede convidada. A função e a identificação do outro PC ficam salvas. Não se deduz a cidade pelo IP: a escolha identifica qual PC oferece a ponte.

A entrada continua no navegador oficial do Tailscale/Google. O programa confirma o estado real e sai da etapa de login mesmo se o auxiliar ainda estiver esperando. Ele não coleta senha nem cria uma tela Google falsa. Sem um serviço próprio de provisionamento, o dono precisa conceder a autorização inicial na página oficial. Não é necessário digitar IPv4 público, abrir porta no roteador ou configurar WireGuard manualmente; Tailscale já fornece esse túnel.

Os RotaGame reconhecem os papéis pelo canal privado. Quando existe uma única ponte RotaGame, o parceiro é salvo; com várias, use **Escolher PC**. As medições completas só vão para o parceiro selecionado. Em Cascavel, os indicadores dizem **PC do amigo**: são os valores medidos no Acre, não um ping local inventado. Se a comunicação ou a sonda ficar antiga, o app retira os valores atuais. Ambos precisam usar 0.8 ou posterior.

## Para jogar

1. No RotaGame do Acre, escolha **Vou jogar neste PC** e clique **Ativar conexão**. Pode fazer isso antes de abrir o jogo.
2. Abra o jogo cadastrado e entre em treino para gerar tráfego. Ele será reconhecido automaticamente.
3. Aguarde a descoberta e a comparação da direta com as pontes disponíveis, normalmente alguns minutos.
4. Acompanhe o **ping médio atual** e as médias da comparação inicial.
5. Clique **Desativar** para parar e restaurar a preferência de conexão anterior.

As trocas iniciais podem reconectar a sessão. Ative em treino, antes da partida competitiva. Depois de escolher, o programa mantém o caminho e o monitora; não alterna pontes a cada oscilação. Se não houver melhora suficiente, mantém a direta. Se a ponte falhar, tenta retornar à direta.

O RotaGame de Cascavel mostra os valores recebidos do Acre e distingue conectado, ponte disponível e caminho escolhido. Conta conectada sozinha não prova que a ponte está autorizada. Quando somente a direta for medida, o motivo aparece no resultado. Pontes com intermediário DERP também são comparadas; só são escolhidas se passarem pelos mesmos limites de qualidade.

O PC de casa precisa permanecer ligado, conectado e sem suspender. A ponte usa saída geral de internet: quando escolhida, navegação e downloads do amigo também podem passar por sua conexão. Esta versão não separa tráfego por aplicativo.

## Limites e estado da entrega

“Melhor caminho” significa o melhor entre a direta e as pontes cadastradas, autorizadas e medidas nessa sessão. Não há rede pública própria de relays nem troca automática de servidor/região do jogo.

A descoberta usa metadados por processo e perfis heurísticos dos três jogos. Pode observar voz ou outro jogador, especialmente em GTA Online. Um destino sem resposta ICMP impede uma conclusão confiável. Ao mudar de sessão/região, desative e ative novamente em treino.

**Esta correção ainda não foi validada entre Acre e Cascavel.** Foram testados o protocolo dos painéis com ICMP e TCP reais em loopback, os estados de login, o seletor de rotas e a interface. O diagnóstico local também confirmou que um peer aprovado pode estar online por intermediário; a versão 0.8 passa a medir esse caminho, sem presumir que melhora o ping.

Leia o [guia do modo automático](docs/AUTOMATICO.md), a [arquitetura atual](docs/ARQUITETURA-AUTO.md), a [validação](VALIDACAO.md) e os [componentes externos](THIRD_PARTY.md). O Laboratório foi retirado da navegação: as medições e a comparação usadas para jogar estão na tela principal. O diagnóstico manual anterior fica apenas como ferramenta de desenvolvimento pelo argumento `--advanced`, documentado em docs/DIAGNOSTICO-AVANCADO.md.

## Desenvolvimento

Requer Windows x64 e SDK .NET 10. A descoberta usa Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0.

    .\build.ps1
    dotnet run --project tests/RotaGame.Tests -c Release -- --integration

Testes externos e descoberta ETW explícita, como administrador:

    dotnet run --project tests/RotaGame.Tests -c Release -- --integration --discovery-integration --public-probe

Smoke local da interface publicada (não muda a rede):

    .\app\RotaGame.exe --simple-smoke .\validation

Validação de pixels realmente apresentados pelo Windows após redimensionar (abre temporariamente a janela de teste):

    .\app\RotaGame.exe --render-smoke .\validation

Validação controlada da aba de conta, sem abrir sites, instalar Tailscale ou criar uma conta real:

    .\app\RotaGame.exe --account-smoke .\validation

Validação do carregamento durante a introdução, duração de 3–5 segundos e exportação dos quadros da animação:

    .\app\RotaGame.exe --intro-smoke .\validation-intro

Validação do diagrama: ICMP local real, média atual, movimento, estados de falha/retorno à direta e prévia visual controlada, identificada como simulação:

    .\app\RotaGame.exe --diagram-smoke .\validation-diagram

Projeto independente, sem afiliação ao ExitLag, Tailscale ou aos jogos citados. Sem promessa de redução de ping.

### Estado e preservação da ponte na 0.8.2

A oferta original é consultada antes de ativar. Se o usuário já a configurou no Tailscale, iniciar/parar o RotaGame preserva essa oferta e informa isso ao encerrar. Se o RotaGame introduziu a oferta, ele a retira ao parar e mantém registro para recuperação. Não se alteram as preferências de uma nova conta/identidade por causa de um registro antigo.

A confirmação pode levar alguns segundos. A tela principal permanece aberta: Confirmando a ponte, Ponte disponível e Permissão não confirmada são estados distintos. Verificar ponte abre a orientação oficial apenas quando o usuário escolhe. O painel não trata clicar em autorizar como prova de aprovação.

No host, amigo conectado não significa ponte em uso. O painel distingue conexão direta, teste da ponte e uso da ponte após escolha e sonda confirmada. A explicação da escolha é recebida do jogador, e a idade dos dados considera também o tempo desde a última mensagem. Conta conectada, aprovação, alcance do túnel e desempenho são verificações separadas.
## Correções e escolha de caminho na 0.8.3

O aviso de painel indisponível podia ser causado pela primeira criação da regra: Windows retorna HRESULT 0x80070002 quando a regra ainda não existe, traduzido pelo .NET para FileNotFoundException. O código tratava apenas COMException. A busca agora considera ausente apenas esse HRESULT; acesso negado e outros erros não são ocultados. A limpeza é idempotente. A regra segue limitada ao executável, IPv4 local do adaptador Tailscale, TCP 52941 e peers privados conhecidos; não se desativa o firewall nem se abrem portas no roteador.

No Acre, clique em qualquer parte do diagrama e escolha Direta, a ponte desejada ou Automático. Manual aplica o caminho escolhido sem exigir que ele vença a comparação. O diagrama identifica MANUAL depois de a seleção ser confirmada. O relatório distingue preferência manual, rota atual e ensaios anteriores. ICMP sem resposta sozinho não desfaz uma escolha manual; indisponibilidade do túnel pode retornar à direta. Automático refaz os testes. Trocas podem reconectar uma partida.

O seletor altera o caminho no computador que vai jogar; o host recebe a identificação do modo e acompanha os resultados. Não há controle remoto de rotas no protocolo de telemetria. Um parceiro único reconhecido no canal privado é salvo sem escolha manual, preservando a privacidade da descoberta. Use a mesma versão nos dois PCs.