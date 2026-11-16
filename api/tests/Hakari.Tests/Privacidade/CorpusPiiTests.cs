using System.Text.Encodings.Web;
using System.Text.Json;
using Hakari.Domain.Privacidade;
using Xunit.Abstractions;

namespace Hakari.Tests.Privacidade;

/// <summary>
/// Mede o motor de PII contra corpus-pii.json: textos como os de um reporte real, cada um com as
/// regras que precisam disparar (e textos que não podem disparar nada). Precisão é "quando o motor
/// esconde algo, era mesmo dado pessoal"; cobertura é "dos dados pessoais, quantos ele escondeu".
/// Com a variável HAKARI_RELATORIO_MOTOR definida, grava as métricas em JSON para o painel.
/// </summary>
public class CorpusPiiTests(ITestOutputHelper saida)
{
    private const double MinimoAceitavel = 0.95;

    private sealed record Caso(string Texto, List<string> Esperado, string? Nota);
    private sealed record Corpus(List<Caso> Casos);
    public sealed record MetricaRegra(string Id, string Rotulo, string Categoria, int Acertos, int FalsosPositivos, int FalsosNegativos);
    public sealed record Erro(string TextoMascarado, IReadOnlyList<string> Esperado, IReadOnlyList<string> Encontrado);
    public sealed record Resultado(int Casos, int ComDadoPessoal, int SemDadoPessoal, int Acertos, int FalsosPositivos,
                                   int FalsosNegativos, double Precisao, double Cobertura, double F1,
                                   IReadOnlyList<MetricaRegra> Regras, IReadOnlyList<Erro> Erros);

    private static readonly Lazy<List<Caso>> Casos = new(() =>
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "Privacidade", "corpus-pii.json");
        return JsonSerializer.Deserialize<Corpus>(File.ReadAllText(caminho),
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Casos;
    });

    public static Resultado Avaliar(MotorPii motor)
    {
        var porRegra = motor.Regras.ToDictionary(r => r.Id, _ => new int[3]); // acertos, fp, fn
        var erros = new List<Erro>();

        foreach (var caso in Casos.Value)
        {
            var esperado = caso.Esperado.ToHashSet();
            var encontrado = motor.Analisar(caso.Texto).Select(a => a.Regra).ToHashSet();

            foreach (var id in esperado.Union(encontrado))
            {
                var m = porRegra[id];
                if (esperado.Contains(id) && encontrado.Contains(id)) m[0]++;
                else if (encontrado.Contains(id)) m[1]++;
                else m[2]++;
            }

            if (!esperado.SetEquals(encontrado))
                erros.Add(new Erro(motor.Filtrar(caso.Texto).Texto, esperado.Order().ToList(), encontrado.Order().ToList()));
        }

        int acertos = porRegra.Values.Sum(m => m[0]), fp = porRegra.Values.Sum(m => m[1]), fn = porRegra.Values.Sum(m => m[2]);
        double precisao = acertos + fp == 0 ? 1 : (double)acertos / (acertos + fp);
        double cobertura = acertos + fn == 0 ? 1 : (double)acertos / (acertos + fn);
        double f1 = precisao + cobertura == 0 ? 0 : 2 * precisao * cobertura / (precisao + cobertura);

        var regras = motor.Regras
            .Select(r => new MetricaRegra(r.Id, r.Rotulo, r.Categoria.ToString(), porRegra[r.Id][0], porRegra[r.Id][1], porRegra[r.Id][2]))
            .ToList();
        var comDado = Casos.Value.Count(c => c.Esperado.Count > 0);

        return new Resultado(Casos.Value.Count, comDado, Casos.Value.Count - comDado, acertos, fp, fn,
                             Math.Round(precisao, 4), Math.Round(cobertura, 4), Math.Round(f1, 4), regras, erros);
    }

    [Fact]
    public void Motor_atinge_precisao_e_cobertura_minimas_no_corpus()
    {
        var r = Avaliar(MotorPii.Padrao);

        saida.WriteLine($"{r.Casos} casos ({r.ComDadoPessoal} com dado pessoal): precisão {r.Precisao:P1}, cobertura {r.Cobertura:P1}, F1 {r.F1:P1}");
        foreach (var e in r.Erros)
            saida.WriteLine($"  divergência: esperado [{string.Join(", ", e.Esperado)}], encontrado [{string.Join(", ", e.Encontrado)}] em \"{e.TextoMascarado}\"");

        var destino = Environment.GetEnvironmentVariable("HAKARI_RELATORIO_MOTOR");
        if (!string.IsNullOrWhiteSpace(destino))
            File.WriteAllText(destino, JsonSerializer.Serialize(r, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        Assert.True(r.Precisao >= MinimoAceitavel, $"Precisão {r.Precisao:P1} abaixo de {MinimoAceitavel:P0}");
        Assert.True(r.Cobertura >= MinimoAceitavel, $"Cobertura {r.Cobertura:P1} abaixo de {MinimoAceitavel:P0}");
    }

    [Fact]
    public void Toda_regra_tem_pelo_menos_um_caso_positivo_no_corpus()
    {
        var cobertas = Casos.Value.SelectMany(c => c.Esperado).ToHashSet();
        var semCaso = MotorPii.Padrao.Regras.Select(r => r.Id).Where(id => !cobertas.Contains(id)).ToList();

        Assert.True(semCaso.Count == 0, $"Regras sem caso no corpus: {string.Join(", ", semCaso)}");
    }

    [Fact]
    public void Texto_filtrado_nunca_contem_o_trecho_original()
    {
        foreach (var caso in Casos.Value)
        {
            var achados = MotorPii.Padrao.Analisar(caso.Texto);
            var filtrado = MotorPii.Padrao.Filtrar(caso.Texto).Texto;
            foreach (var a in achados)
                Assert.DoesNotContain(caso.Texto.Substring(a.Inicio, a.Tamanho), filtrado);
        }
    }
}
