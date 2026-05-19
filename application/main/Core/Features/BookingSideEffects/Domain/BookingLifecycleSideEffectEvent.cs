using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;
using SharedKernel.DomainEvents;

namespace Main.Features.BookingSideEffects.Domain;

public sealed record BookingLifecycleSideEffectEvent(
    TenantId TenantId,
    UserId OwnerUserId,
    EventTypeId EventTypeId,
    BookingId BookingId,
    string Trigger,
    string Title,
    string BookerName,
    string BookerEmail,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status,
    string? LocationType,
    string? LocationValue
) : IDomainEvent;
