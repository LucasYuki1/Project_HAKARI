using Hakari.Domain.Interfaces;

namespace Hakari.Infrastructure.Seguranca;

/// <summary>Senhas nunca são gravadas: só o hash BCrypt, que já embute o salt.</summary>
public class HasherBCrypt : IHasherDeSenha
{
    private const int Custo = 11;

    public string Gerar(string senha) => BCrypt.Net.BCrypt.HashPassword(senha, Custo);

    public bool Verificar(string senha, string hash) => BCrypt.Net.BCrypt.Verify(senha, hash);
}
