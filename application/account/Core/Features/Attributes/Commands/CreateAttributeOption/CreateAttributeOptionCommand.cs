using Account.Features.Attributes.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Attributes.Commands.CreateAttributeOption;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Update, PermissionScope.Organization)]
public sealed record CreateAttributeOptionCommand : ICommand, IRequest<Result<AttributeOptionResponse>>
{
    public required AttributeId AttributeId { get; init; }

    public required string Value { get; init; }
}

public sealed class CreateAttributeOptionValidator : AbstractValidator<CreateAttributeOptionCommand>
{
    public CreateAttributeOptionValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Option value must be between 1 and 255 characters.");
    }
}

public sealed class CreateAttributeOptionHandler(
    IAttributeRepository attributeRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<CreateAttributeOptionCommand, Result<AttributeOptionResponse>>
{
    public async Task<Result<AttributeOptionResponse>> Handle(CreateAttributeOptionCommand command, CancellationToken cancellationToken)
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

        if (attribute.Type is AttributeType.Text or AttributeType.Number)
        {
            return Result<AttributeOptionResponse>.BadRequest("Options can only be added to SingleSelect or MultiSelect attributes.");
        }

        var option = attribute.AddOption(command.Value);
        attributeRepository.Update(attribute);

        events.CollectEvent(new AttributeOptionCreated(option.Id, attribute.Id));

        return option.ToResponse();
    }
}
