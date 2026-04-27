using SharedKernel.Domain;

namespace SharedKernel.Catalog;

public sealed record CatalogEventEnvelope(Guid SourceEventId, string Type, string Payload, DateTimeOffset OccurredAt);

public sealed record TenantCatalogUpserted(
    TenantId TenantId,
    string Name,
    string State,
    string Plan,
    string? LogoUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt
);

public sealed record TenantCatalogDeleted(TenantId TenantId, DateTimeOffset DeletedAt);

public sealed record UserCatalogUpserted(
    UserId UserId,
    TenantId TenantId,
    string Email,
    string Role,
    string FirstName,
    string LastName,
    string Title,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    DateTimeOffset? LastSeenAt
);

public sealed record UserCatalogDeleted(UserId UserId, TenantId TenantId, DateTimeOffset DeletedAt);
