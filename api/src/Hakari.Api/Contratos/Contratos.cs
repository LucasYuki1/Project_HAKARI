using Hakari.Domain.Enums;

namespace Hakari.Api.Contratos;

// DTOs: o formato do JSON que entra e sai da API. Ficam separados das entidades para que o
// banco possa mudar sem quebrar o app, e para nunca devolver por engano um SenhaHash.

// ---------- Autenticação e conta (F3, F8) ----------

/// <param name="Nome">Nome completo.</param>
/// <param name="Email">E-mail, usado para entrar.</param>
/// <param name="Telefone">Telefone com DDD, só números ou formatado.</param>
/// <param name="Senha">Pelo menos 8 caracteres, com letras e números.</param>
/// <param name="AceitouTermo">Precisa ser true: aceite do termo de uso de dados.</param>
public record CadastroRequest(string Nome, string Email, string Telefone, string Senha, bool AceitouTermo);

public record LoginRequest(string Email, string Senha);

public record SessaoResponse(string Token, DateTime ExpiraEm, UsuarioResumo Usuario);

public record UsuarioResumo(int Id, string Nome, PerfilUsuario Perfil);

public record MeusDadosResponse(int Id, string Nome, string Email, string Telefone, PerfilUsuario Perfil,
                                DateTime CriadoEm, DateTime? TermoAceitoEm);

// ---------- Postes (F4) ----------

public record PosteResumo(int Id, string Codigo, string Endereco, string Bairro, double Latitude,
                          double Longitude, string Status, DateTime? UltimaComunicacao);

public record PosteDetalhe(int Id, string Codigo, string Endereco, string Bairro, double Latitude,
                           double Longitude, string Status, DateTime? UltimaComunicacao, int? Brilho,
                           int? Rssi, IReadOnlyList<AlertaResponse> AlertasAtivos,
                           OcorrenciaResumo? OcorrenciaAtiva);

public record AlertaResponse(TipoTrigger Trigger, Severidade Severidade, EstadoAlerta Estado,
                             DateTime AbertoEm, DateTime? NormalizadoEm, string? Detalhe);

public record LeituraResponse(DateTime RecebidaEm, int Ldr, int LdrReferencia, int DistanciaCm,
                              bool Presenca, int Brilho, int Rssi);

public record ResumoTelemetria(string Codigo, int Horas, int Leituras, double? BrilhoMedio,
                               int Presencas, double? RssiMedio, IReadOnlyList<LeituraResponse> Ultimas);

// ---------- Ocorrências (F5, F6, F7) ----------

/// <param name="Categoria">Tipo do problema.</param>
/// <param name="Descricao">O que o cidadão viu. Obrigatória.</param>
public record ReporteRequest(CategoriaProblema Categoria, string Descricao);

/// <param name="OcorrenciaId">Número da ocorrência criada ou apoiada.</param>
/// <param name="ApoioAdicionado">true quando já havia ocorrência ativa e o reporte virou apoio.</param>
public record ReporteResponse(int OcorrenciaId, bool ApoioAdicionado, string Mensagem);

public record OcorrenciaResumo(int Id, int PosteId, string PosteCodigo, string Endereco, string Bairro,
                               string Status, Severidade Severidade, string Origem,
                               CategoriaProblema Categoria, DateTime AbertaEm, int Apoios);

/// <param name="Autor">
/// Quem abriu, conforme quem pergunta: "Você" para o próprio autor, nome abreviado para o técnico,
/// null para outros cidadãos (F6/F8).
/// </param>
public record OcorrenciaDetalhe(OcorrenciaResumo Resumo, string Descricao, string? Autor,
                                string? Responsavel, string? Solucao, DateTime? ResolvidaEm,
                                IReadOnlyList<HistoricoResponse> Historico);

public record HistoricoResponse(DateTime RegistradoEm, string Status, string Descricao);

public record ResolverRequest(string Solucao);

public record ResumoBairro(string Bairro, int Postes, int Funcionando, int Apagados, int SemComunicacao,
                           int AguardandoInstalacao, int OcorrenciasAbertas);

// ---------- Mensagens MQTT vindas do ESP32 (snake_case, igual ao firmware) ----------

public record TelemetriaMensagem(string No, int Ldr, bool Escuro, int LdrRef, int DistanciaCm,
                                 bool Presenca, int Brilho, int Rssi, long UptimeS);

public record AlertaMensagem(string No, string Trigger, string Severidade, string Estado,
                             bool AbreOcorrencia, string? Detalhe);
