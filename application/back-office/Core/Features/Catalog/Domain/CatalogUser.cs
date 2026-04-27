using SharedKernel.Domain;

namespace BackOffice.Features.Catalog.Domain;

public sealed class CatalogUser
{
    private CatalogUser()
    {
    }

    private CatalogUser(UserId id)
    {
        Id = id;
    }

    public UserId Id { get; private set; } = null!;

    public TenantId TenantId { get; private set; } = null!;

    public string Email { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public bool EmailConfirmed { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset SourceUpdatedAt { get; private set; }

    public static CatalogUser Create(UserId id)
    {
        return new CatalogUser(id);
    }

    public void Upsert(TenantId tenantId, string email, string role, string firstName, string lastName, string title, bool emailConfirmed, DateTimeOffset createdAt, DateTimeOffset? modifiedAt, DateTimeOffset? lastSeenAt, DateTimeOffset sourceUpdatedAt)
    {
        if (SourceUpdatedAt > sourceUpdatedAt) return;

        TenantId = tenantId;
        Email = email;
        Role = role;
        FirstName = firstName;
        LastName = lastName;
        Title = title;
        EmailConfirmed = emailConfirmed;
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
        LastSeenAt = lastSeenAt;
        DeletedAt = null;
        SourceUpdatedAt = sourceUpdatedAt;
    }

    public void MarkDeleted(TenantId tenantId, DateTimeOffset deletedAt, DateTimeOffset sourceUpdatedAt)
    {
        if (SourceUpdatedAt > sourceUpdatedAt) return;

        TenantId = tenantId;
        DeletedAt = deletedAt;
        SourceUpdatedAt = sourceUpdatedAt;
    }
}
