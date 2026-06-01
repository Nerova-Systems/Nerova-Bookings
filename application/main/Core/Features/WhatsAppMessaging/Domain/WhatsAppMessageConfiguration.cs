using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.WhatsAppMessaging.Domain;

public sealed class WhatsAppMessageConfiguration : IEntityTypeConfiguration<WhatsAppMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppMessage> builder)
    {
        builder.MapStronglyTypedUuid<WhatsAppMessage, WhatsAppMessageId>(m => m.Id);
        builder.MapStronglyTypedLongId<WhatsAppMessage, TenantId>(m => m.TenantId);
    }
}
