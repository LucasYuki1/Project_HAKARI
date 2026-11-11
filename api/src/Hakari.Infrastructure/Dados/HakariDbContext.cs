using Hakari.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Infrastructure.Dados;

/// <summary>
/// Porta de entrada do EF Core para o PostgreSQL. Cada DbSet vira uma tabela; o mapeamento
/// detalhado de cada entidade fica nas classes de Configuracoes/.
/// </summary>
public class HakariDbContext(DbContextOptions<HakariDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Poste> Postes => Set<Poste>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<Ocorrencia> Ocorrencias => Set<Ocorrencia>();
    public DbSet<Leitura> Leituras => Set<Leitura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HakariDbContext).Assembly);
}
