namespace Hakari.Domain.Privacidade;

/// <summary>
/// Texto livre que já passou pelo motor de PII. O construtor é interno: só o <see cref="MotorPii"/>
/// cria um TextoSeguro. Por isso os métodos da entidade Ocorrencia pedem TextoSeguro em vez de string,
/// e o compilador recusa qualquer caminho em que o texto digitado chegue ao banco sem o filtro.
/// É a barreira de taint no próprio tipo, que vale mesmo onde a análise estática não enxerga.
/// </summary>
public sealed class TextoSeguro
{
    internal TextoSeguro(string texto, IReadOnlyList<AchadoPii> achados)
    {
        Texto = texto;
        Achados = achados;
    }

    /// <summary>O texto com os dados pessoais trocados por marcadores.</summary>
    public string Texto { get; }

    public IReadOnlyList<AchadoPii> Achados { get; }

    public bool OcultouDados => Achados.Count > 0;

    /// <summary>O que foi escondido, sem repetição ("CPF", "telefone").</summary>
    public IReadOnlyList<string> DadosOcultados => Achados.Select(a => a.Rotulo).Distinct().ToList();

    public override string ToString() => Texto;
}
