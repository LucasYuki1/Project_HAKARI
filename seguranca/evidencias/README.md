# Evidências da auditoria de privacidade

Saídas reais geradas em 14 e 15/11/2026 na máquina de desenvolvimento. Nenhum arquivo traz senha, chave
ou dado pessoal real: os trechos de código saem mascarados (`***`), e os dados dos testes são
fictícios.

| Arquivo | O que mostra |
|---|---|
| [`01-testes-das-regras.txt`](01-testes-das-regras.txt) | `semgrep --test`: as 23 regras passam em 76 casos (41 que precisam ser apontados, 35 que não podem), com a contagem por regra e a limitação conhecida do Semgrep |
| [`02-testes-de-privacidade.txt`](02-testes-de-privacidade.txt) | Os 34 testes de privacidade da API: validadores, mascaramento, corpus e canários |
| [`03-canarios.json`](03-canarios.json) | Teste de canários. O controle, com o motor sem regras, tem 26 vazamentos dos 6 dados nas 9 saídas. O Hakari atual tem 0 vazamentos e mostra a mensagem que o cidadão recebe |
| [`04-motor-metricas.json`](04-motor-metricas.json) | Métricas do motor de PII no corpus de 76 textos: precisão, cobertura e F1, por regra |
| [`05-auditoria-antes.txt`](05-auditoria-antes.txt) | Auditoria do commit `4470f4f`, antes da entrega: Risco Cidadão 96,7 (Crítico). Traz os 9 achados com o alerta escrito pelo Claude, o teste de canários e o resumo executivo |
| [`06-auditoria-depois.txt`](06-auditoria-depois.txt) | Auditoria do código corrigido: Risco Cidadão 6,5 (Baixo), 0 achados abertos, 2 riscos aceitos com justificativa e 0 vazamentos |
| [`painel-risco-cidadao.png`](painel-risco-cidadao.png) | Print do painel logo depois de a auditoria rodar pelo botão "Rodar auditoria agora" (15/11, 16:01), a mesma execução que aparece no vídeo: Risco Cidadão 6,5 (Baixo), o resumo escrito pelo Claude e os quatro indicadores |

Os retratos completos de cada auditoria, que o painel lê, estão em
[`../resultados/historico/`](../resultados/historico).
