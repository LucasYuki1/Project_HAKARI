using Hakari.Api.Aplicacao;
using Hakari.Api.Contratos;
using Hakari.Api.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hakari.Api.Controllers;

/// <summary>Dados e ocorrências do próprio usuário (F6 e F8).</summary>
[ApiController]
[Route("api/me")]
[Authorize]
[Produces("application/json")]
public class MeController(IContaService conta, IOcorrenciaService ocorrencias) : ControllerBase
{
    /// <summary>Meus dados cadastrados (nome, e-mail, telefone e quando aceitei o termo).</summary>
    [HttpGet]
    [ProducesResponseType<MeusDadosResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MeusDadosResponse>> MeusDados(CancellationToken ct) =>
        Ok(await conta.MeusDadosAsync(User.Id(), ct));

    /// <summary>Ocorrências que abri ou apoiei, das mais recentes para as mais antigas.</summary>
    [HttpGet("ocorrencias")]
    [Authorize(Roles = Perfis.Cidadao)]
    [ProducesResponseType<IReadOnlyList<OcorrenciaResumo>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OcorrenciaResumo>>> MinhasOcorrencias(CancellationToken ct) =>
        Ok(await ocorrencias.MinhasAsync(User.Id(), ct));

    /// <summary>
    /// Exclui minha conta (LGPD). As ocorrências que abri continuam existindo, sem identificação do autor.
    /// </summary>
    /// <response code="204">Conta excluída; descarte o token.</response>
    [HttpDelete]
    [Authorize(Roles = Perfis.Cidadao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ExcluirConta(CancellationToken ct)
    {
        await conta.ExcluirAsync(User.Id(), ct);
        return NoContent();
    }
}
