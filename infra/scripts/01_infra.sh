#!/usr/bin/env bash
# Etapa 1 · Infraestrutura: pacotes base, identidade da máquina (hostname e /etc/hosts),
# fuso horário, DNS, rede, firewall, swap e auditoria do sistema (auditd).
set -uo pipefail
source "$(dirname "$0")/lib.sh"
exigir_root
iniciar_auditoria "$@"

etapa "Pacotes base"
# Numa VM recém-criada a atualização automática pode estar usando o apt; tenta algumas vezes.
executar "Lista de pacotes atualizada" bash -c 'for i in 1 2 3 4 5; do apt-get -q update && exit 0; sleep 15; done; exit 1'
executar "Ferramentas instaladas: curl, git, jq, ufw, auditd, dig, stress-ng" \
  "${APT[@]}" install curl git jq ufw auditd bind9-dnsutils stress-ng ca-certificates logrotate

etapa "Identidade da máquina"
# O /etc/hosts vem antes do hostname para o novo nome já resolver quando for aplicado.
bloco_gerenciado /etc/hosts "127.0.1.1 $NOME_HOST.$DOMINIO $NOME_HOST
$IP_API api.$DOMINIO
$IP_DB db.$DOMINIO"
log OK "/etc/hosts: $NOME_HOST.$DOMINIO, api.$DOMINIO ($IP_API) e db.$DOMINIO ($IP_DB)"
executar "Hostname: $NOME_HOST" hostnamectl set-hostname "$NOME_HOST"
if [ -d /etc/cloud/cloud.cfg.d ]; then
  printf '%s\n' "# Gerado por infra/scripts/01_infra.sh: o cloud-init não desfaz o hostname nem o /etc/hosts no reboot." \
    "preserve_hostname: true" "manage_etc_hosts: false" > /etc/cloud/cloud.cfg.d/99-hakari.cfg
  log OK "cloud-init orientado a manter o hostname e o /etc/hosts"
fi
executar "Fuso horário: $FUSO_HORARIO" timedatectl set-timezone "$FUSO_HORARIO"
tentar "Sincronização de horário (NTP) ligada" timedatectl set-ntp true

etapa "DNS"
install -d /etc/systemd/resolved.conf.d
cat > /etc/systemd/resolved.conf.d/90-hakari.conf <<EOF
# Gerado por infra/scripts/01_infra.sh
[Resolve]
DNS=$DNS_SERVIDORES
FallbackDNS=$DNS_RESERVA
Domains=$DOMINIO
EOF
executar "systemd-resolved usando $DNS_SERVIDORES (reserva $DNS_RESERVA)" systemctl restart systemd-resolved
sleep 2
executar "Nome externo resolvido (github.com)" resolvectl query --legend=no github.com
executar "Nome interno resolvido (api.$DOMINIO → $IP_API)" getent hosts "api.$DOMINIO"

etapa "Rede e IP"
INTERFACE=$(interface_padrao)
IP_PRIVADO=$(ip -4 -o addr show "$INTERFACE" | awk '{print $4; exit}')
GATEWAY=$(ip route show default | awk '{print $3; exit}')
log OK "Interface $INTERFACE · IP privado $IP_PRIVADO · gateway $GATEWAY · IP público $(ip_publico)"
log INFO "O IP da placa vem da Azure (DHCP) e não é alterado aqui: trocá-lo derrubaria o acesso SSH."
log INFO "Rede interna dos containers: $REDE_SUBNET (API $IP_API, banco $IP_DB), criada na etapa app."
cat > /etc/sysctl.d/90-hakari.conf <<'EOF'
# Gerado por infra/scripts/01_infra.sh
# Resiste a SYN flood
net.ipv4.tcp_syncookies = 1
# Ignora e não envia redirecionamentos ICMP
net.ipv4.conf.all.accept_redirects = 0
net.ipv4.conf.default.accept_redirects = 0
net.ipv4.conf.all.send_redirects = 0
# Ignora ping para endereço de broadcast
net.ipv4.icmp_echo_ignore_broadcasts = 1
# Só usa swap quando a memória estiver acabando
vm.swappiness = 10
EOF
tentar "Parâmetros de rede do kernel aplicados" sysctl -p /etc/sysctl.d/90-hakari.conf

etapa "Firewall (ufw)"
executar "Entrada bloqueada por padrão" ufw default deny incoming
executar "Saída liberada por padrão" ufw default allow outgoing
for porta in $PORTAS_LIBERADAS; do
  executar "Porta $porta liberada" ufw allow "$porta"
done
executar "Conexões bloqueadas registradas em /var/log/ufw.log" ufw logging low
executar "Firewall ativo" ufw --force enable

etapa "Memória swap"
if swapon --show=NAME --noheadings | grep -qx /swapfile; then
  log OK "Swap de $SWAP_MB MB já estava ativo"
else
  [ -f /swapfile ] || executar "Arquivo /swapfile de $SWAP_MB MB criado" fallocate -l "${SWAP_MB}M" /swapfile
  chmod 600 /swapfile
  executar "Swap formatado" mkswap /swapfile
  executar "Swap ligado" swapon /swapfile
  grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
  log OK "Swap registrado no /etc/fstab (continua depois do reboot)"
fi

etapa "Auditoria do sistema (auditd)"
install -d -m 700 "$DIR_SEGREDOS"
sed "s|@INFRA@|$INFRA|g" "$INFRA/config/auditoria.rules" > /etc/audit/rules.d/hakari.rules
executar "auditd ativo e ligado no boot" systemctl enable --now auditd
executar "Regras de auditoria carregadas" augenrules --load
log OK "Eventos em /var/log/audit/audit.log · consulta: sudo ausearch -k hakari_rede -i"

log OK "Infraestrutura pronta"
