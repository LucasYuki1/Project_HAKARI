# Features — Hakari

Process template: **Agile** (Epic → Feature → User Story / Test Case).
Critérios de aceite em **Gherkin** (Dado / Quando / Então).

**Epic:** Hakari — iluminação pública que se reporta sozinha

---

## Parâmetros de referência

Valores usados nos critérios de aceite. Ficam configuráveis no firmware e na API.

| Parâmetro | Valor |
|---|---|
| Ambiente escuro | leitura do LDR ambiente > 3200 (ADC 12 bits) |
| Presença detectada | distância no HC-SR04 entre 1 e 100 cm |
| Brilho em standby | 30% |
| Brilho com presença | 100% |
| Tempo de permanência após presença | 10 s |
| Heartbeat do nó | a cada 30 s |
| Lâmpada sem resposta | brilho comandado > 0 e LDR de referência escuro por 3 leituras seguidas |
| Nó isolado | nenhuma mensagem por 3 min |
| Sinal fraco | RSSI < -80 dBm por 5 leituras seguidas |

### Severidades (modelo de triggers)

| Severidade | Trigger | Abre ocorrência? |
|---|---|---|
| Aviso | Sinal fraco | Não |
| Alta | Lâmpada sem resposta | Sim |
| Desastre | Nó isolado (isolamento total) | Sim |

### Ciclo de vida da ocorrência

`Aberta` → `Em atendimento` → `Resolvida`

Origem: `Automática` (trigger do nó) ou `Cidadão` (reporte no app).

---

## F1 — Controle adaptativo de iluminação

**Descrição:** Como **morador**, quero que o poste ajuste o brilho conforme a luz do dia e a presença de pessoas, para que a rua fique iluminada quando eu passo sem desperdiçar energia quando está vazia.

**Critérios de aceite**

```gherkin
Cenário: Dia claro
  Dado que a leitura do LDR ambiente indica ambiente claro
  Quando o nó executa o ciclo de controle
  Então o brilho da lâmpada é 0%

Cenário: Noite sem presença
  Dado que o ambiente está escuro
  E não há presença detectada
  Quando o nó executa o ciclo de controle
  Então o brilho da lâmpada é 30%

Cenário: Noite com presença
  Dado que o ambiente está escuro
  Quando uma pessoa é detectada a menos de 100 cm
  Então o brilho da lâmpada sobe para 100%

Cenário: Fim da presença
  Dado que a lâmpada está em 100% por presença
  Quando não há presença detectada por 10 segundos
  Então o brilho volta para 30%

Cenário: Telemetria publicada
  Dado que o nó está conectado ao broker
  Quando passam 30 segundos desde a última publicação
  Então o nó publica um heartbeat com luminosidade, presença, brilho e RSSI
```

---

## F2 — Monitoramento de saúde dos nós

**Descrição:** Como **equipe de manutenção**, quero que cada nó seja monitorado continuamente e que falhas gerem alertas com severidade, para descobrir um poste apagado antes que o morador precise reclamar.

**Critérios de aceite**

```gherkin
Cenário: Lâmpada sem resposta
  Dado que o brilho comandado do nó é maior que 0
  Quando o LDR de referência lê escuro por 3 leituras seguidas
  Então é gerado um alerta de severidade "Alta"
  E uma ocorrência de origem "Automática" é aberta para o poste

Cenário: Isolamento total do nó
  Dado que um nó está cadastrado e ativo
  Quando a API não recebe nenhuma mensagem dele por 3 minutos
  Então o nó passa ao status "Isolado"
  E é gerado um alerta de severidade "Desastre"
  E uma ocorrência de origem "Automática" é aberta para o poste

Cenário: Sinal fraco
  Dado que um nó está online
  Quando o RSSI fica abaixo de -80 dBm por 5 leituras seguidas
  Então é gerado um alerta de severidade "Aviso"
  E nenhuma ocorrência é aberta

Cenário: Recuperação do nó
  Dado que existe um alerta ativo para um nó
  Quando a condição que gerou o alerta deixa de ocorrer
  Então o alerta é marcado como "Normalizado"
  E a normalização é registrada no histórico da ocorrência
  E a ocorrência permanece aberta até a manutenção resolvê-la

Cenário: Sem alertas duplicados
  Dado que já existe um alerta ativo de um trigger para um nó
  Quando a mesma condição continua ocorrendo
  Então nenhum novo alerta é gerado para esse trigger
```

---

## F3 — Cadastro e autenticação do cidadão

**Descrição:** Como **morador**, quero criar uma conta e entrar no app, para reportar problemas e acompanhar o que reportei.

**Critérios de aceite**

```gherkin
Cenário: Cadastro válido
  Dado que estou na tela de cadastro
  Quando informo nome, e-mail, telefone e senha válidos
  E aceito o termo de uso de dados
  Então minha conta é criada
  E sou levado à tela inicial já autenticado

Cenário: E-mail já cadastrado
  Dado que já existe uma conta com o e-mail informado
  Quando tento me cadastrar com esse e-mail
  Então vejo a mensagem "Este e-mail já está em uso"
  E nenhuma conta nova é criada

Cenário: Senha fraca
  Quando informo uma senha com menos de 8 caracteres ou sem letra e número
  Então vejo a mensagem explicando a regra da senha
  E o cadastro não é concluído

Cenário: Termo de uso não aceito
  Quando tento concluir o cadastro sem aceitar o termo de uso de dados
  Então o botão de cadastro permanece desabilitado

Cenário: Login inválido
  Dado que tenho uma conta
  Quando informo a senha errada
  Então vejo a mensagem "E-mail ou senha incorretos"
  E não sou autenticado
```

---

## F4 — Consulta de postes do bairro

**Descrição:** Como **morador**, quero ver os postes próximos e o status de cada um, para saber quais ruas estão iluminadas antes de sair.

**Critérios de aceite**

```gherkin
Cenário: Lista de postes
  Dado que estou autenticado
  Quando abro a tela de postes
  Então vejo cada poste com identificação, endereço e status

Cenário: Status legível
  Dado que um poste tem alerta "Alta" ou "Desastre" ativo
  Quando o poste aparece na lista
  Então o status é exibido como "Apagado" ou "Sem comunicação", com cor e texto

Cenário: Detalhe do poste
  Quando toco em um poste
  Então vejo o status atual, a última comunicação e a ocorrência ativa, se houver

Cenário: API indisponível
  Dado que a API não responde
  Quando abro a tela de postes
  Então vejo uma mensagem de erro com a opção "Tentar novamente"
```

---

## F5 — Reporte de problema pelo cidadão

**Descrição:** Como **morador**, quero reportar um problema em um poste, para que a manutenção saiba de falhas que o sensor não detecta (poste danificado, lâmpada piscando).

**Critérios de aceite**

```gherkin
Cenário: Reporte válido
  Dado que estou autenticado e abri o detalhe de um poste sem ocorrência ativa
  Quando escolho uma categoria e escrevo uma descrição
  E envio o reporte
  Então uma ocorrência de origem "Cidadão" é aberta com status "Aberta"
  E vejo o número da ocorrência

Cenário: Poste com ocorrência ativa
  Dado que o poste já tem uma ocorrência ativa
  Quando envio um reporte para esse poste
  Então meu reporte é anexado à ocorrência existente como apoio
  E nenhuma ocorrência nova é criada
  E vejo a mensagem "Este problema já está registrado. Seu apoio foi adicionado."

Cenário: Descrição vazia
  Quando tento enviar o reporte sem descrição
  Então vejo a mensagem "Descreva o problema"
  E o reporte não é enviado

Cenário: Poste inexistente
  Quando a API recebe um reporte para um poste que não existe
  Então a resposta é 404
  E nenhuma ocorrência é criada

Cenário: Apoio repetido
  Dado que já apoiei uma ocorrência
  Quando tento apoiá-la novamente
  Então vejo a mensagem "Você já apoiou esta ocorrência"
```

---

## F6 — Acompanhamento das minhas ocorrências

**Descrição:** Como **morador**, quero acompanhar as ocorrências que abri ou apoiei, para saber se o problema está sendo resolvido.

**Critérios de aceite**

```gherkin
Cenário: Lista das minhas ocorrências
  Dado que abri ou apoiei ocorrências
  Quando abro a tela "Minhas ocorrências"
  Então vejo cada uma com poste, status e data de abertura

Cenário: Histórico da ocorrência
  Quando toco em uma ocorrência
  Então vejo o histórico de mudanças de status com data e hora

Cenário: Nenhuma ocorrência
  Dado que nunca abri nem apoiei ocorrências
  Quando abro a tela "Minhas ocorrências"
  Então vejo a mensagem "Você ainda não reportou nenhum problema"

Cenário: Ocorrência de outro cidadão
  Quando a API recebe um pedido de detalhe de uma ocorrência que não abri nem apoiei
  Então os dados pessoais de quem abriu não são retornados
```

---

## F7 — Painel da equipe de manutenção

**Descrição:** Como **técnico de manutenção**, quero ver a fila de ocorrências priorizada por severidade e atualizar o status de cada uma, para atender primeiro o que deixa a rua no escuro.

**Critérios de aceite**

```gherkin
Cenário: Acesso restrito
  Dado que estou autenticado como cidadão
  Quando tento acessar o painel de manutenção
  Então o acesso é negado

Cenário: Fila priorizada
  Dado que estou autenticado como técnico
  Quando abro o painel
  Então vejo as ocorrências não resolvidas ordenadas por severidade e, depois, por data de abertura

Cenário: Assumir ocorrência
  Dado que uma ocorrência está "Aberta"
  Quando eu a assumo
  Então o status passa a "Em atendimento"
  E meu nome fica registrado como responsável

Cenário: Resolver ocorrência
  Dado que uma ocorrência está "Em atendimento"
  Quando eu a marco como resolvida informando o que foi feito
  Então o status passa a "Resolvida"
  E os cidadãos que abriram ou apoiaram veem a mudança em "Minhas ocorrências"

Cenário: Transição inválida
  Dado que uma ocorrência está "Aberta"
  Quando tento marcá-la como "Resolvida" sem antes assumi-la
  Então vejo a mensagem "Assuma a ocorrência antes de resolvê-la"
  E o status não muda
```

---

## F8 — Privacidade dos dados do cidadão

**Descrição:** Como **morador**, quero ver e excluir os dados pessoais que o app guarda sobre mim, para ter controle sobre minhas informações (LGPD).

**Critérios de aceite**

```gherkin
Cenário: Ver meus dados
  Dado que estou autenticado
  Quando abro "Meus dados"
  Então vejo nome, e-mail e telefone cadastrados

Cenário: Excluir minha conta
  Dado que estou autenticado
  Quando confirmo a exclusão da conta
  Então meus dados pessoais são removidos
  E as ocorrências que abri permanecem, sem identificação do autor
  E sou desconectado

Cenário: Dados pessoais fora dos logs
  Quando qualquer requisição com e-mail, telefone ou senha é processada pela API
  Então esses valores não aparecem em texto aberto nos logs

Cenário: Técnico não vê contato do cidadão
  Dado que estou autenticado como técnico
  Quando abro uma ocorrência de origem "Cidadão"
  Então vejo o nome do cidadão abreviado e não vejo e-mail nem telefone
```
