using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.WhatsApp.Domain;

public sealed class WabaConfigurationConfiguration : IEntityTypeConfiguration<WabaConfiguration>
{
    public void Configure(EntityTypeBuilder<WabaConfiguration> builder)
    {
        builder.MapStronglyTypedLongId<WabaConfiguration, WabaConfigurationId>(w => w.Id);
        builder.MapStronglyTypedLongId<WabaConfiguration, TenantId>(w => w.TenantId);

        // One configuration per tenant.
        builder.HasIndex(w => w.TenantId)
            .IsUnique()
            .HasDatabaseName("uix_waba_configurations_tenant_id");

        // WABA IDs must be globally unique.
        builder.HasIndex(w => w.WabaId)
            .IsUnique()
            .HasDatabaseName("uix_waba_configurations_waba_id");
    }
}
