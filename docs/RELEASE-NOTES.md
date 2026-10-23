# RotaGame 0.8.4 — PANT_CORE

- Automático prioriza o menor ping médio confirmado até o mesmo destino do jogo, entre a conexão direta e as pontes disponíveis. Ganhos pequenos e repetidos podem vencer; removido o corte anterior de 5 ms / 5% e o ranking por pontuação composta.
- Cada ponte precisa confirmar o ganho nos dois blocos de teste, sem aumentar as falhas ICMP ou o p95, e com jitter no máximo 3 ms acima da direta. Empate, resultados insuficientes ou melhora inconsistente mantêm a direta. Um bloco duplicado não conta como dois testes.
- O relatório e a tabela explicam por que uma ponte não foi escolhida. O diagrama diferencia média ao vivo do último teste de uma rota não ativa; o comparativo mostra o horário da medição.
- O seletor destaca “Automático · menor ping confirmado”. Selecioná-lo novamente refaz a comparação. A escolha manual de Direta ou de uma ponte continua disponível e identificada.
- Mantidas as correções de reconhecimento dos dois PCs, canal de medições e recuperação de conexão da 0.8.3. Atualizador embutido com publicação pelo GitHub.

No Acre: Ativar e deixar em Automático. Em Cascavel: manter o PC ponte disponível. Compare antes da partida; alternar o ponto de saída pode reconectar o jogo. O teste mantém o mesmo destino observado, sem inventar servidores ou trocar a região selecionada no jogo.

O programa escolhe entre caminhos que existem e estão autorizados. Com dois PCs e uma saída de internet em cada um, isso significa direta ou pelo PC ponte. Não controla os roteadores das operadoras nem oferece uma rede própria de relays. O Tailscale negocia automaticamente o túnel entre os PCs; adicionar novos pontos de saída exigiria outros PCs ou servidores disponíveis.

Os pings são ICMP do caminho completo e podem diferir do contador do jogo. A seleção é uma comparação por blocos, não uma medição simultânea de todas as rotas nem uma garantia de menor ping em todo momento. Não foi medida melhora desta versão entre os PCs reais de Acre e Cascavel.
