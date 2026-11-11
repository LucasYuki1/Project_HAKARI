using Hakari.Api.Aplicacao;
using Hakari.Api.Contratos;
using Hakari.Domain.Entidades;
using Hakari.Domain.Enums;
using Hakari.Domain.Excecoes;
using Hakari.Domain.Interfaces;
using Hakari.Tests.Suporte;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Tests.Servicos;

/// <summary>
/// Testes dos casos de uso com banco em memória: mostram a injeção de dependência na prática
/// (o serviço recebe um DbContext e um relógio de teste no lugar dos reais).
/// </summary>
public class MonitorServiceTests
{
    private static AlertaMensagem Alerta(string trigger, string estado) =>
        new("HKR-001", trigger, "", estado, estado == "Ativo", "teste");

    private static TelemetriaMensagem Telemetria() =>
        new("HKR-001", 3938, true, 1001, 50, true, 100, -62, 120);

    [Fact]
    public async Task Alerta_de_lampada_abre_ocorrencia_automatica_uma_unica_vez()
    {
        await using var db = Fabrica.Banco();
        db.Postes.Add(Fabrica.Poste());
        await db.SaveChangesAsync();
        var monitor = new MonitorService(db, new RelogioFixo(Fabrica.Inicio), Fabrica.Log<MonitorService>());

        await monitor.ProcessarAlertaAsync("HKR-001", Alerta("lampada_sem_resposta", "Ativo"), default);
        await monitor.ProcessarAlertaAsync("HKR-001", Alerta("lampada_sem_resposta", "Ativo"), default);

        var ocorrencia = await db.Ocorrencias.SingleAsync();
        Assert.Equal(OrigemOcorrencia.Automatica, ocorrencia.Origem);
        Assert.Equal(Severidade.Alta, ocorrencia.Severidade);
        Assert.Equal(1, await db.Alertas.CountAsync());
    }

    [Fact]
    public async Task Aviso_de_sinal_fraco_nao_abre_ocorrencia()
    {
        await using var db = Fabrica.Banco();
        db.Postes.Add(Fabrica.Poste());
        await db.SaveChangesAsync();
        var monitor = new MonitorService(db, new RelogioFixo(Fabrica.Inicio), Fabrica.Log<MonitorService>());

        await monitor.ProcessarAlertaAsync("HKR-001", Alerta("sinal_fraco", "Ativo"), default);

        Assert.Equal(1, await db.Alertas.CountAsync());
        Assert.False(await db.Ocorrencias.AnyAsync());
    }

    [Fact]
    public async Task Normalizacao_entra_no_historico_e_ocorrencia_continua_aberta()
    {
        await using var db = Fabrica.Banco();
        db.Postes.Add(Fabrica.Poste());
        await db.SaveChangesAsync();
        var relogio = new RelogioFixo(Fabrica.Inicio);
        var monitor = new MonitorService(db, relogio, Fabrica.Log<MonitorService>());

        await monitor.ProcessarAlertaAsync("HKR-001", Alerta("lampada_sem_resposta", "Ativo"), default);
        relogio.Avancar(TimeSpan.FromMinutes(2));
        await monitor.ProcessarAlertaAsync("HKR-001", Alerta("lampada_sem_resposta", "Normalizado"), default);

        var ocorrencia = await db.Ocorrencias.Include(o => o.Historico).SingleAsync();
        Assert.Equal(StatusOcorrencia.Aberta, ocorrencia.Status);
        Assert.Contains(ocorrencia.Historico, h => h.Descricao.StartsWith("Alerta normalizado"));
    }

    [Fact]
    public async Task No_sem_mensagens_por_tres_minutos_vira_desastre()
    {
        await using var db = Fabrica.Banco();
        db.Postes.Add(Fabrica.Poste());
        await db.SaveChangesAsync();
        var relogio = new RelogioFixo(Fabrica.Inicio);
        var monitor = new MonitorService(db, relogio, Fabrica.Log<MonitorService>());

        await monitor.ProcessarTelemetriaAsync("HKR-001", Telemetria(), default);
        relogio.Avancar(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1));
        await monitor.VerificarIsolamentoAsync(default);

        var poste = await db.Postes.SingleAsync();
        var ocorrencia = await db.Ocorrencias.SingleAsync();
        Assert.True(poste.Isolado);
        Assert.Equal(Severidade.Desastre, ocorrencia.Severidade);
        Assert.Equal(CategoriaProblema.SemComunicacao, ocorrencia.Categoria);
        Assert.Equal(1, await db.Leituras.CountAsync());
    }

    [Fact]
    public async Task Mensagem_de_no_nao_cadastrado_e_ignorada()
    {
        await using var db = Fabrica.Banco();
        var monitor = new MonitorService(db, new RelogioFixo(Fabrica.Inicio), Fabrica.Log<MonitorService>());

        await monitor.ProcessarAlertaAsync("HKR-999", Alerta("lampada_sem_resposta", "Ativo"), default);

        Assert.False(await db.Alertas.AnyAsync());
    }
}

public class OcorrenciaServiceTests
{
    private static async Task<(Infrastructure.Dados.HakariDbContext db, Poste poste, Cidadao ana, Cidadao bruno)> CenarioAsync()
    {
        var db = Fabrica.Banco();
        var poste = Fabrica.Poste();
        var ana = Fabrica.Cidadao();
        var bruno = Fabrica.Cidadao("Bruno Lima", "bruno@exemplo.com");
        db.AddRange(poste, ana, bruno);
        await db.SaveChangesAsync();
        return (db, poste, ana, bruno);
    }

    [Fact]
    public async Task Segundo_reporte_no_mesmo_poste_vira_apoio()
    {
        var (db, poste, ana, bruno) = await CenarioAsync();
        await using var _ = db;
        var servico = new OcorrenciaService(db, new RelogioFixo(Fabrica.Inicio));

        var primeiro = await servico.ReportarAsync(poste.Id, ana.Id, new ReporteRequest(CategoriaProblema.LampadaApagada, "Apagada"), default);
        var segundo = await servico.ReportarAsync(poste.Id, bruno.Id, new ReporteRequest(CategoriaProblema.LampadaApagada, "Também vi"), default);

        Assert.False(primeiro.ApoioAdicionado);
        Assert.True(segundo.ApoioAdicionado);
        Assert.Equal(primeiro.OcorrenciaId, segundo.OcorrenciaId);
        Assert.Equal("Este problema já está registrado. Seu apoio foi adicionado.", segundo.Mensagem);
        Assert.Equal(1, await db.Ocorrencias.CountAsync());
    }

    [Fact]
    public async Task Reporte_para_poste_inexistente_da_nao_encontrado()
    {
        var (db, _, ana, _) = await CenarioAsync();
        await using var _db = db;
        var servico = new OcorrenciaService(db, new RelogioFixo(Fabrica.Inicio));

        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            servico.ReportarAsync(999, ana.Id, new ReporteRequest(CategoriaProblema.Outro, "x"), default));
        Assert.False(await db.Ocorrencias.AnyAsync());
    }

    [Fact]
    public async Task Fila_ordena_por_severidade_e_depois_por_data()
    {
        var (db, poste, ana, _) = await CenarioAsync();
        await using var _db = db;
        var outroPoste = Fabrica.Poste("HKR-002");
        db.Postes.Add(outroPoste);
        await db.SaveChangesAsync();

        var relogio = new RelogioFixo(Fabrica.Inicio);
        var servico = new OcorrenciaService(db, relogio);
        var monitor = new MonitorService(db, relogio, Fabrica.Log<MonitorService>());

        // Alta aberta primeiro, Desastre depois: o Desastre precisa vir na frente.
        await servico.ReportarAsync(poste.Id, ana.Id, new ReporteRequest(CategoriaProblema.LampadaApagada, "Apagada"), default);
        await monitor.ProcessarTelemetriaAsync("HKR-002", new("HKR-002", 0, false, 0, -1, false, 0, -60, 1), default);
        relogio.Avancar(TimeSpan.FromMinutes(4));
        await monitor.VerificarIsolamentoAsync(default);

        var fila = await servico.FilaAsync(default);

        Assert.Equal([Severidade.Desastre, Severidade.Alta], fila.Select(o => o.Severidade));
    }

    [Fact]
    public async Task Outro_cidadao_nao_ve_quem_abriu_e_tecnico_ve_nome_abreviado()
    {
        var (db, poste, ana, bruno) = await CenarioAsync();
        await using var _db = db;
        var tecnico = Fabrica.Tecnico();
        db.Usuarios.Add(tecnico);
        await db.SaveChangesAsync();
        var servico = new OcorrenciaService(db, new RelogioFixo(Fabrica.Inicio));
        var criada = await servico.ReportarAsync(poste.Id, ana.Id, new ReporteRequest(CategoriaProblema.LampadaApagada, "Apagada"), default);

        var vistaPeloBruno = await servico.DetalharAsync(criada.OcorrenciaId, bruno.Id, PerfilUsuario.Cidadao, default);
        var vistaPelaAna = await servico.DetalharAsync(criada.OcorrenciaId, ana.Id, PerfilUsuario.Cidadao, default);
        var vistaPeloTecnico = await servico.DetalharAsync(criada.OcorrenciaId, tecnico.Id, PerfilUsuario.Tecnico, default);

        Assert.Null(vistaPeloBruno.Autor);
        Assert.Equal("Você", vistaPelaAna.Autor);
        Assert.Equal("Ana S.", vistaPeloTecnico.Autor);
    }
}

public class AuthServiceTests
{
    /// <summary>Hasher falso: o teste não precisa pagar o custo do BCrypt.</summary>
    private sealed class HasherFalso : IHasherDeSenha
    {
        public string Gerar(string senha) => "hash:" + senha;
        public bool Verificar(string senha, string hash) => hash == "hash:" + senha;
    }

    private sealed class TokenFalso : ITokenService
    {
        public SessaoResponse Gerar(Usuario u) => new("token", Fabrica.Inicio, new UsuarioResumo(u.Id, u.Nome, u.Perfil));
    }

    private static AuthService Servico(Infrastructure.Dados.HakariDbContext db) =>
        new(db, new HasherFalso(), new TokenFalso(), new RelogioFixo(Fabrica.Inicio));

    [Fact]
    public async Task Email_ja_cadastrado_e_recusado()
    {
        await using var db = Fabrica.Banco();
        var auth = Servico(db);
        var pedido = new CadastroRequest("Ana Souza", "ana@exemplo.com", "11988887777", "senhaBoa123", true);
        await auth.CadastrarAsync(pedido, default);

        var erro = await Assert.ThrowsAsync<ConflitoException>(() =>
            auth.CadastrarAsync(pedido with { Email = "ANA@exemplo.com" }, default));

        Assert.Equal("Este e-mail já está em uso", erro.Message);
        Assert.Equal(1, await db.Usuarios.CountAsync());
    }

    [Fact]
    public async Task Cadastro_sem_aceitar_termo_e_recusado()
    {
        await using var db = Fabrica.Banco();

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db).CadastrarAsync(
            new CadastroRequest("Ana Souza", "ana@exemplo.com", "11988887777", "senhaBoa123", false), default));
    }

    [Fact]
    public async Task Senha_errada_da_mensagem_generica()
    {
        await using var db = Fabrica.Banco();
        var auth = Servico(db);
        await auth.CadastrarAsync(new CadastroRequest("Ana Souza", "ana@exemplo.com", "11988887777", "senhaBoa123", true), default);

        var erro = await Assert.ThrowsAsync<NaoAutorizadoException>(() =>
            auth.EntrarAsync(new LoginRequest("ana@exemplo.com", "outraSenha1"), default));

        Assert.Equal("E-mail ou senha incorretos", erro.Message);
    }
}
