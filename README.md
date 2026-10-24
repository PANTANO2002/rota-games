# RotaGame 0.9.0 · PANT_CORE

**Autoria: PANT_CORE.** Protótipo pessoal para Windows 11 x64: compara conexão direta e **Cloudflare WARP gratuito**, mede latência e mostra o trajeto observado até o destino do jogo. Não garante redução de ping.

[Instalador](https://github.com/PANTANO2002/rota-games/releases/latest/download/RotaGame-Setup.exe) · [Versões](https://github.com/PANTANO2002/rota-games/releases)

## Como usar

1. Instale RotaGame-Setup.exe no PC do jogador. A descoberta de tráfego exige a autorização de administrador do Windows; o runtime acompanha o programa.
2. Clique **Preparar rota gratuita**, leia os termos e a política da Cloudflare e aceite se concordar. Conclua o instalador oficial; o app tenta continuar a preparação automaticamente. Se o serviço ainda estiver iniciando, tente o botão novamente. Não contrate WARP+.
3. Abra um jogo instalado, entre em treino e clique **Ativar**. Alternar a saída pode reconectar a sessão.
4. Aguarde a comparação. O app usa WARP somente quando confirma melhora nos critérios de qualidade; caso contrário mantém a direta.
5. **Desativar** encerra as medições e restaura o estado anterior da conexão WARP.

Sem WARP instalado, o diagnóstico direto funciona. O PC de Cascavel não precisa estar ligado. O fluxo normal dispensa Tailscale, convites, IP manual, VPS e conta RotaGame. WARP é um componente da Cloudflare, separado do RotaGame. Quando escolhido, afeta a saída geral de internet do computador, não apenas o jogo. Contas corporativas, pagas e configurações desconhecidas são preservadas e bloqueiam o controle automático.

## Comparação e mapa

O teste fixa o mesmo IPv4 observado e mede quatro blocos: direta / WARP / WARP / direta. São 24 sondas por bloco, além do aquecimento; demora alguns minutos. Exige dez respostas por bloco, melhora de média repetida nos dois blocos alternativos, sem piorar p95 ou falhas ICMP e sem aumentar jitter em mais de 3 ms. Empates e testes inconclusivos mantêm a direta. Não há margem mínima de 5 ms ou 5%.

Após a escolha, monitora o caminho. **Recomparar rotas** repete o teste; faça isso fora de uma partida competitiva. Se a alternativa perde confirmação ou deixa de responder, tenta voltar à direta. Não troca de saída a cada oscilação durante a partida.

O diagrama mostra saltos respondendo a sondas ICMP com TTL crescente e o destino observado IP:porta/protocolo. O RTT é acumulado de ida e volta da sonda, não atraso isolado do trecho. Saltos silenciosos e o interior do WARP podem ficar ocultos. Não inventa cidades, servidores ou latências. Clique no mapa para medir novamente o trajeto. A troca de interface durante a medição invalida os saltos antigos.

A média atual considera as últimas 30 sondas do caminho em medição; o gráfico conserva até 180. Timeout não vira zero. Falhas ICMP não comprovam perda de UDP do jogo. As luzes são ilustrativas e só indicam fluxo com resposta recente. O mapa cresce em novas linhas, com barra estilizada quando necessário.

“Melhor” significa o melhor entre as duas saídas medidas nessa sessão. O programa não escolhe cada roteador da operadora, POP da Cloudflare, cidade ou servidor do jogo. Menos saltos não garante menor ping. Confirme o resultado também no contador da partida.

## Biblioteca

The Finals, Rocket League e GTA Online só aparecem com executáveis instalados encontrados. Use **+ Adicionar jogo** para escolher outro .exe; o ícone vem do arquivo. A biblioteca é revista a cada 30 segundos enquanto inativa e os processos a cada dois segundos. Cartões excedentes vão para outra linha, sem barra horizontal. Remova um cadastro pelo botão direito.

Ativar pode aguardar um jogo e seu tráfego; o app não abre o jogo. Clicar em um cartão limita a detecção; clicar novamente volta a todos. A descoberta prioriza UDP sustentado, mas é heurística: pode detectar voz ou outro jogador, especialmente em GTA Online. Uma mudança persistente de destino inicia nova comparação. Apenas IPv4 nesta versão.

## Atualizador embutido

Na abertura, verifica o GitHub e aplica uma versão posterior antes de abrir o painel. Com o app aberto, verifica a cada 15 minutos e oferece **Atualizar agora / Depois**. O botão **Atualizações**, no topo, permite verificar ou retomar um adiamento. Erros da consulta ficam visíveis.

Antes de reiniciar, encerra medições e restaura alterações próprias de conexão. Recuperação pendente bloqueia o reinício. Biblioteca e diários ficam em %LOCALAPPDATA%/RotaGame. O ZIP portátil precisa ser extraído inteiro; o EXE solto não recebe atualização. Versões anteriores a 0.7 exigem migração pelo instalador.

A logo animada cobre cerca de quatro segundos do carregamento. Uma atualização pode prolongar a abertura, com estado informado. PANT_CORE é assinatura de autoria; não há certificado Authenticode próprio no executável.

## Validação e desenvolvimento

112 testes automatizados incluem ICMP e traceroute reais de loopback, recuperação e cenários simulados de seleção e interface. **O WARP real e a melhora em partidas no Acre ainda precisam de validação de campo.** Não se afirma equivalência à rede do ExitLag.

[Guia](docs/AUTOMATICO.md) · [Arquitetura](docs/ARQUITETURA-AUTO.md) · [Validação](VALIDACAO.md) · [Atualizações](docs/ATUALIZACOES.md) · [Componentes](THIRD_PARTY.md)

Requer Windows x64 e SDK .NET 10:

    dotnet run --project tests/RotaGame.Tests -c Release -- --integration
    dotnet build src/RotaGame.App -c Release
    .\app\RotaGame.exe --network-smoke .\validation\v0.9
    .\pack.ps1 -Version 0.9.0

Os testes de interface usam dados simulados identificados sem instalar WARP nem alterar conexões. Código legado Tailscale permanece para recuperação de alterações da versão anterior e testes históricos, sem navegação de ponte na abertura normal. O diagnóstico antigo só abre por --advanced.
