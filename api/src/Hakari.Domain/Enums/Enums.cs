namespace Hakari.Domain.Enums;

/// <summary>Gravidade de um alerta, no modelo de triggers (Aviso &lt; Alta &lt; Desastre).</summary>
public enum Severidade { Aviso = 1, Alta = 2, Desastre = 3 }

public enum EstadoAlerta { Ativo, Normalizado }

/// <summary>Condições que o nó (ou a própria API, no caso do isolamento) consegue detectar.</summary>
public enum TipoTrigger { LampadaSemResposta, SinalFraco, NoIsolado }

/// <summary>Ciclo de vida: Aberta → Em atendimento → Resolvida.</summary>
public enum StatusOcorrencia { Aberta, EmAtendimento, Resolvida }

public enum OrigemOcorrencia { Automatica, Cidadao }

public enum CategoriaProblema { LampadaApagada, LampadaPiscando, PosteDanificado, FiacaoExposta, SemComunicacao, Outro }

public enum PerfilUsuario { Cidadao, Tecnico }

/// <summary>Regras e textos ligados aos enums, para não espalhar "switch" pelo código.</summary>
public static class Textos
{
    public static Severidade Severidade(this TipoTrigger trigger) => trigger switch
    {
        TipoTrigger.SinalFraco => Enums.Severidade.Aviso,
        TipoTrigger.LampadaSemResposta => Enums.Severidade.Alta,
        TipoTrigger.NoIsolado => Enums.Severidade.Desastre,
        _ => throw new ArgumentOutOfRangeException(nameof(trigger))
    };

    public static string Descricao(this TipoTrigger trigger) => trigger switch
    {
        TipoTrigger.SinalFraco => "Sinal Wi-Fi fraco no nó",
        TipoTrigger.LampadaSemResposta => "Lâmpada comandada acesa, mas sem luz no sensor de referência",
        TipoTrigger.NoIsolado => "Nó sem comunicação há mais de 3 minutos",
        _ => trigger.ToString()
    };

    public static string Texto(this StatusOcorrencia status) => status switch
    {
        StatusOcorrencia.Aberta => "Aberta",
        StatusOcorrencia.EmAtendimento => "Em atendimento",
        StatusOcorrencia.Resolvida => "Resolvida",
        _ => status.ToString()
    };

    public static string Texto(this OrigemOcorrencia origem) =>
        origem == OrigemOcorrencia.Automatica ? "Automática" : "Cidadão";
}
