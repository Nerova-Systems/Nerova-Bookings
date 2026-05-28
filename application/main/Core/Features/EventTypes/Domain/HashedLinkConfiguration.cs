using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.EventTypes.Domain;

public sealed class HashedLinkConfiguration : IEntityTypeConfiguration<HashedLink>
{
    public void Configure(EntityTypeBuilder<HashedLink> builder)
    {
        builder.MapStronglyTypedUuid<HashedLink, HashedLinkId>(link => link.Id);
        builder.MapStronglyTypedLongId<HashedLink, TenantId>(link => link.TenantId);
        builder.MapStronglyTypedUuid<HashedLink, EventTypeId>(link => link.EventTypeId);

        builder.Property(link => link.Hash).HasMaxLength(200);

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(link => link.EventTypeId)
            .HasPrincipalKey(eventType => eventType.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(link => new { link.TenantId, link.Hash }).IsUnique();
        builder.HasIndex(link => link.EventTypeId);
    }
}
