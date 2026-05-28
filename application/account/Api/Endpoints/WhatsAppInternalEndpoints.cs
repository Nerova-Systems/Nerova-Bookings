using Account.Features.Subscriptions.Domain;
using Account.Features.WhatsApp.Commands;
using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Infrastructure;
using Account.Features.WhatsApp.Queries;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.ApiResults;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

/// <summary>
///     Internal-only endpoints called by the main SCS to read WABA profile data and push back
///     flow status / generated JSON. Not exposed publicly — guarded by an internal API key
///     header (<c>Authorization: ApiKey &lt;key&gt;</c>) validated via <see cref="IWhatsAppInternalApiKeyValidator" />.
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
                IWhatsAppInternalApiKeyValidator validator
            ) =>
            {
                if (!validator.IsValid(httpContext.Request.Headers.Authorization)) return Result<WhatsAppFlowProfileDto>.Unauthorized("Invalid internal API key");
                var result = await mediator.Send(new GetWhatsAppFlowProfileQuery(new TenantId(tenantId)));
                return result is null
                    ? Result<WhatsAppFlowProfileDto>.NotFound("Profile not found")
                    : Result<WhatsAppFlowProfileDto>.Success(result);
            }
        );

        group.MapGet("/profile/by-phone-number/{phoneNumberId}", async Task<ApiResult<WhatsAppFlowProfileDto>> (
                string phoneNumberId,
                HttpContext httpContext,
                IMediator mediator,
                IWhatsAppInternalApiKeyValidator validator
            ) =>
            {
                if (!validator.IsValid(httpContext.Request.Headers.Authorization)) return Result<WhatsAppFlowProfileDto>.Unauthorized("Invalid internal API key");
                var result = await mediator.Send(new GetWhatsAppFlowProfileByPhoneNumberIdQuery(phoneNumberId));
                return result is null
                    ? Result<WhatsAppFlowProfileDto>.NotFound("Profile not found")
                    : Result<WhatsAppFlowProfileDto>.Success(result);
            }
        );

        group.MapPost("/flow-status", async Task<ApiResult> (
            [FromQuery] long tenantId,
            UpdateFlowStatusInternalRequest body,
            HttpContext httpContext,
            IMediator mediator,
            IWhatsAppInternalApiKeyValidator validator
        ) =>
        {
            if (!validator.IsValid(httpContext.Request.Headers.Authorization)) return Result.Unauthorized("Invalid internal API key");
            return await mediator.Send(new UpdateFlowStatusInternalCommand(new TenantId(tenantId), body.FlowId, body.Status, body.GeneratedFlowJson));
        });

        // Subscription plan lookup — consumed by main SCS ITierService to resolve the WhatsApp
        // Flows tier (null = no subscription = Starter tier).
        group.MapGet("/subscription", async Task<ApiResult<SubscriptionLookupResponse>> (
                [FromQuery] long tenantId,
                HttpContext httpContext,
                ISubscriptionRepository subscriptionRepository,
                IWhatsAppInternalApiKeyValidator validator,
                CancellationToken cancellationToken
            ) =>
            {
                if (!validator.IsValid(httpContext.Request.Headers.Authorization)) return Result<SubscriptionLookupResponse>.Unauthorized("Invalid internal API key");
                var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(new TenantId(tenantId), cancellationToken);
                return Result<SubscriptionLookupResponse>.Success(new SubscriptionLookupResponse(subscription?.Plan.ToString()));
            }
        );
    }
}

[PublicAPI]
public sealed record UpdateFlowStatusInternalRequest(string FlowId, string Status, string? GeneratedFlowJson);

[PublicAPI]
public sealed record SubscriptionLookupResponse(string? Plan);
