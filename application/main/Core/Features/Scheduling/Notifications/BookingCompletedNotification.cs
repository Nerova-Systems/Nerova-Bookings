using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;

namespace Main.Features.Scheduling.Notifications;

/// <summary>
///     Published explicitly by <c>MarkBookingCompletedHandler</c> after the unit-of-work commit
///     succeeds. The post-session payment dispatcher consumes this and (for tenants whose
///     <c>PaymentTiming = AfterSession</c>) creates a Paystack payment link + WhatsApps the
///     booker.
///
///     Plain <see cref="INotification" /> (not <see cref="SharedKernel.DomainEvents.IDomainEvent" />)
///     so the handler is free to perform external I/O — domain events in this project run inside
///     the pipeline and must remain side-effect free.
/// </summary>
[PublicAPI]
public sealed record BookingCompletedNotification(BookingId BookingId, TenantId TenantId) : INotification;
