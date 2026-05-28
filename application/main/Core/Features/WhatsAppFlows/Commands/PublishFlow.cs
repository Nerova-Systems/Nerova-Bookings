using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Main.Features.WhatsAppFlows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.WhatsAppFlows.Commands;

/// <summary>
///     Orchestrates the publish pipeline:
///     <list type="number">
///         <item>Load the tenant's <see cref="TenantFlowConfig" /></item>
///         <item>Pull the WABA profile from the account SCS</item>
///         <item>Generate Flow JSON via <see cref="IFlowTemplateEngine" /></item>
///         <item>Create the Meta flow if one does not yet exist</item>
///         <item>Upload the Flow JSON asset</item>
///         <item>Publish the flow</item>
///         <item>Push the new flow id + status + cached JSON back to the account SCS</item>
///         <item>Return the preview URL</item>
///     </list>
/// </summary>
[PublicAPI]
public sealed record PublishFlowCommand(string? BusinessName) : ICommand, IRequest<Result<PublishFlowResponse>>;

public sealed class PublishFlowHandler(
    ITenantFlowConfigRepository repository,
    IFlowTemplateEngine templateEngine,
    IMetaFlowsApiClient metaClient,
    IWhatsAppFlowProfileSync profileSync,
    ITierService tierService,
    IExecutionContext executionContext,
    ILogger<PublishFlowHandler> logger
) : IRequestHandler<PublishFlowCommand, Result<PublishFlowResponse>>
{
    public async Task<Result<PublishFlowResponse>> Handle(PublishFlowCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result<PublishFlowResponse>.Unauthorized("Authentication is required.");

        var config = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (config is null) return Result<PublishFlowResponse>.NotFound("Tenant flow configuration has not been created yet.");

        var profile = await profileSync.GetByTenantId(tenantId, cancellationToken);
        if (profile is null) return Result<PublishFlowResponse>.NotFound("WhatsApp profile not found for this tenant.");
        if (!profile.IsOnboardingComplete) return Result<PublishFlowResponse>.BadRequest("WhatsApp onboarding is not yet complete.");
        if (string.IsNullOrWhiteSpace(profile.WabaAccessToken)) return Result<PublishFlowResponse>.BadRequest("WABA access token is missing on the profile.");

        var tier = await tierService.GetTierAsync(tenantId, cancellationToken);
        var flowJson = templateEngine.GenerateFlowJson(config, command.BusinessName ?? "your business", tier);

        var flowId = profile.FlowId;
        if (string.IsNullOrWhiteSpace(flowId))
        {
            var created = await metaClient.CreateFlowAsync(profile.WabaId, $"booking-flow-{tenantId.Value}", profile.WabaAccessToken, cancellationToken);
            if (!created.IsSuccess) return Result<PublishFlowResponse>.From(created);
            flowId = created.Value!.FlowId;
        }

        var upload = await metaClient.UploadFlowAssetAsync(flowId, flowJson, profile.WabaAccessToken, cancellationToken);
        if (!upload.IsSuccess) return Result<PublishFlowResponse>.From(upload);

        var publish = await metaClient.PublishFlowAsync(flowId, profile.WabaAccessToken, cancellationToken);
        if (!publish.IsSuccess) return Result<PublishFlowResponse>.From(publish);

        var preview = await metaClient.GetFlowPreviewUrlAsync(flowId, profile.WabaAccessToken, cancellationToken);
        if (!preview.IsSuccess) return Result<PublishFlowResponse>.From(preview);

        var synced = await profileSync.UpdateFlowStatus(tenantId, flowId, "Published", flowJson, cancellationToken);
        if (!synced)
        {
            // The flow IS published on Meta — log loud, but do not fail the caller.
            logger.LogError("Flow published on Meta (flowId={FlowId}) but failed to sync status back to account SCS for tenant {TenantId}", flowId, tenantId);
        }

        return new PublishFlowResponse(flowId, "Published", preview.Value!.PreviewUrl, preview.Value.ExpiresAt);
    }
}
