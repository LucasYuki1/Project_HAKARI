using Hakari.Api.Aplicacao;

namespace Hakari.Api.Workers;

/// <summary>
/// A cada 30 s procura nós que não mandam nada há 3 min. O próprio nó não consegue avisar que
/// sumiu, então essa detecção (alerta "Desastre") é responsabilidade da API.
/// </summary>
public class IsolamentoWorker(IServiceScopeFactory escopos, ILogger<IsolamentoWorker> log) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken parar)
    {
        using var timer = new PeriodicTimer(Intervalo);
        try
        {
            while (await timer.WaitForNextTickAsync(parar))
            {
                try
                {
                    using var escopo = escopos.CreateScope();
                    var monitor = escopo.ServiceProvider.GetRequiredService<IMonitorService>();
                    var isolados = await monitor.VerificarIsolamentoAsync(parar);
                    if (isolados > 0) log.LogWarning("{Quantidade} nó(s) marcado(s) como isolado(s)", isolados);
                }
                catch (Exception ex) when (!parar.IsCancellationRequested)
                {
                    log.LogError(ex, "Falha na verificação de isolamento");
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
