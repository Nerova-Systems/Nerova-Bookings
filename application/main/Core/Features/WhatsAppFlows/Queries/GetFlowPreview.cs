using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Main.Features.WhatsAppFlows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.WhatsAppFlows.Queries;

/// <summary>
///     Returns a freshly-issued Meta Flow preview URL for the current tenant's published flow.
///     The "Preview your flow" button in the questionnaire/admin UI calls this; the preview URL
///     itself is short-lived (Meta supplies <c>expires_at</c>), so we never cache it.
/// </summary>
[PublicAPI]
public sealed record GetFlowPreviewQuery : IRequest<Result<FlowPreviewLinkResponse>>;

public sealed class GetFlowPreviewHandler(
    ITenantFlowConfigRepository repository,
    IWhatsAppFlowProfileSync profileSync,
    IMetaFlowsApiClient metaClient,
    IExecutionContext executionContext
) : IRequestHandler<GetFlowPreviewQuery, Result<FlowPreviewLinkResponse>>
{
    private const string PreviewUnavailableMessage = "Publish your flow first.";

    public async Task<Result<FlowPreviewLinkResponse>> Handle(GetFlowPreviewQuery request, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result<FlowPreviewLinkResponse>.Unauthorized("Authentication is required.");

        var config = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (config is null) return Result<FlowPreviewLinkResponse>.NotFound(PreviewUnavailableMessage);

        var profile = await profileSync.GetByTenantId(tenantId, cancellationToken);
        if (profile is null) return Result<FlowPreviewLinkResponse>.NotFound(PreviewUnavailableMessage);
        if (string.IsNullOrWhiteSpace(profile.FlowId)) return Result<FlowPreviewLinkResponse>.NotFound(PreviewUnavailableMessage);
        if (!string.Equals(profile.FlowStatus, "Published", StringComparison.OrdinalIgnoreCase))
        {
            return Result<FlowPreviewLinkResponse>.NotFound(PreviewUnavailableMessage);
        }

        if (string.IsNullOrWhiteSpace(profile.WabaAccessToken))
        {
            return Result<FlowPreviewLinkResponse>.BadRequest("WABA access token is missing on the profile.");
        }

        var preview = await metaClient.GetFlowPreviewUrlAsync(profile.FlowId, profile.WabaAccessToken, cancellationToken);
        if (!preview.IsSuccess) return Result<FlowPreviewLinkResponse>.From(preview);

        return new FlowPreviewLinkResponse(preview.Value!.PreviewUrl, preview.Value.ExpiresAt);
    }
}
