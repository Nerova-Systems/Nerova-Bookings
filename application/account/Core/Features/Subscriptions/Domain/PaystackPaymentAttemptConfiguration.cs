using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Subscriptions.Domain;

public sealed class PaystackPaymentAttemptConfiguration : IEntityTypeConfiguration<PaystackPaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaystackPaymentAttempt> builder)
    {
        builder.MapStronglyTypedUuid<PaystackPaymentAttempt, PaystackPaymentAttemptId>(a => a.Id);
        builder.MapStronglyTypedLongId<PaystackPaymentAttempt, TenantId>(a => a.TenantId);
        builder.MapStronglyTypedUuid<PaystackPaymentAttempt, SubscriptionId>(a => a.SubscriptionId);
        builder.MapStronglyTypedId<PaystackPaymentAttempt, PaystackCustomerId, string>(a => a.PaystackCustomerId);
        builder.MapStronglyTypedNullableId<PaystackPaymentAttempt, PaystackAuthorizationCode, string>(a => a.PaystackAuthorizationCode);

        builder.HasOne<Subscription>().WithMany().HasForeignKey(a => a.SubscriptionId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId);

        builder.Property(a => a.PaystackCustomerId).HasColumnName("paystack_customer_code");
        builder.Property(a => a.PaystackAuthorizationCode).HasColumnName("paystack_authorization_code");
        builder.Property(a => a.Purpose);
        builder.Property(a => a.Plan);
        builder.Property(a => a.Amount).HasPrecision(18, 2);
        builder.Property(a => a.Currency);

        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.SubscriptionId);
        builder.HasIndex(a => a.PaystackReference).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.Status });
    }
}
