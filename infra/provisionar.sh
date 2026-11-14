#!/usr/bin/env bash
# Provisiona o servidor Hakari: infraestrutura, aplicação e monitoramento, nessa ordem.
# Pode rodar de novo quando quiser: cada etapa confere o que já existe e só ajusta o que falta.
#
# Uso: sudo bash infra/provisionar.sh                  tudo
#      sudo bash infra/provisionar.sh app              uma etapa só (infra, app ou monitoramento)
set -uo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
source "$DIR/scripts/lib.sh"
exigir_root
iniciar_auditoria "$@"

etapas=(infra app monitoramento)
[ $# -gt 0 ] && etapas=("$@")

for etapa_atual in "${etapas[@]}"; do
  case "$etapa_atual" in
    infra) script=01_infra.sh ;;
    app) script=02_app.sh ;;
    monitoramento) script=03_monitoramento.sh ;;
    *) log ERRO "Etapa desconhecida: $etapa_atual (use infra, app ou monitoramento)"; exit 2 ;;
  esac
  if ! bash "$DIR/scripts/$script"; then
    log ERRO "A etapa '$etapa_atual' falhou; provisionamento interrompido. Veja $LOG_EXECUCAO"
    exit 1
  fi
done

bash "$DIR/scripts/status.sh"
