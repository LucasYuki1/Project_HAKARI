using Hakari.Domain.Excecoes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hakari.Api.Infra;

/// <summary>
/// Converte as exceções do domínio em respostas HTTP padronizadas (ProblemDetails, RFC 9457).
/// Assim os controllers não precisam de try/catch e o app sempre recebe o mesmo formato de erro.
/// </summary>
public class TratadorDeErros(IProblemDetailsService problemas, ILogger<TratadorDeErros> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, titulo) = ex switch
        {
            ValidacaoException => (StatusCodes.Status400BadRequest, "Dados inválidos"),
            NaoAutorizadoException => (StatusCodes.Status401Unauthorized, "Não autorizado"),
            NaoEncontradoException => (StatusCodes.Status404NotFound, "Não encontrado"),
            ConflitoException => (StatusCodes.Status409Conflict, "Conflito"),
            RegraDeNegocioException => (StatusCodes.Status422UnprocessableEntity, "Operação não permitida"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };

        if (status == StatusCodes.Status500InternalServerError)
            log.LogError(ex, "Erro não tratado em {Metodo} {Caminho}", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = status;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                // A mensagem do domínio é escrita para o usuário; erros internos não vazam detalhes.
                Detail = ex is HakariException ? ex.Message : "Algo deu errado. Tente novamente."
            }
        });
    }
}
