using System.Linq.Expressions;
using Hakari.Api.Contratos;
using Hakari.Domain.Entidades;
using Hakari.Domain.Enums;

namespace Hakari.Api.Aplicacao;

/// <summary>Conversões entidade → DTO usadas por mais de um serviço.</summary>
internal static class Mapeamento
{
    /// <summary>
    /// Projeção usada direto nas consultas LINQ: o EF Core gera um SELECT só com as colunas
    /// necessárias (incluindo o COUNT de apoios), sem carregar a entidade inteira.
    /// </summary>
    public static readonly Expression<Func<Ocorrencia, OcorrenciaResumo>> OcorrenciaParaResumo = o =>
        new OcorrenciaResumo(o.Id, o.PosteId, o.Poste.Codigo, o.Poste.Endereco, o.Poste.Bairro,
                             o.Status.Texto(), o.Severidade, o.Origem.Texto(), o.Categoria,
                             o.AbertaEm, o.Apoios.Count());

    private static readonly Func<Ocorrencia, OcorrenciaResumo> OcorrenciaParaResumoCompilado =
        OcorrenciaParaResumo.Compile();

    /// <summary>Versão em memória: exige Poste e Apoios carregados.</summary>
    public static OcorrenciaResumo Resumo(Ocorrencia o) => OcorrenciaParaResumoCompilado(o);

    public static PosteResumo Resumo(Poste p) =>
        new(p.Id, p.Codigo, p.Endereco, p.Bairro, p.Latitude, p.Longitude, p.StatusLegivel(), p.UltimaComunicacao);

    public static AlertaResponse Resposta(Alerta a) =>
        new(a.Trigger, a.Severidade, a.Estado, a.AbertoEm, a.NormalizadoEm, a.Detalhe);

    public static HistoricoResponse Resposta(HistoricoOcorrencia h) =>
        new(h.RegistradoEm, h.Status.Texto(), h.Descricao);

    /// <summary>Filtro reutilizável: só ocorrências que ainda não foram resolvidas.</summary>
    public static readonly Expression<Func<Ocorrencia, bool>> Ativa = o => o.Status != StatusOcorrencia.Resolvida;
}
