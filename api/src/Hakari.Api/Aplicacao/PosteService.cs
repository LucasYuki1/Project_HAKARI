using Hakari.Api.Contratos;
using Hakari.Domain.Enums;
using Hakari.Domain.Excecoes;
using Hakari.Infrastructure.Dados;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Api.Aplicacao;

public interface IPosteService
{
    Task<IReadOnlyList<PosteResumo>> ListarAsync(string? bairro, CancellationToken ct);
    Task<PosteDetalhe> DetalharAsync(int id, CancellationToken ct);
    Task<ResumoTelemetria> TelemetriaAsync(int id, int horas, CancellationToken ct);
    Task<IReadOnlyList<ResumoBairro>> ResumoPorBairroAsync(CancellationToken ct);
}

/// <summary>F4: consulta de postes e da telemetria que os nós mandam.</summary>
public class PosteService(HakariDbContext db, TimeProvider relogio) : IPosteService
{
    public async Task<IReadOnlyList<PosteResumo>> ListarAsync(string? bairro, CancellationToken ct)
    {
        IQueryable<Domain.Entidades.Poste> consulta = db.Postes.AsNoTracking()
            .Include(p => p.Alertas.Where(a => a.Estado == EstadoAlerta.Ativo));

        if (!string.IsNullOrWhiteSpace(bairro))
        {
            var termo = bairro.Trim().ToLower();
            consulta = consulta.Where(p => p.Bairro.ToLower().Contains(termo));
        }

        var postes = await consulta.OrderBy(p => p.Codigo).ToListAsync(ct);
        return postes.Select(Mapeamento.Resumo).ToList();
    }

    public async Task<PosteDetalhe> DetalharAsync(int id, CancellationToken ct)
    {
        var poste = await db.Postes.AsNoTracking()
                        .Include(p => p.Alertas.Where(a => a.Estado == EstadoAlerta.Ativo))
                        .FirstOrDefaultAsync(p => p.Id == id, ct)
                    ?? throw new NaoEncontradoException("Poste não encontrado");

        var ocorrenciaAtiva = await db.Ocorrencias.AsNoTracking()
            .Where(o => o.PosteId == id)
            .Where(Mapeamento.Ativa)
            .Select(Mapeamento.OcorrenciaParaResumo)
            .FirstOrDefaultAsync(ct);

        return new PosteDetalhe(poste.Id, poste.Codigo, poste.Endereco, poste.Bairro, poste.Latitude,
                                poste.Longitude, poste.StatusLegivel(), poste.UltimaComunicacao,
                                poste.UltimoBrilho, poste.UltimoRssi,
                                poste.Alertas.OrderByDescending(a => a.Severidade).Select(Mapeamento.Resposta).ToList(),
                                ocorrenciaAtiva);
    }

    public async Task<ResumoTelemetria> TelemetriaAsync(int id, int horas, CancellationToken ct)
    {
        horas = Math.Clamp(horas, 1, 168);
        var codigo = await db.Postes.Where(p => p.Id == id).Select(p => p.Codigo).FirstOrDefaultAsync(ct)
                     ?? throw new NaoEncontradoException("Poste não encontrado");

        var desde = relogio.GetUtcNow().UtcDateTime.AddHours(-horas);
        var periodo = db.Leituras.AsNoTracking().Where(l => l.PosteId == id && l.RecebidaEm >= desde);

        // As agregações rodam no banco (COUNT/AVG), não em memória.
        var total = await periodo.CountAsync(ct);
        var brilhoMedio = await periodo.AverageAsync(l => (double?)l.Brilho, ct);
        var rssiMedio = await periodo.AverageAsync(l => (double?)l.Rssi, ct);
        var presencas = await periodo.CountAsync(l => l.Presenca, ct);

        var ultimas = await periodo
            .OrderByDescending(l => l.RecebidaEm)
            .Take(20)
            .Select(l => new LeituraResponse(l.RecebidaEm, l.Ldr, l.LdrReferencia, l.DistanciaCm,
                                             l.Presenca, l.Brilho, l.Rssi))
            .ToListAsync(ct);

        return new ResumoTelemetria(codigo, horas, total,
                                    brilhoMedio is null ? null : Math.Round(brilhoMedio.Value, 1),
                                    presencas,
                                    rssiMedio is null ? null : Math.Round(rssiMedio.Value, 1),
                                    ultimas);
    }

    public async Task<IReadOnlyList<ResumoBairro>> ResumoPorBairroAsync(CancellationToken ct)
    {
        // Uma consulta traz o estado de cada poste; o agrupamento por bairro é LINQ em memória.
        var postes = await db.Postes.AsNoTracking()
            .Select(p => new
            {
                p.Bairro,
                Instalado = p.UltimaComunicacao != null,
                SemComunicacao = p.Isolado,
                Apagado = p.Alertas.Any(a => a.Estado == EstadoAlerta.Ativo && a.Trigger == TipoTrigger.LampadaSemResposta)
            })
            .ToListAsync(ct);

        var abertasPorBairro = await db.Ocorrencias.AsNoTracking()
            .Where(Mapeamento.Ativa)
            .GroupBy(o => o.Poste.Bairro)
            .Select(g => new { Bairro = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Bairro, x => x.Total, ct);

        return postes
            .GroupBy(p => p.Bairro)
            .Select(g => new ResumoBairro(
                g.Key,
                g.Count(),
                g.Count(p => p.Instalado && !p.SemComunicacao && !p.Apagado),
                g.Count(p => p.Apagado && !p.SemComunicacao),
                g.Count(p => p.SemComunicacao),
                g.Count(p => !p.Instalado),
                abertasPorBairro.GetValueOrDefault(g.Key)))
            .OrderByDescending(r => r.OcorrenciasAbertas)
            .ThenBy(r => r.Bairro)
            .ToList();
    }
}
