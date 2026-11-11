#:package MQTTnet@5.2.0.1603
#:property PublishAot=false
// Simula um nó Hakari publicando no broker, com o mesmo formato do firmware do Wokwi.
// Útil para testar a API sem abrir o simulador.
//
// Uso:  dotnet run scripts/simular_no.cs -- <codigo> <acao>
//   acao: telemetria | queimar | normalizar | sinal-fraco
// Ex.:  dotnet run scripts/simular_no.cs -- HKR-001 queimar

using MQTTnet;

var codigo = args.ElementAtOrDefault(0) ?? "HKR-001";
var acao = args.ElementAtOrDefault(1) ?? "telemetria";

var (tipo, json) = acao switch
{
    "telemetria" => ("telemetria",
        $$"""{"no":"{{codigo}}","ldr":3938,"escuro":true,"ldr_ref":1001,"distancia_cm":50,"presenca":true,"brilho":100,"rssi":-62,"uptime_s":120}"""),
    "queimar" => ("alerta",
        $$"""{"no":"{{codigo}}","trigger":"lampada_sem_resposta","severidade":"Alta","estado":"Ativo","abre_ocorrencia":true,"detalhe":"brilho comandado sem luz na referencia"}"""),
    "normalizar" => ("alerta",
        $$"""{"no":"{{codigo}}","trigger":"lampada_sem_resposta","severidade":"Alta","estado":"Normalizado","abre_ocorrencia":false,"detalhe":"referencia voltou a ver luz"}"""),
    "sinal-fraco" => ("alerta",
        $$"""{"no":"{{codigo}}","trigger":"sinal_fraco","severidade":"Aviso","estado":"Ativo","abre_ocorrencia":false,"detalhe":"RSSI abaixo de -80 dBm"}"""),
    _ => throw new ArgumentException($"Ação desconhecida: {acao}")
};

var fabrica = new MqttClientFactory();
using var cliente = fabrica.CreateMqttClient();
await cliente.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("broker.hivemq.com", 1883).Build());

var topico = $"hakari/postes/{codigo}/{tipo}";
await cliente.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topico).WithPayload(json).Build());
Console.WriteLine($"Publicado em {topico}: {json}");
await cliente.DisconnectAsync();
