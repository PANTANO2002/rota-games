# Relay WireGuard — preparação para o teste

**Estado desta entrega:** não há VPS; este roteiro e o script não foram executados num relay real. O diagnóstico funciona sem eles. Não existe evidência de redução de ping para o Acre nesta entrega.

## Arquitetura do túnel

Windows `10.77.0.2` → túnel WireGuard/UDP → VPS `10.77.0.1` → encaminhamento com NAT → **mesmo IPv4 público do experimento**.

O ping é enviado ao destino final pelo computador Windows; o app não soma ping Windows→VPS com ping VPS→jogo. Somar dois testes separados não substitui o RTT pelo caminho completo.

## Antes de contratar

Colete primeiro dados no Acre e confirme os destinos reais dos jogos. Localização da VPS por si só não garante uma boa rota da operadora. Se testar uma VPS temporária, registre região, provedor, custo e tráfego incluído; compare em horários diferentes antes de manter mensalidade.

## 1. Cliente Windows

Instale o [cliente oficial WireGuard](https://www.wireguard.com/install/). Crie um túnel vazio no próprio cliente para gerar as chaves. **A chave privada permanece no Windows**; copie só a chave pública para a configuração do servidor. Não compartilhe arquivos `.conf` com chaves privadas.

O aplicativo RotaGame não precisa receber essas chaves. O túnel será ativado/desativado manualmente no cliente oficial.

## 2. VPS Linux dedicada

O exemplo pressupõe Debian/Ubuntu, IPv4 público de entrada, acesso administrativo e uma VPS dedicada ao experimento. Revise o firewall do provedor, a interface WAN real e se `10.77.0.0/24` conflita com alguma rede existente antes de executar. O script recusa um IP local/documentação como destino e recusa sobrescrever configurações existentes.

Instale os utilitários:

```bash
sudo apt-get update
sudo apt-get install wireguard-tools iptables iproute2 python3
ip -4 route show default
```

A interface da rota padrão, por exemplo `ens3` ou `eth0`, é o terceiro argumento abaixo. Use o **IPv4 público real do destino do jogo** e a **chave pública do cliente**:

```bash
sudo bash prepare-relay.sh IPV4_REAL_DESTINO CHAVE_PUBLICA_CLIENTE INTERFACE_WAN
```

O script:

- Gera a chave do servidor localmente, com permissões restritas, em `/etc/wireguard/rg0.conf`.
- Configura um único peer, com IP de túnel `10.77.0.2/32`.
- Habilita encaminhamento IPv4 e anota o valor anterior em `/etc/wireguard/rg0.previous-forward`.
- Prepara regras de encaminhamento/NAT limitadas ao IP do cliente no túnel e ao destino escolhido. O restante do encaminhamento pelo `rg0` é descartado.
- Exibe somente a chave **pública** do servidor.
- **Não inicia o túnel nem altera a política de entrada do firewall.**

Libere **UDP/51820** no firewall do provedor e no firewall local da VPS, usando o mecanismo já existente. Se usar UFW, permita a porta por ele. Não limpe o firewall nem altere a regra de SSH. O script insere regras de encaminhamento para o teste; firewalls com regras próprias podem exigir integração adicional. Não o execute num servidor de produção com outros serviços/VPNs.

Depois:

```bash
sudo wg-quick up rg0
sudo wg show rg0
```

Este fluxo não habilita o túnel no boot. Se a subida falhar, revise a saída e as regras antes de repetir; não interprete uma interface criada como prova de funcionamento.

## 3. Completar o modelo de cliente

Use `client.conf.example` como referência, preenchendo no cliente oficial:

- `PrivateKey`: chave privada do **cliente**, gerada no Windows.
- `PublicKey` em `[Peer]`: chave pública da **VPS**, exibida pelo script.
- `Endpoint`: IPv4 público da VPS e porta `51820`.
- `AllowedIPs`: IPv4 final do teste com `/32`.

O endereço `203.0.113.10` do arquivo é reservado a documentação; substitua-o. O modelo é deliberadamente incompleto e não conecta antes do preenchimento.

Não use `0.0.0.0/0`: o experimento só precisa do destino final. Não inclua o IP público da própria VPS em AllowedIPs. Não adicione configurações DNS neste teste dividido por IP.

O `PersistentKeepalive = 25` ajuda quando o cliente fica atrás de NAT. O MTU inicial fica com o comportamento automático do WireGuard; se o handshake funciona mas dados maiores falham, investigue o MTU antes de tirar conclusões. Não use um MTU aleatório como suposta otimização de ping.

## 4. Verificar o caminho completo

1. Ative o túnel no Windows. Envie um ping ao IP final.
2. Confira no cliente e em `sudo wg show rg0` se há handshake recente e contadores de transferência aumentando.
3. Confira no Windows a rota para o destino, opcionalmente com `Find-NetRoute -RemoteIPAddress IPV4_REAL_DESTINO`.
4. Confira na VPS as regras e contadores com `sudo iptables -vnL FORWARD` e `sudo iptables -t nat -vnL POSTROUTING`.
5. Se precisar, capture o destino na interface WAN para confirmar que o teste sai pela VPS. Não publique capturas com dados privados.
6. Só então execute os blocos A–B–B–A no RotaGame.

Ping no IP **da VPS** só avalia a chegada ao relay. Um WireGuard ativo sem encaminhamento/NAT não mede o caminho completo até o jogo. Uma falha ICMP do servidor final pode ser filtragem; não equivale automaticamente a problema no túnel.

O script permite um destino. Para cobrir mais IPs reais de jogo, atualize de maneira coerente a lista AllowedIPs no cliente **e** as regras de encaminhamento/NAT no servidor, com o túnel parado. Esta primeira versão não automatiza isso.

## 5. Encerrar o experimento

Desative o túnel no Windows. No servidor:

```bash
sudo wg-quick down rg0
```

Isso remove as regras listadas em PostDown. Não habilitamos serviço automático. Para remover a persistência de encaminhamento criada pelo script, revise e remova apenas `/etc/sysctl.d/90-rotagame.conf`; restaure `net.ipv4.ip_forward` ao valor salvo em `/etc/wireguard/rg0.previous-forward` **somente se nenhum outro serviço precisar dele**. Revogue a regra UDP/51820 no provedor/firewall quando encerrar. Preserve ou remova as chaves de acordo com a continuidade do teste.

## Referências oficiais

- [WireGuard Quick Start](https://www.wireguard.com/quickstart/).
- [WireGuard Windows: roteamento de AllowedIPs e particularidades](https://git.zx2c4.com/wireguard-windows/about/docs/netquirk.md).
- [Microsoft Find-NetRoute](https://learn.microsoft.com/powershell/module/nettcpip/find-netroute).
