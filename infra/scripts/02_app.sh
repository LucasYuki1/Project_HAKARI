#!/usr/bin/env bash
# Etapa 2 · Aplicação: instala Docker e Nginx, gera senhas e chaves, constrói a imagem da API
# e sobe API + PostgreSQL com docker compose, controlados pelo systemd (hakari-app.service).
set -uo pipefail
source "$(dirname "$0")/lib.sh"
exigir_root
iniciar_auditoria "$@"

COMPOSE=(docker compose -p hakari --env-file "$ARQUIVO_AMBIENTE" -f "$INFRA/config/docker-compose.yml")

etapa "Dependências"
executar "Docker, Docker Compose e Nginx instalados" "${APT[@]}" install docker.io docker-compose-v2 nginx openssl
tentar "Docker Buildx instalado" "${APT[@]}" install docker-buildx
executar "Docker ativo e ligado no boot" systemctl enable --now docker
log OK "$(docker --version | cut -d, -f1) · Compose $(docker compose version --short 2>/dev/null)"
printf '%s\n' "## Gerado por infra/scripts/02_app.sh" "-w /usr/bin/docker -p x -k hakari_docker" \
  "-w /etc/nginx/ -p wa -k hakari_web" > /etc/audit/rules.d/hakari-app.rules
tentar "Auditoria do Docker e do Nginx carregada" augenrules --load

etapa "Variáveis e segredos"
install -d -m 700 "$DIR_SEGREDOS"
if [ -f "$ARQUIVO_AMBIENTE" ]; then
  # shellcheck disable=SC1090
  source "$ARQUIVO_AMBIENTE"
  log OK "Senhas já existentes mantidas (o banco continua abrindo com a mesma senha)"
fi
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-$(openssl rand -hex 24)}"
JWT_CHAVE="${JWT_CHAVE:-$(openssl rand -hex 32)}"
SENHA_TECNICO="${SENHA_TECNICO:-Hk$(openssl rand -hex 6)7}"   # letras e números, como a API exige
(
  umask 077
  cat > "$ARQUIVO_AMBIENTE" <<EOF
# Gerado por infra/scripts/02_app.sh em $(agora). Lido pelo docker compose. Não versionar.
POSTGRES_PASSWORD=$POSTGRES_PASSWORD
JWT_CHAVE=$JWT_CHAVE
SENHA_TECNICO=$SENHA_TECNICO
REDE_SUBNET=$REDE_SUBNET
REDE_GATEWAY=$REDE_GATEWAY
IP_API=$IP_API
IP_DB=$IP_DB
EOF
)
chmod 600 "$ARQUIVO_AMBIENTE"
log OK "Variáveis em $ARQUIVO_AMBIENTE (permissão 600: só o root lê)"

etapa "Imagens"
executar "Imagem do PostgreSQL 16 baixada" "${COMPOSE[@]}" pull db
log INFO "Compilando a API .NET dentro do Docker (na primeira vez leva alguns minutos)..."
executar "Imagem hakari-api construída" "${COMPOSE[@]}" build api

etapa "Serviço da aplicação (systemd)"
sed -e "s|@INFRA@|$INFRA|g" -e "s|@AMBIENTE@|$ARQUIVO_AMBIENTE|g" "$INFRA/config/hakari-app.service" \
  > /etc/systemd/system/hakari-app.service
executar "Unidade hakari-app.service instalada" systemctl daemon-reload
executar "hakari-app ligado no boot" systemctl enable hakari-app
executar "API e banco iniciados (docker compose up)" systemctl restart hakari-app

etapa "Conferindo a aplicação"
codigo=000
for _ in $(seq 1 40); do
  codigo=$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "http://$IP_API:$PORTA_API/health") || true
  [ "$codigo" = 200 ] && break
  sleep 3
done
if [ "$codigo" != 200 ]; then
  log ERRO "A API não respondeu em http://$IP_API:$PORTA_API/health (último código: $codigo). Veja: sudo docker logs hakari-api"
  exit 1
fi
log OK "Containers: $(docker ps --filter name=hakari --format '{{.Names}} ({{.Status}})' | tr '\n' ';' | sed 's/;$//; s/;/; /g')"
log OK "API saudável em http://api.$DOMINIO:$PORTA_API (rede interna)"

etapa "Nginx na porta 80"
sed "s|@API@|api.$DOMINIO:$PORTA_API|g" "$INFRA/config/nginx-hakari.conf" > /etc/nginx/sites-available/hakari
ln -sf /etc/nginx/sites-available/hakari /etc/nginx/sites-enabled/hakari
rm -f /etc/nginx/sites-enabled/default
executar "Configuração do Nginx válida" nginx -t
executar "Nginx ligado no boot" systemctl enable nginx
executar "Nginx recarregado" systemctl reload-or-restart nginx
# O reload do Nginx volta antes de a configuração nova valer; espera até 15 s pela resposta.
executar "Aplicação respondendo pela porta 80" bash -c \
  'for i in $(seq 1 15); do curl -sf --max-time 3 -o /dev/null http://127.0.0.1/health && exit 0; sleep 1; done; exit 1'
IP=$(ip_publico)
log OK "Aplicação publicada: http://${IP:-<ip-publico>}/swagger"
