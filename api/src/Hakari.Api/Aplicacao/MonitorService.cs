using Hakari.Api.Contratos;
using Hakari.Domain.Entidades;
using Hakari.Domain.Enums;
using Hakari.Infrastructure.Dados;
using Microsoft.EntityFrameworkCore;

namespace Hakari.Api.Aplicacao;

public interface IMonitorService
{
    Task ProcessarTelemetriaAsync(string codigo, TelemetriaMensagem mensagem, CancellationToken ct);
    Task ProcessarAlertaAsync(string codigo, AlertaMensagem mensagem, CancellationToken ct);
    Task ProcessarStatusAsync(string codigo, string status, CancellationToken ct);
    Task<int> VerificarIsolamentoAsync(CancellationToken ct);
}

/// <summary>
/// F2: transforma as mensagens dos nós em alertas e ocorrências automáticas. É chamado pelo
/// MqttWorker (mensagens que chegam) e pelo IsolamentoWorker (nós que pararam de falar).
/// </summary>
public class MonitorService(HakariDbContext db, TimeProvider relogio, ILogger<MonitorService> log) : IMonitorService
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public async Task ProcessarTelemetriaAsync(string codigo, TelemetriaMensagem m, CancellationToken ct)
    {
        var poste = await CarregarAsync(codigo, ct);
        if (poste is null) return;

        await RegistrarComunicacaoAsync(poste, m.Brilho, m.Rssi, ct);
        db.Leituras.Add(new Leitura(poste, Agora, m.Ldr, m.LdrRef, m.DistanciaCm, m.Presenca, m.Brilho, m.Rssi));
        await db.SaveChangesAsync(ct);
    }

    public async Task ProcessarAlertaAsync(string codigo, AlertaMensagem m, CancellationToken ct)
    {
        var trigger = TriggerDoFirmware(m.Trigger);
        if (trigger is null)
        {
            log.LogWarning("Trigger desconhecido {Trigger} recebido do nó {Codigo}", m.Trigger, codigo);
            return;
        }

        var poste = await CarregarAsync(codigo, ct);
        if (poste is null) return;

        await RegistrarComunicacaoAsync(poste, null, null, ct);

        if (string.Equals(m.Estado, "Ativo", StringComparison.OrdinalIgnoreCase))
        {
            // AbrirAlerta devolve null quando já existe alerta ativo do mesmo trigger.
            var alerta = poste.AbrirAlerta(trigger.Value, m.Detalhe, Agora);
            if (alerta is not null)
            {
                log.LogInformation("Alerta {Severidade} {Trigger} aberto no nó {Codigo}", alerta.Severidade, alerta.Trigger, codigo);
                if (alerta.AbreOcorrencia) await AbrirOuAnexarAsync(poste, alerta, ct);
            }
        }
        else
        {
            var alerta = poste.NormalizarAlerta(trigger.Value, Agora);
            if (alerta is not null)
            {
                log.LogInformation("Alerta {Trigger} normalizado no nó {Codigo}", alerta.Trigger, codigo);
                if (alerta.AbreOcorrencia)
                    (await OcorrenciaAtivaAsync(poste.Id, ct))?.RegistrarEvento(
                        $"Alerta normalizado: {alerta.Trigger.Descricao()}. Aguardando verificação da manutenção.", Agora);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// "online" conta como comunicação. "offline" é o last will publicado pelo broker quando o nó
    /// cai: só registramos no log, e o isolamento é confirmado depois de 3 min sem mensagens.
    /// </summary>
    public async Task ProcessarStatusAsync(string codigo, string status, CancellationToken ct)
    {
        if (!string.Equals(status, "online", StringComparison.OrdinalIgnoreCase))
        {
            log.LogWarning("Nó {Codigo} informou status {Status}", codigo, status);
            return;
        }

        var poste = await CarregarAsync(codigo, ct);
        if (poste is null) return;

        await RegistrarComunicacaoAsync(poste, null, null, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> VerificarIsolamentoAsync(CancellationToken ct)
    {
        var agora = Agora;
        var limite = agora - Poste.LimiteSemComunicacao;

        var candidatos = await db.Postes
            .Include(p => p.Alertas.Where(a => a.Estado == EstadoAlerta.Ativo))
            .Where(p => !p.Isolado && p.UltimaComunicacao != null && p.UltimaComunicacao <= limite)
            .ToListAsync(ct);

        foreach (var poste in candidatos)
        {
            var alerta = poste.MarcarIsolado(agora);
            if (alerta is null) continue;

            log.LogWarning("Nó {Codigo} isolado: sem mensagens desde {Ultima:u}", poste.Codigo, poste.UltimaComunicacao);
            await AbrirOuAnexarAsync(poste, alerta, ct);
        }

        await db.SaveChangesAsync(ct);
        return candidatos.Count;
    }

    // ---------- apoio ----------

    private async Task RegistrarComunicacaoAsync(Poste poste, int? brilho, int? rssi, CancellationToken ct)
    {
        var normalizado = poste.RegistrarComunicacao(Agora, brilho, rssi);
        if (normalizado is null) return;

        log.LogInformation("Nó {Codigo} voltou a se comunicar", poste.Codigo);
        (await OcorrenciaAtivaAsync(poste.Id, ct))?.RegistrarEvento(
            "O nó voltou a se comunicar: alerta de isolamento normalizado.", Agora);
    }

    /// <summary>Abre a ocorrência automática ou, se o poste já tem uma em aberto, só registra o novo alerta nela.</summary>
    private async Task AbrirOuAnexarAsync(Poste poste, Alerta alerta, CancellationToken ct)
    {
        var ativa = await OcorrenciaAtivaAsync(poste.Id, ct);
        if (ativa is null)
            db.Ocorrencias.Add(Ocorrencia.AbrirAutomatica(poste, alerta, Agora));
        else
            ativa.RegistrarEvento($"Novo alerta {alerta.Severidade}: {alerta.Trigger.Descricao()}", Agora);
    }

    private Task<Ocorrencia?> OcorrenciaAtivaAsync(int posteId, CancellationToken ct) =>
        db.Ocorrencias.Where(o => o.PosteId == posteId).Where(Mapeamento.Ativa).FirstOrDefaultAsync(ct);

    private async Task<Poste?> CarregarAsync(string codigo, CancellationToken ct)
    {
        var poste = await db.Postes
            .Include(p => p.Alertas.Where(a => a.Estado == EstadoAlerta.Ativo))
            .FirstOrDefaultAsync(p => p.Codigo == codigo, ct);

        if (poste is null)
            log.LogWarning("Mensagem de nó não cadastrado {Codigo} ignorada", codigo);
        return poste;
    }

    /// <summary>Converte o nome usado no firmware (snake_case) para o enum do domínio.</summary>
    internal static TipoTrigger? TriggerDoFirmware(string? trigger) => trigger switch
    {
        "lampada_sem_resposta" => TipoTrigger.LampadaSemResposta,
        "sinal_fraco" => TipoTrigger.SinalFraco,
        "no_isolado" => TipoTrigger.NoIsolado,
        _ => null
    };
}
