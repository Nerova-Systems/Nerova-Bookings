using System.Collections.Immutable;
using System.Text.Json;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Subscriptions.Domain;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.MapStronglyTypedUuid<Subscription, SubscriptionId>(s => s.Id);
        builder.MapStronglyTypedLongId<Subscription, TenantId>(s => s.TenantId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId);
        builder.MapStronglyTypedNullableId<Subscription, PaystackCustomerId, string>(s => s.PaystackCustomerId);
        builder.MapStronglyTypedNullableId<Subscription, PaystackSubscriptionId, string>(s => s.PaystackSubscriptionId);
        builder.HasIndex(s => s.PaystackCustomerId)
            .HasDatabaseName("ix_subscriptions_paystack_customer_id")
            .HasFilter("paystack_customer_id IS NOT NULL");

        builder.Property(s => s.Plan)
            .HasConversion(
                v => v.ToString(),
                v => v == "Trial" ? SubscriptionPlan.Basis : Enum.Parse<SubscriptionPlan>(v)
            );

        builder.Property(s => s.ScheduledPlan)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v == null || v == "Trial" ? null : Enum.Parse<SubscriptionPlan>(v)
            );

        builder.Property(s => s.CurrentPriceAmount).HasPrecision(18, 2);

        builder.Property(s => s.PaymentTransactions)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.ToArray(), JsonSerializerOptions),
                v => JsonSerializer.Deserialize<ImmutableArray<PaymentTransaction>>(v, JsonSerializerOptions)
            )
            .Metadata.SetValueComparer(new ValueComparer<ImmutableArray<PaymentTransaction>>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c
                )
            );

        builder.OwnsOne(s => s.PaymentMethod, b => b.ToJson());

        builder.Property(s => s.BillingInfo)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => v == null ? null : JsonSerializer.Deserialize<BillingInfo>(v, JsonSerializerOptions)
            );
    }
}
