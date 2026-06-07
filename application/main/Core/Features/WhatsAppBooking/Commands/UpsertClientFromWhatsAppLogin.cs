using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppBooking.Commands;

/// <summary>
///     Upserts a <see cref="Client" /> from a WhatsApp login Flow completion. Matches by phone number;
///     creates a new client if none found. Does not go through the booking side-effect infrastructure
///     because there is no booking yet at this point.
/// </summary>
[PublicAPI]
public sealed record UpsertClientFromWhatsAppLoginCommand(TenantId TenantId, string FullName, string Email, string PhoneNumber) : IRequest<Result>;

public sealed class UpsertClientFromWhatsAppLoginHandler(
    IClientRepository clientRepository,
    TimeProvider timeProvider
) : IRequestHandler<UpsertClientFromWhatsAppLoginCommand, Result>
{
    public async Task<Result> Handle(UpsertClientFromWhatsAppLoginCommand command, CancellationToken cancellationToken)
    {
        var existing = await clientRepository.GetByTenantAndContactUnfilteredAsync(
            command.TenantId, command.PhoneNumber, command.Email, cancellationToken
        );

        if (existing is not null)
        {
            existing.RecordVisit(timeProvider.GetUtcNow());

            var mergedEmail = string.IsNullOrWhiteSpace(existing.Email) ? command.Email : existing.Email;
            var mergedPhone = string.IsNullOrWhiteSpace(existing.PhoneNumber) ? command.PhoneNumber : existing.PhoneNumber;
            if (!string.Equals(mergedEmail, existing.Email, StringComparison.Ordinal)
                || !string.Equals(mergedPhone, existing.PhoneNumber, StringComparison.Ordinal))
            {
                existing.Update(existing.FirstName, existing.LastName, mergedEmail, mergedPhone);
            }

            clientRepository.Update(existing);
            return Result.Success();
        }

        var (firstName, lastName) = SplitName(command.FullName);
        var client = Client.Create(command.TenantId, firstName, lastName, command.Email, command.PhoneNumber);
        await clientRepository.AddAsync(client, cancellationToken);
        return Result.Success();
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (trimmed.Length == 0) return ("Guest", string.Empty);
        var sep = trimmed.IndexOf(' ');
        return sep < 0 ? (trimmed, string.Empty) : (trimmed[..sep], trimmed[(sep + 1)..].Trim());
    }
}
