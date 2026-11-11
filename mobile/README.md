# Hakari: app do cidadão e da manutenção

**Mobile Development & IOT · Global Solution 2026/2 · CitizenTech**
**Aluno:** Lucas Yuki (RM 554865) · **Vídeo:** _link do vídeo_

A Ficha da CitizenTech está no [README da raiz](../README.md).

## A jornada que o app resolve

Hoje, um poste apagado só é consertado depois que alguém reclama, e o conserto demora. No Hakari o
próprio poste avisa que apagou. O app mostra isso ao cidadão e o deixa participar:

1. **Ver** a situação dos postes do bairro (funcionando, apagado, sem comunicação).
2. **Reportar** um problema que o sensor não pega (poste danificado, fiação exposta) ou **apoiar**
   uma ocorrência que já existe, sem duplicar o chamado.
3. **Acompanhar** a ocorrência até o conserto, pela linha do tempo.

A equipe de manutenção usa o mesmo app: entra com a conta de técnico e vê a fila ordenada por
severidade, assume a ocorrência e registra a solução.

## Telas

| Tela | Quem vê | O que faz |
|---|---|---|
| Entrar / Criar conta | Todos | Login com e-mail e senha; cadastro com aceite do uso de dados (LGPD) |
| Postes | Cidadão | Lista com status, busca por bairro ou rua, funciona sem internet com a última lista salva |
| Detalhe do poste | Todos | Status, último sinal, o que o sensor detectou e a ocorrência ativa |
| Reportar problema | Cidadão | Tipo do problema e descrição; o rascunho fica salvo no celular |
| Meus reportes | Cidadão | Ocorrências que a pessoa abriu ou apoiou |
| Ocorrência | Todos | Detalhe e linha do tempo; apoiar (cidadão), assumir e resolver (técnico) |
| Fila | Técnico | Ocorrências abertas, Desastre primeiro |
| Bairros | Técnico | Situação da iluminação por bairro |
| Perfil / Conta | Todos | Meus dados, sair e excluir a conta |

Prints em [`docs/prints`](docs/prints).

## Como atende cada critério

| Critério | Onde está |
|---|---|
| **Telas e navegação** | Expo Router em `src/app/`: pilha principal com rotas protegidas por perfil (`_layout.tsx`), abas do cidadão em `(cidadao)/` e da manutenção em `(tecnico)/`, rotas dinâmicas `poste/[id]`, `ocorrencia/[id]` e `reportar/[id]` |
| **Consumo de dados** | A API REST do projeto (`../api`). `src/api/cliente.ts` envia o token JWT, aplica tempo limite e transforma os erros da API em mensagens simples; `src/api/hakari.ts` tem uma função por endpoint |
| **Banco de dados** | SQLite no celular (`expo-sqlite`), em `src/dados/banco.ts`: sessão (o app abre já logado), postes salvos para consulta sem internet, rascunhos de reporte e o endereço do servidor |
| **Acessibilidade** | Ver a seção abaixo |
| **Aderência ao problema** | A jornada segue as Features F3 a F8 de `docs/testing/features.md` |
| **Documentação** | Este README, a Ficha na raiz e os comentários no início de cada arquivo |

## Acessibilidade

- Letra base de 17 pt e títulos de 24 pt; o app respeita o tamanho de fonte escolhido no celular.
- Cores com contraste de pelo menos 4,5:1 (WCAG AA). O status **nunca depende só da cor**: sempre
  vem com ícone e texto ("Apagado", "Sem comunicação").
- Botões e opções com pelo menos 52 dp de altura.
- Rótulos para leitor de tela (TalkBack/VoiceOver) em botões, campos, cartões e abas; mensagens de
  erro e de sucesso são anunciadas assim que aparecem.
- Linguagem simples, sem termos técnicos para o cidadão ("último sinal do poste", "eu também vi esse
  problema").

## Como rodar

Precisa do Node 20+, do app **Expo Go** no celular e da API do Hakari rodando no PC. O celular e o PC
precisam estar no mesmo Wi-Fi.

**1. Suba o banco e a API aberta para a rede** (na pasta `api/`):

```bash
docker compose up -d
dotnet run --project src/Hakari.Api --launch-profile celular
```

Se o Windows perguntar, permita o acesso em **redes privadas**.

**2. Aponte o app para o IP do PC.** Copie `.env.example` para `.env` e troque o IP pelo do seu PC
(`ipconfig`, adaptador Wi-Fi). Também dá para trocar o endereço pela própria tela de login, em
"Servidor · Alterar".

**3. Rode o app** (nesta pasta):

```bash
npm install
npx expo start
```

Leia o QR code com o Expo Go (Android) ou com a câmera (iOS).

**Contas para testar:** crie uma conta de cidadão pelo próprio app. A conta de técnico é
`tecnico@hakari.dev`, com a senha definida em `api/src/Hakari.Api/appsettings.Development.json`
(`Seed:SenhaTecnico`).

## Estrutura

```
src/
  app/              telas (cada arquivo é uma rota do Expo Router)
    (cidadao)/      abas do cidadão: Postes, Meus reportes, Perfil
    (tecnico)/      abas da manutenção: Fila, Bairros, Conta
    poste/[id]      detalhe do poste
    ocorrencia/[id] detalhe e linha do tempo da ocorrência
    reportar/[id]   formulário de reporte
  api/              cliente HTTP, tipos e chamadas da API
  dados/            banco SQLite local
  sessao/           login salvo e contexto de sessão
  ui/               tema, componentes acessíveis e textos
```

## Tecnologias

Expo SDK 58 · React Native 0.88 · TypeScript · Expo Router · expo-sqlite · @expo/vector-icons
