using Hakari.Domain.Enums;
using Hakari.Domain.Excecoes;

namespace Hakari.Domain.Entidades;

/// <summary>
/// Chamado de manutenção de um poste. Nasce de um alerta (origem Automática) ou de um reporte
/// (origem Cidadão). O status só muda pelos métodos abaixo, que aplicam o ciclo
/// Aberta → Em atendimento → Resolvida e registram cada passo no histórico.
/// </summary>
public class Ocorrencia : Entidade
{
    private readonly List<HistoricoOcorrencia> _historico = [];
    private readonly List<Apoio> _apoios = [];

    private Ocorrencia() { } // usado pelo EF Core

    private Ocorrencia(Poste poste, OrigemOcorrencia origem, Severidade severidade,
                       CategoriaProblema categoria, string descricao, DateTime agora)
    {
        Poste = poste;
        Origem = origem;
        Severidade = severidade;
        Categoria = categoria;
        Descricao = descricao;
        Status = StatusOcorrencia.Aberta;
        AbertaEm = agora;
        Registrar($"Ocorrência aberta ({origem.Texto()})", agora);
    }

    public int PosteId { get; private set; }
    public Poste Poste { get; private set; } = null!;
    public OrigemOcorrencia Origem { get; private set; }
    public Severidade Severidade { get; private set; }
    public CategoriaProblema Categoria { get; private set; }
    public string Descricao { get; private set; } = "";
    public StatusOcorrencia Status { get; private set; }
    public DateTime AbertaEm { get; private set; }
    public DateTime? ResolvidaEm { get; private set; }
    public string? Solucao { get; private set; }

    /// <summary>Cidadão que abriu. Fica nulo em ocorrências automáticas e se ele excluir a conta.</summary>
    public int? AutorId { get; private set; }
    public Cidadao? Autor { get; private set; }

    public int? ResponsavelId { get; private set; }
    public Tecnico? Responsavel { get; private set; }

    public int? AlertaId { get; private set; }
    public Alerta? Alerta { get; private set; }

    public IReadOnlyCollection<HistoricoOcorrencia> Historico => _historico;
    public IReadOnlyCollection<Apoio> Apoios => _apoios;

    public bool EstaAtiva => Status != StatusOcorrencia.Resolvida;

    // ---------- Criação ----------

    public static Ocorrencia AbrirAutomatica(Poste poste, Alerta alerta, DateTime agora)
    {
        if (!alerta.AbreOcorrencia)
            throw new RegraDeNegocioException("Alertas de severidade Aviso não abrem ocorrência");

        var categoria = alerta.Trigger == TipoTrigger.NoIsolado
            ? CategoriaProblema.SemComunicacao
            : CategoriaProblema.LampadaApagada;

        return new Ocorrencia(poste, OrigemOcorrencia.Automatica, alerta.Severidade, categoria,
                              alerta.Trigger.Descricao(), agora) { Alerta = alerta };
    }

    public static Ocorrencia AbrirPorCidadao(Poste poste, Cidadao autor, CategoriaProblema categoria,
                                             string descricao, DateTime agora)
    {
        ValidarDescricao(descricao);
        return new Ocorrencia(poste, OrigemOcorrencia.Cidadao, Severidade.Alta, categoria,
                              descricao.Trim(), agora) { Autor = autor };
    }

    public static void ValidarDescricao(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ValidacaoException("Descreva o problema");
    }

    // ---------- Ciclo de vida (F7) ----------

    public void Assumir(Tecnico tecnico, DateTime agora)
    {
        if (Status == StatusOcorrencia.EmAtendimento)
            throw new RegraDeNegocioException("Esta ocorrência já está em atendimento");
        if (Status == StatusOcorrencia.Resolvida)
            throw new RegraDeNegocioException("Esta ocorrência já foi resolvida");

        Responsavel = tecnico;
        Status = StatusOcorrencia.EmAtendimento;
        Registrar($"Assumida por {tecnico.NomeAbreviado()}", agora);
    }

    public void Resolver(Tecnico tecnico, string solucao, DateTime agora)
    {
        if (Status == StatusOcorrencia.Aberta)
            throw new RegraDeNegocioException("Assuma a ocorrência antes de resolvê-la");
        if (Status == StatusOcorrencia.Resolvida)
            throw new RegraDeNegocioException("Esta ocorrência já foi resolvida");
        if (!tecnico.Mesma(Responsavel) && ResponsavelId != tecnico.Id)
            throw new RegraDeNegocioException("Só o técnico responsável pode resolver esta ocorrência");
        if (string.IsNullOrWhiteSpace(solucao))
            throw new ValidacaoException("Informe o que foi feito");

        Status = StatusOcorrencia.Resolvida;
        Solucao = solucao.Trim();
        ResolvidaEm = agora;
        Registrar($"Resolvida: {Solucao}", agora);
    }

    // ---------- Participação do cidadão (F5 e F6) ----------

    /// <summary>Um cidadão reforça uma ocorrência já aberta em vez de abrir outra igual.</summary>
    public void Apoiar(Cidadao cidadao, DateTime agora)
    {
        if (!EstaAtiva)
            throw new RegraDeNegocioException("Esta ocorrência já foi resolvida");
        if (EnvolveCidadao(cidadao))
            throw new ConflitoException("Você já apoiou esta ocorrência");

        _apoios.Add(new Apoio(this, cidadao, agora));
        Registrar("Novo apoio de um cidadão", agora);
    }

    /// <summary>Verdadeiro se o cidadão abriu ou apoiou esta ocorrência.</summary>
    public bool EnvolveCidadao(Cidadao cidadao) =>
        cidadao.Mesma(Autor) || (cidadao.Id != 0 && AutorId == cidadao.Id) ||
        _apoios.Any(a => cidadao.Mesma(a.Cidadao) || (cidadao.Id != 0 && a.CidadaoId == cidadao.Id));

    /// <summary>Registra no histórico algo que aconteceu no poste (novo alerta, normalização).</summary>
    public void RegistrarEvento(string descricao, DateTime agora) => Registrar(descricao, agora);

    private void Registrar(string descricao, DateTime agora) =>
        _historico.Add(new HistoricoOcorrencia(this, Status, descricao, agora));
}

/// <summary>Linha do histórico: o que aconteceu, quando, e em que status a ocorrência estava.</summary>
public class HistoricoOcorrencia : Entidade
{
    private HistoricoOcorrencia() { }

    internal HistoricoOcorrencia(Ocorrencia ocorrencia, StatusOcorrencia status, string descricao, DateTime agora)
    {
        Ocorrencia = ocorrencia;
        Status = status;
        Descricao = descricao;
        RegistradoEm = agora;
    }

    public int OcorrenciaId { get; private set; }
    public Ocorrencia Ocorrencia { get; private set; } = null!;
    public StatusOcorrencia Status { get; private set; }
    public string Descricao { get; private set; } = "";
    public DateTime RegistradoEm { get; private set; }
}

/// <summary>Cidadão que confirmou um problema já registrado ("eu também vi").</summary>
public class Apoio : Entidade
{
    private Apoio() { }

    internal Apoio(Ocorrencia ocorrencia, Cidadao cidadao, DateTime agora)
    {
        Ocorrencia = ocorrencia;
        Cidadao = cidadao;
        ApoiadoEm = agora;
    }

    public int OcorrenciaId { get; private set; }
    public Ocorrencia Ocorrencia { get; private set; } = null!;
    public int CidadaoId { get; private set; }
    public Cidadao Cidadao { get; private set; } = null!;
    public DateTime ApoiadoEm { get; private set; }
}
