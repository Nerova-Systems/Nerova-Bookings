using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.EventTypes.Domain;

public sealed class HostConfiguration : IEntityTypeConfiguration<Host>
{
    public void Configure(EntityTypeBuilder<Host> builder)
    {
        builder.MapStronglyTypedUuid<Host, HostId>(host => host.Id);
        builder.MapStronglyTypedLongId<Host, TenantId>(host => host.TenantId);
        builder.MapStronglyTypedUuid<Host, EventTypeId>(host => host.EventTypeId);
        builder.MapStronglyTypedUuid<Host, UserId>(host => host.UserId);

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(host => host.EventTypeId)
            .HasPrincipalKey(eventType => eventType.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(host => new { host.EventTypeId, host.UserId }).IsUnique();
        builder.HasIndex(host => host.EventTypeId);
    }
}
