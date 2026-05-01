using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.FeatureFlags.Domain;

public static class FeatureFlagKeys
{
    public const string Teams = "teams";
    public const string LocationsResources = "locations-resources";
    public const string GoogleCalendar = "google-calendar";
    public const string StaffCalendarConnectors = "staff-calendar-connectors";

    public static readonly string[] All =
    [
        Teams,
        LocationsResources,
        GoogleCalendar,
        StaffCalendarConnectors
    ];
}

public sealed class FeatureFlag : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string? UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.HasKey(flag => flag.Id);
        builder.MapStronglyTypedLongId<FeatureFlag, TenantId>(flag => flag.TenantId);
        builder.Property(flag => flag.Key).HasMaxLength(80);
        builder.Property(flag => flag.UserId).HasMaxLength(32);
        builder.HasIndex(flag => new { flag.TenantId, flag.Key, flag.UserId }).IsUnique();
    }
}
