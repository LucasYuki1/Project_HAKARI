using Hakari.Api.Contratos;
using Hakari.Domain.Entidades;
using Hakari.Domain.Enums;
using Hakari.Domain.Excecoes;
using Hakari.Infrastructure.Dados;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Api.Aplicacao;

public interface IOcorrenciaService
{
    Task<ReporteResponse> ReportarAsync(int posteId, int cidadaoId, ReporteRequest pedido, CancellationToken ct);
    Task ApoiarAsync(int ocorrenciaId, int cidadaoId, CancellationToken ct);
    Task<OcorrenciaDetalhe> DetalharAsync(int ocorrenciaId, int usuarioId, PerfilUsuario perfil, CancellationToken ct);
    Task<IReadOnlyList<OcorrenciaResumo>> MinhasAsync(int cidadaoId, CancellationToken ct);
    Task<IReadOnlyList<OcorrenciaResumo>> FilaAsync(CancellationToken ct);
    Task<OcorrenciaDetalhe> AssumirAsync(int ocorrenciaId, int tecnicoId, CancellationToken ct);
    Task<OcorrenciaDetalhe> ResolverAsync(int ocorrenciaId, int tecnicoId, string solucao, CancellationToken ct);
}

/// <summary>
/// Casos de uso das ocorrências (F5, F6, F7). O serviço só busca e salva; quem decide se algo
/// pode acontecer são os métodos da entidade Ocorrencia.
/// </summary>
public class OcorrenciaService(HakariDbContext db, TimeProvider relogio) : IOcorrenciaService
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public async Task<ReporteResponse> ReportarAsync(int posteId, int cidadaoId, ReporteRequest pedido,
                                                     CancellationToken ct)
    {
        Ocorrencia.ValidarDescricao(pedido.Descricao);

        var poste = await db.Postes.FirstOrDefaultAsync(p => p.Id == posteId, ct)
                    ?? throw new NaoEncontradoException("Poste não encontrado");
        var cidadao = await BuscarCidadaoAsync(cidadaoId, ct);

        // Se o poste já tem ocorrência em aberto, o reporte vira apoio (não duplica chamado).
        var ativa = await db.Ocorrencias.Include(o => o.Apoios)
            .Where(o => o.PosteId == posteId)
            .Where(Mapeamento.Ativa)
            .FirstOrDefaultAsync(ct);

        if (ativa is not null)
        {
            ativa.Apoiar(cidadao, Agora);
            await db.SaveChangesAsync(ct);
            return new ReporteResponse(ativa.Id, true, "Este problema já está registrado. Seu apoio foi adicionado.");
        }

        var nova = Ocorrencia.AbrirPorCidadao(poste, cidadao, pedido.Categoria, pedido.Descricao, Agora);
        db.Ocorrencias.Add(nova);
        await db.SaveChangesAsync(ct);
        return new ReporteResponse(nova.Id, false, $"Ocorrência #{nova.Id} aberta");
    }

    public async Task ApoiarAsync(int ocorrenciaId, int cidadaoId, CancellationToken ct)
    {
        var ocorrencia = await db.Ocorrencias.Include(o => o.Apoios)
                             .FirstOrDefaultAsync(o => o.Id == ocorrenciaId, ct)
                         ?? throw new NaoEncontradoException("Ocorrência não encontrada");
        var cidadao = await BuscarCidadaoAsync(cidadaoId, ct);

        ocorrencia.Apoiar(cidadao, Agora);
        await db.SaveChangesAsync(ct);
    }

    public async Task<OcorrenciaDetalhe> DetalharAsync(int ocorrenciaId, int usuarioId, PerfilUsuario perfil,
                                                       CancellationToken ct)
    {
        var ocorrencia = await CarregarCompletaAsync(ocorrenciaId, rastrear: false, ct);
        return Detalhe(ocorrencia, usuarioId, perfil);
    }

    public async Task<IReadOnlyList<OcorrenciaResumo>> MinhasAsync(int cidadaoId, CancellationToken ct) =>
        await db.Ocorrencias.AsNoTracking()
            .Where(o => o.AutorId == cidadaoId || o.Apoios.Any(a => a.CidadaoId == cidadaoId))
            .OrderByDescending(o => o.AbertaEm)
            .Select(Mapeamento.OcorrenciaParaResumo)
            .ToListAsync(ct);

    /// <summary>F7: não resolvidas, Desastre antes de Alta, e as mais antigas primeiro.</summary>
    public async Task<IReadOnlyList<OcorrenciaResumo>> FilaAsync(CancellationToken ct) =>
        await db.Ocorrencias.AsNoTracking()
            .Where(Mapeamento.Ativa)
            .OrderByDescending(o => o.Severidade)
            .ThenBy(o => o.AbertaEm)
            .Select(Mapeamento.OcorrenciaParaResumo)
            .ToListAsync(ct);

    public async Task<OcorrenciaDetalhe> AssumirAsync(int ocorrenciaId, int tecnicoId, CancellationToken ct)
    {
        var ocorrencia = await CarregarCompletaAsync(ocorrenciaId, rastrear: true, ct);
        var tecnico = await BuscarTecnicoAsync(tecnicoId, ct);

        ocorrencia.Assumir(tecnico, Agora);
        await db.SaveChangesAsync(ct);
        return Detalhe(ocorrencia, tecnicoId, PerfilUsuario.Tecnico);
    }

    public async Task<OcorrenciaDetalhe> ResolverAsync(int ocorrenciaId, int tecnicoId, string solucao,
                                                       CancellationToken ct)
    {
        var ocorrencia = await CarregarCompletaAsync(ocorrenciaId, rastrear: true, ct);
        var tecnico = await BuscarTecnicoAsync(tecnicoId, ct);

        ocorrencia.Resolver(tecnico, solucao, Agora);
        await db.SaveChangesAsync(ct);
        return Detalhe(ocorrencia, tecnicoId, PerfilUsuario.Tecnico);
    }

    // ---------- apoio ----------

    private async Task<Ocorrencia> CarregarCompletaAsync(int id, bool rastrear, CancellationToken ct)
    {
        var consulta = db.Ocorrencias
            .Include(o => o.Poste)
            .Include(o => o.Autor)
            .Include(o => o.Responsavel)
            .Include(o => o.Apoios)
            .Include(o => o.Historico)
            .AsSplitQuery();

        if (!rastrear) consulta = consulta.AsNoTracking();

        return await consulta.FirstOrDefaultAsync(o => o.Id == id, ct)
               ?? throw new NaoEncontradoException("Ocorrência não encontrada");
    }

    private async Task<Cidadao> BuscarCidadaoAsync(int id, CancellationToken ct) =>
        await db.Usuarios.OfType<Cidadao>().FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NaoAutorizadoException("Sessão inválida: entre novamente");

    private async Task<Tecnico> BuscarTecnicoAsync(int id, CancellationToken ct) =>
        await db.Usuarios.OfType<Tecnico>().FirstOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new NaoAutorizadoException("Sessão inválida: entre novamente");

    /// <summary>
    /// Monta o detalhe escondendo dados pessoais conforme quem pergunta (F6 e F8):
    /// o autor vê "Você", o técnico vê o nome abreviado e outros cidadãos não veem nada.
    /// </summary>
    private static OcorrenciaDetalhe Detalhe(Ocorrencia o, int usuarioId, PerfilUsuario perfil)
    {
        string? autor = o.Autor is null ? null
            : o.Autor.Id == usuarioId ? "Você"
            : perfil == PerfilUsuario.Tecnico ? o.Autor.NomeAbreviado()
            : null;

        return new OcorrenciaDetalhe(
            Mapeamento.Resumo(o), o.Descricao, autor, o.Responsavel?.NomeAbreviado(), o.Solucao, o.ResolvidaEm,
            o.Historico.OrderBy(h => h.RegistradoEm).ThenBy(h => h.Id).Select(Mapeamento.Resposta).ToList());
    }
}
