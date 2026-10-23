# RotaGame 0.7 — PANT_CORE

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

## Primeira preparação

Nos dois PCs, abra a engrenagem **Conta e conexão**. Cada pessoa usa sua própria conta.

A engrenagem agora abre a aba **Conta e conexão** dentro da janela principal. **Entrar ou criar conta** instala o componente se necessário e inicia a autenticação oficial, com Google ou outro provedor disponível. A aba acompanha automaticamente o estado deste PC e os computadores disponíveis. Ela não cria um cadastro próprio do RotaGame nem modifica a identidade das telas do provedor.

Há quatro abas com o estilo do programa:

- **Minha conta:** entrada, instalação e computadores encontrados. Se os dois PCs já aparecem na mesma rede, não é necessário um novo convite.
- **Convidar amigo:** mostra de onde vem o link e abre a página oficial. Em Users, use Invite external users → Copy invite link → Member → Generate & copy invite link. Volte e use **Colar link** / **Copiar convite para enviar**. O convite só fica nesta sessão e não é salvo pelo RotaGame.
- **Aceitar convite:** no Acre, cole o link de usuário (`https://login.tailscale.com/uinv/…`), clique **Abrir e aceitar convite** e aceite na página oficial. Depois use **Já aceitei · conectar este PC**, escolhendo a rede convidada na autorização. O programa consulta o estado real e mostra as pontes encontradas; abrir o convite não é tratado como prova de aceitação.
- **Autorizar ponte:** em Cascavel, clique **Preparar este PC como ponte**. Se já houver aprovação automática na rede, o estado muda ao ser confirmado pelo serviço. Se ficar pendente, **Abrir autorização oficial** leva à lista de PCs: encontre o nome mostrado na aba, escolha Edit route settings / Edit exit node, marque Use as exit node e salve. A aba verifica o resultado a cada três segundos; também há **Verificar autorização**. A oferta permanece ativa até Desativar ou encerrar normalmente o app.

O login Google/Tailscale e as autorizações da conta continuam no navegador oficial. O app não gera convites nem aprova permissões administrativas silenciosamente. O Google não permite autenticação OAuth em navegadores embutidos controlados pelo aplicativo: [política oficial](https://developers.google.com/identity/protocols/oauth2/policies). Eliminar contas Tailscale para os usuários exigiria um serviço próprio de cadastro e provisionamento, que não está hospedado nem incluído nesta versão.

Essas etapas de login, convite e autorização precisam da interação do dono da conta. Depois da preparação, o aplicativo encontra as pontes autorizadas e muda o caminho automaticamente. Não é necessário configurar WireGuard ou roteador manualmente.

## Para jogar

1. No RotaGame, deixe **Jogar neste PC** e clique **Ativar**. Pode fazer isso antes de abrir o jogo.
2. Abra o jogo cadastrado e entre em treino para gerar tráfego. Ele será reconhecido automaticamente.
3. Aguarde a descoberta e a comparação da direta com as pontes disponíveis, normalmente alguns minutos.
4. Acompanhe o **ping médio atual** e as médias da comparação inicial.
5. Clique **Desativar** para parar e restaurar a preferência de conexão anterior.

As trocas iniciais podem reconectar a sessão. Ative em treino, antes da partida competitiva. Depois de escolher, o programa mantém o caminho e o monitora; não alterna pontes a cada oscilação. Se não houver melhora suficiente, mantém a direta. Se a ponte falhar, tenta retornar à direta.

O PC de casa precisa permanecer ligado, conectado e sem suspender. A ponte usa saída geral de internet: quando escolhida, navegação e downloads do amigo também podem passar por sua conexão. Esta versão não separa tráfego por aplicativo.

## Limites e estado da entrega

“Melhor caminho” significa o melhor entre a direta e as pontes cadastradas, autorizadas e medidas nessa sessão. Não há rede pública própria de relays nem troca automática de servidor/região do jogo.

A descoberta usa metadados por processo e perfis heurísticos dos três jogos. Pode observar voz ou outro jogador, especialmente em GTA Online. Um destino sem resposta ICMP impede uma conclusão confiável. Ao mudar de sessão/região, desative e ative novamente em treino.

**Ainda não houve teste de ponte real entre Cascavel e Acre.** Instalação/login/convite/autorização do Tailscale precisam ser concluídos nos PCs de casa. Não foi instalado ou conectado um transporte nesta máquina de trabalho. As chamadas de controle foram validadas com fixtures de contrato; o diagnóstico, a descoberta ETW e a interface tiveram testes locais reais.

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
