using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace HolyChat.Api;

/// <summary>
/// Adiciona o esquema de segurança "Bearer" ao documento OpenAPI,
/// para que o botão "Authorize" apareça no Swagger UI e permita
/// testar endpoints protegidos com [Authorize].
/// </summary>
public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "Cola aqui o token JWT recebido do /api/auth/login (sem a palavra 'Bearer', o Swagger adiciona automaticamente)."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = scheme;

        var securityRequirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme } }] = Array.Empty<string>()
        };

        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations.Values))
        {
            operation.Security.Add(securityRequirement);
        }

        return Task.CompletedTask;
    }
}
