using System.Security.Claims;
using Hakari.Domain.Enums;
using Hakari.Domain.Excecoes;

namespace Hakari.Api.Infra;

/// <summary>Lê do token JWT quem está fazendo a requisição.</summary>
public static class UsuarioLogado
{
    public static int Id(this ClaimsPrincipal usuario) =>
        int.TryParse(usuario.FindFirstValue("sub"), out var id)
            ? id
            : throw new NaoAutorizadoException("Sessão inválida: entre novamente");

    public static PerfilUsuario Perfil(this ClaimsPrincipal usuario) =>
        Enum.TryParse<PerfilUsuario>(usuario.FindFirstValue("role"), out var perfil)
            ? perfil
            : throw new NaoAutorizadoException("Sessão inválida: entre novamente");
}

/// <summary>Nomes das roles usadas em [Authorize(Roles = ...)].</summary>
public static class Perfis
{
    public const string Cidadao = nameof(PerfilUsuario.Cidadao);
    public const string Tecnico = nameof(PerfilUsuario.Tecnico);
}
