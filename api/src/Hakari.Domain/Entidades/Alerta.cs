using Hakari.Domain.Enums;

namespace Hakari.Domain.Entidades;

/// <summary>
/// Algo errado detectado em um poste. Só o próprio <see cref="Poste"/> cria e normaliza alertas
/// (construtor e Normalizar são internal), o que garante a regra de não duplicar.
/// </summary>
public class Alerta : Entidade
{
    private Alerta() { } // usado pelo EF Core

    internal Alerta(Poste poste, TipoTrigger trigger, string? detalhe, DateTime agora)
    {
        Poste = poste;
        Trigger = trigger;
        Severidade = trigger.Severidade();
        Estado = EstadoAlerta.Ativo;
        Detalhe = detalhe;
        AbertoEm = agora;
    }

    public int PosteId { get; private set; }
    public Poste Poste { get; private set; } = null!;
    public TipoTrigger Trigger { get; private set; }
    public Severidade Severidade { get; private set; }
    public EstadoAlerta Estado { get; private set; }
    public string? Detalhe { get; private set; }
    public DateTime AbertoEm { get; private set; }
    public DateTime? NormalizadoEm { get; private set; }

    /// <summary>Avisos só informam; Alta e Desastre abrem ocorrência.</summary>
    public bool AbreOcorrencia => Severidade != Severidade.Aviso;

    internal void Normalizar(DateTime agora)
    {
        Estado = EstadoAlerta.Normalizado;
        NormalizadoEm = agora;
    }
}
