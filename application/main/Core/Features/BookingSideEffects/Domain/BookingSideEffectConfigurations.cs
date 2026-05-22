using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.BookingSideEffects.Domain;

public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflows");

        builder.MapStronglyTypedUuid<Workflow, WorkflowId>(workflow => workflow.Id);
        builder.MapStronglyTypedLongId<Workflow, TenantId>(workflow => workflow.TenantId);
        builder.MapStronglyTypedUuid<Workflow, UserId>(workflow => workflow.OwnerUserId);
        builder.MapStronglyTypedUuid<Workflow, EventTypeId>(workflow => workflow.EventTypeId);

        builder.Property(workflow => workflow.Name).HasMaxLength(160);
        builder.Property(workflow => workflow.Trigger).HasMaxLength(80);
        builder.Property(workflow => workflow.StepsJson).HasColumnType("jsonb");

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(workflow => workflow.EventTypeId)
            .HasPrincipalKey(eventType => eventType.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workflow => new { workflow.TenantId, workflow.EventTypeId, workflow.Trigger });
    }
}

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.MapStronglyTypedUuid<WebhookSubscription, WebhookSubscriptionId>(subscription => subscription.Id);
        builder.MapStronglyTypedLongId<WebhookSubscription, TenantId>(subscription => subscription.TenantId);
        builder.MapStronglyTypedUuid<WebhookSubscription, UserId>(subscription => subscription.OwnerUserId);
        builder.MapStronglyTypedUuid<WebhookSubscription, EventTypeId>(subscription => subscription.EventTypeId);

        builder.Property(subscription => subscription.SubscriberUrl).HasMaxLength(2048);
        builder.Property(subscription => subscription.Secret).HasMaxLength(500);
        builder.Property(subscription => subscription.TriggersJson).HasColumnType("jsonb");
        builder.Property(subscription => subscription.PayloadFormat).HasMaxLength(80);
        builder.Property(subscription => subscription.PayloadVersion).HasMaxLength(40);

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(subscription => subscription.EventTypeId)
            .HasPrincipalKey(eventType => eventType.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(subscription => new { subscription.TenantId, subscription.EventTypeId });
    }
}

public sealed class BookingSideEffectDeliveryConfiguration : IEntityTypeConfiguration<BookingSideEffectDelivery>
{
    public void Configure(EntityTypeBuilder<BookingSideEffectDelivery> builder)
    {
        builder.MapStronglyTypedUuid<BookingSideEffectDelivery, BookingSideEffectDeliveryId>(delivery => delivery.Id);
        builder.MapStronglyTypedLongId<BookingSideEffectDelivery, TenantId>(delivery => delivery.TenantId);
        builder.MapStronglyTypedUuid<BookingSideEffectDelivery, BookingId>(delivery => delivery.BookingId);
        builder.MapStronglyTypedUuid<BookingSideEffectDelivery, EventTypeId>(delivery => delivery.EventTypeId);

        builder.Property(delivery => delivery.Trigger).HasMaxLength(80);
        builder.Property(delivery => delivery.Kind).HasMaxLength(40);
        builder.Property(delivery => delivery.Status).HasMaxLength(40);
        builder.Property(delivery => delivery.LastError).HasMaxLength(2000);
        builder.Property(delivery => delivery.PayloadJson).HasColumnType("jsonb");
        builder.Property(delivery => delivery.DedupeKey).HasMaxLength(500);

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(delivery => delivery.EventTypeId)
            .HasPrincipalKey(eventType => eventType.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(delivery => new { delivery.Status, delivery.NextRetryAt });
        builder.HasIndex(delivery => delivery.DedupeKey).IsUnique();
    }
}
