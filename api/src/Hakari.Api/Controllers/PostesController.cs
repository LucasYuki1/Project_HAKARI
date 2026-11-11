using Hakari.Api.Aplicacao;
using Hakari.Api.Contratos;
using Hakari.Api.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hakari.Api.Controllers;

/// <summary>Postes, telemetria e reportes de problema (F4 e F5).</summary>
[ApiController]
[Route("api/postes")]
[Authorize]
[Produces("application/json")]
public class PostesController(IPosteService postes, IOcorrenciaService ocorrencias) : ControllerBase
{
    /// <summary>Lista os postes com o status de cada um ("Funcionando", "Apagado", "Sem comunicação"...).</summary>
    /// <param name="bairro">Filtro opcional por parte do nome do bairro.</param>
    /// <param name="ct">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PosteResumo>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PosteResumo>>> Listar([FromQuery] string? bairro, CancellationToken ct) =>
        Ok(await postes.ListarAsync(bairro, ct));

    /// <summary>Detalhe de um poste: status, última comunicação, alertas e ocorrência ativa.</summary>
    /// <response code="404">Poste não encontrado.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PosteDetalhe>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosteDetalhe>> Detalhar(int id, CancellationToken ct) =>
        Ok(await postes.DetalharAsync(id, ct));

    /// <summary>Resumo da telemetria do nó nas últimas horas (médias e últimas leituras).</summary>
    /// <param name="id">Id do poste.</param>
    /// <param name="horas">Janela de tempo, de 1 a 168 horas (padrão 24).</param>
    /// <param name="ct">Cancelamento da requisição.</param>
    [HttpGet("{id:int}/telemetria")]
    [ProducesResponseType<ResumoTelemetria>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResumoTelemetria>> Telemetria(int id, [FromQuery] int horas = 24, CancellationToken ct = default) =>
        Ok(await postes.TelemetriaAsync(id, horas, ct));

    /// <summary>
    /// Cidadão reporta um problema no poste. Se já houver ocorrência ativa, o reporte vira apoio a ela.
    /// </summary>
    /// <response code="201">Ocorrência nova aberta.</response>
    /// <response code="200">Já havia ocorrência: o apoio foi adicionado.</response>
    /// <response code="400">Descreva o problema.</response>
    /// <response code="404">Poste não encontrado.</response>
    /// <response code="409">Você já apoiou esta ocorrência.</response>
    [HttpPost("{id:int}/reportes")]
    [Authorize(Roles = Perfis.Cidadao)]
    [ProducesResponseType<ReporteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ReporteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReporteResponse>> Reportar(int id, ReporteRequest pedido, CancellationToken ct)
    {
        var resultado = await ocorrencias.ReportarAsync(id, User.Id(), pedido, ct);
        return resultado.ApoioAdicionado
            ? Ok(resultado)
            : CreatedAtAction(nameof(OcorrenciasController.Detalhar), "Ocorrencias", new { id = resultado.OcorrenciaId }, resultado);
    }
}
