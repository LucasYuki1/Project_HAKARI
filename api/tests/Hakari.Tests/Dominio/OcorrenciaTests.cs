using Hakari.Domain.Entidades;
using Hakari.Domain.Enums;
using Hakari.Domain.Excecoes;
using Hakari.Tests.Suporte;

namespace Hakari.Tests.Dominio;

/// <summary>Ciclo de vida da ocorrência (F5 e F7).</summary>
public class OcorrenciaTests
{
    private static readonly DateTime Agora = Fabrica.Inicio;

    private static Ocorrencia NovaDoCidadao(Cidadao? autor = null) =>
        Ocorrencia.AbrirPorCidadao(Fabrica.Poste(), autor ?? Fabrica.Cidadao(), CategoriaProblema.LampadaPiscando,
                                   "Lâmpada piscando", Agora);

    [Fact]
    public void Reporte_do_cidadao_abre_ocorrencia_aberta_com_historico()
    {
        var ocorrencia = NovaDoCidadao();

        Assert.Equal(StatusOcorrencia.Aberta, ocorrencia.Status);
        Assert.Equal(OrigemOcorrencia.Cidadao, ocorrencia.Origem);
        Assert.Single(ocorrencia.Historico);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Reporte_sem_descricao_e_recusado(string? descricao)
    {
        var erro = Assert.Throws<ValidacaoException>(() =>
            Ocorrencia.AbrirPorCidadao(Fabrica.Poste(), Fabrica.Cidadao(), CategoriaProblema.Outro, descricao!, Agora));

        Assert.Equal("Descreva o problema", erro.Message);
    }

    [Fact]
    public void Resolver_sem_assumir_e_recusado_e_status_nao_muda()
    {
        var ocorrencia = NovaDoCidadao();

        var erro = Assert.Throws<RegraDeNegocioException>(() =>
            ocorrencia.Resolver(Fabrica.Tecnico(), "Lâmpada trocada", Agora));

        Assert.Equal("Assuma a ocorrência antes de resolvê-la", erro.Message);
        Assert.Equal(StatusOcorrencia.Aberta, ocorrencia.Status);
    }

    [Fact]
    public void Assumir_e_resolver_percorre_o_ciclo_completo()
    {
        var ocorrencia = NovaDoCidadao();
        var tecnico = Fabrica.Tecnico();

        ocorrencia.Assumir(tecnico, Agora.AddMinutes(5));
        Assert.Equal(StatusOcorrencia.EmAtendimento, ocorrencia.Status);
        Assert.Same(tecnico, ocorrencia.Responsavel);

        ocorrencia.Resolver(tecnico, "Reator substituído", Agora.AddMinutes(40));
        Assert.Equal(StatusOcorrencia.Resolvida, ocorrencia.Status);
        Assert.Equal(Agora.AddMinutes(40), ocorrencia.ResolvidaEm);
        Assert.Equal(3, ocorrencia.Historico.Count);
        Assert.Equal("Resolvida: Reator substituído", ocorrencia.Historico.Last().Descricao);
    }

    [Fact]
    public void Nao_e_possivel_assumir_duas_vezes()
    {
        var ocorrencia = NovaDoCidadao();
        ocorrencia.Assumir(Fabrica.Tecnico(), Agora);

        Assert.Throws<RegraDeNegocioException>(() => ocorrencia.Assumir(Fabrica.Tecnico("Outro Técnico"), Agora));
    }

    [Fact]
    public void Apenas_o_responsavel_resolve()
    {
        var ocorrencia = NovaDoCidadao();
        ocorrencia.Assumir(Fabrica.Tecnico("Rafael Mendes"), Agora);

        Assert.Throws<RegraDeNegocioException>(() =>
            ocorrencia.Resolver(Fabrica.Tecnico("Outro Técnico"), "feito", Agora));
    }

    [Fact]
    public void Resolver_exige_descricao_do_que_foi_feito()
    {
        var ocorrencia = NovaDoCidadao();
        var tecnico = Fabrica.Tecnico();
        ocorrencia.Assumir(tecnico, Agora);

        Assert.Throws<ValidacaoException>(() => ocorrencia.Resolver(tecnico, " ", Agora));
    }

    [Fact]
    public void Outro_cidadao_pode_apoiar_uma_vez()
    {
        var ocorrencia = NovaDoCidadao();
        var bruno = Fabrica.Cidadao("Bruno Lima", "bruno@exemplo.com");

        ocorrencia.Apoiar(bruno, Agora);

        Assert.Single(ocorrencia.Apoios);
        Assert.True(ocorrencia.EnvolveCidadao(bruno));
        var erro = Assert.Throws<ConflitoException>(() => ocorrencia.Apoiar(bruno, Agora));
        Assert.Equal("Você já apoiou esta ocorrência", erro.Message);
    }

    [Fact]
    public void Autor_nao_apoia_a_propria_ocorrencia()
    {
        var ana = Fabrica.Cidadao();
        var ocorrencia = NovaDoCidadao(ana);

        Assert.Throws<ConflitoException>(() => ocorrencia.Apoiar(ana, Agora));
    }

    [Fact]
    public void Ocorrencia_resolvida_nao_recebe_apoio()
    {
        var ocorrencia = NovaDoCidadao();
        var tecnico = Fabrica.Tecnico();
        ocorrencia.Assumir(tecnico, Agora);
        ocorrencia.Resolver(tecnico, "feito", Agora);

        Assert.Throws<RegraDeNegocioException>(() => ocorrencia.Apoiar(Fabrica.Cidadao("Bruno Lima"), Agora));
    }

    [Fact]
    public void Alerta_de_aviso_nao_abre_ocorrencia_automatica()
    {
        var poste = Fabrica.Poste();
        var aviso = poste.AbrirAlerta(TipoTrigger.SinalFraco, null, Agora)!;

        Assert.False(aviso.AbreOcorrencia);
        Assert.Throws<RegraDeNegocioException>(() => Ocorrencia.AbrirAutomatica(poste, aviso, Agora));
    }
}
