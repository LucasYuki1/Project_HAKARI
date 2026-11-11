# Hakari API — backend em C# / .NET 10

**Aluno:** Lucas Yuki — RM 554865
**Disciplinas:** Arquitetura Orientada a Serviços (SOA) e Web Services · C# Software Development

API REST da solução Hakari: iluminação pública que se reporta sozinha. Os nós ESP32 nos postes
(pasta [`../iot`](../iot)) publicam telemetria e alertas por MQTT; esta API escuta essas mensagens,
transforma falhas em ocorrências automáticas e atende o app do cidadão e da equipe de manutenção.

## O problema

A base do SP156 não tem nenhum registro de lâmpada apagada e os pedidos de postes e fiação são os
mais lentos da cidade (só 28% resolvidos em até 15 dias — ver [`../ml/RELATORIO_ML.md`](../ml/RELATORIO_ML.md)).
Hoje o poste só entra na fila quando alguém reclama. No Hakari, o próprio poste abre o chamado.

## Arquitetura

```
api/
├─ src/
│  ├─ Hakari.Domain          Regras do negócio: entidades, enums, exceções. Sem banco, sem HTTP.
│  ├─ Hakari.Infrastructure  PostgreSQL via EF Core (DbContext, mapeamento, migrações) e BCrypt.
│  └─ Hakari.Api             Controllers REST, casos de uso (Aplicacao/), workers MQTT, Swagger.
└─ tests/
   └─ Hakari.Tests           xUnit: regras do domínio e casos de uso com banco em memória.
```

As dependências só apontam para dentro: `Api → Infrastructure → Domain`. O domínio não conhece
nenhuma das outras camadas.

```
ESP32 (Wokwi) ──MQTT──> MqttWorker ──┐
                                     ├─> Serviços (Aplicacao/) ─> Entidades (regras) ─> EF Core ─> PostgreSQL
App (cidadão/técnico) ──HTTP──> Controllers ──┘
IsolamentoWorker (a cada 30 s) ──────┘
```

### Onde cada conceito aparece

| Conceito | Onde |
|---|---|
| POO: encapsulamento | Entidades com `private set`: o status de uma `Ocorrencia` só muda por `Assumir()` e `Resolver()` |
| POO: herança e polimorfismo | `Usuario` abstrato → `Cidadao` e `Tecnico`, cada um sobrescreve `Perfil` (TPH no banco) |
| Interfaces e injeção de dependência | `IOcorrenciaService`, `IMonitorService`, `IHasherDeSenha`, `TimeProvider`... registrados em `Program.cs` |
| EF Core + LINQ | `Infrastructure/Dados`; projeções com `Select`, `GroupBy`, `AverageAsync`, filtros em `Include` |
| Migrações | `Infrastructure/Dados/Migracoes` |
| Async/await | Todos os acessos a banco e MQTT, com `CancellationToken` |
| Serviços em segundo plano | `MqttWorker` (assina `hakari/postes/#`) e `IsolamentoWorker` (detecta nó isolado) |
| Tratamento de erros | `TratadorDeErros`: exceções do domínio viram ProblemDetails (400/401/404/409/422) |
| Documentação automática | Swagger em `/swagger`, gerado a partir dos comentários `///` |

## Banco de dados

PostgreSQL 16 em Docker. Tabelas: `usuarios`, `postes`, `alertas`, `ocorrencias`,
`historico_ocorrencias`, `apoios`, `leituras`.

- Severidade é gravada como número, para a fila ordenar Desastre > Alta > Aviso; os outros enums
  ficam como texto, para o banco ser legível.
- Índices nas consultas mais frequentes: alertas ativos por poste, ocorrência ativa por poste e
  leituras por poste e data. A telemetria é a tabela que mais cresce (um heartbeat a cada 30 s por nó).
- LGPD: excluir a conta apaga os apoios (`CASCADE`) e deixa as ocorrências sem autor (`SET NULL`).
- Senhas só como hash BCrypt.

## Como rodar

Pré-requisitos: .NET SDK 10 e Docker.

```bash
cd api
docker compose up -d                       # PostgreSQL em localhost:5433
dotnet run --project src/Hakari.Api        # aplica as migrações, semeia os dados e abre o Swagger
```

A API sobe em http://localhost:5080. O Swagger abre em http://localhost:5080/swagger.

As migrações e o seed rodam sozinhos no ambiente de desenvolvimento. Para aplicá-las manualmente:

```bash
dotnet tool install -g dotnet-ef
dotnet ef database update -p src/Hakari.Infrastructure -s src/Hakari.Api
```

Testes:

```bash
dotnet test
```

### Dados iniciais

- Seis postes em São Paulo (`HKR-001` a `HKR-006`). O `HKR-001` é o nó do Wokwi.
- Um técnico de manutenção: `tecnico@hakari.dev`. A senha de demonstração está em
  `src/Hakari.Api/appsettings.Development.json` (`Seed:SenhaTecnico`).
- Cidadãos se cadastram por `POST /api/auth/cadastro`.

### Configuração fora do desenvolvimento

`appsettings.json` não guarda segredos. Em outro ambiente, defina por variável de ambiente:

| Variável | Para quê |
|---|---|
| `ConnectionStrings__Hakari` | Conexão com o PostgreSQL |
| `Jwt__Chave` | Chave de assinatura do JWT (mínimo 32 caracteres) |
| `Mqtt__Host`, `Mqtt__Porta` | Broker MQTT |
| `Banco__AplicarMigracoes` | `true` para migrar na inicialização |

## Endpoints

Todos, exceto cadastro e login, exigem `Authorization: Bearer <token>`.

| Método | Rota | Quem | Feature |
|---|---|---|---|
| POST | `/api/auth/cadastro` | público | F3 |
| POST | `/api/auth/login` | público | F3 |
| GET | `/api/postes?bairro=` | logado | F4 |
| GET | `/api/postes/{id}` | logado | F4 |
| GET | `/api/postes/{id}/telemetria?horas=24` | logado | F1/F4 |
| POST | `/api/postes/{id}/reportes` | cidadão | F5 |
| GET | `/api/ocorrencias/{id}` | logado | F6 |
| POST | `/api/ocorrencias/{id}/apoios` | cidadão | F5 |
| POST | `/api/ocorrencias/{id}/assumir` | técnico | F7 |
| POST | `/api/ocorrencias/{id}/resolver` | técnico | F7 |
| GET | `/api/manutencao/fila` | técnico | F7 |
| GET | `/api/manutencao/bairros` | técnico | F7 |
| GET | `/api/me` | logado | F8 |
| GET | `/api/me/ocorrencias` | cidadão | F6 |
| DELETE | `/api/me` | cidadão | F8 |
| GET | `/health` | público | monitoramento |

As Features e os critérios de aceite (Gherkin) estão em [`../docs/testing/features.md`](../docs/testing/features.md).

## Integração com o nó (MQTT)

O `MqttWorker` assina `hakari/postes/#` no broker público `broker.hivemq.com`:

| Tópico | O que a API faz |
|---|---|
| `.../{codigo}/telemetria` | Grava a leitura e atualiza a última comunicação do poste |
| `.../{codigo}/alerta` com `Ativo` | Abre o alerta (sem duplicar); Alta abre ocorrência automática |
| `.../{codigo}/alerta` com `Normalizado` | Normaliza o alerta e registra no histórico; a ocorrência segue aberta até a manutenção |
| `.../{codigo}/status` | `online` conta como comunicação; `offline` (last will) só vai para o log |

O nó não consegue avisar que sumiu. Por isso o `IsolamentoWorker` marca como **Isolado** (alerta
Desastre e ocorrência) o poste que fica 3 minutos sem mandar nada.

Para testar sem abrir o Wokwi, há um simulador de nó:

```bash
dotnet run scripts/simular_no.cs -- HKR-001 queimar       # alerta Alta -> ocorrência automática
dotnet run scripts/simular_no.cs -- HKR-001 normalizar
dotnet run scripts/simular_no.cs -- HKR-001 telemetria
```

O broker é público e sem autenticação, o que serve só para a demonstração. Em produção o broker
precisa de TLS e de credenciais por nó, para ninguém publicar alertas falsos.

## Testes automatizados

- `tests/Hakari.Tests`: xUnit cobrindo o ciclo da ocorrência, alertas sem duplicidade,
  isolamento, regras de senha e privacidade. Os casos de uso rodam com banco em memória e um
  relógio falso (`TimeProvider`), o que permite testar "3 minutos sem mensagem" sem esperar.
- `scripts/smoke_test.py`: percorre as Features F3 a F8 pela HTTP, com a API rodando
  (`python scripts/smoke_test.py`).

## Evidências

Prints do Swagger, das respostas e do banco ficam em [`docs/evidencias`](docs/evidencias).
