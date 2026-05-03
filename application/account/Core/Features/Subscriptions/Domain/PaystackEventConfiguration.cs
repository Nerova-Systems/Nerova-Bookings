using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Subscriptions.Domain;

public sealed class PaystackEventConfiguration : IEntityTypeConfiguration<PaystackEvent>
{
    public void Configure(EntityTypeBuilder<PaystackEvent> builder)
    {
        builder.MapStronglyTypedString(e => e.Id);
        builder.MapStronglyTypedNullableId<PaystackEvent, PaystackCustomerId, string>(e => e.PaystackCustomerId);
        builder.MapStronglyTypedNullableId<PaystackEvent, PaystackSubscriptionId, string>(e => e.PaystackSubscriptionId);
        builder.MapStronglyTypedNullableLongId<PaystackEvent, TenantId>(e => e.TenantId);
        builder.Property(e => e.Payload).HasColumnType("jsonb");
    }
}
