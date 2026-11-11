using Hakari.Api.Aplicacao;
using Hakari.Api.Contratos;
using Hakari.Api.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hakari.Api.Controllers;

/// <summary>Detalhe, apoio e atendimento de ocorrências (F5, F6 e F7).</summary>
[ApiController]
[Route("api/ocorrencias")]
[Authorize]
[Produces("application/json")]
public class OcorrenciasController(IOcorrenciaService ocorrencias) : ControllerBase
{
    /// <summary>Detalhe com histórico. Dados de quem abriu só aparecem conforme o perfil de quem pergunta.</summary>
    /// <response code="404">Ocorrência não encontrada.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OcorrenciaDetalhe>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OcorrenciaDetalhe>> Detalhar(int id, CancellationToken ct) =>
        Ok(await ocorrencias.DetalharAsync(id, User.Id(), User.Perfil(), ct));

    /// <summary>Cidadão apoia uma ocorrência existente ("eu também vi esse problema").</summary>
    /// <response code="204">Apoio registrado.</response>
    /// <response code="409">Você já apoiou esta ocorrência.</response>
    [HttpPost("{id:int}/apoios")]
    [Authorize(Roles = Perfis.Cidadao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Apoiar(int id, CancellationToken ct)
    {
        await ocorrencias.ApoiarAsync(id, User.Id(), ct);
        return NoContent();
    }

    /// <summary>Técnico assume a ocorrência: Aberta → Em atendimento.</summary>
    /// <response code="422">A ocorrência não está Aberta.</response>
    [HttpPost("{id:int}/assumir")]
    [Authorize(Roles = Perfis.Tecnico)]
    [ProducesResponseType<OcorrenciaDetalhe>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OcorrenciaDetalhe>> Assumir(int id, CancellationToken ct) =>
        Ok(await ocorrencias.AssumirAsync(id, User.Id(), ct));

    /// <summary>Técnico responsável resolve a ocorrência: Em atendimento → Resolvida.</summary>
    /// <response code="400">Informe o que foi feito.</response>
    /// <response code="422">Assuma a ocorrência antes de resolvê-la.</response>
    [HttpPost("{id:int}/resolver")]
    [Authorize(Roles = Perfis.Tecnico)]
    [ProducesResponseType<OcorrenciaDetalhe>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OcorrenciaDetalhe>> Resolver(int id, ResolverRequest pedido, CancellationToken ct) =>
        Ok(await ocorrencias.ResolverAsync(id, User.Id(), pedido.Solucao, ct));
}
