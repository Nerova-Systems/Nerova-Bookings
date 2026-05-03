using Account.Features.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Tenants.Domain;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.MapStronglyTypedLongId<Tenant, TenantId>(t => t.Id);

        builder.Property(t => t.Plan)
            .HasConversion(
                v => v.ToString(),
                v => v == "Trial" ? SubscriptionPlan.Basis : Enum.Parse<SubscriptionPlan>(v)
            );

        builder.OwnsOne(t => t.Logo, b => b.ToJson());
    }
}
