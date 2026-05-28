using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Webhooks.Domain;

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.MapStronglyTypedUuid<WebhookDelivery, WebhookDeliveryId>(delivery => delivery.Id);
        builder.MapStronglyTypedLongId<WebhookDelivery, TenantId>(delivery => delivery.TenantId);
        builder.MapStronglyTypedUuid<WebhookDelivery, WebhookId>(delivery => delivery.WebhookId);

        builder.Property(delivery => delivery.EventType).HasConversion<string>().HasMaxLength(40);
        builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(delivery => delivery.PayloadJson).HasColumnType("text");
        builder.Property(delivery => delivery.RequestHeadersJson).HasColumnType("text");
        builder.Property(delivery => delivery.RequestUrl).HasMaxLength(2000);
        builder.Property(delivery => delivery.ResponseBody).HasColumnType("text");

        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAt });
        builder.HasIndex(delivery => delivery.WebhookId);
    }
}
