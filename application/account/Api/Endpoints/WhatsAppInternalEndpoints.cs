using Account.Features.WhatsApp.Commands;
using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Queries;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.ApiResults;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.ExecutionContext;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

/// <summary>
///     Internal-only endpoints called by the main SCS to read WABA profile data and push back
///     flow status / generated JSON. Not exposed publicly — guarded by an internal API key
///     header (<c>X-Internal-Api-Key</c>) configured via <c>WhatsApp:InternalApiKey</c>.
/// </summary>
public sealed class WhatsAppInternalEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/whatsapp/internal";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup(RoutesPrefix)
            .WithTags("WhatsAppInternal")
            .WithGroupName(OpenApiDocumentNames.Account)
            .AllowAnonymous();

        group.MapGet("/profile", async Task<ApiResult<WhatsAppFlowProfileDto>> (
            [FromQuery] long tenantId,
            HttpContext httpContext,
            IMediator mediator,
            Microsoft.Extensions.Configuration.IConfiguration configuration
        ) =>
        {
            if (!IsAuthorized(httpContext, configuration)) return Result<WhatsAppFlowProfileDto>.Unauthorized("Invalid internal API key");
            var result = await mediator.Send(new GetWhatsAppFlowProfileQuery(new TenantId(tenantId)));
            return result is null
                ? Result<WhatsAppFlowProfileDto>.NotFound("Profile not found")
                : Result<WhatsAppFlowProfileDto>.Success(result);
        });

        group.MapPost("/flow-status", async Task<ApiResult> (
            [FromQuery] long tenantId,
            UpdateFlowStatusInternalRequest body,
            HttpContext httpContext,
            IMediator mediator,
            Microsoft.Extensions.Configuration.IConfiguration configuration
        ) =>
        {
            if (!IsAuthorized(httpContext, configuration)) return Result.Unauthorized("Invalid internal API key");
            return await mediator.Send(new UpdateFlowStatusInternalCommand(new TenantId(tenantId), body.FlowId, body.Status, body.GeneratedFlowJson));
        });
    }

    private static bool IsAuthorized(HttpContext httpContext, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var expected = configuration["WhatsApp:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expected)) return false;

        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var auth)) return false;
        var value = auth.ToString();
        if (!value.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase)) return false;
        return string.Equals(value["ApiKey ".Length..].Trim(), expected, StringComparison.Ordinal);
    }
}

[PublicAPI]
public sealed record UpdateFlowStatusInternalRequest(string FlowId, string Status, string? GeneratedFlowJson);
