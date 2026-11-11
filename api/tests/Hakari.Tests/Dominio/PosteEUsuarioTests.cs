using Hakari.Domain.Entidades;
using Hakari.Domain.Enums;
using Hakari.Domain.Excecoes;
using Hakari.Tests.Suporte;

namespace Hakari.Tests.Dominio;

/// <summary>Alertas, isolamento e status legível do poste (F2 e F4).</summary>
public class PosteTests
{
    private static readonly DateTime Agora = Fabrica.Inicio;

    [Fact]
    public void Mesmo_trigger_ativo_nao_gera_alerta_duplicado()
    {
        var poste = Fabrica.Poste();

        var primeiro = poste.AbrirAlerta(TipoTrigger.LampadaSemResposta, null, Agora);
        var segundo = poste.AbrirAlerta(TipoTrigger.LampadaSemResposta, null, Agora.AddSeconds(30));

        Assert.NotNull(primeiro);
        Assert.Null(segundo);
        Assert.Single(poste.Alertas);
    }

    [Fact]
    public void Severidade_vem_do_trigger()
    {
        var poste = Fabrica.Poste();

        Assert.Equal(Severidade.Aviso, poste.AbrirAlerta(TipoTrigger.SinalFraco, null, Agora)!.Severidade);
        Assert.Equal(Severidade.Alta, poste.AbrirAlerta(TipoTrigger.LampadaSemResposta, null, Agora)!.Severidade);
    }

    [Fact]
    public void Depois_de_normalizado_um_novo_alerta_pode_ser_aberto()
    {
        var poste = Fabrica.Poste();
        poste.AbrirAlerta(TipoTrigger.LampadaSemResposta, null, Agora);

        var normalizado = poste.NormalizarAlerta(TipoTrigger.LampadaSemResposta, Agora.AddMinutes(1));

        Assert.Equal(EstadoAlerta.Normalizado, normalizado!.Estado);
        Assert.NotNull(poste.AbrirAlerta(TipoTrigger.LampadaSemResposta, null, Agora.AddMinutes(2)));
    }

    [Fact]
    public void No_que_nunca_falou_nao_e_considerado_isolado()
    {
        var poste = Fabrica.Poste();

        Assert.False(poste.DeveSerIsolado(Agora.AddHours(1)));
        Assert.Equal("Aguardando instalação", poste.StatusLegivel());
    }

    [Fact]
    public void Tres_minutos_sem_mensagem_isolam_o_no_com_alerta_desastre()
    {
        var poste = Fabrica.Poste();
        poste.RegistrarComunicacao(Agora);

        Assert.Null(poste.MarcarIsolado(Agora.AddMinutes(2)));
        var alerta = poste.MarcarIsolado(Agora.AddMinutes(3));

        Assert.True(poste.Isolado);
        Assert.Equal(Severidade.Desastre, alerta!.Severidade);
        Assert.Equal("Sem comunicação", poste.StatusLegivel());
    }

    [Fact]
    public void Nova_mensagem_encerra_o_isolamento()
    {
        var poste = Fabrica.Poste();
        poste.RegistrarComunicacao(Agora);
        poste.MarcarIsolado(Agora.AddMinutes(4));

        var normalizado = poste.RegistrarComunicacao(Agora.AddMinutes(5), brilho: 30, rssi: -60);

        Assert.False(poste.Isolado);
        Assert.Equal(TipoTrigger.NoIsolado, normalizado!.Trigger);
        Assert.Equal(EstadoAlerta.Normalizado, normalizado.Estado);
        Assert.Equal("Funcionando", poste.StatusLegivel());
    }

    [Fact]
    public void Lampada_sem_resposta_aparece_como_apagado()
    {
        var poste = Fabrica.Poste();
        poste.RegistrarComunicacao(Agora);
        poste.AbrirAlerta(TipoTrigger.LampadaSemResposta, null, Agora);

        Assert.Equal("Apagado", poste.StatusLegivel());
    }
}

/// <summary>Regras de cadastro e privacidade do usuário (F3 e F8).</summary>
public class UsuarioTests
{
    [Theory]
    [InlineData("curta1")]
    [InlineData("semnumeros")]
    [InlineData("12345678")]
    public void Senha_fraca_e_recusada(string senha)
    {
        var erro = Assert.Throws<ValidacaoException>(() => PoliticaDeSenha.Validar(senha));
        Assert.Equal(PoliticaDeSenha.Mensagem, erro.Message);
    }

    [Fact]
    public void Senha_com_letras_e_numeros_e_aceita() => PoliticaDeSenha.Validar("senhaBoa123");

    [Fact]
    public void Email_e_normalizado_e_telefone_fica_so_com_digitos()
    {
        var cidadao = new Cidadao("Ana Souza", "  Ana@Exemplo.COM ", "(11) 98888-7777", "hash", Fabrica.Inicio);

        Assert.Equal("ana@exemplo.com", cidadao.Email);
        Assert.Equal("11988887777", cidadao.Telefone);
    }

    [Fact]
    public void Email_invalido_e_recusado() =>
        Assert.Throws<ValidacaoException>(() => new Cidadao("Ana", "ana.exemplo.com", "11988887777", "hash", Fabrica.Inicio));

    [Theory]
    [InlineData("Ana Paula Souza", "Ana S.")]
    [InlineData("Ana", "Ana")]
    public void Tecnico_ve_nome_abreviado(string nome, string esperado) =>
        Assert.Equal(esperado, Fabrica.Cidadao(nome).NomeAbreviado());

    [Fact]
    public void Cada_tipo_de_usuario_informa_o_proprio_perfil()
    {
        Usuario cidadao = Fabrica.Cidadao();
        Usuario tecnico = Fabrica.Tecnico();

        Assert.Equal(PerfilUsuario.Cidadao, cidadao.Perfil);
        Assert.Equal(PerfilUsuario.Tecnico, tecnico.Perfil);
    }
}
