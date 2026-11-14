# Evidências do servidor

Saídas reais da VM da Azure (Ubuntu 24.04, Standard_B2s, IP 40.124.106.65), em 14/11/2026.
Nenhum arquivo traz senha ou chave: os segredos ficam só em `/etc/hakari/hakari.env` na VM.

| Arquivo | O que mostra |
|---|---|
| [`01-provisionamento.txt`](01-provisionamento.txt) | `sudo bash infra/provisionar.sh` do começo ao fim: infraestrutura, aplicação, monitoramento e o painel final |
| [`02-testes-de-falha.txt`](02-testes-de-falha.txt) | Os sete testes de falha em sequência, os alertas gerados e o monitor de volta depois do watchdog |
| [`03-teste-api-apos-ajuste.txt`](03-teste-api-apos-ajuste.txt) | A API derrubada de novo: Desastre, recuperação automática e normalização em 33 s |
| [`04-painel-status.txt`](04-painel-status.txt) | O painel `status.sh`: identidade, rede, serviços, containers, recursos, alertas e auditoria |
| [`05-servicos-systemd.txt`](05-servicos-systemd.txt) | Os serviços ativos e ligados no boot, e o histórico de reinícios do monitor (sinal e watchdog) |
| [`06-rede-dns-firewall.txt`](06-rede-dns-firewall.txt) | Hostname, `/etc/hosts`, DNS, IP e rotas, firewall, rede dos containers, ajustes do kernel e swap |
| [`07-logs.txt`](07-logs.txt) | `auditoria.log` (cada execução dos scripts), `alertas.log`, `monitor.log` e as permissões das pastas |
| [`08-auditoria-auditd.txt`](08-auditoria-auditd.txt) | Eventos do auditd por regra e os acessos mais recentes aos arquivos sensíveis |
