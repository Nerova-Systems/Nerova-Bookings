using SharedKernel.Domain;

namespace BackOffice.Features.Catalog.Domain;

public sealed class CatalogTenant
{
    private CatalogTenant()
    {
    }

    private CatalogTenant(TenantId id)
    {
        Id = id;
    }

    public TenantId Id { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public string Plan { get; private set; } = string.Empty;

    public string? LogoUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset SourceUpdatedAt { get; private set; }

    public static CatalogTenant Create(TenantId id)
    {
        return new CatalogTenant(id);
    }

    public void Upsert(string name, string state, string plan, string? logoUrl, DateTimeOffset createdAt, DateTimeOffset? modifiedAt, DateTimeOffset sourceUpdatedAt)
    {
        if (SourceUpdatedAt > sourceUpdatedAt) return;

        Name = name;
        State = state;
        Plan = plan;
        LogoUrl = logoUrl;
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
        DeletedAt = null;
        SourceUpdatedAt = sourceUpdatedAt;
    }

    public void MarkDeleted(DateTimeOffset deletedAt, DateTimeOffset sourceUpdatedAt)
    {
        if (SourceUpdatedAt > sourceUpdatedAt) return;

        DeletedAt = deletedAt;
        SourceUpdatedAt = sourceUpdatedAt;
    }
}
