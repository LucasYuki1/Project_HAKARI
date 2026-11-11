using System.Net.Mail;
using Hakari.Domain.Enums;
using Hakari.Domain.Excecoes;

namespace Hakari.Domain.Entidades;

/// <summary>
/// Pessoa que usa o sistema. É abstrata: todo usuário é um <see cref="Cidadao"/> ou um
/// <see cref="Tecnico"/>. No banco, os dois ficam na mesma tabela (herança TPH do EF Core).
/// </summary>
public abstract class Usuario : Entidade
{
    protected Usuario() { } // usado pelo EF Core

    protected Usuario(string nome, string email, string telefone, string senhaHash, DateTime criadoEm)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length < 2)
            throw new ValidacaoException("Informe seu nome");
        if (!MailAddress.TryCreate(email?.Trim(), out _))
            throw new ValidacaoException("Informe um e-mail válido");

        var digitos = new string((telefone ?? "").Where(char.IsDigit).ToArray());
        if (digitos.Length is < 10 or > 11)
            throw new ValidacaoException("Informe um telefone com DDD (10 ou 11 dígitos)");

        Nome = nome.Trim();
        Email = NormalizarEmail(email!);
        Telefone = digitos;
        SenhaHash = senhaHash;
        CriadoEm = criadoEm;
    }

    public string Nome { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string Telefone { get; private set; } = "";
    public string SenhaHash { get; private set; } = "";
    public DateTime CriadoEm { get; private set; }

    /// <summary>Cada subclasse diz qual é o seu perfil (polimorfismo). Vira a "role" do JWT.</summary>
    public abstract PerfilUsuario Perfil { get; }

    /// <summary>"Lucas Henrique Yuki" vira "Lucas Y.": o técnico vê só isso do cidadão (F8).</summary>
    public string NomeAbreviado()
    {
        var partes = Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 1 ? partes[0] : $"{partes[0]} {partes[^1][0]}.";
    }

    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();
}

/// <summary>Morador que se cadastra pelo app, reporta problemas e acompanha ocorrências.</summary>
public class Cidadao : Usuario
{
    private Cidadao() { }

    public Cidadao(string nome, string email, string telefone, string senhaHash, DateTime agora)
        : base(nome, email, telefone, senhaHash, agora)
    {
        TermoAceitoEm = agora;
    }

    /// <summary>Momento em que o cidadão aceitou o termo de uso de dados (LGPD).</summary>
    public DateTime TermoAceitoEm { get; private set; }

    public override PerfilUsuario Perfil => PerfilUsuario.Cidadao;
}

/// <summary>Integrante da equipe de manutenção: assume e resolve ocorrências.</summary>
public class Tecnico : Usuario
{
    private Tecnico() { }

    public Tecnico(string nome, string email, string telefone, string senhaHash, string matricula, DateTime agora)
        : base(nome, email, telefone, senhaHash, agora)
    {
        Matricula = matricula;
    }

    public string Matricula { get; private set; } = "";

    public override PerfilUsuario Perfil => PerfilUsuario.Tecnico;
}

/// <summary>Regra de senha da F3: pelo menos 8 caracteres, com letra e número.</summary>
public static class PoliticaDeSenha
{
    public const string Mensagem = "A senha precisa ter pelo menos 8 caracteres, com letras e números";

    public static void Validar(string? senha)
    {
        if (senha is null || senha.Length < 8 || !senha.Any(char.IsLetter) || !senha.Any(char.IsDigit))
            throw new ValidacaoException(Mensagem);
    }
}
