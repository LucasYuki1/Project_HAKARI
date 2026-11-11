using Hakari.Domain.Entidades;
using Hakari.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Infrastructure.Dados;

/// <summary>
/// Dados iniciais para a demonstração: alguns postes em São Paulo (o HKR-001 é o nó do Wokwi) e
/// um técnico de manutenção. Só roda se o banco estiver vazio.
/// </summary>
public static class SemeadorDeDados
{
    public static async Task SemearAsync(HakariDbContext db, IHasherDeSenha hasher, string? senhaTecnico,
                                         DateTime agora, CancellationToken ct = default)
    {
        if (!await db.Postes.AnyAsync(ct))
        {
            db.Postes.AddRange(
                new Poste("HKR-001", "Av. Paulista, 1578", "Bela Vista", -23.5614, -46.6559),
                new Poste("HKR-002", "R. Augusta, 2200", "Jardim Paulista", -23.5629, -46.6656),
                new Poste("HKR-003", "Av. São João, 473", "República", -23.5419, -46.6390),
                new Poste("HKR-004", "R. Vergueiro, 3185", "Vila Mariana", -23.5880, -46.6340),
                new Poste("HKR-005", "Av. Paranaguá, 1500", "Ermelino Matarazzo", -23.4943, -46.4793),
                new Poste("HKR-006", "Av. Adolfo Pinheiro, 1000", "Santo Amaro", -23.6449, -46.7050));
        }

        if (!string.IsNullOrWhiteSpace(senhaTecnico) && !await db.Usuarios.OfType<Tecnico>().AnyAsync(ct))
        {
            db.Usuarios.Add(new Tecnico("Técnico Demonstração", "tecnico@hakari.dev", "11900000000",
                                        hasher.Gerar(senhaTecnico), "MNT-0001", agora));
        }

        await db.SaveChangesAsync(ct);
    }
}
