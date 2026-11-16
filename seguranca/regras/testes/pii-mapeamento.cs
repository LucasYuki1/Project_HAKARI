// Casos de teste das regras de mapeamento (semgrep --test). Não é código do Hakari.
public class Pessoa
{
    // ruleid: hakari.mapa.identificacao
    public string Nome { get; private set; } = "";
    // ruleid: hakari.mapa.contato
    public string Email { get; private set; } = "";
    // ruleid: hakari.mapa.credencial
    public string SenhaHash { get; private set; } = "";
    // ruleid: hakari.mapa.saude
    public string? Deficiencia { get; set; }
    // ruleid: hakari.mapa.identificacao
    public string Cpf { get; init; } = "";
    // ruleid: hakari.mapa.texto-livre
    public string? Solucao { get; private set; }
    // ruleid: hakari.mapa.consentimento
    public DateTime TermoAceitoEm { get; private set; }
    // ok: hakari.mapa.identificacao
    public string NomeDoArquivo { get; set; } = "";
    // ok: hakari.mapa.texto-livre
    public int PosteId { get; private set; }
    // ok: hakari.mapa.localizacao-pessoal
    public string Endereco { get; private set; } = "";

    public Pessoa(string nome, string email)
    {
        // ok: hakari.mapa.identificacao
        Nome = nome;
    }
}

// ruleid: hakari.mapa.identificacao, hakari.mapa.contato, hakari.mapa.credencial, hakari.mapa.consentimento
public record CadastroRequest(string Nome, string Email, string Telefone, string Senha, bool AceitouTermo);

// ok: hakari.mapa.localizacao-pessoal
public record PosteResumo(int Id, string Codigo, string Endereco, string Bairro, double Latitude);

// ruleid: hakari.mapa.saude, hakari.mapa.localizacao-pessoal
public record FichaCidadao(int Id, bool MobilidadeReduzida, string Cep);
