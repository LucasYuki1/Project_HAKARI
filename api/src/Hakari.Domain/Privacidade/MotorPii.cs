using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Hakari.Domain.Privacidade;

/// <summary>
/// Motor de PII: encontra dado pessoal em texto livre (CPF, telefone, e-mail, endereço, saúde,
/// senha, token...) e devolve o texto com cada trecho trocado por um marcador como "[CPF oculto]".
///
/// As regras ficam em regras-pii.json, embutido na DLL: criar uma regra nova é acrescentar um item
/// ao arquivo. Cada achado recebe uma confiança: o validador (dígito verificador, Luhn, DDD) e as
/// palavras de contexto por perto aumentam a confiança; um número que falha no validador só é
/// escondido se houver contexto ("meu CPF é ..."). Na dúvida, o motor prefere esconder a mais.
/// </summary>
public sealed class MotorPii
{
    public const double LimiarDeConfianca = 0.5;
    private const string RecursoDasRegras = "Hakari.Domain.Privacidade.regras-pii.json";

    // Expressão regular com tempo máximo: um texto montado para travar a regex (ReDoS) não derruba a API.
    private static readonly TimeSpan TempoMaximo = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Lazy<MotorPii> PadraoPreguicoso = new(() => DeJson(LerRegrasEmbutidas()));

    private readonly List<RegraCompilada> _regras;
    private readonly Dictionary<string, string> _mascaras;

    public MotorPii(IEnumerable<RegraPii> regras)
    {
        Regras = regras.ToList();
        _regras = Regras.Select((r, ordem) => new RegraCompilada(r, ordem)).ToList();
        _mascaras = Regras.ToDictionary(r => r.Id, r => r.Mascara);
    }

    /// <summary>O motor com as regras de regras-pii.json.</summary>
    public static MotorPii Padrao => PadraoPreguicoso.Value;

    public IReadOnlyList<RegraPii> Regras { get; }

    public static MotorPii DeJson(string json)
    {
        var arquivo = JsonSerializer.Deserialize<ArquivoDeRegras>(json, OpcoesJson)
                      ?? throw new InvalidOperationException("regras-pii.json vazio");
        var repetidas = arquivo.Regras.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidas.Count > 0)
            throw new InvalidOperationException($"Regras com id repetido: {string.Join(", ", repetidas)}");
        return new MotorPii(arquivo.Regras);
    }

    public static string LerRegrasEmbutidas()
    {
        using var fluxo = typeof(MotorPii).Assembly.GetManifestResourceStream(RecursoDasRegras)
                          ?? throw new InvalidOperationException($"Recurso {RecursoDasRegras} não encontrado");
        using var leitor = new StreamReader(fluxo, Encoding.UTF8);
        return leitor.ReadToEnd();
    }

    /// <summary>Encontra os dados pessoais do texto, sem sobreposição, em ordem de posição.</summary>
    public IReadOnlyList<AchadoPii> Analisar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return [];

        var normalizado = Normalizar(texto);
        var candidatos = _regras.SelectMany(r => r.Procurar(texto, normalizado)).ToList();

        // Quando dois achados disputam o mesmo trecho, fica o de maior confiança, depois o mais
        // longo, depois o da regra que vem primeiro no arquivo.
        var escolhidos = new List<AchadoPii>();
        foreach (var (achado, _) in candidatos.OrderByDescending(c => c.Achado.Confianca)
                                              .ThenByDescending(c => c.Achado.Tamanho)
                                              .ThenBy(c => c.Ordem))
        {
            if (!escolhidos.Any(e => achado.Inicio < e.Inicio + e.Tamanho && e.Inicio < achado.Inicio + achado.Tamanho))
                escolhidos.Add(achado);
        }
        return escolhidos.OrderBy(a => a.Inicio).ToList();
    }

    /// <summary>
    /// Devolve o texto pronto para ser guardado e mostrado a outras pessoas. Se alguma regra estourar
    /// o tempo, o motor falha fechado: esconde o texto inteiro em vez de deixá-lo passar sem análise.
    /// </summary>
    public TextoSeguro Filtrar(string? texto)
    {
        var original = texto?.Trim() ?? "";
        IReadOnlyList<AchadoPii> achados;
        try
        {
            achados = Analisar(original);
        }
        catch (RegexMatchTimeoutException)
        {
            var tudo = new AchadoPii("tempo-esgotado", "texto não verificado", CategoriaPii.Identificacao, false,
                                     0, original.Length, 1.0);
            return new TextoSeguro("[texto oculto: não foi possível verificar dados pessoais]", [tudo]);
        }

        if (achados.Count == 0) return new TextoSeguro(original, achados);

        var saida = new StringBuilder(original.Length);
        var cursor = 0;
        foreach (var a in achados)
        {
            saida.Append(original, cursor, a.Inicio - cursor).Append(_mascaras[a.Regra]);
            cursor = a.Inicio + a.Tamanho;
        }
        saida.Append(original, cursor, original.Length - cursor);
        return new TextoSeguro(saida.ToString(), achados);
    }

    /// <summary>
    /// Minúsculas e sem acento, com o mesmo tamanho do original: a posição de um termo achado no
    /// texto normalizado é a mesma no texto original.
    /// </summary>
    internal static string Normalizar(string texto)
    {
        var saida = new char[texto.Length];
        for (var i = 0; i < texto.Length; i++)
        {
            var c = char.ToLowerInvariant(texto[i]);
            if (c > 127 && !char.IsSurrogate(c))
            {
                var decomposto = c.ToString().Normalize(NormalizationForm.FormD);
                if (decomposto.Length > 0 && decomposto[0] <= 127) c = decomposto[0];
            }
            saida[i] = c;
        }
        return new string(saida);
    }

    private sealed record ArquivoDeRegras(int Versao, List<RegraPii> Regras);

    private sealed class RegraCompilada
    {
        private readonly RegraPii _regra;
        private readonly int _ordem;
        private readonly Regex? _padrao;
        private readonly Regex? _termos;
        private readonly Regex? _excecoes;
        private readonly Regex? _contexto;

        public RegraCompilada(RegraPii regra, int ordem)
        {
            _regra = regra;
            _ordem = ordem;

            var opcoes = RegexOptions.CultureInvariant | (regra.IgnorarCaixa ? RegexOptions.IgnoreCase : RegexOptions.None);
            if (!string.IsNullOrWhiteSpace(regra.Padrao)) _padrao = new Regex(regra.Padrao, opcoes, TempoMaximo);
            if (regra.Termos.Length > 0) _termos = Palavras(regra.Termos);
            if (regra.Excecoes.Length > 0) _excecoes = Palavras(regra.Excecoes);
            if (regra.Contexto.Length > 0) _contexto = Palavras(regra.Contexto);

            if (_padrao is null && _termos is null)
                throw new InvalidOperationException($"A regra '{regra.Id}' precisa de 'padrao' ou de 'termos'");

            // Um validador com nome errado falha aqui, ao carregar as regras, e não no meio de um reporte.
            _ = Validadores.Validar(regra.Validador, "");
        }

        public IEnumerable<(AchadoPii Achado, int Ordem)> Procurar(string texto, string normalizado)
        {
            if (_padrao is not null)
            {
                foreach (Match m in _padrao.Matches(texto))
                {
                    var g = m.Groups[_regra.Grupo];
                    if (!g.Success || g.Length == 0) continue;
                    var achado = Avaliar(g.Index, g.Length, g.Value, normalizado);
                    if (achado is not null) yield return (achado, _ordem);
                }
            }

            if (_termos is not null)
            {
                var excecoes = _excecoes?.Matches(normalizado).Select(e => (e.Index, e.Length)).ToList() ?? [];
                foreach (Match m in _termos.Matches(normalizado))
                {
                    if (excecoes.Any(e => m.Index < e.Index + e.Length && e.Index < m.Index + m.Length)) continue;
                    var achado = Avaliar(m.Index, m.Length, texto.Substring(m.Index, m.Length), normalizado);
                    if (achado is not null) yield return (achado, _ordem);
                }
            }
        }

        private AchadoPii? Avaliar(int inicio, int tamanho, string valor, string normalizado)
        {
            var temContexto = _contexto is not null && _contexto.IsMatch(Janela(normalizado, inicio, tamanho));
            if (_regra.ExigeContexto && !temContexto) return null;

            double confianca;
            if (_regra.Validador is not null)
            {
                if (Validadores.Validar(_regra.Validador, valor)) confianca = temContexto ? 0.99 : 0.85;
                else if (temContexto) confianca = 0.6; // tem cara do dado e alguém disse o que é
                else return null;
            }
            else
            {
                confianca = temContexto ? 0.95 : _termos is not null ? 0.8 : 0.75;
            }

            return confianca < LimiarDeConfianca
                ? null
                : new AchadoPii(_regra.Id, _regra.Rotulo, _regra.Categoria, _regra.Sensivel, inicio, tamanho, confianca);
        }

        private string Janela(string normalizado, int inicio, int tamanho)
        {
            var de = Math.Max(0, inicio - _regra.JanelaContexto);
            var ate = Math.Min(normalizado.Length, inicio + tamanho + _regra.JanelaContexto);
            return normalizado[de..ate];
        }

        // Palavras inteiras, sem acento e sem diferenciar caixa; espaço vira "um ou mais espaços".
        private static Regex Palavras(IEnumerable<string> termos)
        {
            var alternativas = termos
                .Select(t => Regex.Escape(Normalizar(t.Trim())).Replace(@"\ ", @"\s+"))
                .OrderByDescending(t => t.Length);
            return new Regex($@"(?<![\p{{L}}\d])(?:{string.Join("|", alternativas)})(?![\p{{L}}\d])",
                             RegexOptions.CultureInvariant, TempoMaximo);
        }
    }
}
