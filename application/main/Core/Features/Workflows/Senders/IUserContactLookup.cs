using SharedKernel.Domain;

namespace Main.Features.Workflows.Senders;

/// <summary>
///     Cross-SCS lookup for a user's contact info (email, locale, display name). Used by booking
///     notification dispatch and EmailHost workflow reminders to resolve the booking owner.
/// </summary>
public interface IUserContactLookup
{
    Task<UserContactInfo?> GetAsync(UserId userId, CancellationToken cancellationToken);
}

public sealed record UserContactInfo(string Email, string Locale, string DisplayName);
