# RotaGame 0.9.0 · PANT_CORE

Reestruturação para comparar **conexão direta e Cloudflare WARP gratuito** no próprio PC do jogador.

- Retira da abertura normal os papéis Acre/Cascavel, Tailscale, convites e dependência do PC ponte.
- Preparar rota gratuita identifica a Cloudflare e seus termos, verifica a assinatura do instalador oficial e tenta continuar a preparação ao término.
- Compara o mesmo destino em quatro blocos; exige menor média repetida e limites de p95, falhas ICMP e jitter. Não promete reduzir ping.
- Diagrama de saltos observados com IP, RTT acumulado, destino IP:porta, brilho, degradê e animação ilustrativa; trechos ocultos permanecem desconhecidos.
- Biblioteca sem jogos fictícios, ícones dos executáveis e novas linhas para cartões excedentes. Painel cresce para trajetos longos.
- Atualizações visíveis no topo, preservando aviso e erro da abertura. Aplica ao abrir e oferece Agora/Depois durante o uso.
- Desativar/sair/atualizar restaura alterações próprias de conexão. Preserva identidades e configurações externas.

WARP afeta a saída geral do PC. Não seleciona cada roteador, POP ou servidor do jogo. A descoberta de destino é heurística e IPv4; ICMP pode não responder ou diferir do ping da partida.

Validação: 112 testes automatizados com diagnóstico/traceroute reais de loopback e interface/seleção WARP simuladas e identificadas. O WARP real e a melhora em partidas no Acre ainda precisam de validação de campo.
