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

        builder.OwnsOne(s => s.BillingInfo, b =>
            {
                b.ToJson();
                b.OwnsOne(i => i.Address);
            }
        );

        builder.OwnsOne(s => s.PaymentMethod, b => b.ToJson());
    }
}
