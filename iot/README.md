# Hakari — nó de poste (ESP32 no Wokwi)

**Projeto no Wokwi:** _[link](https://wokwi.com/projects/480175142815074305)_
**Aluno:** Lucas Yuki — RM 554865

## Onde o circuito entra na solução

O Hakari parte de um problema que a própria Prefeitura não enxerga: a base do SP156 não tem
nenhum registro de lâmpada apagada, e os pedidos sobre postes e fiação são os mais lentos da
cidade (só 28% resolvidos em até 15 dias, ver `ml/RELATORIO_ML.md`).

Este circuito é o nó que fica em cada poste. Ele faz duas coisas:

1. **Controla a lâmpada** conforme a luz do dia e a presença de pedestres (feature F1).
2. **Vigia a própria saúde** e, quando a lâmpada deveria estar acesa e não está, publica um alerta
   que abre a ocorrência sozinho, sem depender de um morador reclamar (feature F2).

As mensagens saem por MQTT. A API do Hakari consome esses tópicos, registra os alertas e abre as
ocorrências que aparecem no app do cidadão e no painel de manutenção.

```
ESP32 no poste ──MQTT──> broker ──> API Hakari ──> app (cidadão e manutenção)
```

## Circuito

| Componente | Pino | Papel |
|---|---|---|
| LDR ambiente | D34 | Dia ou noite |
| LDR de referência | D35 | Aponta para a própria lâmpada e confirma que ela acendeu |
| HC-SR04 | TRIG D5, ECHO D18 | Presença de pedestre |
| LED amarelo + 220 Ω | D26 (PWM) | A lâmpada do poste |
| LED vermelho + 220 Ω | D27 | Alerta ativo no nó |

## Regras

Os valores vêm de `docs/testing/features.md` e são os mesmos dos casos de teste no Azure Boards.

| Situação | Comportamento |
|---|---|
| Dia (LDR ≤ 3200) | Lâmpada em 0% |
| Noite, sem ninguém | 30% |
| Noite, pessoa a menos de 100 cm | 100% e mantém por 10 s depois que a pessoa sai |
| Brilho > 0 e referência escura por 3 leituras | Alerta **Alta**, abre ocorrência, LED vermelho acende |
| RSSI < -80 dBm por 5 leituras | Alerta **Aviso**, não abre ocorrência |
| Nó some da rede | O broker publica `offline` (last will); a API marca **Desastre** após 3 min |

Quando a condição some, o nó publica o mesmo alerta com estado `Normalizado`. Alertas não se
repetem enquanto a condição continua.

## Mensagens MQTT

Broker público `broker.hivemq.com:1883`, usado só para a demonstração.

| Tópico | Quando | Exemplo |
|---|---|---|
| `hakari/postes/HKR-001/status` | Ao conectar e ao cair (retido) | `online` / `offline` |
| `hakari/postes/HKR-001/telemetria` | A cada 30 s | `{"no":"HKR-001","ldr":3938,"escuro":true,"ldr_ref":3938,"distancia_cm":50,"presenca":true,"brilho":100,"rssi":-96,"uptime_s":4}` |
| `hakari/postes/HKR-001/alerta` | Abertura e normalização | `{"no":"HKR-001","trigger":"lampada_sem_resposta","severidade":"Alta","estado":"Ativo","abre_ocorrencia":true,"detalhe":"brilho comandado sem luz na referencia"}` |

Para acompanhar ao vivo, abra o cliente web da HiveMQ
(https://www.hivemq.com/demos/websocket-client/), conecte e assine `hakari/postes/#`.

## Como rodar

1. Em https://wokwi.com/projects/new/esp32, substitua `sketch.ino` e `diagram.json` pelos desta pasta.
2. Em **Library Manager**, adicione **PubSubClient** (ou crie `libraries.txt` com o conteúdo desta pasta).
3. Clique em ▶. O Serial Monitor mostra a conexão, as leituras e cada mensagem publicada.

### Simulando as situações

Clique em um componente durante a simulação para mexer no valor dele:

- **Anoitecer:** LDR ambiente (o de cima) com lux baixo, por exemplo 1. A lâmpada vai a 30%.
- **Pedestre:** HC-SR04 com distância abaixo de 100 cm. A lâmpada vai a 100%.
- **Lâmpada queimada:** LDR de referência (o de baixo) com lux baixo, à noite. Depois de 3 s sai o
  alerta Alta e o LED vermelho acende. Volte o lux para cima e o alerta é normalizado.
- **Nó isolado:** pare a simulação. O broker publica `offline` no tópico de status.
