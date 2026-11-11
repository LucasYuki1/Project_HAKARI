using System.Text;
using System.Text.Json;
using Hakari.Api.Aplicacao;
using Hakari.Api.Contratos;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Hakari.Api.Workers;

public class MqttOpcoes
{
    public bool Habilitado { get; set; } = true;
    public string Host { get; set; } = "broker.hivemq.com";
    public int Porta { get; set; } = 1883;
    public string Topico { get; set; } = "hakari/postes/#";
}

/// <summary>
/// Serviço em segundo plano que assina os tópicos dos nós (hakari/postes/{codigo}/{tipo}) e
/// repassa cada mensagem ao MonitorService. Se a conexão cair, tenta de novo a cada 5 s.
/// </summary>
public class MqttWorker(IServiceScopeFactory escopos, IOptions<MqttOpcoes> opcoes, ILogger<MqttWorker> log)
    : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken parar)
    {
        var o = opcoes.Value;
        if (!o.Habilitado)
        {
            log.LogInformation("MQTT desabilitado por configuração");
            return;
        }

        var fabrica = new MqttClientFactory();
        using var cliente = fabrica.CreateMqttClient();
        cliente.ApplicationMessageReceivedAsync += e =>
        {
            // Mensagens retidas são antigas (ex.: o último "online"): não provam que o nó está vivo agora.
            if (e.ApplicationMessage.Retain) return Task.CompletedTask;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            return TratarAsync(e.ApplicationMessage.Topic, payload, parar);
        };

        var conexao = new MqttClientOptionsBuilder()
            .WithTcpServer(o.Host, o.Porta)
            .WithClientId($"hakari-api-{Guid.NewGuid():N}"[..23])
            .WithCleanSession()
            .Build();

        while (!parar.IsCancellationRequested)
        {
            if (!cliente.IsConnected)
            {
                try
                {
                    await cliente.ConnectAsync(conexao, parar);
                    await cliente.SubscribeAsync(fabrica.CreateSubscribeOptionsBuilder().WithTopicFilter(o.Topico).Build(), parar);
                    log.LogInformation("MQTT conectado em {Host}:{Porta}, assinando {Topico}", o.Host, o.Porta, o.Topico);
                }
                catch (Exception ex) when (!parar.IsCancellationRequested)
                {
                    log.LogWarning("Falha ao conectar no broker MQTT ({Erro}); nova tentativa em 5 s", ex.Message);
                }
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), parar); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Interpreta o tópico e o JSON. Separado do evento para poder ser testado.</summary>
    internal async Task TratarAsync(string topico, string payload, CancellationToken ct)
    {
        var partes = topico.Split('/');
        if (partes is not ["hakari", "postes", var codigo, var tipo]) return;

        try
        {
            using var escopo = escopos.CreateScope();
            var monitor = escopo.ServiceProvider.GetRequiredService<IMonitorService>();

            switch (tipo)
            {
                case "telemetria":
                    var telemetria = JsonSerializer.Deserialize<TelemetriaMensagem>(payload, Json);
                    if (telemetria is not null) await monitor.ProcessarTelemetriaAsync(codigo, telemetria, ct);
                    break;
                case "alerta":
                    var alerta = JsonSerializer.Deserialize<AlertaMensagem>(payload, Json);
                    if (alerta is not null) await monitor.ProcessarAlertaAsync(codigo, alerta, ct);
                    break;
                case "status":
                    await monitor.ProcessarStatusAsync(codigo, payload.Trim(), ct);
                    break;
            }
        }
        catch (JsonException)
        {
            log.LogWarning("Mensagem inválida em {Topico} ignorada", topico);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogError(ex, "Erro ao processar mensagem de {Topico}", topico);
        }
    }
}
