using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.WhatsAppFlows.Domain;

public sealed class TenantFlowConfigConfiguration : IEntityTypeConfiguration<TenantFlowConfig>
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Default;

    public void Configure(EntityTypeBuilder<TenantFlowConfig> builder)
    {
        builder.MapStronglyTypedLongId<TenantFlowConfig, TenantFlowConfigId>(t => t.Id);
        builder.MapStronglyTypedLongId<TenantFlowConfig, TenantId>(t => t.TenantId);

        builder.HasIndex(t => t.TenantId)
            .IsUnique()
            .HasDatabaseName("uix_tenant_flow_configs_tenant_id");

        builder.Property(t => t.BusinessVertical).HasConversion(v => v.ToString(), v => Enum.Parse<BusinessVertical>(v)).HasMaxLength(40);
        builder.Property(t => t.StaffAssignment).HasConversion(v => v.ToString(), v => Enum.Parse<StaffAssignment>(v)).HasMaxLength(40);
        builder.Property(t => t.PaymentTiming).HasConversion(v => v.ToString(), v => Enum.Parse<PaymentTiming>(v)).HasMaxLength(40);

        builder.Property(t => t.ConfirmationMessageTemplate).HasMaxLength(1000);
        builder.Property(t => t.CancellationContact).HasMaxLength(500);
        builder.Property(t => t.ConfigVersionHash).HasMaxLength(64);

        // Owned list serialized as a single JSON column. We use a value-conversion + EF backing
        // field strategy so the collection round-trips cleanly without OwnsMany overhead.
        builder.Property<List<CustomQuestion>>("_customPreBookingQuestions")
            .HasColumnName("custom_pre_booking_questions")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => string.IsNullOrEmpty(v)
                    ? new List<CustomQuestion>()
                    : JsonSerializer.Deserialize<List<CustomQuestion>>(v, JsonOptions) ?? new List<CustomQuestion>(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<CustomQuestion>>(
                    (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                    v => JsonSerializer.Deserialize<List<CustomQuestion>>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!
                )
            );

        builder.Ignore(t => t.CustomPreBookingQuestions);
    }
}
