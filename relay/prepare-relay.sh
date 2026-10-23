#!/usr/bin/env bash
# Preparação para uma VPS Debian/Ubuntu DEDICADA. Não executado pela aplicação.
# Requer: wireguard-tools, iptables, iproute2, python3 e acesso root.
set -euo pipefail
umask 077

if [[ ${EUID} -ne 0 || $# -ne 3 ]]; then
  printf 'Uso (root): bash prepare-relay.sh IPV4_DESTINO CHAVE_PUBLICA_CLIENTE INTERFACE_WAN\n' >&2
  exit 1
fi

target="$1"
client_public="$2"
wan="$3"
for command in wg ip iptables python3 sysctl; do
  command -v "$command" >/dev/null || { printf 'Dependência ausente: %s\n' "$command" >&2; exit 1; }
done

python3 - "$target" "$client_public" "$wan" <<'PY'
import base64, ipaddress, re, sys
try:
    target = ipaddress.ip_address(sys.argv[1])
    assert target.version == 4 and target.is_global and not target.is_multicast and not target.is_reserved, 'Destino precisa ser IPv4 público unicast.'
    key = base64.b64decode(sys.argv[2], validate=True)
    assert len(key) == 32 and any(key), 'Chave pública deve ter 32 bytes.'
    assert re.fullmatch(r'[a-zA-Z0-9_-]{1,15}', sys.argv[3]), 'Nome de interface inválido.'
except Exception as exc:
    print('Parâmetro inválido: ' + str(exc), file=sys.stderr)
    sys.exit(1)
PY

ip link show dev "$wan" >/dev/null
if [[ -e /etc/wireguard/rg0.conf || -e /etc/sysctl.d/90-rotagame.conf ]]; then
  printf 'Configuração RotaGame já existe. Revise manualmente; nenhum arquivo foi sobrescrito.\n' >&2
  exit 1
fi
if ip link show dev rg0 >/dev/null 2>&1; then
  printf 'A interface rg0 já está em uso. Interrompendo.\n' >&2
  exit 1
fi
if ip -4 addr show | python3 -c 'import sys; sys.exit(0 if "10.77.0." in sys.stdin.read() else 1)'; then
  printf 'A faixa 10.77.0.x já aparece na VPS. Escolha outra faixa nos modelos.\n' >&2
  exit 1
fi

mkdir -p /etc/wireguard
server_private="$(wg genkey)"
server_public="$(printf '%s\n' "$server_private" | wg pubkey)"
previous_forward="$(sysctl -n net.ipv4.ip_forward)"
printf '%s\n' "$previous_forward" > /etc/wireguard/rg0.previous-forward

cat > /etc/wireguard/rg0.conf <<EOF
[Interface]
Address = 10.77.0.1/24
ListenPort = 51820
PrivateKey = $server_private
# Regras restritas ao destino do experimento. Não é um relay aberto.
PostUp = iptables -w -I FORWARD 1 -i %i -j DROP
PostUp = iptables -w -I FORWARD 1 -i %i -o $wan -s 10.77.0.2/32 -d $target/32 -j ACCEPT
PostUp = iptables -w -I FORWARD 1 -o %i -j DROP
PostUp = iptables -w -I FORWARD 1 -i $wan -o %i -s $target/32 -d 10.77.0.2/32 -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT
PostUp = iptables -w -t nat -A POSTROUTING -s 10.77.0.2/32 -d $target/32 -o $wan -j MASQUERADE
PostDown = iptables -w -D FORWARD -i %i -j DROP
PostDown = iptables -w -D FORWARD -i %i -o $wan -s 10.77.0.2/32 -d $target/32 -j ACCEPT
PostDown = iptables -w -D FORWARD -o %i -j DROP
PostDown = iptables -w -D FORWARD -i $wan -o %i -s $target/32 -d 10.77.0.2/32 -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT
PostDown = iptables -w -t nat -D POSTROUTING -s 10.77.0.2/32 -d $target/32 -o $wan -j MASQUERADE

[Peer]
PublicKey = $client_public
AllowedIPs = 10.77.0.2/32
EOF
chmod 600 /etc/wireguard/rg0.conf /etc/wireguard/rg0.previous-forward
printf 'net.ipv4.ip_forward=1\n' > /etc/sysctl.d/90-rotagame.conf
sysctl -p /etc/sysctl.d/90-rotagame.conf >/dev/null
unset server_private
printf 'Chave PÚBLICA da VPS: %s\n' "$server_public"
printf 'Configuração criada. Libere UDP/51820 no provedor e no firewall local.\n'
printf 'Para iniciar manualmente: wg-quick up rg0\n'
printf 'Não foi habilitado início automático. Destino permitido: %s/32\n' "$target"
