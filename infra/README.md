# Hakari: servidor Linux provisionado por Shell Script

**Operating Systems · Global Solution 2026/2 · CitizenTech**
**Aluno:** Lucas Yuki (RM 554865) · **Vídeo:** [youtu.be/cPQ85GJY4b0](https://youtu.be/cPQ85GJY4b0)

Um comando prepara uma VM Ubuntu na Azure, do zero, para hospedar a aplicação web do Hakari: a API
em C#/.NET (com o Swagger) e o banco PostgreSQL. O mesmo comando configura rede, DNS, firewall,
monitoramento, auditoria e os serviços do systemd. Pode rodar de novo quando quiser: cada etapa
confere o que já existe e só ajusta o que falta.

```bash
sudo git clone https://github.com/LucasYuki1/Project_HAKARI.git /opt/hakari
sudo bash /opt/hakari/infra/provisionar.sh
```

Ao terminar, o script mostra o painel do servidor e o endereço `http://<ip-publico>/swagger`.

## Como fica o servidor

```
Internet ──► Azure (porta 80 e 22) ──► ufw ──► Nginx :80
                                                  │  proxy
                                                  ▼
                      rede interna hakari-net 172.28.0.0/24 (docker compose)
                      ├── hakari-api  172.28.0.10:8080  api.hakari.internal  (.NET, usuário comum)
                      └── hakari-db   172.28.0.20:5432  db.hakari.internal   (PostgreSQL 16, só a API enxerga)

systemd: hakari-app.service     liga e desliga a API e o banco
         hakari-monitor.service monitor com reinício automático e watchdog
logs:    /var/log/hakari/  ·  auditd  ·  journalctl
```

## Onde está cada item da avaliação

| Critério | Onde |
|---|---|
| Provisionamento da infraestrutura (redes, DNS, IP) | [`scripts/01_infra.sh`](scripts/01_infra.sh): hostname e `/etc/hosts`, DNS no systemd-resolved, levantamento de IP e rotas, parâmetros de rede do kernel, firewall ufw, swap e fuso horário |
| Provisionamento da aplicação (dependências, configurações, orquestração) | [`scripts/02_app.sh`](scripts/02_app.sh): instala Docker e Nginx, gera senhas e chaves, constrói a imagem da API ([`api/Dockerfile`](../api/Dockerfile)) e sobe tudo com [`config/docker-compose.yml`](config/docker-compose.yml) controlado pelo [`hakari-app.service`](config/hakari-app.service) |
| Monitoramento com alertas de CPU, disco e memória | [`scripts/monitorar.sh`](scripts/monitorar.sh): mede a cada 15 s, compara com os limites de [`hakari.conf`](hakari.conf) e gera alertas |
| Monitoramento como serviço tolerante a falhas | [`config/hakari-monitor.service`](config/hakari-monitor.service): reinício automático, watchdog e limite de reinícios |
| Logs de auditoria e execução dos scripts | [`scripts/lib.sh`](scripts/lib.sh) registra cada execução; o auditd acompanha os arquivos sensíveis ([`config/auditoria.rules`](config/auditoria.rules)) |
| Funcionamento completo | [`provisionar.sh`](provisionar.sh) roda as três etapas; [`scripts/status.sh`](scripts/status.sh) mostra o painel; [`scripts/teste_falha.sh`](scripts/teste_falha.sh) prova a recuperação |

## A máquina na Azure

| Item | Valor |
|---|---|
| Imagem | Ubuntu Server 24.04 LTS |
| Tamanho | Standard D2als v6 (2 vCPU, 4 GB de RAM) |
| Região | South Central US |
| Acesso | Chave SSH, usuário `azureuser` |
| Portas abertas no grupo de segurança | 22 (SSH) e 80 (HTTP) |
| Nome DNS público | Rótulo `hakari-gs` no IP público |

**Por que o script não muda o IP da placa de rede.** Na Azure, o IP da VM é entregue por DHCP pela
própria plataforma. Trocar esse IP de dentro da máquina derruba o SSH na hora. Por isso o IP fixo
é definido do lado da Azure, e o script cuida do que é da máquina: hostname, `/etc/hosts`, DNS,
firewall, parâmetros de rede do kernel e uma rede interna com IPs fixos para os containers.

## As etapas

### 1. Infraestrutura (`scripts/01_infra.sh`)
- **Pacotes base:** curl, git, jq, ufw, auditd, dig e stress-ng (usado nos testes de falha).
- **Identidade:** hostname `hakari-srv` e um bloco gerenciado no `/etc/hosts` com
  `hakari-srv.hakari.internal`, `api.hakari.internal` e `db.hakari.internal`. O cloud-init é
  orientado a não desfazer isso no reboot.
- **Horário:** fuso `America/Sao_Paulo` e NTP ligado.
- **DNS:** o systemd-resolved passa a usar 1.1.1.1 e 8.8.8.8, com 9.9.9.9 de reserva e
  `hakari.internal` como domínio de busca. O script testa um nome externo e um interno.
- **Rede e IP:** registra interface, IP privado, gateway e IP público, e aplica proteções de rede
  no kernel (SYN cookies, sem redirecionamentos ICMP).
- **Firewall:** ufw bloqueando toda entrada, exceto 22 e 80, e registrando as tentativas
  bloqueadas em `/var/log/ufw.log`.
- **Swap:** 2 GB, para a compilação da API não esgotar a memória.
- **Auditoria:** auditd ligado com regras para segredos, scripts, serviços, rede, firewall e contas.

### 2. Aplicação (`scripts/02_app.sh`)
- **Dependências:** Docker, Docker Compose e Nginx pelos pacotes do Ubuntu.
- **Segredos:** senha do banco, chave do JWT e senha do técnico de demonstração são geradas
  aleatoriamente na primeira instalação e guardadas em `/etc/hakari/hakari.env` (permissão 600).
  Nada disso fica no Git. Rodar de novo mantém as mesmas senhas.
- **Imagem da API:** compilada dentro do Docker em duas etapas; a imagem final só tem o runtime e
  roda com usuário comum. A configuração de desenvolvimento fica fora da imagem.
- **Orquestração:** o docker compose sobe o PostgreSQL, espera ele ficar saudável e então sobe a
  API, que aplica as migrações. O `hakari-app.service` liga tudo no boot.
- **Publicação:** o Nginx atende a porta 80 e repassa para `api.hakari.internal:8080`. Nenhuma
  porta de container é publicada no host, então o banco não é acessível de fora.

### 3. Monitoramento (`scripts/03_monitoramento.sh`)
Instala o monitor em `/usr/local/bin/hakari-monitor`, o serviço `hakari-monitor` e a rotação diária
dos logs (14 dias, comprimidos).

## O monitor

A cada 15 segundos ele mede CPU, memória e disco, consulta `http://127.0.0.1/health` (o caminho
completo: Nginx → API → banco) e confere os dois containers. Cada ciclo vira uma linha em
`monitor.log`.

| Situação | Severidade |
|---|---|
| CPU, memória ou disco acima de 85% | ALTA |
| Acima de 95%, ou a API fora do ar | DESASTRE |
| O problema acabou | NORMALIZADO |
| O monitor agiu sozinho | RECUPERACAO |

Os alertas vão para `alertas.log`, para o journal (`journalctl -t hakari-monitor`) e aparecem no
terminal de quem estiver logado (wall). Um alerta só é repetido a cada 10 minutos enquanto o
problema continuar. Se a API falhar duas checagens seguidas, o monitor religa o container que caiu;
se os containers estiverem no ar e a API não responder, reinicia a API. Os limites ficam em
[`hakari.conf`](hakari.conf).

**Tolerância a falhas do próprio monitor** (`hakari-monitor.service`):
- `Restart=always`: se o processo morrer, o systemd sobe outro em 5 s;
- `WatchdogSec=45`: o monitor dá sinal de vida a cada ciclo; se travar, o systemd mata e reinicia;
- `StartLimitBurst=10` em 5 min: se falhar em loop, o systemd para de insistir e marca a falha;
- `systemctl status hakari-monitor` mostra o último resumo (CPU, memória, disco e API).

## Logs e auditoria

| Arquivo | Conteúdo |
|---|---|
| `/var/log/hakari/provisionamento.log` | Cada passo e cada comando dos scripts, com a saída completa |
| `/var/log/hakari/auditoria.log` | Quem rodou qual script, quando, com quais argumentos, resultado e duração |
| `/var/log/hakari/monitor.log` | Uma linha por ciclo do monitor |
| `/var/log/hakari/alertas.log` | Só os alertas, normalizações e recuperações |
| `/var/log/audit/audit.log` | Eventos do auditd nos arquivos sensíveis |
| `/var/log/ufw.log` | Conexões bloqueadas pelo firewall |
| `/var/log/nginx/hakari_acesso.log` | Acessos à aplicação |

Consultas úteis:

```bash
sudo bash /opt/hakari/infra/scripts/status.sh           # painel do servidor
sudo tail -f /var/log/hakari/alertas.log                # alertas ao vivo
sudo journalctl -u hakari-monitor -f                    # monitor no journal
sudo aureport -k --summary                              # eventos de auditoria por chave
sudo ausearch -k hakari_segredos -i                     # quem mexeu ou leu os segredos
```

## Testes de falha

```bash
sudo bash /opt/hakari/infra/scripts/teste_falha.sh <teste>
```

| Teste | O que faz | O que acontece |
|---|---|---|
| `cpu` | CPU em 100% por 60 s | Alerta de CPU e, depois, normalização |
| `memoria` | Ocupa a memória até 90% por 60 s | Alerta de memória e normalização |
| `disco` | Enche o disco até 88% por 60 s | Alerta de disco; o arquivo de teste é apagado no fim |
| `api` | Para o container da API | O monitor detecta, religa o container e registra a recuperação |
| `banco` | Mata o PostgreSQL com `kill -9` | O Docker religa o banco sozinho (política de reinício) |
| `monitor` | Mata o monitor com `kill -9` | O systemd sobe um monitor novo em 5 s |
| `travar-monitor` | Congela o monitor (`kill -STOP`) | O watchdog percebe a falta de sinal e reinicia o monitor |

Cada teste também entra no `auditoria.log`.

## Refazer do zero

O `desfazer.sh` devolve a VM ao estado de antes do provisionamento:
- remove a aplicação, o banco e as imagens;
- desinstala Docker, Nginx, auditd e stress-ng;
- desfaz o hostname, o `/etc/hosts`, o DNS, o firewall, o swap e o fuso;
- apaga `/opt/hakari` e os logs.

Ele pede confirmação antes. Serve para repetir a demonstração sem recriar a VM:

```bash
sudo bash /opt/hakari/infra/scripts/desfazer.sh
sudo reboot
```

## Evidências

As saídas reais da VM na Azure (provisionamento completo, os sete testes de falha, painel, serviços,
rede, logs e auditoria) estão em [`evidencias/`](evidencias/README.md).

## Estrutura

```
infra/
  provisionar.sh            roda as três etapas e mostra o painel
  hakari.conf               variáveis: nomes, rede, limites do monitor, pastas
  scripts/
    lib.sh                  log, auditoria e funções comuns
    01_infra.sh             etapa 1: rede, DNS, hostname, firewall, swap, auditd
    02_app.sh               etapa 2: Docker, segredos, imagem, compose, Nginx
    03_monitoramento.sh     etapa 3: monitor como serviço e rotação de logs
    monitorar.sh            o monitor (vira /usr/local/bin/hakari-monitor)
    status.sh               painel do servidor
    teste_falha.sh          testes de falha
    desfazer.sh             volta a VM ao estado de antes do provisionamento
  evidencias/               saídas reais da VM: provisionamento, testes de falha, logs
  config/
    docker-compose.yml      API + PostgreSQL com IPs fixos
    nginx-hakari.conf       proxy da porta 80 para a API
    hakari-app.service      serviço da aplicação
    hakari-monitor.service  serviço do monitor
    auditoria.rules         regras do auditd
    logrotate-hakari        rotação dos logs
```
