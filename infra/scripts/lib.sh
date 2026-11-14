#!/usr/bin/env bash
# Funções usadas por todos os scripts: leitura da configuração, log com data e hora,
# registro de auditoria de cada execução e ajudas para editar arquivos do sistema com segurança.

INFRA="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=../hakari.conf
source "$INFRA/hakari.conf"

SCRIPT_NOME="$(basename "$0")"
LOG_EXECUCAO="$DIR_LOGS/provisionamento.log"
LOG_AUDITORIA="$DIR_LOGS/auditoria.log"

# apt sem perguntas, esperando a trava do dpkg se outro processo (ex.: atualização automática) estiver usando
export DEBIAN_FRONTEND=noninteractive NEEDRESTART_SUSPEND=1
APT=(apt-get -y -q -o DPkg::Lock::Timeout=300)

if [ -t 1 ]; then
  VERDE=$'\e[32m' AMARELO=$'\e[33m' VERMELHO=$'\e[31m' CIANO=$'\e[1;36m' NORMAL=$'\e[0m'
else
  VERDE='' AMARELO='' VERMELHO='' CIANO='' NORMAL=''
fi

agora() { date '+%Y-%m-%d %H:%M:%S'; }

# log NIVEL mensagem: mostra na tela e grava em provisionamento.log
log() {
  local nivel="$1" cor=""
  shift
  case "$nivel" in OK) cor="$VERDE" ;; AVISO) cor="$AMARELO" ;; ERRO) cor="$VERMELHO" ;; esac
  printf '%s %s%-5s%s %s\n' "$(date +%H:%M:%S)" "$cor" "$nivel" "$NORMAL" "$*"
  printf '%s [%s] [%s] %s\n' "$(agora)" "$nivel" "$SCRIPT_NOME" "$*" >> "$LOG_EXECUCAO"
}

etapa() {
  printf '\n%s▶ %s%s\n' "$CIANO" "$*" "$NORMAL"
  printf '%s [ETAPA] [%s] %s\n' "$(agora)" "$SCRIPT_NOME" "$*" >> "$LOG_EXECUCAO"
}

# executar "descrição" comando...: roda, guarda a saída no log e interrompe o script se falhar
executar() {
  local descricao="$1" codigo
  shift
  printf '%s [CMD] [%s] %s\n' "$(agora)" "$SCRIPT_NOME" "$*" >> "$LOG_EXECUCAO"
  if "$@" >> "$LOG_EXECUCAO" 2>&1; then
    log OK "$descricao"
  else
    codigo=$?
    log ERRO "$descricao: falhou (código $codigo). Detalhes em $LOG_EXECUCAO"
    exit "$codigo"
  fi
}

# tentar "descrição" comando...: igual ao executar, mas só avisa se falhar
tentar() {
  local descricao="$1"
  shift
  printf '%s [CMD] [%s] %s\n' "$(agora)" "$SCRIPT_NOME" "$*" >> "$LOG_EXECUCAO"
  if "$@" >> "$LOG_EXECUCAO" 2>&1; then
    log OK "$descricao"
  else
    log AVISO "$descricao: não foi possível, seguindo em frente"
  fi
}

exigir_root() {
  if [ "$EUID" -ne 0 ]; then
    echo "Este script precisa de root. Use: sudo bash $0" >&2
    exit 1
  fi
  install -d -m 750 "$DIR_LOGS"
}

# Auditoria das execuções: quem rodou, quando, com quais argumentos e como terminou.
# Vai para auditoria.log e para o journal do sistema (journalctl -t hakari-auditoria).
iniciar_auditoria() {
  INICIO_EXECUCAO=$(date +%s)
  local registro="inicio | script=$SCRIPT_NOME | usuario=${SUDO_USER:-$(id -un)} | args=${*:-nenhum} | host=$(hostname)"
  echo "$(agora) | $registro" >> "$LOG_AUDITORIA"
  logger -t hakari-auditoria "$registro"
  trap 'finalizar_auditoria $?' EXIT
}

finalizar_auditoria() {
  local codigo="$1" resultado=sucesso registro
  [ "$codigo" -eq 0 ] || resultado=falha
  registro="fim    | script=$SCRIPT_NOME | resultado=$resultado | codigo=$codigo | duracao=$(( $(date +%s) - INICIO_EXECUCAO ))s"
  echo "$(agora) | $registro" >> "$LOG_AUDITORIA"
  logger -t hakari-auditoria "$registro"
}

# Blocos gerenciados: trechos marcados que os scripts gravam dentro de arquivos do sistema
# (ex.: /etc/hosts) sem mexer no resto do arquivo.
MARCA_INICIO="# >>> HAKARI: gerenciado por infra/scripts >>>"
MARCA_FIM="# <<< HAKARI <<<"

sem_bloco() {   # imprime o arquivo sem o bloco do Hakari
  awk -v i="$MARCA_INICIO" -v f="$MARCA_FIM" '$0==i{pula=1; next} $0==f{pula=0; next} !pula' "$1"
}

# Grava (ou substitui) o bloco. Rodar de novo troca o bloco em vez de duplicar.
bloco_gerenciado() {
  local arquivo="$1" conteudo="$2" resto
  touch "$arquivo"
  resto="$(sem_bloco "$arquivo")"
  printf '%s\n%s\n%s\n%s\n' "$resto" "$MARCA_INICIO" "$conteudo" "$MARCA_FIM" > "$arquivo"
}

remover_bloco() {
  local arquivo="$1" resto
  [ -f "$arquivo" ] || return 0
  resto="$(sem_bloco "$arquivo")"
  printf '%s\n' "$resto" > "$arquivo"
}

ip_publico() { curl -s --max-time 5 https://api.ipify.org 2>/dev/null || true; }
interface_padrao() { ip route show default | awk '{print $5; exit}'; }
