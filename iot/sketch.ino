/*
  Hakari — nó de poste (ESP32)

  Controla a lâmpada do poste e vigia a própria saúde. Quando a lâmpada deveria estar acesa e o
  sensor de referência não vê luz, o nó publica um alerta e a ocorrência nasce sozinha, sem
  depender de alguém reclamar.

  Sensores e atuadores
    LDR ambiente (D34)      dia ou noite
    LDR de referência (D35) aponta para a própria lâmpada: confirma que ela acendeu
    HC-SR04 (D5 / D18)      presença de pedestre
    LED amarelo (D26)       a lâmpada do poste (PWM)
    LED vermelho (D27)      alerta ativo no nó

  Regras (docs/testing/features.md, F1 e F2)
    Dia: 0% · Noite: 30% · Noite com presença (< 100 cm): 100% por 10 s
    Lâmpada sem resposta: brilho > 0 e referência escura por 3 leituras -> alerta "Alta"
    Sinal fraco: RSSI < -80 dBm por 5 leituras -> alerta "Aviso"
    Nó isolado: o broker publica "offline" (last will) quando o nó some; a API confirma após 3 min

  MQTT (broker público, só para a demonstração)
    hakari/postes/<id>/telemetria   heartbeat a cada 30 s
    hakari/postes/<id>/alerta       abertura e normalização de alertas
    hakari/postes/<id>/status       "online" / "offline" (retido, last will)
*/
#include <WiFi.h>
#include <PubSubClient.h>

// ---------- Identificação e rede ----------
const char* NO_ID        = "HKR-001";
const char* WIFI_SSID    = "Wokwi-GUEST";   // rede aberta do simulador
const char* WIFI_SENHA   = "";
const char* MQTT_BROKER  = "broker.hivemq.com";
const int   MQTT_PORTA   = 1883;

// ---------- Pinos ----------
const int PINO_LDR_AMBIENTE = 34;
const int PINO_LDR_REF      = 35;
const int PINO_TRIG         = 5;
const int PINO_ECHO         = 18;
const int PINO_LAMPADA      = 26;
const int PINO_ALERTA       = 27;

// ---------- Parâmetros (features.md) ----------
const int LIMIAR_ESCURO          = 3200;    // ADC 12 bits: acima disso está escuro
const int PRESENCA_MAX_CM        = 100;
const int BRILHO_STANDBY         = 30;      // %
const int BRILHO_PRESENCA        = 100;     // %
const unsigned long PERMANENCIA_MS = 10000;
const unsigned long HEARTBEAT_MS   = 30000;
const unsigned long LEITURA_MS     = 1000;  // ciclo de leitura dos triggers
const int LEITURAS_LAMPADA       = 3;
const int RSSI_FRACO_DBM         = -80;
const int LEITURAS_RSSI          = 5;

WiFiClient wifi;
PubSubClient mqtt(wifi);

char topicoTelemetria[48], topicoAlerta[48], topicoStatus[48];

int brilhoAtual = 0;
unsigned long ultimaPresenca = 0, ultimoHeartbeat = 0, ultimaLeitura = 0;
int leiturasLampadaApagada = 0, leiturasSinalFraco = 0, leiturasSinalBom = 0;
bool alertaLampada = false, alertaSinal = false;

// ---------- Sensores ----------
long medirDistanciaCm() {
  digitalWrite(PINO_TRIG, LOW);  delayMicroseconds(2);
  digitalWrite(PINO_TRIG, HIGH); delayMicroseconds(10);
  digitalWrite(PINO_TRIG, LOW);
  long us = pulseIn(PINO_ECHO, HIGH, 30000);  // timeout ~5 m
  return us == 0 ? -1 : us / 58;
}

bool estaEscuro(int leitura) { return leitura > LIMIAR_ESCURO; }

void aplicarBrilho(int porcento) {
  brilhoAtual = porcento;
  ledcWrite(PINO_LAMPADA, map(porcento, 0, 100, 0, 255));
}

// ---------- MQTT ----------
void publicar(const char* topico, const char* json, bool retido = false) {
  bool ok = mqtt.connected() && mqtt.publish(topico, json, retido);
  Serial.printf("%s %s %s\n", ok ? "[MQTT]" : "[MQTT falhou]", topico, json);
}

void publicarAlerta(const char* trigger, const char* severidade, bool ativo, const char* detalhe) {
  char json[256];
  snprintf(json, sizeof(json),
    "{\"no\":\"%s\",\"trigger\":\"%s\",\"severidade\":\"%s\",\"estado\":\"%s\",\"abre_ocorrencia\":%s,\"detalhe\":\"%s\"}",
    NO_ID, trigger, severidade, ativo ? "Ativo" : "Normalizado",
    (ativo && strcmp(severidade, "Aviso") != 0) ? "true" : "false", detalhe);
  publicar(topicoAlerta, json);
}

void publicarHeartbeat(int ldr, int ldrRef, long distancia) {
  char json[256];
  snprintf(json, sizeof(json),
    "{\"no\":\"%s\",\"ldr\":%d,\"escuro\":%s,\"ldr_ref\":%d,\"distancia_cm\":%ld,\"presenca\":%s,"
    "\"brilho\":%d,\"rssi\":%d,\"uptime_s\":%lu}",
    NO_ID, ldr, estaEscuro(ldr) ? "true" : "false", ldrRef, distancia,
    (distancia > 0 && distancia < PRESENCA_MAX_CM) ? "true" : "false",
    brilhoAtual, WiFi.RSSI(), millis() / 1000);
  publicar(topicoTelemetria, json);
}

void conectarWifi() {
  Serial.printf("Conectando ao Wi-Fi %s", WIFI_SSID);
  WiFi.begin(WIFI_SSID, WIFI_SENHA, 6);
  while (WiFi.status() != WL_CONNECTED) { delay(250); Serial.print("."); }
  Serial.printf(" ok, IP %s\n", WiFi.localIP().toString().c_str());
}

void conectarMqtt() {
  while (!mqtt.connected()) {
    char clientId[32];
    snprintf(clientId, sizeof(clientId), "hakari-%s-%04X", NO_ID, (unsigned)random(0xFFFF));
    Serial.print("Conectando ao broker MQTT... ");
    // Last will: se o nó sumir, o próprio broker avisa que ele está offline.
    if (mqtt.connect(clientId, topicoStatus, 1, true, "offline")) {
      Serial.println("ok");
      publicar(topicoStatus, "online", true);
    } else {
      Serial.printf("falhou (rc=%d), nova tentativa em 2 s\n", mqtt.state());
      delay(2000);
    }
  }
}

// ---------- Triggers de saúde (F2) ----------
void avaliarSaude(int ldrRef) {
  // Lâmpada sem resposta: comandada acesa, mas a referência continua no escuro.
  bool apagadaAgora = brilhoAtual > 0 && estaEscuro(ldrRef);
  leiturasLampadaApagada = apagadaAgora ? leiturasLampadaApagada + 1 : 0;
  if (!alertaLampada && leiturasLampadaApagada >= LEITURAS_LAMPADA) {
    alertaLampada = true;
    publicarAlerta("lampada_sem_resposta", "Alta", true, "brilho comandado sem luz na referencia");
  } else if (alertaLampada && !apagadaAgora && brilhoAtual > 0) {
    alertaLampada = false;
    publicarAlerta("lampada_sem_resposta", "Alta", false, "referencia voltou a ver luz");
  }

  // Sinal fraco: só aviso, não abre ocorrência. Abre e normaliza só após 5 leituras seguidas,
  // para o RSSI oscilando em torno do limiar não gerar alertas em sequência.
  bool fracoAgora = WiFi.RSSI() < RSSI_FRACO_DBM;
  leiturasSinalFraco = fracoAgora ? leiturasSinalFraco + 1 : 0;
  leiturasSinalBom = fracoAgora ? 0 : leiturasSinalBom + 1;
  if (!alertaSinal && leiturasSinalFraco >= LEITURAS_RSSI) {
    alertaSinal = true;
    publicarAlerta("sinal_fraco", "Aviso", true, "RSSI abaixo de -80 dBm");
  } else if (alertaSinal && leiturasSinalBom >= LEITURAS_RSSI) {
    alertaSinal = false;
    publicarAlerta("sinal_fraco", "Aviso", false, "RSSI normalizado");
  }

  digitalWrite(PINO_ALERTA, alertaLampada ? HIGH : LOW);
}

void setup() {
  Serial.begin(115200);
  pinMode(PINO_TRIG, OUTPUT);
  pinMode(PINO_ECHO, INPUT);
  pinMode(PINO_ALERTA, OUTPUT);
  ledcAttach(PINO_LAMPADA, 5000, 8);
  aplicarBrilho(0);

  snprintf(topicoTelemetria, sizeof(topicoTelemetria), "hakari/postes/%s/telemetria", NO_ID);
  snprintf(topicoAlerta, sizeof(topicoAlerta), "hakari/postes/%s/alerta", NO_ID);
  snprintf(topicoStatus, sizeof(topicoStatus), "hakari/postes/%s/status", NO_ID);

  Serial.printf("\nHakari - no %s\n", NO_ID);
  conectarWifi();
  mqtt.setServer(MQTT_BROKER, MQTT_PORTA);
  mqtt.setKeepAlive(60);  // folga para o simulador, que às vezes roda abaixo do tempo real
  conectarMqtt();
  ultimoHeartbeat = millis() - HEARTBEAT_MS;  // primeiro heartbeat sai já
}

void loop() {
  if (!mqtt.connected()) conectarMqtt();
  mqtt.loop();

  unsigned long agora = millis();
  if (agora - ultimaLeitura < LEITURA_MS) return;
  ultimaLeitura = agora;

  int ldr = analogRead(PINO_LDR_AMBIENTE);
  int ldrRef = analogRead(PINO_LDR_REF);
  long distancia = medirDistanciaCm();
  bool presenca = distancia > 0 && distancia < PRESENCA_MAX_CM;

  // Controle adaptativo (F1)
  int brilhoAnterior = brilhoAtual;
  if (presenca) ultimaPresenca = agora;
  if (!estaEscuro(ldr)) {
    aplicarBrilho(0);
  } else if (ultimaPresenca && agora - ultimaPresenca < PERMANENCIA_MS) {
    aplicarBrilho(BRILHO_PRESENCA);
  } else {
    aplicarBrilho(BRILHO_STANDBY);
  }

  Serial.printf("LDR %4d (%s) | ref %4d | dist %4ld cm | brilho %3d%% | RSSI %d\n",
                ldr, estaEscuro(ldr) ? "noite" : "dia", ldrRef, distancia, brilhoAtual, WiFi.RSSI());

  if (brilhoAtual != brilhoAnterior) {
    Serial.printf(">> Brilho %d%% -> %d%%\n", brilhoAnterior, brilhoAtual);
  }

  avaliarSaude(ldrRef);

  if (agora - ultimoHeartbeat >= HEARTBEAT_MS) {
    ultimoHeartbeat = agora;
    publicarHeartbeat(ldr, ldrRef, distancia);
  }
}
