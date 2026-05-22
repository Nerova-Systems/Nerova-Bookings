using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;

namespace Main.Features.ManagedEventTypes.Shared;

[PublicAPI]
public sealed record ManagedEventTypeChildResponse(
    EventTypeId ChildId,
    UserId MemberUserId,
    string Title,
    string Slug,
    string[] UnlockedFields
)
{
    public static ManagedEventTypeChildResponse From(EventType child)
    {
        return new ManagedEventTypeChildResponse(
            child.Id,
            child.OwnerUserId,
            child.Title,
            child.Slug,
            child.UnlockedFields
        );
    }
}

[PublicAPI]
public sealed record ManagedEventTypeChildrenResponse(ManagedEventTypeChildResponse[] Children);

[PublicAPI]
public sealed record ManagedEventTypeAssignmentStatusResponse(
    EventTypeId ParentId,
    string[] UnlockedFields,
    ManagedEventTypeChildResponse[] Children
)
{
    public static ManagedEventTypeAssignmentStatusResponse From(EventType parent, EventType[] children)
    {
        return new ManagedEventTypeAssignmentStatusResponse(
            parent.Id,
            parent.UnlockedFields,
            children.Select(ManagedEventTypeChildResponse.From).ToArray()
        );
    }
}

[PublicAPI]
public sealed record UpdateManagedEventTypeLocksRequest(string[] UnlockedFields);

[PublicAPI]
public sealed record AssignManagedEventTypeRequest(UserId MemberUserId);
