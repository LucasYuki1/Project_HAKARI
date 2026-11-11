using Hakari.Domain.Interfaces;
using Hakari.Infrastructure.Dados;
using Hakari.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hakari.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra o banco (PostgreSQL) e os serviços técnicos da Infrastructure.</summary>
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<HakariDbContext>(o => o.UseNpgsql(connectionString));
        services.AddSingleton<IHasherDeSenha, HasherBCrypt>();
        return services;
    }
}
