namespace Hakari.Domain.Privacidade;

/// <summary>Grupo do dado pessoal, usado no painel e para decidir o peso do risco.</summary>
public enum CategoriaPii
{
    Identificacao,
    Contato,
    Localizacao,
    Financeiro,
    Saude,
    Credencial
}

/// <summary>
/// Uma regra do motor de PII, lida de regras-pii.json. A regra tem um padrão (expressão regular,
/// aplicada ao texto original) ou uma lista de termos (aplicada ao texto em minúsculas e sem acento).
/// </summary>
public sealed record RegraPii
{
    /// <summary>Identificador estável, usado nos testes e nos relatórios (ex.: "cpf").</summary>
    public required string Id { get; init; }

    /// <summary>Nome que o cidadão lê na mensagem ("CPF", "telefone").</summary>
    public required string Rotulo { get; init; }

    public required CategoriaPii Categoria { get; init; }

    /// <summary>Dado pessoal sensível (LGPD, art. 5º, II), como saúde.</summary>
    public bool Sensivel { get; init; }

    /// <summary>Expressão regular. O grupo <see cref="Grupo"/> é o dado em si.</summary>
    public string? Padrao { get; init; }

    public int Grupo { get; init; }

    public bool IgnorarCaixa { get; init; }

    /// <summary>Termos procurados como palavras inteiras, sem diferenciar acento e caixa.</summary>
    public string[] Termos { get; init; } = [];

    /// <summary>Expressões que anulam um termo ("ponto cego" não é dado de saúde).</summary>
    public string[] Excecoes { get; init; } = [];

    /// <summary>Validador do valor encontrado: cpf, cns, luhn ou telefone.</summary>
    public string? Validador { get; init; }

    /// <summary>Palavras que, perto do valor, confirmam que ele é o dado procurado.</summary>
    public string[] Contexto { get; init; } = [];

    /// <summary>Sem uma palavra de contexto por perto, o valor é ignorado (ex.: RG, data de nascimento).</summary>
    public bool ExigeContexto { get; init; }

    /// <summary>Quantos caracteres antes e depois do valor procurar o contexto.</summary>
    public int JanelaContexto { get; init; } = 30;

    /// <summary>Texto que substitui o dado ("[CPF oculto]").</summary>
    public required string Mascara { get; init; }
}

/// <summary>
/// Um dado pessoal encontrado. Guarda onde ele está e o que ele é, nunca o valor: assim o achado
/// pode ir para log ou relatório sem levar o dado junto.
/// </summary>
public sealed record AchadoPii(string Regra, string Rotulo, CategoriaPii Categoria, bool Sensivel,
                               int Inicio, int Tamanho, double Confianca);
