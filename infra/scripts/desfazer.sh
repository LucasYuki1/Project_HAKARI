#!/usr/bin/env bash
# Desfaz o provisionamento e devolve a VM ao estado de antes do provisionar.sh: remove a
# aplicação, o banco, os pacotes instalados e as configurações de rede, DNS, firewall, swap,
# monitoramento e auditoria. Serve para refazer a demonstração do zero sem recriar a VM.
#
# Uso: sudo bash infra/scripts/desfazer.sh          pede confirmação
#      sudo bash infra/scripts/desfazer.sh --sim    sem perguntar
set -uo pipefail
source "$(dirname "$0")/lib.sh"
exigir_root
RAIZ="$(cd "$INFRA/.." && pwd)"

if [ "${1:-}" != "--sim" ]; then
  read -r -p "Isto apaga a aplicação, o banco de dados e as configurações do Hakari. Digite SIM para continuar: " resposta
  [ "$resposta" = SIM ] || { echo "Cancelado."; exit 1; }
fi
logger -t hakari-auditoria "desfazer.sh iniciado por ${SUDO_USER:-$(id -un)}"

etapa "Serviços e containers"
tentar "Monitor parado e fora do boot" systemctl disable --now hakari-monitor
tentar "API e banco parados (docker compose down)" systemctl disable --now hakari-app
rm -f /etc/systemd/system/hakari-app.service /etc/systemd/system/hakari-monitor.service /usr/local/bin/hakari-monitor
systemctl daemon-reload
systemctl reset-failed hakari-app hakari-monitor 2>/dev/null
log OK "Serviços do systemd e o monitor removidos"

etapa "Pacotes e dados"
tentar "Regras do auditd descarregadas do kernel" auditctl -D
tentar "Docker, Nginx e stress-ng desinstalados" \
  "${APT[@]}" purge docker.io docker-compose-v2 docker-buildx containerd runc nginx nginx-common stress-ng
# O auditd recusa parada manual; se não sair, ficam só as regras do Hakari removidas.
tentar "auditd desinstalado" "${APT[@]}" purge auditd
tentar "Dependências que sobraram removidas" "${APT[@]}" autoremove --purge
rm -rf /var/lib/docker /var/lib/containerd /etc/docker /etc/nginx /var/log/nginx
if dpkg -s auditd >/dev/null 2>&1; then
  rm -f /etc/audit/rules.d/hakari.rules /etc/audit/rules.d/hakari-app.rules
else
  rm -rf /etc/audit /var/log/audit
fi
groupdel docker 2>/dev/null
log OK "Imagens, volume do banco de dados e configurações do Docker, Nginx e auditd apagados"
rm -rf "$DIR_SEGREDOS" /etc/logrotate.d/hakari /var/tmp/hakari-teste-disco.img
log OK "Segredos ($DIR_SEGREDOS) e rotação de logs removidos"

etapa "Rede, DNS, firewall e swap"
tentar "Firewall desligado e regras zeradas" ufw --force reset
rm -f /etc/ufw/*.rules.20*
rm -f /etc/systemd/resolved.conf.d/90-hakari.conf /etc/sysctl.d/90-hakari.conf
tentar "DNS de volta ao padrão da Azure" systemctl restart systemd-resolved
tentar "Parâmetros do kernel recarregados" sysctl --system
if swapon --show=NAME --noheadings | grep -qx /swapfile; then swapoff /swapfile; fi
rm -f /swapfile
sed -i '\#^/swapfile #d' /etc/fstab
log OK "Swap desligado e removido do /etc/fstab"

etapa "Identidade da máquina"
# Nome original da VM, consultado no serviço de metadados da Azure
nome=$(curl -s -H Metadata:true --max-time 3 \
  "http://169.254.169.254/metadata/instance/compute/name?api-version=2021-02-01&format=text")
[ -n "$nome" ] && executar "Hostname de volta para $nome" hostnamectl set-hostname "$nome"
remover_bloco /etc/hosts
rm -f /etc/cloud/cloud.cfg.d/99-hakari.cfg
log OK "Bloco do Hakari removido do /etc/hosts"
executar "Fuso horário de volta para UTC" timedatectl set-timezone Etc/UTC

etapa "Logs e código"
logger -t hakari-auditoria "desfazer.sh concluído: VM de volta ao estado inicial"
rm -rf "$DIR_LOGS"
if [ "$RAIZ" = /opt/hakari ]; then
  rm -rf /opt/hakari
  echo "  /opt/hakari e $DIR_LOGS apagados"
else
  echo "  $DIR_LOGS apagado; o código em $RAIZ foi mantido"
fi

printf '\n%sVM de volta ao estado inicial%s (hostname %s, fuso UTC, sem Docker, Nginx nem auditd, firewall desligado, sem swap).\n' \
  "$VERDE" "$NORMAL" "$(hostname)"
printf 'Recomendado: sudo reboot, para o kernel também voltar ao padrão.\n'
printf 'Para provisionar de novo:\n  sudo git clone https://github.com/LucasYuki1/Project_HAKARI.git /opt/hakari\n  sudo bash /opt/hakari/infra/provisionar.sh\n'
