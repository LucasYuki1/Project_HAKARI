using System.Diagnostics;
using Hakari.Domain.Privacidade;

namespace Hakari.Tests.Privacidade;

/// <summary>Validadores, mascaramento e configuração do motor de PII.</summary>
public class MotorPiiTests
{
    private static readonly MotorPii Motor = MotorPii.Padrao;

    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("529.982.247-24", false)] // dígito verificador errado
    [InlineData("111.111.111-11", false)] // todos os dígitos iguais
    [InlineData("12345", false)]
    public void Cpf_so_vale_com_digito_verificador_correto(string cpf, bool valido) =>
        Assert.Equal(valido, Validadores.Cpf(cpf));

    [Theory]
    [InlineData("(11) 98765-4321", true)]
    [InlineData("11 3456-7890", true)]
    [InlineData("+55 21 99876-5432", true)]
    [InlineData("(20) 98765-4321", false)] // DDD 20 não existe
    [InlineData("(11) 88765-4321", false)] // celular de 11 dígitos sem o 9
    public void Telefone_exige_ddd_valido_e_formato_brasileiro(string telefone, bool valido) =>
        Assert.Equal(valido, Validadores.Telefone(telefone));

    [Theory]
    [InlineData("198 7654 3210 0003", true)]
    [InlineData("198 7654 3210 0004", false)]
    public void Cartao_do_sus_confere_a_soma_ponderada(string cns, bool valido) =>
        Assert.Equal(valido, Validadores.Cns(cns));

    [Theory]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111 1111 1111 1112", false)]
    public void Cartao_de_pagamento_confere_o_luhn(string cartao, bool valido) =>
        Assert.Equal(valido, Validadores.Luhn(cartao));

    [Theory]
    [InlineData("Meu CPF é 529.982.247-25", "Meu CPF é [CPF oculto]")]
    [InlineData("Sou diabética e moro sozinha", "Sou [dado de saúde oculto] e moro sozinha")]
    [InlineData("minha senha é Hakari@2026", "minha senha é [senha oculta]")]
    [InlineData("Liga no (11) 98765-4321 depois das 18h", "Liga no [telefone oculto] depois das 18h")]
    [InlineData("Moro na Rua das Acácias, 123 e está escuro", "Moro na [endereço residencial oculto] e está escuro")]
    [InlineData("O poste fica num ponto cego", "O poste fica num ponto cego")]
    [InlineData("Lâmpada apagada na Av. Paulista, 1578", "Lâmpada apagada na Av. Paulista, 1578")]
    public void Filtrar_troca_so_o_dado_pessoal_pelo_marcador(string entrada, string esperado) =>
        Assert.Equal(esperado, Motor.Filtrar(entrada).Texto);

    [Fact]
    public void Filtrar_conta_o_que_escondeu_sem_repetir()
    {
        var resultado = Motor.Filtrar("CPF 529.982.247-25, telefone (11) 98765-4321 ou (11) 3456-7890");

        Assert.True(resultado.OcultouDados);
        Assert.Equal(["CPF", "telefone"], resultado.DadosOcultados);
    }

    [Fact]
    public void Achado_diz_o_tipo_do_dado_mas_nunca_carrega_o_valor()
    {
        var achado = Assert.Single(Motor.Analisar("CPF 529.982.247-25"));

        Assert.Equal("cpf", achado.Regra);
        Assert.Equal(CategoriaPii.Identificacao, achado.Categoria);
        Assert.DoesNotContain("529", achado.ToString()); // pode ir para log sem levar o CPF
    }

    [Fact]
    public void Texto_sem_dado_pessoal_passa_igual()
    {
        var resultado = Motor.Filtrar("  Lâmpada piscando desde ontem à noite  ");

        Assert.False(resultado.OcultouDados);
        Assert.Equal("Lâmpada piscando desde ontem à noite", resultado.Texto);
    }

    [Fact]
    public void Regras_carregam_do_arquivo_embutido_com_ids_unicos()
    {
        Assert.True(Motor.Regras.Count >= 16);
        Assert.Equal(Motor.Regras.Count, Motor.Regras.Select(r => r.Id).Distinct().Count());
        Assert.Contains(Motor.Regras, r => r.Categoria == CategoriaPii.Saude && r.Sensivel);
        Assert.Contains(Motor.Regras, r => r.Categoria == CategoriaPii.Credencial);
    }

    [Fact]
    public void Regra_nova_entra_so_com_configuracao()
    {
        // Exemplo de regra nova: matrícula de servidor municipal, sem mexer em código.
        const string json = """
            {"versao": 1, "regras": [{"id": "matricula", "rotulo": "matrícula", "categoria": "Identificacao",
              "padrao": "\\bMAT-\\d{6}\\b", "mascara": "[matrícula oculta]"}]}
            """;

        var motor = MotorPii.DeJson(json);

        Assert.Equal("Servidor [matrícula oculta] atendeu", motor.Filtrar("Servidor MAT-123456 atendeu").Texto);
    }

    [Fact]
    public void Regra_com_validador_desconhecido_falha_ao_carregar()
    {
        const string json = """
            {"versao": 1, "regras": [{"id": "x", "rotulo": "x", "categoria": "Contato", "padrao": "x",
              "validador": "inexistente", "mascara": "[x]"}]}
            """;

        Assert.Throws<InvalidOperationException>(() => MotorPii.DeJson(json));
    }

    [Fact]
    public void Texto_enorme_e_hostil_nao_trava_o_motor()
    {
        var hostil = string.Concat(Enumerable.Repeat("1.2-3 999 ", 12_000)) + "@@@" + new string('a', 50_000);

        var tempo = Stopwatch.StartNew();
        var resultado = Motor.Filtrar(hostil);

        Assert.True(tempo.Elapsed < TimeSpan.FromSeconds(5), $"O motor levou {tempo.Elapsed}");
        Assert.False(string.IsNullOrEmpty(resultado.Texto));
    }
}
