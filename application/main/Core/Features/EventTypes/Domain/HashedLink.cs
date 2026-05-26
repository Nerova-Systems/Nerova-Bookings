using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.EventTypes.Domain;

[PublicAPI]
[IdPrefix("hlink")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, HashedLinkId>))]
public sealed record HashedLinkId(string Value) : StronglyTypedUlid<HashedLinkId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class HashedLink : AggregateRoot<HashedLinkId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private HashedLink() : base(HashedLinkId.NewId())
    {
        EventTypeId = new EventTypeId(string.Empty);
        Hash = string.Empty;
    }

    private HashedLink(TenantId tenantId, EventTypeId eventTypeId, string hash, int? expiresAfterUses, DateTimeOffset? expiresAt)
        : base(HashedLinkId.NewId())
    {
        TenantId = tenantId;
        EventTypeId = eventTypeId;
        Hash = hash;
        ExpiresAfterUses = expiresAfterUses;
        ExpiresAt = expiresAt;
    }

    public EventTypeId EventTypeId { get; private set; }

    public string Hash { get; private set; }

    public int? ExpiresAfterUses { get; private set; }

    public DateTimeOffset? ExpiresAt { get; }

    public TenantId TenantId { get; } = new(0);

    public static HashedLink Create(TenantId tenantId, EventTypeId eventTypeId, string hash, int? expiresAfterUses, DateTimeOffset? expiresAt)
    {
        return new HashedLink(tenantId, eventTypeId, hash.Trim(), expiresAfterUses, expiresAt);
    }

    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiresAt is not null && ExpiresAt.Value <= now;
    }
}
