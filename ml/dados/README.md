# Dados — SP156, reparos de infraestrutura do bairro

## Fonte

**Dados do SP156 - Solicitações**, Portal de Dados Abertos da Prefeitura de São Paulo.
https://dados.prefeitura.sp.gov.br/dataset/dados-do-sp156

Dicionário de dados (2020 em diante):
https://dados.prefeitura.sp.gov.br/dataset/0aecfa2b-aa3a-40d4-8183-0d4351b7fd0a/resource/e5c102e2-f27a-4e0a-ae99-5948abecff0e/download/dicionario-dados-abertos-sp156-2020-em-diante.txt

Dados públicos e reais. Nenhum dado pessoal: o SP156 não publica quem fez a solicitação, e o
recorte usado aqui ainda descarta logradouro, número, CEP e coordenadas.

## Arquivos e período

| Trimestre | Arquivo | Download | Abertura das solicitações |
|---|---|---|---|
| 2025T4 | `arquivo-final-4-trim-25.csv` | [link](https://dados.prefeitura.sp.gov.br/dataset/0aecfa2b-aa3a-40d4-8183-0d4351b7fd0a/resource/d3a98097-8128-45d4-b8fe-21874b81c971/download/arquivo-final-4-trim-25.csv) | 01/10/2025 a 31/12/2025 |
| 2026T1 | `arquivo-final-1-trim-26.csv` | [link](https://dados.prefeitura.sp.gov.br/dataset/0aecfa2b-aa3a-40d4-8183-0d4351b7fd0a/resource/75105148-4efd-48f3-8ae0-fc6c2adf7060/download/arquivo-final-1-trim-26.csv) | 01/01/2026 a 31/03/2026 |
| 2026T2 | `arquivo-final-2-trim-26.csv` | [link](https://dados.prefeitura.sp.gov.br/dataset/0aecfa2b-aa3a-40d4-8183-0d4351b7fd0a/resource/368eb32d-ef25-4441-be8f-fce0146575a1/download/arquivo-final-2-trim-26.csv) | 01/04/2026 a 30/06/2026 |
| 2026T3 | `arquivo-final-3-trim-26.csv` | [link](https://dados.prefeitura.sp.gov.br/dataset/0aecfa2b-aa3a-40d4-8183-0d4351b7fd0a/resource/f67fcc12-8da4-485f-92a3-d2af40cc771e/download/arquivo-final-3-trim-26.csv) | 01/07/2026 a 30/09/2026 |

Baixados em 06/11/2026. Os arquivos brutos (cerca de 430 MB) ficam fora do repositório, em
`dados/raw/`. Para reproduzir, baixe-os pelos links acima nessa pasta e rode
`python preparar_dados.py`.

## Recorte

Tema **Rua e bairro**, só os assuntos de reparo físico:

- Reparos em asfalto, pontes, viadutos e túneis (tapa-buraco)
- Fiação e postes
- Drenagem de água de chuva
- Capinação e roçada de áreas verdes
- Praças
- Ruas, vilas, vielas e escadarias

Ficam de fora PSIU, ambulantes, terrenos e eventos, que são fiscalização e não conserto.

Resultado: `sp156_rua_e_bairro.csv.gz`, com **231.236 solicitações**.

Iluminação pública não aparece no SP156: nenhum serviço da base menciona lâmpada, luminária ou
iluminação. Por isso o modelo trata da manutenção da infraestrutura do bairro como um todo.

## Tratamento

Os arquivos não seguem um formato único, e cada um é lido com o seu:

| Trimestre | Separador | Codificação | Datas |
|---|---|---|---|
| 2025T4 a 2026T2 | `;` | Latin-1 | dia/mês/ano |
| 2026T3 | `,` | UTF-8 | mês/dia/ano |

O portal não informa a data de extração de cada arquivo. Ela é aproximada pela última data de
finalização presente no arquivo inteiro: 07/08/2026 para 2025T4, 06/08/2026 para 2026T1 e
2026T2, e 08/10/2026 para 2026T3.

## Colunas do recorte

| Coluna | Descrição |
|---|---|
| `abertura` | Data de abertura da solicitação |
| `finalizacao` | Data de finalização (vazia se ainda aberta) |
| `meio` | Canal: PORTAL, MOBILE, CRM (central telefônica), CHATBOT |
| `assunto` | Assunto dentro do tema Rua e bairro |
| `servico` | Serviço solicitado |
| `status` | Aberto, Em Andamento, Finalizada, Cancelada |
| `orgao` | Órgão responsável |
| `prefeitura` | Prefeitura operacional (antiga subprefeitura) |
| `distrito` | Distrito |
| `trimestre` | Trimestre do arquivo de origem |
| `data_extracao` | Data de extração aproximada do arquivo |

## Limitações

- O SP156 registra pedidos, não problemas: um buraco sem reclamação não aparece na base.
- A Prefeitura avisa que solicitações podem ser reclassificadas entre órgãos e assuntos de um
  relatório para outro. "Fiação e postes" passa de 738 pedidos em 2025T4 para 13.332 em 2026T3,
  o que indica mudança de classificação, e não só aumento de demanda.
- Pedidos ainda abertos têm resultado desconhecido. O tratamento disso está no notebook, na
  definição do rótulo.
