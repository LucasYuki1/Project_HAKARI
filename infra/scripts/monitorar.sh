#!/usr/bin/env bash
# Monitor do servidor Hakari. Instalado como /usr/local/bin/hakari-monitor e executado pelo systemd.
# A cada ciclo: mede CPU, memória e disco, confere a API (passando pelo Nginx) e os containers,
# grava uma linha em monitor.log, dispara alertas quando passa dos limites e tenta recuperar a
# aplicação sozinho. Se o próprio monitor morrer ou travar, o systemd o reinicia.
set -uo pipefail

source /etc/hakari/hakari.conf
LOG_MONITOR="$DIR_LOGS/monitor.log"
LOG_ALERTAS="$DIR_LOGS/alertas.log"
mkdir -p "$DIR_LOGS"

declare -A ESTADO=() ULTIMO_ALERTA=()
FALHAS_API=0

agora() { date '+%Y-%m-%d %H:%M:%S'; }

# Conversa com o systemd (pronto, sinal de vida e texto de status), só quando roda como serviço.
avisar_systemd() {
  [ -n "${NOTIFY_SOCKET:-}" ] && systemd-notify "$@" 2>/dev/null
  return 0
}

# registrar SEVERIDADE texto: grava o alerta, manda para o journal e avisa quem está logado
registrar() {
  local linha prioridade=user.warning
  linha="$(agora) [$1] $2"
  echo "$linha" >> "$LOG_ALERTAS"
  echo "$linha" >> "$LOG_MONITOR"
  case "$1" in
    DESASTRE) prioridade=user.crit ;;
    NORMALIZADO|RECUPERACAO) prioridade=user.notice ;;
  esac
  logger -t hakari-monitor -p "$prioridade" "[$1] $2"
  case "$1" in ALTA|DESASTRE) echo "Hakari [$1] $2" | wall -n 2>/dev/null ;; esac
}

# avaliar ITEM SEVERIDADE mensagem: alerta quando o estado muda e repete se o problema continuar
avaliar() {
  local item="$1" sev="$2" msg="$3" agora_s anterior
  agora_s=$(date +%s)
  anterior="${ESTADO[$item]:-OK}"
  if [ "$sev" = OK ]; then
    [ "$anterior" != OK ] && registrar NORMALIZADO "$item: $msg"
  elif [ "$sev" != "$anterior" ] || (( agora_s - ${ULTIMO_ALERTA[$item]:-0} >= REPETIR_ALERTA_MIN * 60 )); then
    registrar "$sev" "$item: $msg"
    ULTIMO_ALERTA[$item]=$agora_s
  fi
  ESTADO[$item]=$sev
}

# severidade VALOR LIMITE: OK abaixo do limite, ALTA acima dele, DESASTRE acima do limite crítico
severidade() {
  if (( $1 >= LIMITE_CRITICO )); then echo DESASTRE
  elif (( $1 >= $2 )); then echo ALTA
  else echo OK
  fi
}

# ---------- Medições ----------
amostra_cpu() { awk '/^cpu /{print $2+$3+$4+$5+$6+$7+$8+$9, $5+$6}' /proc/stat; }   # total e ocioso
uso_memoria() { awk '/^MemTotal/{t=$2} /^MemAvailable/{a=$2} END{printf "%d", (t-a)*100/t}' /proc/meminfo; }
uso_disco() { df -P / | awk 'NR==2{sub("%","",$5); print $5}'; }
estado_container() {
  docker inspect -f '{{.State.Status}}{{if .State.Health}}/{{.State.Health.Status}}{{end}}' "$1" 2>/dev/null || echo ausente
}

# ---------- Recuperação automática ----------
recuperar_aplicacao() {
  if ! systemctl is-active -q hakari-app; then
    avaliar "Aplicação" AVISO "hakari-app foi parado pelo administrador; sem recuperação automática"
    return
  fi
  local c est religou=0
  for c in hakari-db hakari-api; do
    est=$(estado_container "$c")
    case "$est" in
      running*|restarting*) ;;   # no ar, ou o próprio Docker já está religando
      *)
        registrar RECUPERACAO "container $c estava '$est'; iniciando de novo"
        docker start "$c" >/dev/null 2>&1 && religou=1
        ;;
    esac
  done
  if (( religou == 0 && FALHAS_API >= FALHAS_PARA_RECUPERAR * 2 )); then
    registrar RECUPERACAO "containers no ar, mas a API não responde; reiniciando hakari-api"
    docker restart hakari-api >/dev/null 2>&1
    FALHAS_API=0
  fi
}

# ---------- Início ----------
REINICIOS=$(systemctl show -p NRestarts --value hakari-monitor 2>/dev/null)
echo "$(agora) [INICIO] monitor iniciado (pid $$, reinícios pelo systemd: ${REINICIOS:-0}, intervalo ${INTERVALO_SEGUNDOS}s)" >> "$LOG_MONITOR"
logger -t hakari-monitor "monitor iniciado (pid $$, reinícios pelo systemd: ${REINICIOS:-0})"
avisar_systemd --ready --status="iniciando"

read -r cpu_total_ant cpu_ocioso_ant < <(amostra_cpu)
sleep 1

while true; do
  read -r cpu_total cpu_ocioso < <(amostra_cpu)
  dt=$(( cpu_total - cpu_total_ant ))
  di=$(( cpu_ocioso - cpu_ocioso_ant ))
  cpu=$(( dt > 0 ? (dt - di) * 100 / dt : 0 ))   # uso médio desde o ciclo anterior
  cpu_total_ant=$cpu_total
  cpu_ocioso_ant=$cpu_ocioso
  mem=$(uso_memoria)
  disco=$(uso_disco)

  avaliar "CPU" "$(severidade "$cpu" "$LIMITE_CPU")" "uso de ${cpu}% (limite ${LIMITE_CPU}%)"
  avaliar "Memória" "$(severidade "$mem" "$LIMITE_MEMORIA")" "uso de ${mem}% (limite ${LIMITE_MEMORIA}%)"
  avaliar "Disco" "$(severidade "$disco" "$LIMITE_DISCO")" "uso de ${disco}% em / (limite ${LIMITE_DISCO}%)"

  http=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$URL_SAUDE") || true
  api=$(estado_container hakari-api)
  banco=$(estado_container hakari-db)
  if [ "$http" = 200 ]; then
    FALHAS_API=0
    avaliar "API" OK "respondendo de novo (HTTP 200)"
  else
    FALHAS_API=$(( FALHAS_API + 1 ))
    avaliar "API" DESASTRE "fora do ar (HTTP $http · api $api · banco $banco)"
    (( FALHAS_API >= FALHAS_PARA_RECUPERAR )) && recuperar_aplicacao
  fi

  echo "$(agora) cpu=${cpu}% mem=${mem}% disco=${disco}% http=$http api=$api banco=$banco" >> "$LOG_MONITOR"
  avisar_systemd --status="CPU ${cpu}% · memória ${mem}% · disco ${disco}% · API HTTP $http" WATCHDOG=1
  sleep "$INTERVALO_SEGUNDOS"
done
