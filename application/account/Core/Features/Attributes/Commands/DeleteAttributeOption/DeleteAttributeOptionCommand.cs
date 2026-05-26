using Account.Features.Attributes.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Attributes.Commands.DeleteAttributeOption;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Delete, PermissionScope.Organization)]
public sealed record DeleteAttributeOptionCommand : ICommand, IRequest<Result>
{
    public required AttributeId AttributeId { get; init; }

    public required AttributeOptionId OptionId { get; init; }
}

public sealed class DeleteAttributeOptionHandler(
    IAttributeRepository attributeRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<DeleteAttributeOptionCommand, Result>
{
    public async Task<Result> Handle(DeleteAttributeOptionCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapAttributes.Key))
        {
            return Result.Forbidden("The attributes feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;

        var attribute = await attributeRepository.GetByIdUnfilteredAsync(command.AttributeId, cancellationToken);
        if (attribute is null)
        {
            return Result.NotFound($"Attribute '{command.AttributeId}' not found.");
        }

        if (attribute.TenantId != orgId)
        {
            return Result.Forbidden("You do not have access to this attribute.");
        }

        if (!attribute.RemoveOption(command.OptionId))
        {
            return Result.NotFound($"Option '{command.OptionId}' not found on attribute '{command.AttributeId}'.");
        }

        attributeRepository.Update(attribute);

        events.CollectEvent(new AttributeOptionDeleted(command.OptionId, command.AttributeId));

        return Result.Success();
    }
}
