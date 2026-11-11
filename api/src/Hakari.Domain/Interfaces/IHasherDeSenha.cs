namespace Hakari.Domain.Interfaces;

/// <summary>
/// Contrato para guardar senhas com segurança. O domínio só conhece a interface; a implementação
/// (BCrypt) fica na Infrastructure e é entregue por injeção de dependência.
/// </summary>
public interface IHasherDeSenha
{
    string Gerar(string senha);
    bool Verificar(string senha, string hash);
}
