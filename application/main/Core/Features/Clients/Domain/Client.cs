using JetBrains.Annotations;
using SharedKernel.Domain;

namespace Main.Features.Clients.Domain;

/// <summary>
///     A booking customer (client) belonging to a tenant. Clients are created when a customer books
///     or is added manually. The aggregate's <c>CreatedAt</c> represents the client's first visit and
///     <see cref="LastVisitAt" /> the most recent one. A client missing an email or phone number is
///     flagged via <see cref="NeedsAttention" /> so staff can complete the record.
/// </summary>
public sealed class Client : AggregateRoot<ClientId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Client() : base(ClientId.NewId())
    {
    }

    private Client(TenantId tenantId, string firstName, string lastName, string? email, string? phoneNumber)
        : base(ClientId.NewId())
    {
        TenantId = tenantId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = Normalize(email)?.ToLowerInvariant();
        PhoneNumber = Normalize(phoneNumber);
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? AvatarUrl { get; private set; }

    public DateTimeOffset? LastVisitAt { get; private set; }

    public bool NeedsAttention => string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(PhoneNumber);

    public TenantId TenantId { get; } = new(0);

    public static Client Create(TenantId tenantId, string firstName, string lastName, string? email, string? phoneNumber)
    {
        return new Client(tenantId, firstName, lastName, email, phoneNumber);
    }

    public void Update(string firstName, string lastName, string? email, string? phoneNumber)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = Normalize(email)?.ToLowerInvariant();
        PhoneNumber = Normalize(phoneNumber);
    }

    public void UpdateAvatar(string? avatarUrl)
    {
        AvatarUrl = Normalize(avatarUrl);
    }

    public void RecordVisit(DateTimeOffset visitedAt)
    {
        LastVisitAt = visitedAt;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
