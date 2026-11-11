# Evidências da API

Prints tirados com a API rodando (`docker compose up -d` e `dotnet run --project src/Hakari.Api`).
Os dados aparecem com nomes fictícios e e-mails `@exemplo.com`: nenhum dado pessoal real.

| Arquivo | O que mostra |
|---|---|
| `01-swagger.png` | Swagger com todos os endpoints agrupados |
| `02-cadastro.png` | `POST /api/auth/cadastro` devolvendo 201 e o token |
| `03-cadastro-email-repetido.png` | O mesmo e-mail de novo: 409 "Este e-mail já está em uso" |
| `04-postes.png` | `GET /api/postes` com os status |
| `05-reporte.png` | `POST /api/postes/{id}/reportes` abrindo a ocorrência |
| `06-fila-tecnico.png` | `GET /api/manutencao/fila` ordenada por severidade |
| `07-resolver-sem-assumir.png` | 422 "Assuma a ocorrência antes de resolvê-la" |
| `08-historico.png` | Detalhe da ocorrência resolvida, com o histórico |
| `09-mqtt-automatica.png` | Log da API recebendo o alerta do Wokwi e a ocorrência automática na fila |
| `10-banco.png` | Tabelas no PostgreSQL (`\dt` no psql) e `__EFMigrationsHistory` |
| `11-testes.png` | `dotnet test` com todos os testes aprovados |
