namespace Hakari.Domain.Excecoes;

/// <summary>
/// Base das exceções do domínio. Cada tipo corresponde a uma resposta HTTP diferente
/// (o mapeamento fica na API, em TratadorDeErros), mas o domínio não sabe nada de HTTP.
/// </summary>
public abstract class HakariException(string mensagem) : Exception(mensagem);

/// <summary>Dado de entrada inválido (ex.: senha fraca, descrição vazia). Vira 400.</summary>
public class ValidacaoException(string mensagem) : HakariException(mensagem);

/// <summary>Credenciais ausentes ou incorretas. Vira 401.</summary>
public class NaoAutorizadoException(string mensagem) : HakariException(mensagem);

/// <summary>Recurso inexistente (ex.: poste que não existe). Vira 404.</summary>
public class NaoEncontradoException(string mensagem) : HakariException(mensagem);

/// <summary>Conflito com o estado atual (ex.: e-mail já usado, apoio repetido). Vira 409.</summary>
public class ConflitoException(string mensagem) : HakariException(mensagem);

/// <summary>Operação que fere uma regra de negócio (ex.: resolver sem assumir). Vira 422.</summary>
public class RegraDeNegocioException(string mensagem) : HakariException(mensagem);
