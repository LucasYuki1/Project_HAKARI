using Hakari.Api.Aplicacao;
using Hakari.Api.Contratos;
using Microsoft.AspNetCore.Mvc;

namespace Hakari.Api.Controllers;

/// <summary>Cadastro e login (F3).</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Cria a conta de um cidadão e já devolve o token de acesso.</summary>
    /// <response code="201">Conta criada.</response>
    /// <response code="400">Dados inválidos, senha fraca ou termo não aceito.</response>
    /// <response code="409">Este e-mail já está em uso.</response>
    [HttpPost("cadastro")]
    [ProducesResponseType<SessaoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessaoResponse>> Cadastrar(CadastroRequest pedido, CancellationToken ct)
    {
        var sessao = await auth.CadastrarAsync(pedido, ct);
        return StatusCode(StatusCodes.Status201Created, sessao);
    }

    /// <summary>Entra com e-mail e senha (cidadão ou técnico).</summary>
    /// <response code="200">Login feito; use o token no botão Authorize.</response>
    /// <response code="401">E-mail ou senha incorretos.</response>
    [HttpPost("login")]
    [ProducesResponseType<SessaoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessaoResponse>> Entrar(LoginRequest pedido, CancellationToken ct) =>
        Ok(await auth.EntrarAsync(pedido, ct));
}
