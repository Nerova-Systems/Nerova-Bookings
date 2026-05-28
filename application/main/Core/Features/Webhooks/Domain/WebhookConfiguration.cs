using Main.Features.EventTypes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Webhooks.Domain;

public sealed class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> builder)
    {
        builder.MapStronglyTypedUuid<Webhook, WebhookId>(webhook => webhook.Id);
        builder.MapStronglyTypedLongId<Webhook, TenantId>(webhook => webhook.TenantId);
        builder.MapStronglyTypedNullableId<Webhook, UserId, string>(webhook => webhook.UserId);
        builder.MapStronglyTypedNullableId<Webhook, EventTypeId, string>(webhook => webhook.EventTypeId);

        builder.Property(webhook => webhook.TargetUrl).HasMaxLength(2000);
        builder.Property(webhook => webhook.Secret).HasMaxLength(200);
        builder.Property(webhook => webhook.EventSubscriptionsJson).HasColumnType("text");
        builder.Property(webhook => webhook.Active);

        builder.HasIndex(webhook => new { webhook.TenantId, webhook.Active });
    }
}
