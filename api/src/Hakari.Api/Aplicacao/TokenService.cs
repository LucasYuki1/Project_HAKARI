using System.Security.Claims;
using System.Text;
using Hakari.Api.Contratos;
using Hakari.Domain.Entidades;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hakari.Api.Aplicacao;

public class JwtOpcoes
{
    public string Emissor { get; set; } = "hakari-api";
    public string Audiencia { get; set; } = "hakari-app";
    public string Chave { get; set; } = "";
    public int ExpiracaoHoras { get; set; } = 8;

    public SymmetricSecurityKey ChaveDeAssinatura() => new(Encoding.UTF8.GetBytes(Chave));
}

public interface ITokenService
{
    SessaoResponse Gerar(Usuario usuario);
}

/// <summary>
/// Gera o JWT que o app manda no cabeçalho Authorization. Ele carrega só o id, o nome e o perfil:
/// nada de e-mail ou telefone dentro do token.
/// </summary>
public class TokenService(IOptions<JwtOpcoes> opcoes, TimeProvider relogio) : ITokenService
{
    private readonly JwtOpcoes _opcoes = opcoes.Value;

    public SessaoResponse Gerar(Usuario usuario)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var expira = agora.AddHours(_opcoes.ExpiracaoHoras);

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Audiencia,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expira,
            SigningCredentials = new SigningCredentials(_opcoes.ChaveDeAssinatura(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim("name", usuario.Nome),
                new Claim("role", usuario.Perfil.ToString())
            ])
        };

        var token = new JsonWebTokenHandler().CreateToken(descritor);
        return new SessaoResponse(token, expira, new UsuarioResumo(usuario.Id, usuario.Nome, usuario.Perfil));
    }
}
