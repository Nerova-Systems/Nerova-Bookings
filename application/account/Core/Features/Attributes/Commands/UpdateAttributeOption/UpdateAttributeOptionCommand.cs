using Account.Features.Attributes.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Attributes.Commands.UpdateAttributeOption;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Update, PermissionScope.Organization)]
public sealed record UpdateAttributeOptionCommand : ICommand, IRequest<Result<AttributeOptionResponse>>
{
    public required AttributeId AttributeId { get; init; }

    public required AttributeOptionId OptionId { get; init; }

    public required string Value { get; init; }

    public bool IsGroup { get; init; }

    public string[] Contains { get; init; } = [];
}

public sealed class UpdateAttributeOptionValidator : AbstractValidator<UpdateAttributeOptionCommand>
{
    public UpdateAttributeOptionValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Option value must be between 1 and 255 characters.");
    }
}

public sealed class UpdateAttributeOptionHandler(
    IAttributeRepository attributeRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<UpdateAttributeOptionCommand, Result<AttributeOptionResponse>>
{
    public async Task<Result<AttributeOptionResponse>> Handle(UpdateAttributeOptionCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapAttributes.Key))
        {
            return Result<AttributeOptionResponse>.Forbidden("The attributes feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;

        var attribute = await attributeRepository.GetByIdUnfilteredAsync(command.AttributeId, cancellationToken);
        if (attribute is null)
        {
            return Result<AttributeOptionResponse>.NotFound($"Attribute '{command.AttributeId}' not found.");
        }

        if (attribute.TenantId != orgId)
        {
            return Result<AttributeOptionResponse>.Forbidden("You do not have access to this attribute.");
        }

        if (!attribute.UpdateOption(command.OptionId, command.Value, command.IsGroup, command.Contains))
        {
            return Result<AttributeOptionResponse>.NotFound($"Option '{command.OptionId}' not found on attribute '{command.AttributeId}'.");
        }

        attributeRepository.Update(attribute);

        var updated = attribute.Options.Single(o => o.Id == command.OptionId);
        events.CollectEvent(new AttributeOptionUpdated(updated.Id, attribute.Id));

        return updated.ToResponse();
    }
}
