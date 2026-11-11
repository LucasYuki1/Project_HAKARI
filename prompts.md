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
