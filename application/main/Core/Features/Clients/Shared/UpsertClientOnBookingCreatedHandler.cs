using Main.Features.BookingSideEffects.Domain;
using Main.Features.Clients.Domain;

namespace Main.Features.Clients.Shared;

/// <summary>
///     Upserts a <see cref="Client" /> whenever a booking is created so booking customers automatically
///     appear in the Clients portal. Matches an existing client by phone number (preferred) or email within
///     the tenant; on a match it bumps the last-visit timestamp and backfills any missing contact detail,
///     otherwise it creates a new client. Runs inside the booking's unit-of-work transaction because domain
///     events are published before the commit (Validation → Command → PublishDomainEvents → UnitOfWork).
/// </summary>
public sealed class UpsertClientOnBookingCreatedHandler(
    IClientRepository clientRepository,
    TimeProvider timeProvider
) : INotificationHandler<BookingLifecycleSideEffectEvent>
{
    public async Task Handle(BookingLifecycleSideEffectEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Trigger != BookingSideEffectConstants.BookingCreated)
        {
            return;
        }

        var existing = await clientRepository.GetByTenantAndContactUnfilteredAsync(
            notification.TenantId,
            notification.BookerPhone,
            notification.BookerEmail,
            cancellationToken
        );

        if (existing is not null)
        {
            existing.RecordVisit(timeProvider.GetUtcNow());

            var mergedEmail = string.IsNullOrWhiteSpace(existing.Email) ? notification.BookerEmail : existing.Email;
            var mergedPhone = string.IsNullOrWhiteSpace(existing.PhoneNumber) ? notification.BookerPhone : existing.PhoneNumber;
            if (!string.Equals(mergedEmail, existing.Email, StringComparison.Ordinal) || !string.Equals(mergedPhone, existing.PhoneNumber, StringComparison.Ordinal))
            {
                existing.Update(existing.FirstName, existing.LastName, mergedEmail, mergedPhone);
            }

            clientRepository.Update(existing);
            return;
        }

        var (firstName, lastName) = SplitName(notification.BookerName);
        var client = Client.Create(notification.TenantId, firstName, lastName, notification.BookerEmail, notification.BookerPhone);
        await clientRepository.AddAsync(client, cancellationToken);
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (trimmed.Length == 0)
        {
            return ("Guest", string.Empty);
        }

        var separatorIndex = trimmed.IndexOf(' ');
        return separatorIndex < 0
            ? (trimmed, string.Empty)
            : (trimmed[..separatorIndex], trimmed[(separatorIndex + 1)..].Trim());
    }
}
