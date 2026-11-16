# Registro de uso de IA

A GS permite IA como copiloto, desde que os prompts fiquem registrados e cada parte possa ser
explicada. Usei o Claude (Anthropic) no Claude Code, dentro do próprio repositório. Abaixo estão
os pedidos principais de cada disciplina, o que a IA produziu e o que eu revisei ou decidi.

## Testing, Compliance & Quality Assurance

**Pedido:** transformar a ideia do Hakari em Features do Azure Boards (process Agile), com
critérios de aceite em Gherkin, e importar tudo no projeto ProjectHAKARI.

**Resultado:** `docs/testing/features.md` (F1 a F8 com parâmetros e severidades),
`azure-boards.json` e o script `import-azure.ps1`.

**Minha parte:** definir o escopo (cidadão com cadastro, técnico no próprio app, modelo de triggers
Aviso/Alta/Desastre inspirado no Zabbix), revisar os cenários e montar o PDF de entrega.

## Inteligência Artificial & Machine Learning

**Pedido:** validar a base pública do SP156, escolher um modelo, testar hiperparâmetros e mostrar
treino × teste. Depois, ajuda com o roteiro e a edição do vídeo a partir das minhas gravações.

**Resultado:** a pasta `ml/` (preparo dos dados, experimentos, `RELATORIO_ML.md`) e o vídeo
editado (cortes, legendas, cartelas e música de fundo).

**Minha parte:** a pergunta de negócio (quais pedidos de zeladoria passam do prazo), a leitura dos
resultados e a gravação e narração do vídeo.

## Physical Computing: IOT & IOB

**Pedido:** um circuito ESP32 no Wokwi que fizesse parte da solução, seguindo as regras de F1 e F2.

**Resultado:** `iot/sketch.ino`, `iot/diagram.json` e `iot/README.md`. São dois LDRs (ambiente e
referência na própria lâmpada), um HC-SR04, PWM na lâmpada e MQTT com last will. Também a edição
do vídeo de demonstração.

**Minha parte:** montar e salvar o projeto no Wokwi, testar os cenários pelo cliente MQTT da
HiveMQ e gravar o vídeo.

## SOA e Web Services · C# Software Development

**Pedido:** uma única API em C#/.NET que atendesse as duas disciplinas, com uma arquitetura
profissional mas que eu conseguisse entender e explicar. O banco escolhido foi o PostgreSQL.

**Resultado:** a pasta `api/`:
- solução em 4 projetos (Domain, Infrastructure, Api, Tests);
- EF Core com migrações, JWT e Swagger;
- workers que escutam o MQTT do Wokwi e detectam o isolamento do nó;
- testes xUnit e um script de teste de ponta a ponta.

**Minha parte:**
- escolher o banco e aprovar a arquitetura depois de uma explicação camada por camada;
- validar as regras de negócio contra as Features que eu já tinha escrito;
- tirar os prints de evidência.

## Mobile Development & IOT

**Pedido:** um app em Expo que consumisse a API do projeto e cobrisse a jornada do cidadão, com a
tela da manutenção no mesmo app, rodando no meu celular pelo Expo Go.

**Resultado:** a pasta `mobile/`:
- Expo Router com rotas protegidas por perfil (cidadão e técnico);
- cliente da API com token JWT e mensagens de erro simples;
- banco SQLite no celular para a sessão, a lista de postes sem internet e os rascunhos de reporte;
- componentes com rótulos de acessibilidade, contraste AA e status com ícone e texto;
- o README com a Ficha da CitizenTech na raiz do repositório.

**Minha parte:** definir a jornada a partir das Features, testar o app no celular contra a API, tirar
os prints e gravar o vídeo.

## Operating Systems

**Pedido:** uma infraestrutura Linux que se provisionasse sozinha por Shell Script para hospedar a
API do projeto. Pedi para usar uma VM na Azure em vez de VM local, porque o VMware não roda bem no
meu computador.

**Resultado:** a pasta `infra/`:
- `provisionar.sh` com três etapas: infraestrutura (hostname, DNS, firewall, swap, auditd),
  aplicação (Docker, segredos gerados na instalação, docker compose e Nginx) e monitoramento;
- um monitor de CPU, memória, disco e da API rodando como serviço do systemd, com watchdog e
  recuperação automática;
- logs de execução e de auditoria, o painel `status.sh` e os testes de falha;
- o `Dockerfile` da API.

**Minha parte:** criar e configurar a VM na Azure, rodar o provisionamento e os testes de falha,
conferir os logs e gravar o vídeo.

## Cybersecurity

**Pedido:** a missão da disciplina para o Hakari. Pedi três coisas:
- mapear os dados pessoais e criar regras para CPF, saúde e senhas;
- rastrear o fluxo desses dados e bloquear vazamentos;
- traduzir as falhas para o cidadão com um LLM e montar o painel Risco Cidadão.

**Resultado:** a pasta `seguranca/` e o motor de PII da API:
- regras Semgrep de mapeamento de PII, de taint (fonte, sorvedouro e sanitizador) e de segredos,
  cada uma com casos de teste;
- o motor de PII em C# (`api/src/Hakari.Domain/Privacidade`), com regras em JSON, validadores de CPF,
  CNS, Luhn e DDD, léxico de saúde e o tipo `TextoSeguro`;
- o teste dinâmico com canários e o corpus de 76 textos com métricas de precisão e cobertura;
- as correções do protótipo:
  - texto livre filtrado;
  - segredos de desenvolvimento no user-secrets;
  - sessão do app no `expo-secure-store`;
  - nome fora do JWT;
  - banco local só em `127.0.0.1`;
- o pipeline `auditar.py`, a fórmula do Risco Cidadão, o painel em Streamlit e o workflow de CI.

**IA dentro do produto:** a auditoria chama o Claude Opus 5.5 pela API da Anthropic para escrever o
alerta de cada falha e o resumo executivo. O código enviado vai mascarado, sem senha nem dado
pessoal.

**Minha parte:**
- escolher o Claude e criar a chave da API;
- autorizar a troca do armazenamento da sessão no app;
- rodar a auditoria e conferir os alertas;
- gravar o vídeo e tirar o print do painel.

