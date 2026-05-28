using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.EventTypes.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Update)]
public sealed record CreateHashedLinkCommand(
    EventTypeId EventTypeId,
    string? Hash = null,
    int? ExpiresAfterUses = null,
    DateTimeOffset? ExpiresAt = null
) : ICommand, IRequest<Result<HashedLinkResponse>>;

public sealed class CreateHashedLinkValidator : AbstractValidator<CreateHashedLinkCommand>
{
    public CreateHashedLinkValidator()
    {
        RuleFor(command => command.Hash).MaximumLength(200);
        RuleFor(command => command.ExpiresAfterUses).GreaterThan(0).When(command => command.ExpiresAfterUses is not null);
    }
}

public sealed class CreateHashedLinkHandler(
    IEventTypeRepository eventTypeRepository,
    IHashedLinkRepository hashedLinkRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<CreateHashedLinkCommand, Result<HashedLinkResponse>>
{
    public async Task<Result<HashedLinkResponse>> Handle(CreateHashedLinkCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result<HashedLinkResponse>.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<HashedLinkResponse>.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(command.EventTypeId, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result<HashedLinkResponse>.NotFound($"Event type '{command.EventTypeId}' was not found.");
        }

        var hash = string.IsNullOrWhiteSpace(command.Hash)
            ? Guid.NewGuid().ToString("N")
            : command.Hash.Trim();

        if (await hashedLinkRepository.HashExistsAsync(hash, null, cancellationToken))
        {
            return Result<HashedLinkResponse>.BadRequest($"Hashed link '{hash}' already exists.");
        }

        var link = eventType.AddHashedLink(hash, command.ExpiresAfterUses, command.ExpiresAt);
        await hashedLinkRepository.AddAsync(link, cancellationToken);
        events.CollectEvent(new HashedLinkCreated(eventType.Id, link.Id));

        return HashedLinkResponse.From(link);
    }
}
