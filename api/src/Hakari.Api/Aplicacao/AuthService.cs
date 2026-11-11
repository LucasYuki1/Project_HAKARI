using Hakari.Api.Contratos;
using Hakari.Domain.Entidades;
using Hakari.Domain.Excecoes;
using Hakari.Domain.Interfaces;
using Hakari.Infrastructure.Dados;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Api.Aplicacao;

public interface IAuthService
{
    Task<SessaoResponse> CadastrarAsync(CadastroRequest pedido, CancellationToken ct);
    Task<SessaoResponse> EntrarAsync(LoginRequest pedido, CancellationToken ct);
}

/// <summary>F3: cadastro do cidadão e login de cidadãos e técnicos.</summary>
public class AuthService(HakariDbContext db, IHasherDeSenha hasher, ITokenService tokens, TimeProvider relogio)
    : IAuthService
{
    public async Task<SessaoResponse> CadastrarAsync(CadastroRequest pedido, CancellationToken ct)
    {
        if (!pedido.AceitouTermo)
            throw new ValidacaoException("É preciso aceitar o termo de uso de dados");
        PoliticaDeSenha.Validar(pedido.Senha);

        var email = Usuario.NormalizarEmail(pedido.Email ?? "");
        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            throw new ConflitoException("Este e-mail já está em uso");

        // O construtor de Cidadao valida nome, e-mail e telefone.
        var cidadao = new Cidadao(pedido.Nome, email, pedido.Telefone, hasher.Gerar(pedido.Senha),
                                  relogio.GetUtcNow().UtcDateTime);
        db.Usuarios.Add(cidadao);
        await db.SaveChangesAsync(ct);

        return tokens.Gerar(cidadao);
    }

    public async Task<SessaoResponse> EntrarAsync(LoginRequest pedido, CancellationToken ct)
    {
        var email = Usuario.NormalizarEmail(pedido.Email ?? "");
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Mesma mensagem para e-mail inexistente e senha errada: não revela quem tem conta.
        if (usuario is null || !hasher.Verificar(pedido.Senha ?? "", usuario.SenhaHash))
            throw new NaoAutorizadoException("E-mail ou senha incorretos");

        return tokens.Gerar(usuario);
    }
}
