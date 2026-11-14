#!/usr/bin/env bash
# Etapa 3 · Monitoramento: instala o monitor como serviço do systemd, tolerante a falhas,
# e configura a rotação dos logs do projeto.
set -uo pipefail
source "$(dirname "$0")/lib.sh"
exigir_root
iniciar_auditoria "$@"

etapa "Monitor de recursos e da aplicação"
install -d -m 700 "$DIR_SEGREDOS"
executar "Configuração copiada para /etc/hakari/hakari.conf" install -m 600 "$INFRA/hakari.conf" /etc/hakari/hakari.conf
executar "Monitor instalado em /usr/local/bin/hakari-monitor" install -m 750 "$INFRA/scripts/monitorar.sh" /usr/local/bin/hakari-monitor
sed "s|@INFRA@|$INFRA|g" "$INFRA/config/hakari-monitor.service" > /etc/systemd/system/hakari-monitor.service
executar "Unidade hakari-monitor.service instalada" systemctl daemon-reload
executar "Monitor ligado no boot" systemctl enable hakari-monitor
executar "Monitor iniciado" systemctl restart hakari-monitor
sleep 4
if ! systemctl is-active -q hakari-monitor; then
  log ERRO "O monitor não subiu. Veja: sudo journalctl -u hakari-monitor -n 30"
  exit 1
fi
log OK "Monitor ativo: $(systemctl show -p StatusText --value hakari-monitor)"
log INFO "Limites: CPU ${LIMITE_CPU}% · memória ${LIMITE_MEMORIA}% · disco ${LIMITE_DISCO}% · Desastre a partir de ${LIMITE_CRITICO}% · checagem a cada ${INTERVALO_SEGUNDOS}s"

etapa "Rotação de logs"
executar "Rotação diária instalada (guarda 14 dias, comprimidos)" install -m 644 "$INFRA/config/logrotate-hakari" /etc/logrotate.d/hakari
executar "Regra de rotação válida" logrotate --debug /etc/logrotate.d/hakari

log OK "Monitoramento pronto. Logs em $DIR_LOGS"
