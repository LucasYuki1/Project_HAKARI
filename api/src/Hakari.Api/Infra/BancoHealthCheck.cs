using Hakari.Infrastructure.Dados;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hakari.Api.Infra;

/// <summary>/health responde "Unhealthy" se o PostgreSQL não estiver acessível (útil para monitoramento).</summary>
public class BancoHealthCheck(HakariDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy("PostgreSQL acessível")
            : HealthCheckResult.Unhealthy("PostgreSQL inacessível");
}
