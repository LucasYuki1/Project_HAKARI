# Hakari: auditoria de privacidade e Risco Cidadão

**Cybersecurity · Global Solution 2026/2 · CitizenTech**
**Aluno:** Lucas Yuki (RM 554865) · **Vídeo:** [youtu.be/9-7lvfZ55Fg](https://youtu.be/9-7lvfZ55Fg)

O Hakari guarda o cadastro do cidadão (nome, e-mail, telefone e senha) e publica ocorrências escritas
em texto livre, que outros cidadãos e a manutenção leem. Esta pasta é o módulo de privacidade do
projeto. Ele faz quatro coisas:

1. mapeia onde o código guarda dado pessoal;
2. rastreia por onde esse dado pode vazar e bloqueia o vazamento;
3. traduz cada falha, com IA, num alerta que o cidadão entende;
4. resume tudo no indicador **Risco Cidadão**, de 0 a 100.

## O resultado

| | Antes (commit `4470f4f`) | Depois |
|---|---|---|
| **Risco Cidadão** | **96,7 (Crítico)** | **6,5 (Baixo)** |
| Achados abertos | 9 (4 graves) | 0 |
| Riscos aceitos, com justificativa | 0 | 2 |
| Vazamentos no teste de canários | 26, nas 9 saídas verificadas | 0 |
| Dados pessoais mapeados no código | 38 campos | 36 campos (token e nome saíram do SQLite do celular) |

## Onde está cada critério

| Critério | O que foi feito | Onde |
|---|---|---|
| Robustez do motor de Parsing PII (3,0) | **Motor de PII** com 16 regras configuráveis em JSON. CPF, cartão do SUS e cartão de pagamento só contam se o dígito verificador bater; o telefone precisa de DDD válido. Saúde é detectada por léxico, com exceções como "ponto cego". O motor também pega senhas, tokens e chaves. A isso se somam **9 regras Semgrep** que mapeiam os campos pessoais do código. São 76 casos de teste no corpus. | [`regras-pii.json`](../api/src/Hakari.Domain/Privacidade/regras-pii.json), [`MotorPii.cs`](../api/src/Hakari.Domain/Privacidade/MotorPii.cs), [`regras/pii-mapeamento.yaml`](regras/pii-mapeamento.yaml) |
| Eficácia do Taint Analysis (3,0) | **8 regras de taint** (fonte, sorvedouro e sanitizador) para a API e o app. O tipo `TextoSeguro` faz o compilador recusar texto sem filtro. O **teste de canários** planta dados fictícios na entrada e procura nas saídas, com um controle que prova que o teste enxerga o vazamento. O **CI** bloqueia o merge quando há vazamento. | [`regras/pii-taint.yaml`](regras/pii-taint.yaml), [`TaintCanarioTests.cs`](../api/tests/Hakari.Tests/Privacidade/TaintCanarioTests.cs), [`privacidade.yml`](../.github/workflows/privacidade.yml) |
| Inovação com IA na remediação (2,0) | O **Claude Opus 5.5** escreve, para cada falha, o alerta ao cidadão e a correção sugerida à equipe, com o trecho de código pronto. Também escreve o resumo executivo. O código vai mascarado, sem senha nem dado pessoal, a resposta tem formato fixo (JSON Schema) e fica em cache. | [`auditoria/ia.py`](auditoria/ia.py) |
| Dashboard Executivo, Risco Cidadão (2,0) | **Painel** em Streamlit com o indicador, de onde vem o risco (antes × agora), alertas, canários, mapa de dados e métricas do motor. O botão "Rodar auditoria" executa tudo na hora. | [`painel.py`](painel.py), [`auditoria/risco.py`](auditoria/risco.py) |

## Como rodar

Na raiz do repositório (Windows; no Linux troque `Scripts` por `bin`):

```bash
python -m venv seguranca/.venv
seguranca/.venv/Scripts/pip install -r seguranca/requirements.txt
```

A IA é opcional. Para usar, copie `seguranca/.env.example` para `seguranca/.env` e cole a sua chave
da API da Anthropic. O `.env` fica fora do Git.

```bash
# retrato "antes": o último commit antes desta entrega, numa cópia temporária
seguranca/.venv/Scripts/python seguranca/auditar.py --ref 4470f4f --rotulo antes --ignorar-aceites

# retrato do código atual
seguranca/.venv/Scripts/python seguranca/auditar.py --rotulo depois

# painel (com o Streamlit do venv ou, se ele já estiver no Python da máquina, python -m streamlit)
seguranca/.venv/Scripts/streamlit run seguranca/painel.py
```

Testes:

```bash
seguranca/.venv/Scripts/semgrep --test --metrics=off --config seguranca/regras/ seguranca/regras/testes/
dotnet test api/Hakari.slnx --filter "FullyQualifiedName~Privacidade"
```

O `auditar.py` termina com código 1 quando há achado grave em aberto ou teste de privacidade
falhando. É isso que o CI usa para bloquear o merge.

## 1. Mapeamento de PII

O motor trabalha em duas camadas.

**No código (Semgrep).** As regras de [`pii-mapeamento.yaml`](regras/pii-mapeamento.yaml)
encontram propriedades, parâmetros de record, tipos do app e colunas do SQLite com nome de dado
pessoal e classificam cada um: identificação, contato, credencial, saúde, localização, texto livre e
consentimento. O resultado é o mapa de dados do painel: 36 campos em 3 camadas (banco, API e app).

O Hakari não pede CPF nem dado de saúde, por minimização (LGPD, art. 6º, III). Mesmo assim, o mapa
mostra o ponto fraco: o **texto livre**. Quem reporta um poste pode escrever o próprio CPF, o
telefone ou que é cadeirante, e outras pessoas leem essa descrição.

**No texto (motor de PII, C#).** O [`MotorPii`](../api/src/Hakari.Domain/Privacidade/MotorPii.cs) roda
dentro da API e mascara o texto livre antes de guardá-lo. As regras ficam em
[`regras-pii.json`](../api/src/Hakari.Domain/Privacidade/regras-pii.json), embutido na DLL. Para
criar uma regra nova, basta acrescentar um item ao arquivo.

| Regra | O que encontra | Como evita falso positivo |
|---|---|---|
| `cpf` | CPF com ou sem pontuação | Dígito verificador; sem ele, só com "CPF" escrito perto |
| `cns` | Cartão Nacional de Saúde (dado sensível) | Soma ponderada do CNS |
| `cartao` | Cartão de pagamento | Algoritmo de Luhn |
| `telefone` | Fixo e celular, com ou sem +55 | DDD da lista da Anatel; celular com o 9 na frente |
| `email` | E-mail | Formato |
| `rg` | RG | Só com "RG" ou "identidade" perto |
| `data-nascimento` | Data de nascimento | Só com "nasci" ou "nascimento" perto; "apagado desde 19/11/2026" passa |
| `nome-declarado` | "Meu nome é Carlos Lima" | Nome próprio em maiúscula depois da expressão |
| `endereco-residencial` | "Moro na Rua das Acácias, 123" | Exige "moro", "minha casa fica" etc.; o endereço do poste é público e passa |
| `placa` | Placa antiga e Mercosul | Só com "carro", "moto" ou "placa" perto |
| `senha-texto` | "minha senha é Hakari@2026" | O valor precisa ter dígito ou símbolo; "a senha é muito fraca" passa |
| `token-jwt`, `hash-senha`, `chave-acesso` | Token JWT, hash BCrypt, chaves `sk-ant-`, `AKIA`, `ghp_` | Formato |
| `saude-condicao` | Cerca de 90 termos: diabetes, cadeirante, gestante, AVC, epilepsia, baixa visão... | Palavra inteira, sem acento; exceções como "ponto cego", "beco cego", "derrame de óleo" |
| `saude-cid10` | Código de doença (CID-10) | Só com "CID", "laudo" ou "atestado" perto |

Para dar mais robustez ao motor:
- cada achado tem uma confiança: o validador e as palavras de contexto aumentam essa confiança;
- quando dois achados disputam o mesmo trecho, fica o mais confiável;
- toda regex tem tempo máximo. Se um texto montado para travar a regex (ReDoS) estourar o tempo, o
  motor **falha fechado**: esconde o texto inteiro em vez de deixá-lo passar;
- o achado guarda a posição e o tipo, nunca o valor. Por isso o próprio relatório não vaza o dado.

**Métricas** ([`CorpusPiiTests`](../api/tests/Hakari.Tests/Privacidade/CorpusPiiTests.cs)): o corpus
tem 76 textos de reporte, 46 com dado pessoal e 30 sem. Os textos sem dado pessoal foram escolhidos
para enganar o motor: protocolo com cara de CPF, 0800, DDD inexistente, código do poste, "ponto
cego". O resultado é **precisão de 100% e cobertura de 100%**.

O corpus foi escrito junto com as regras, então essas métricas medem consistência e servem de
regressão. Elas não medem desempenho em texto desconhecido. O teste exige no mínimo 95% e
também exige pelo menos um caso positivo para cada regra.

## 2. Taint Analysis

O dado pessoal entra pelas **fontes**: o cadastro e o texto livre do reporte e da solução. Ele pode
vazar pelos **sorvedouros**:
- a tela de outros cidadãos;
- os logs;
- as mensagens de erro;
- o token JWT;
- o SQLite do celular;
- a URL da requisição;
- o Git.

O vazamento só é aceito se o dado passar por um **sanitizador**: o motor de PII, o hash da senha ou o
nome abreviado.

**Análise estática (Semgrep, modo taint).** São 8 regras em [`pii-taint.yaml`](regras/pii-taint.yaml).
Somadas às de mapeamento e de segredos ([`segredos.yaml`](regras/segredos.yaml)), as 23 regras
passam em 76 casos de teste: 41 marcados com `ruleid`, que precisam ser apontados, e 35 com `ok`,
que não podem ser.

Durante a auditoria apareceu um ponto cego do próprio Semgrep 1.183: em C#, depois de uma expressão
`?? throw`, ele perde o fluxo. Foi por isso que o reporte do cidadão escapou da varredura. A
limitação está registrada como `todoruleid` nos testes e foi coberta de duas formas:

- **barreira de tipo:** a entidade `Ocorrencia` só aceita
  [`TextoSeguro`](../api/src/Hakari.Domain/Privacidade/TextoSeguro.cs), e só o motor cria um. O
  compilador recusa qualquer caminho em que o texto digitado chegue ao banco sem filtro;
- **teste dinâmico com canários** ([`TaintCanarioTests`](../api/tests/Hakari.Tests/Privacidade/TaintCanarioTests.cs)):
  seis dados fictícios e válidos (CPF, telefone, e-mail, endereço, condição de saúde e cartão do SUS)
  entram no reporte e na solução do técnico. O teste procura cada um em 9 saídas: as respostas da
  API para a autora, para o vizinho e para o técnico, e as colunas do banco. Ele roda duas vezes:
  - **controle**, com o motor sem regras, como era o Hakari: **26 vazamentos**, os 6 dados;
  - **protegido**, com o Hakari atual: **0 vazamentos**.

  O controle prova que o teste enxergaria um vazamento se ele existisse.

**Onde o vazamento é bloqueado:**
- **na API:** o motor mascara o texto antes de gravar, e o cidadão recebe o aviso "Para proteger
  você, escondemos da descrição: CPF, telefone...";
- **no compilador:** o `TextoSeguro`;
- **no CI:** o workflow [`privacidade.yml`](../.github/workflows/privacidade.yml) falha o pull
  request.

### O que a auditoria encontrou e o que mudou

| Achado no protótipo | Gravidade | Correção |
|---|---|---|
| Descrição do reporte e solução do técnico publicadas sem filtro | ERROR | Motor de PII no `OcorrenciaService` + `TextoSeguro` no domínio |
| Chave de assinatura do JWT no `appsettings.Development.json` versionado | ERROR | Movida para o user-secrets do .NET; a chave foi **trocada** |
| Senha do técnico de demonstração no mesmo arquivo | ERROR | Movida para o user-secrets |
| Token de acesso salvo em texto puro no SQLite do celular | ERROR | `expo-secure-store` (Keystore/Keychain). Na primeira abertura, a sessão antiga migra, a tabela é apagada e o `VACUUM` limpa o arquivo |
| Nome completo dentro do JWT, legível por quem tiver o token | WARNING | Claim removida; o app recebe o nome no corpo da resposta |
| Senha do banco na connection string versionada | WARNING | Movida para o user-secrets |
| PostgreSQL local publicado em todas as interfaces | WARNING | Publicado só em `127.0.0.1` |
| Senha padrão do banco local no `docker-compose.yml` | WARNING | **Aceito**: banco só da máquina e sem dado real; no servidor a senha é aleatória ([registro](regras/riscos-aceitos.toml)) |
| Cadastro confirma se um e-mail já usa o Hakari (revisão manual) | WARNING | **Aceito** com mitigação planejada (limite de tentativas por IP) |

O registro de riscos aceitos ([`riscos-aceitos.toml`](regras/riscos-aceitos.toml)) guarda a
justificativa, o responsável e a data de revisão de cada aceite. Vencida a data, o achado volta a
contar como aberto.

## 3. Tradução via IA

Para cada falha, o [`ia.py`](auditoria/ia.py) manda ao Claude Opus 5.5 a regra, o tipo de dado, para
onde ele vaza, o artigo da LGPD e o trecho de código. O modelo devolve um JSON com campo fixo:
- título e alerta para o cidadão, em linguagem comum;
- o que pode acontecer e quem é afetado;
- o que o cidadão pode fazer;
- a correção técnica e o trecho corrigido;
- a base legal.

Com todos os achados, o modelo também escreve o resumo executivo que abre o painel.

A IA foi usada sem abrir mão da privacidade:
- **o LLM nunca recebe senha, chave nem dado pessoal:** o trecho de código passa por
  [`redacao.py`](auditoria/redacao.py) antes do envio;
- **a chave da API** fica em `seguranca/.env`, fora do Git, e nunca é impressa;
- **formato garantido:** a resposta segue um JSON Schema (structured outputs), então o painel não
  depende de o texto "vir certo";
- **recusa tratada:** se o modelo recusar, a API tenta um modelo substituto (`fallbacks`);
- **cache:** repetir a auditoria só paga pelas falhas novas. Sem chave, o painel mostra o texto da
  própria regra e avisa que não houve IA.

**Custo real:** as duas auditorias fizeram 13 chamadas (24 mil tokens de entrada e 14 mil de saída) e
custaram cerca de **US$ 0,38**. Rodar de novo sem mudar o código sai de graça, porque as respostas
vêm do cache.

Exemplo de alerta escrito pelo Claude para o achado "Token de acesso salvo sem criptografia no
celular" (ERROR, `mobile/src/dados/banco.ts`):

> **Chave de acesso do app fica desprotegida no celular**
>
> O app guarda no seu celular a chave de acesso que mantém você conectado, mas sem proteção. Se alguém
> pegar seu celular desbloqueado, acessar uma cópia de segurança do aparelho ou instalar um programa
> malicioso com controle total do sistema, pode copiar essa chave e entrar na sua conta sem saber sua
> senha. Isso depende de alguém ter acesso ao seu aparelho ou ao backup dele.
>
> *O que você pode fazer:* mantenha o celular com senha ou biometria e, se perder o aparelho, troque
> sua senha no Hakari assim que possível.

Para a equipe, o mesmo alerta traz a correção com o trecho pronto: gravar o token com
`expo-secure-store` e tirar a coluna do SQLite. A sugestão bate com o que foi feito no app.

## 4. Painel Risco Cidadão

```
Risco = 100 × (1 − Π(1 − p))      p = 0,3 × sensibilidade do dado × exposição × gravidade
```

O indicador é a chance de pelo menos um dado do cidadão parar onde não devia:
- um achado grave pesa muito;
- vários achados se somam sem passar de 100;
- corrigir qualquer um derruba o número;
- risco aceito conta com 25% do peso, porque é risco residual;
- vazamento confirmado nos canários conta como certo.

| Peso | Valores |
|---|---|
| Sensibilidade | saúde 1,0 · credencial 0,9 · identificação e texto livre 0,8 · localização 0,7 · contato 0,6 · infraestrutura 0,5 |
| Exposição | outros cidadãos e repositório público 1,0 · rede 0,8 · token 0,7 · logs, erros e celular 0,6 · banco 0,5 |
| Gravidade | ERROR 1,0 · WARNING 0,7 |
| Faixas | Baixo < 20 · Moderado < 40 · Alto < 70 · Crítico ≥ 70 |

O [`painel.py`](painel.py) mostra:
- o indicador com a faixa e a variação desde o retrato anterior;
- o resumo executivo;
- de onde vem o risco, por tipo de dado, em gráfico antes × agora;
- os alertas abertos, aceitos e corrigidos;
- a tabela dos canários;
- o mapa de dados pessoais;
- as métricas do motor.

O painel funciona no tema claro e no escuro, e todo gráfico tem a versão em tabela.

## Evidências

As saídas reais estão em [`evidencias/`](evidencias/README.md), incluindo o
[print do painel](evidencias/painel-risco-cidadao.png) gerando o indicador. Os retratos JSON das
auditorias, que o painel lê, ficam em [`resultados/historico/`](resultados/historico).

## Estrutura

```
seguranca/
  auditar.py               pipeline: Semgrep, testes de privacidade, Risco Cidadão, IA e retrato JSON
  painel.py                painel Streamlit
  auditoria/
    varredura.py           roda o Semgrep e normaliza os achados (trechos já mascarados)
    testes_dotnet.py       roda os testes do motor e dos canários e lê os relatórios
    risco.py               fórmula do Risco Cidadão
    ia.py                  tradução via Claude, cache e resumo executivo
    redacao.py             mascara segredos e dados pessoais antes de guardar ou enviar
    config.py              caminhos, modelo e rótulos
  regras/
    pii-mapeamento.yaml    9 regras: onde o código guarda dado pessoal
    pii-taint.yaml         8 regras: fonte -> sorvedouro sem sanitizador
    segredos.yaml          6 regras: senha, chave e porta aberta versionadas
    riscos-aceitos.toml    registro de riscos aceitos e achados da revisão manual
    testes/                casos de teste das regras (semgrep --test)
  resultados/              retratos das auditorias e cache da IA
  evidencias/              saídas reais para a entrega
api/src/Hakari.Domain/Privacidade/
  regras-pii.json          16 regras do motor de PII
  MotorPii.cs              motor: regex + validadores + contexto + mascaramento
  Validadores.cs           CPF, CNS, Luhn e telefone
  TextoSeguro.cs           barreira de taint no tipo
api/tests/Hakari.Tests/Privacidade/
  MotorPiiTests.cs, CorpusPiiTests.cs, corpus-pii.json, TaintCanarioTests.cs
```
