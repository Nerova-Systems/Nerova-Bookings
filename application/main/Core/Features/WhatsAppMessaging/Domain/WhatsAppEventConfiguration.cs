using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.EntityFramework;

namespace Main.Features.WhatsAppMessaging.Domain;

public sealed class WhatsAppEventConfiguration : IEntityTypeConfiguration<WhatsAppEvent>
{
    public void Configure(EntityTypeBuilder<WhatsAppEvent> builder)
    {
        builder.MapStronglyTypedUuid<WhatsAppEvent, WhatsAppEventId>(e => e.Id);
        builder.Property(e => e.Payload).HasColumnType("jsonb");
    }
}
