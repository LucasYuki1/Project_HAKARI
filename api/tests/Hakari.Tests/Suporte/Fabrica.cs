using Hakari.Domain.Entidades;
using Hakari.Infrastructure.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hakari.Tests.Suporte;

/// <summary>Relógio controlado pelo teste: permite "avançar 3 minutos" sem esperar.</summary>
public class RelogioFixo(DateTime inicio) : TimeProvider
{
    public DateTime Agora { get; private set; } = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);

    public override DateTimeOffset GetUtcNow() => Agora;

    public void Avancar(TimeSpan tempo) => Agora += tempo;
}

/// <summary>Objetos prontos para os testes, para cada teste mostrar só o que importa.</summary>
public static class Fabrica
{
    public static readonly DateTime Inicio = new(2026, 11, 19, 22, 0, 0, DateTimeKind.Utc);

    public static Poste Poste(string codigo = "HKR-001") =>
        new(codigo, "Av. Paulista, 1578", "Bela Vista", -23.56, -46.65);

    public static Cidadao Cidadao(string nome = "Ana Paula Souza", string email = "ana@exemplo.com") =>
        new(nome, email, "11988887777", "hash", Inicio);

    public static Tecnico Tecnico(string nome = "Rafael Mendes") =>
        new(nome, "rafael@hakari.dev", "11900000000", "hash", "MNT-0001", Inicio);

    /// <summary>Banco em memória, novo a cada teste (nome aleatório).</summary>
    public static HakariDbContext Banco() =>
        new(new DbContextOptionsBuilder<HakariDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static NullLogger<T> Log<T>() => NullLogger<T>.Instance;
}
