using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Hakari.Api.Infra;

/// <summary>Mostra o cadeado no Swagger só nas rotas que exigem token (cadastro e login ficam livres).</summary>
public class SegurancaSwaggerFiltro : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadados = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var exigeToken = metadados.OfType<IAuthorizeData>().Any() && !metadados.OfType<IAllowAnonymous>().Any();
        if (!exigeToken) return;

        operation.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] }
        ];
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Token ausente ou inválido" });
    }
}
