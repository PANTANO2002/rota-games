# RotaGame 0.8.1 — PANT_CORE

- Duas escolhas grandes: Vou jogar neste PC (Acre) e Este PC é a ponte (Cascavel). A função e o parceiro ficam salvos.
- Assistente com uma ação por vez, avançando pelo estado real após o login e a permissão. Conta conectada não volta a pedir login.
- Convites de compartilhamento de máquina aceitos, sem exigir troca de rede. Convites antigos de usuário continuam reconhecidos.
- Reconhecimento do outro RotaGame e painel da ponte com as medições recebidas do jogador, incluindo média, variação, p95, falhas, gráfico e caminho. Requer 0.8 nos dois PCs.
- Comparação inclui túneis diretos e caminhos por intermediário DERP, sob os mesmos limites de qualidade. A rota só muda se houver vantagem medida. Mudanças do tipo de túnel invalidam o bloco.
- Motivo visível quando apenas a direta pode ser medida. Botão mostra verificação, espera pelo jogo, ativação e desativação.
- Ajustes de contraste, identificação dos PCs e distribuição do espaço em janelas menores.
- Atualizador automático mantido: ao abrir aplica a nova versão; com o app aberto oferece Atualizar agora / Depois.

Tailscale já usa WireGuard. O login e a permissão inicial de compartilhamento continuam na página oficial. Sem VPS ou serviço próprio de provisionamento, essa autorização não pode ser inventada pelo app. Não é necessário configurar IP público ou roteador.

Os indicadores são ICMP, podem diferir do contador do jogo e não garantem melhora. A ponte usa saída geral IPv4, sem separar tráfego por jogo. Testes locais cobrem comunicação entre painéis e recuperação de rotas; a nova versão ainda requer validação real entre os dois PCs.
A verificação do túnel usa --until-direct=false: respostas via DERP são aceitas como conectividade sem exigir P2P. A fixture da CLI reproduz o erro padrão que antes descartava esse caminho.
