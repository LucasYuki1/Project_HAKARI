using Hakari.Api.Contratos;
using Hakari.Domain.Entidades;
using Hakari.Domain.Excecoes;
using Hakari.Infrastructure.Dados;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Api.Aplicacao;

public interface IContaService
{
    Task<MeusDadosResponse> MeusDadosAsync(int usuarioId, CancellationToken ct);
    Task ExcluirAsync(int usuarioId, CancellationToken ct);
}

/// <summary>F8 (LGPD): o cidadão vê e apaga os próprios dados.</summary>
public class ContaService(HakariDbContext db) : IContaService
{
    public async Task<MeusDadosResponse> MeusDadosAsync(int usuarioId, CancellationToken ct)
    {
        var u = await BuscarAsync(usuarioId, ct);
        return new MeusDadosResponse(u.Id, u.Nome, u.Email, u.Telefone, u.Perfil, u.CriadoEm,
                                     (u as Cidadao)?.TermoAceitoEm);
    }

    /// <summary>
    /// Remove o usuário. Pelo mapeamento do banco, as ocorrências que ele abriu ficam com autor nulo
    /// (ON DELETE SET NULL) e os apoios dele são apagados (ON DELETE CASCADE).
    /// </summary>
    public async Task ExcluirAsync(int usuarioId, CancellationToken ct)
    {
        var u = await BuscarAsync(usuarioId, ct);
        if (u is Tecnico)
            throw new RegraDeNegocioException("Contas de técnico são removidas pela administração");

        db.Usuarios.Remove(u);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Usuario> BuscarAsync(int id, CancellationToken ct) =>
        await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new NaoEncontradoException("Conta não encontrada");
}
