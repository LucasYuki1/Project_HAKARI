# Hakari: iluminação pública que se reporta sozinha

**Global Solution 2026/2 · CitizenTech · FIAP Engenharia de Software**
**Aluno:** Lucas Yuki (RM 554865)

## Ficha da CitizenTech

| | |
|---|---|
| **Nome da CitizenTech** | Hakari |
| **Cidade e bairro** | São Paulo (SP). Piloto com postes na Bela Vista, Jardim Paulista, República, Vila Mariana, Ermelino Matarazzo e Santo Amaro. |
| **Cidadão beneficiado** | Quem caminha à noite pela rua: o morador que volta do trabalho ou da escola e o pedestre que depende da luz do poste para se sentir seguro. |
| **Problema, em uma frase** | Um poste apagado só entra na fila da Prefeitura quando alguém reclama, e esse tipo de reparo é dos mais lentos da cidade. |
| **Como funciona** | Um nó ESP32 em cada poste controla a lâmpada e confere, com um sensor de luz apontado para ela, se ela realmente acendeu. Quando a lâmpada falha ou o nó para de responder, ele avisa por MQTT e a API abre a ocorrência sozinha. O cidadão acompanha pelo app e pode reportar ou apoiar; a manutenção atende pela fila, da mais grave para a menos grave. |
| **O que cada parte faz** | `iot/`: o nó do poste no Wokwi. `api/`: a API em C#/.NET que recebe os alertas e guarda as ocorrências no PostgreSQL. `mobile/`: o app em Expo do cidadão e da manutenção. `ml/`: o modelo que prevê se um pedido de reparo do SP156 passa do prazo. `docs/testing/`: as Features e critérios de aceite. `infra/`: o ambiente Linux. |
| **Link do repositório** | https://github.com/LucasYuki1/Project_HAKARI |
| **Indicador de impacto** | Tempo entre a lâmpada apagar e a ocorrência ser aberta: hoje depende de alguém reclamar; com o Hakari, até 3 minutos. Hoje só 28% dos pedidos de fiação e postes do SP156 são resolvidos em até 15 dias (ver `ml/RELATORIO_ML.md`). |
| **ODS conectados** | 11 (Cidades e comunidades sustentáveis), 9 (Indústria, inovação e infraestrutura), 13 (Ação contra a mudança global do clima: a lâmpada só fica em 100% quando há alguém passando). |

## Como as partes se conectam

```
ESP32 no poste ──MQTT──> broker ──> API Hakari (.NET + PostgreSQL) ──> app Expo (cidadão e manutenção)
```

## Pastas

| Pasta | Disciplina | O que tem |
|---|---|---|
| [`docs/testing`](docs/testing) | Testing, Compliance & QA | Features, critérios de aceite em Gherkin, importação no Azure Boards |
| [`ml`](ml) | Inteligência Artificial & Machine Learning | Notebook, preparo dos dados do SP156 e `RELATORIO_ML.md` |
| [`iot`](iot) | Physical Computing | Sketch do ESP32, circuito do Wokwi e documentação |
| [`api`](api) | SOA e Web Services · C# Software Development | API REST, banco, testes e evidências |
| [`mobile`](mobile) | Mobile Development & IOT | App em Expo com React Native e TypeScript |
| [`infra`](infra) | Operating Systems | Provisionamento e monitoramento em Linux |

O uso de IA em cada parte está registrado em [`prompts.md`](prompts.md). Todos os dados de pessoas
são fictícios; os dados do SP156 são públicos e não têm dados pessoais.
