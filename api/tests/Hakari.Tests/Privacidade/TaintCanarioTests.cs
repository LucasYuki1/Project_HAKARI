using System.Text.Encodings.Web;
using System.Text.Json;
using Hakari.Api.Aplicacao;
using Hakari.Api.Contratos;
using Hakari.Domain.Enums;
using Hakari.Domain.Privacidade;
using Hakari.Tests.Suporte;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Tests.Privacidade;

/// <summary>
/// Taint analysis dinâmico com canários. O teste planta dados pessoais fictícios e únicos nas duas
/// entradas de texto livre (o reporte da cidadã e a solução do técnico) e procura cada um em todas
/// as saídas que outra pessoa lê: as respostas da API e o que fica no banco.
///
/// Ele roda duas vezes. Com o motor de PII de verdade, nenhum canário pode aparecer. Com um motor
/// sem regras (o "controle", que imita o Hakari antes do filtro), todos os canários precisam
/// aparecer: é a prova de que o teste enxerga um vazamento quando ele existe.
/// Com a variável HAKARI_RELATORIO_TAINT definida, grava o resultado em JSON para o painel.
/// </summary>
public class TaintCanarioTests
{
    private static readonly (string Dado, string Valor)[] Canarios =
    [
        ("CPF", "529.982.247-25"),
        ("telefone", "(11) 97654-3210"),
        ("e-mail", "canario.hakari@exemplo.com"),
        ("endereço residencial", "Rua dos Canários, 77"),
        ("dado de saúde", "diabética"),
        ("cartão do SUS", "198 7654 3210 0003")
    ];

    private const string Descricao =
        "Poste apagado e a calçada some no escuro. Sou diabética, moro na Rua dos Canários, 77. " +
        "Meu CPF é 529.982.247-25 e o cartão do SUS 198 7654 3210 0003. " +
        "Liguem no (11) 97654-3210 ou escrevam para canario.hakari@exemplo.com.";

    private const string Solucao = "Reator trocado. Avisei a moradora pelo (11) 97654-3210.";

    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public sealed record Vazamento(string Saida, string QuemLe, string Dado);

    public sealed record Execucao(string Modo, int SaidasVerificadas, IReadOnlyList<string> Saidas,
                                  IReadOnlyList<Vazamento> Vazamentos, string MensagemAoCidadao);

    public static async Task<Execucao> ExecutarAsync(MotorPii motor, string modo)
    {
        await using var db = Fabrica.Banco();
        var poste = Fabrica.Poste();
        var autora = Fabrica.Cidadao("Ana Paula Souza", "ana@exemplo.com");
        var vizinho = Fabrica.Cidadao("Bruno Lima", "bruno@exemplo.com");
        var tecnico = Fabrica.Tecnico();
        db.AddRange(poste, autora, vizinho, tecnico);
        await db.SaveChangesAsync();

        var servico = new OcorrenciaService(db, new RelogioFixo(Fabrica.Inicio), motor);
        var saidas = new List<(string Nome, string QuemLe, string Conteudo)>();

        // Entrada 1: o reporte, com todos os canários na descrição.
        var reporte = await servico.ReportarAsync(poste.Id, autora.Id,
            new ReporteRequest(CategoriaProblema.LampadaApagada, Descricao), default);
        saidas.Add(("resposta de POST /api/postes/{id}/reportes", "autora", Serializar(reporte)));
        saidas.Add(("GET /api/ocorrencias/{id}", "outro cidadão",
            Serializar(await servico.DetalharAsync(reporte.OcorrenciaId, vizinho.Id, PerfilUsuario.Cidadao, default))));
        saidas.Add(("GET /api/manutencao/fila", "técnico", Serializar(await servico.FilaAsync(default))));

        // Entrada 2: a solução do técnico, com o telefone da moradora.
        await servico.AssumirAsync(reporte.OcorrenciaId, tecnico.Id, default);
        var resolvida = await servico.ResolverAsync(reporte.OcorrenciaId, tecnico.Id, Solucao, default);
        saidas.Add(("resposta de POST /api/ocorrencias/{id}/resolver", "técnico", Serializar(resolvida)));
        saidas.Add(("GET /api/ocorrencias/{id} (resolvida)", "outro cidadão",
            Serializar(await servico.DetalharAsync(reporte.OcorrenciaId, vizinho.Id, PerfilUsuario.Cidadao, default))));
        saidas.Add(("GET /api/ocorrencias/minhas", "autora", Serializar(await servico.MinhasAsync(autora.Id, default))));

        // O que fica guardado: o banco é lido por quem opera o sistema e vai para os backups.
        db.ChangeTracker.Clear();
        var guardada = await db.Ocorrencias.Include(o => o.Historico).SingleAsync();
        saidas.Add(("banco: ocorrencias.descricao", "quem acessa o banco", guardada.Descricao));
        saidas.Add(("banco: ocorrencias.solucao", "quem acessa o banco", guardada.Solucao ?? ""));
        saidas.Add(("banco: historico_ocorrencias", "quem acessa o banco",
            string.Join(" | ", guardada.Historico.Select(h => h.Descricao))));

        var vazamentos = (from s in saidas
                          from c in Canarios
                          where Contem(s.Conteudo, c.Valor)
                          select new Vazamento(s.Nome, s.QuemLe, c.Dado)).ToList();

        return new Execucao(modo, saidas.Count, saidas.Select(s => $"{s.Nome} ({s.QuemLe})").ToList(),
                            vazamentos, reporte.Mensagem);
    }

    [Fact]
    public async Task Com_o_motor_nenhum_canario_chega_a_uma_saida()
    {
        var protegido = await ExecutarAsync(MotorPii.Padrao, "protegido");

        Assert.Empty(protegido.Vazamentos);
        Assert.Contains("escondemos da descrição", protegido.MensagemAoCidadao);
    }

    [Fact]
    public async Task Controle_sem_regras_prova_que_o_teste_enxerga_o_vazamento()
    {
        var controle = await ExecutarAsync(new MotorPii([]), "controle");

        Assert.Equal(Canarios.Select(c => c.Dado).ToHashSet(), controle.Vazamentos.Select(v => v.Dado).ToHashSet());
    }

    [Fact]
    public async Task Grava_o_relatorio_dos_canarios_quando_pedido()
    {
        var destino = Environment.GetEnvironmentVariable("HAKARI_RELATORIO_TAINT");
        if (string.IsNullOrWhiteSpace(destino)) return;

        var relatorio = new
        {
            Canarios = Canarios.Select(c => c.Dado).ToList(),
            Execucoes = new[]
            {
                await ExecutarAsync(new MotorPii([]), "controle"),
                await ExecutarAsync(MotorPii.Padrao, "protegido")
            }
        };
        await File.WriteAllTextAsync(destino, JsonSerializer.Serialize(relatorio, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    private static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Json);

    /// <summary>Acha o canário na forma original ou só com os dígitos (caso alguém normalize o número).</summary>
    private static bool Contem(string conteudo, string canario)
    {
        if (conteudo.Contains(canario, StringComparison.OrdinalIgnoreCase)) return true;
        var digitos = new string(canario.Where(char.IsAsciiDigit).ToArray());
        return digitos.Length >= 8 && new string(conteudo.Where(char.IsAsciiDigit).ToArray()).Contains(digitos);
    }
}
