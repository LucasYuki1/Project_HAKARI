#!/usr/bin/env bash
# Painel do servidor Hakari: identidade, rede, serviços, containers, recursos e últimos registros.
# Uso: sudo bash infra/scripts/status.sh
set -uo pipefail
source "$(dirname "$0")/lib.sh"
exigir_root

titulo() { printf '\n%s%s%s\n' "$CIANO" "$*" "$NORMAL"; }
linha() {   # alinha contando caracteres (o printf conta bytes e desalinha palavras com acento)
  local espacos=$(( 24 - ${#1} ))
  (( espacos < 1 )) && espacos=1
  printf '  %s%*s %s\n' "$1" "$espacos" "" "$2"
}
container() {
  docker inspect -f '{{.State.Status}}{{if .State.Health}}/{{.State.Health.Status}}{{end}} · IP {{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}} · reinícios {{.RestartCount}}' "$1" 2>/dev/null || echo ausente
}

titulo "Servidor"
linha "Hostname" "$(hostname -f 2>/dev/null || hostname)"
linha "Sistema" "$(. /etc/os-release && echo "$PRETTY_NAME") · kernel $(uname -r)"
linha "Ligado há" "$(uptime -p | sed 's/^up //')"
linha "Fuso horário" "$(timedatectl show -p Timezone --value) · $(date '+%d/%m/%Y %H:%M')"

titulo "Rede"
INTERFACE=$(interface_padrao)
linha "IP privado" "$(ip -4 -o addr show "$INTERFACE" | awk '{print $4; exit}') ($INTERFACE)"
linha "IP público" "$(ip_publico)"
linha "DNS" "$(resolvectl dns 2>/dev/null | awk -F': ' 'NF>1 && $2!="" {print $2}' | tr '\n' ' ')"
linha "Domínio de busca" "$(resolvectl domain 2>/dev/null | awk -F': ' 'NF>1 && $2!="" {print $2}' | tr '\n' ' ')"
linha "Nomes internos" "$(getent hosts "api.$DOMINIO" "db.$DOMINIO" | awk '{printf "%s → %s   ", $2, $1}')"
linha "Firewall (ufw)" "$(ufw status | awk 'NR==1{print $2}') · entrada liberada: $(ufw status | awk '/ALLOW/ && !/\(v6\)/{print $1}' | sort -u | tr '\n' ' ')"
linha "Rede dos containers" "$(docker network inspect hakari-net -f '{{(index .IPAM.Config 0).Subnet}}' 2>/dev/null || echo ausente)"

titulo "Serviços"
for s in docker nginx auditd hakari-app hakari-monitor; do
  linha "$s" "$(systemctl is-active "$s") · $(systemctl is-enabled "$s" 2>/dev/null) no boot"
done
linha "Monitor agora" "$(systemctl show -p StatusText --value hakari-monitor)"
linha "Reinícios do monitor" "$(systemctl show -p NRestarts --value hakari-monitor)"

titulo "Containers"
linha "hakari-api" "$(container hakari-api)"
linha "hakari-db" "$(container hakari-db)"

titulo "Aplicação"
linha "Saúde (pelo Nginx)" "HTTP $(curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1/health)"
linha "Endereço público" "http://$(ip_publico)/swagger"

titulo "Recursos"
linha "CPU (carga 1/5/15 min)" "$(cut -d' ' -f1-3 /proc/loadavg) · $(nproc) vCPU"
linha "Memória" "$(free -h | awk '/^Mem/{print $3 " usados de " $2}')"
linha "Swap" "$(free -h | awk '/^Swap/{print $3 " usados de " $2}')"
linha "Disco /" "$(df -h / | awk 'NR==2{print $3 " usados de " $2 " (" $5 ")"}')"

titulo "Últimos alertas ($DIR_LOGS/alertas.log)"
if [ -s "$DIR_LOGS/alertas.log" ]; then tail -n 6 "$DIR_LOGS/alertas.log" | sed 's/^/  /'; else echo "  nenhum alerta até agora"; fi

titulo "Últimas execuções dos scripts ($LOG_AUDITORIA)"
tail -n 6 "$LOG_AUDITORIA" 2>/dev/null | sed 's/^/  /'

titulo "Auditoria do sistema (auditd, eventos por chave)"
# --input-logs: sem terminal (script, SSH) o aureport leria a entrada padrão em vez do log
aureport -k --summary --input-logs 2>/dev/null | awk '$1 ~ /^[0-9]+$/ && $2 ~ /^hakari/ {printf "  %-24s %s eventos\n", $2, $1}'
echo
