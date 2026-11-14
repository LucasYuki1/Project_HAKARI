#!/usr/bin/env bash
# Testes de falha: provocam um problema de propósito e mostram o servidor reagindo.
# Uso: sudo bash infra/scripts/teste_falha.sh <teste>
#   cpu             CPU em 100% por 60 s              → alerta de CPU
#   memoria         ocupa a memória por 60 s          → alerta de memória
#   disco           enche o disco por 60 s            → alerta de disco
#   api             para o container da API           → o monitor detecta e religa
#   banco           derruba o PostgreSQL (kill -9)    → o Docker religa e o monitor registra
#   monitor         mata o monitor (kill -9)          → o systemd reinicia em 5 s
#   travar-monitor  congela o monitor (kill -STOP)    → o watchdog do systemd reinicia
set -uo pipefail
source "$(dirname "$0")/lib.sh"
exigir_root

ALERTAS="$DIR_LOGS/alertas.log"
touch "$ALERTAS"

acompanhar_alertas() {   # mostra os alertas novos durante N segundos
  log INFO "Alertas novos em $ALERTAS (acompanhando por $1 s):"
  timeout "$1" tail -n 0 -f "$ALERTAS"
  return 0
}
pid_monitor() { systemctl show -p MainPID --value hakari-monitor; }
reinicios_monitor() { systemctl show -p NRestarts --value hakari-monitor; }
esperar_novo_monitor() {   # espera o systemd subir um processo novo no lugar do antigo
  local antigo="$1" limite="$2" atual=0
  for _ in $(seq 1 "$limite"); do
    sleep 1
    atual=$(pid_monitor)
    if [ "$atual" != 0 ] && [ "$atual" != "$antigo" ]; then break; fi
  done
  echo "$atual"
}

case "${1:-}" in
  cpu)
    iniciar_auditoria "$@"
    etapa "Teste de falha: CPU em 100% por 60 s (limite ${LIMITE_CPU}%)"
    stress-ng --cpu 0 --timeout 60s --quiet &
    acompanhar_alertas 85
    wait
    ;;
  memoria)
    iniciar_auditoria "$@"
    alvo=$(( LIMITE_MEMORIA + 5 ))
    etapa "Teste de falha: memória em ${alvo}% por 60 s (limite ${LIMITE_MEMORIA}%)"
    kb=$(awk -v alvo="$alvo" '/^MemTotal/{t=$2} /^MemAvailable/{a=$2} END{printf "%d", a - t*(100-alvo)/100}' /proc/meminfo)
    if (( kb <= 0 )); then log AVISO "A memória já está acima de ${alvo}%"; exit 0; fi
    log INFO "Ocupando $(( kb / 1024 )) MB"
    stress-ng --vm 1 --vm-bytes "${kb}k" --vm-keep --timeout 60s --quiet &
    acompanhar_alertas 85
    wait
    ;;
  disco)
    iniciar_auditoria "$@"
    alvo=$(( LIMITE_DISCO + 3 ))
    etapa "Teste de falha: disco em ${alvo}% por 60 s (limite ${LIMITE_DISCO}%)"
    read -r total usado < <(df -B1 --output=size,used / | awk 'NR==2{print $1, $2}')
    falta=$(( total * alvo / 100 - usado ))
    arquivo=/var/tmp/hakari-teste-disco.img
    if (( falta > 0 )); then
      executar "Arquivo de teste de $(( falta / 1024 / 1024 )) MB criado em $arquivo" fallocate -l "$falta" "$arquivo"
    fi
    acompanhar_alertas 60
    rm -f "$arquivo"
    log OK "Arquivo de teste apagado"
    acompanhar_alertas 30
    ;;
  api)
    iniciar_auditoria "$@"
    etapa "Teste de falha: API fora do ar"
    executar "Container hakari-api parado" docker stop hakari-api
    log INFO "O monitor confere a cada ${INTERVALO_SEGUNDOS} s e religa depois de ${FALHAS_PARA_RECUPERAR} falhas seguidas"
    acompanhar_alertas 75
    log OK "hakari-api agora: $(docker inspect -f '{{.State.Status}}' hakari-api)"
    ;;
  banco)
    iniciar_auditoria "$@"
    etapa "Teste de falha: PostgreSQL derrubado com kill -9"
    pid=$(docker inspect -f '{{.State.Pid}}' hakari-db)
    executar "Processo principal do PostgreSQL ($pid) morto" kill -9 "$pid"
    log INFO "A política de reinício do Docker religa o banco; o monitor registra a queda e a volta"
    acompanhar_alertas 60
    log OK "hakari-db agora: $(docker inspect -f '{{.State.Status}}{{if .State.Health}}/{{.State.Health.Status}}{{end}} · reinícios {{.RestartCount}}' hakari-db)"
    ;;
  monitor)
    iniciar_auditoria "$@"
    etapa "Teste de falha: monitor morto com kill -9"
    antes=$(pid_monitor)
    n_antes=$(reinicios_monitor)
    executar "Processo do monitor ($antes) morto" kill -9 "$antes"
    depois=$(esperar_novo_monitor "$antes" 20)
    log OK "O systemd reiniciou o monitor: pid $antes → $depois (reinícios: $n_antes → $(reinicios_monitor))"
    journalctl -u hakari-monitor -n 5 --no-pager -o short
    ;;
  travar-monitor)
    iniciar_auditoria "$@"
    etapa "Teste de falha: monitor congelado (kill -STOP)"
    antes=$(pid_monitor)
    executar "Monitor congelado (pid $antes)" kill -STOP "$antes"
    log INFO "Sem sinal de vida por $(systemctl show -p WatchdogUSec --value hakari-monitor), o watchdog mata e reinicia o monitor..."
    depois=$(esperar_novo_monitor "$antes" 120)
    log OK "O watchdog reiniciou o monitor: pid $antes → $depois"
    journalctl -u hakari-monitor -n 6 --no-pager -o short
    ;;
  *)
    sed -n '2,11p' "$0"
    exit 1
    ;;
esac
