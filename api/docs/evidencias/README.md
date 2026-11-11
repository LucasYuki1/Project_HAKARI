# Evidências da API

Prints tirados com a API rodando (`docker compose up -d` e `dotnet run --project src/Hakari.Api`).
Os dados aparecem com nomes fictícios e e-mails `@exemplo.com`: nenhum dado pessoal real.

| Arquivo | O que mostra |
|---|---|
| `01-swagger.png` | Swagger com todos os endpoints agrupados |
| `02-cadastro.png` | `POST /api/auth/cadastro` devolvendo 201 e o token |
| `03-cadastro-email-repetido.png` | O mesmo e-mail de novo: 409 "Este e-mail já está em uso" |
| `04-postes.png` | `GET /api/postes` com os status, ao lado do nó HKR-001 no Wokwi |
| `05-reporte.png` | `POST /api/postes/{id}/reportes` abrindo a ocorrência |
| `06-fila-tecnico.png` | `GET /api/manutencao/fila` ordenada por severidade |
| `07-resolver-sem-assumir.png` | Resposta 422 documentada: "Assuma a ocorrência antes de resolvê-la" |
| `08-historico.png` | Técnico assume a ocorrência #2: status, responsável e histórico |
| `09-mqtt-automatica.png` | Log da API recebendo os alertas do Wokwi e marcando o nó como isolado |
| `10-banco.png` | Tabelas no PostgreSQL (`\dt` no psql) e `__EFMigrationsHistory` |
| `11-testes.png` | `dotnet test` com todos os testes aprovados |

Tudo reunido em um PDF: [Hakari_Evidencias_SOA_CSharp.pdf](Hakari_Evidencias_SOA_CSharp.pdf).
