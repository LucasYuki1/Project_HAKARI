using Hakari.Domain.Enums;

namespace Hakari.Domain.Entidades;

/// <summary>
/// Poste com um nó Hakari (ESP32). Guarda o último estado conhecido e os alertas.
/// É ele quem decide se um alerta é novo ou duplicado e quando está isolado.
/// </summary>
public class Poste : Entidade
{
    /// <summary>Tempo sem nenhuma mensagem até o nó ser considerado isolado (F2).</summary>
    public static readonly TimeSpan LimiteSemComunicacao = TimeSpan.FromMinutes(3);

    private readonly List<Alerta> _alertas = [];

    private Poste() { } // usado pelo EF Core

    public Poste(string codigo, string endereco, string bairro, double latitude, double longitude)
    {
        Codigo = codigo;
        Endereco = endereco;
        Bairro = bairro;
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Identificação do nó, a mesma do firmware e dos tópicos MQTT (ex.: HKR-001).</summary>
    public string Codigo { get; private set; } = "";
    public string Endereco { get; private set; } = "";
    public string Bairro { get; private set; } = "";
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }

    public DateTime? UltimaComunicacao { get; private set; }
    public bool Isolado { get; private set; }
    public int? UltimoBrilho { get; private set; }
    public int? UltimoRssi { get; private set; }

    public IReadOnlyCollection<Alerta> Alertas => _alertas;

    public Alerta? AlertaAtivo(TipoTrigger trigger) =>
        _alertas.FirstOrDefault(a => a.Trigger == trigger && a.Estado == EstadoAlerta.Ativo);

    /// <summary>
    /// Abre um alerta, a não ser que já exista um ativo para o mesmo trigger
    /// (cenário "Sem alertas duplicados"). Devolve null quando nada foi aberto.
    /// </summary>
    public Alerta? AbrirAlerta(TipoTrigger trigger, string? detalhe, DateTime agora)
    {
        if (AlertaAtivo(trigger) is not null) return null;

        var alerta = new Alerta(this, trigger, detalhe, agora);
        _alertas.Add(alerta);
        return alerta;
    }

    /// <summary>Normaliza o alerta ativo do trigger, se houver. Devolve o alerta normalizado.</summary>
    public Alerta? NormalizarAlerta(TipoTrigger trigger, DateTime agora)
    {
        var alerta = AlertaAtivo(trigger);
        alerta?.Normalizar(agora);
        return alerta;
    }

    /// <summary>
    /// Qualquer mensagem do nó passa por aqui. Se ele estava isolado, o isolamento acaba e o
    /// alerta de "nó isolado" é normalizado (e devolvido, para registrar na ocorrência).
    /// </summary>
    public Alerta? RegistrarComunicacao(DateTime agora, int? brilho = null, int? rssi = null)
    {
        UltimaComunicacao = agora;
        if (brilho.HasValue) UltimoBrilho = brilho;
        if (rssi.HasValue) UltimoRssi = rssi;

        if (!Isolado) return null;
        Isolado = false;
        return NormalizarAlerta(TipoTrigger.NoIsolado, agora);
    }

    /// <summary>Um nó que nunca falou ainda não foi instalado: não conta como isolado.</summary>
    public bool DeveSerIsolado(DateTime agora) =>
        !Isolado && UltimaComunicacao.HasValue && agora - UltimaComunicacao.Value >= LimiteSemComunicacao;

    public Alerta? MarcarIsolado(DateTime agora)
    {
        if (!DeveSerIsolado(agora)) return null;

        Isolado = true;
        return AbrirAlerta(TipoTrigger.NoIsolado, "nenhuma mensagem do nó há 3 minutos", agora);
    }

    /// <summary>Status em linguagem do morador (F4). Depende dos alertas ativos estarem carregados.</summary>
    public string StatusLegivel()
    {
        if (Isolado || AlertaAtivo(TipoTrigger.NoIsolado) is not null) return "Sem comunicação";
        if (AlertaAtivo(TipoTrigger.LampadaSemResposta) is not null) return "Apagado";
        if (UltimaComunicacao is null) return "Aguardando instalação";
        return "Funcionando";
    }
}
