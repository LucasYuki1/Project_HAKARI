using Hakari.Api.Aplicacao;
using Hakari.Api.Contratos;
using Hakari.Api.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hakari.Api.Controllers;

/// <summary>Painel da equipe de manutenção (F7). Cidadãos recebem 403.</summary>
[ApiController]
[Route("api/manutencao")]
[Authorize(Roles = Perfis.Tecnico)]
[Produces("application/json")]
public class ManutencaoController(IOcorrenciaService ocorrencias, IPosteService postes) : ControllerBase
{
    /// <summary>Fila de ocorrências não resolvidas: Desastre primeiro, depois Alta; as mais antigas antes.</summary>
    [HttpGet("fila")]
    [ProducesResponseType<IReadOnlyList<OcorrenciaResumo>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OcorrenciaResumo>>> Fila(CancellationToken ct) =>
        Ok(await ocorrencias.FilaAsync(ct));

    /// <summary>Situação da iluminação por bairro: postes funcionando, apagados, sem comunicação e ocorrências abertas.</summary>
    [HttpGet("bairros")]
    [ProducesResponseType<IReadOnlyList<ResumoBairro>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ResumoBairro>>> Bairros(CancellationToken ct) =>
        Ok(await postes.ResumoPorBairroAsync(ct));
}
