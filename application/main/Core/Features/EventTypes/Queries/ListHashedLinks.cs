using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.EventTypes.Queries;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Read)]
public sealed record ListHashedLinksQuery(EventTypeId EventTypeId) : IRequest<Result<HashedLinksResponse>>;

public sealed class ListHashedLinksHandler(
    IEventTypeRepository eventTypeRepository,
    IHashedLinkRepository hashedLinkRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListHashedLinksQuery, Result<HashedLinksResponse>>
{
    public async Task<Result<HashedLinksResponse>> Handle(ListHashedLinksQuery query, CancellationToken cancellationToken)
    {
        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<HashedLinksResponse>.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(query.EventTypeId, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result<HashedLinksResponse>.NotFound($"Event type '{query.EventTypeId}' was not found.");
        }

        var links = await hashedLinkRepository.GetForEventTypeAsync(query.EventTypeId, cancellationToken);
        return new HashedLinksResponse(links.Select(HashedLinkResponse.From).ToArray());
    }
}
