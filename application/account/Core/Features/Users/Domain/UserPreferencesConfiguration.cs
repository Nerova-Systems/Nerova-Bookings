using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Users.Domain;

public sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.MapStronglyTypedUuid<UserPreferences, UserPreferencesId>(p => p.Id);
        builder.MapStronglyTypedUuid<UserPreferences, UserId>(p => p.UserId);

        // Cascade: deleting a user removes their preferences row automatically.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1:1 invariant — a user has at most one preferences row.
        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasDatabaseName("uix_user_preferences_user_id");

        builder.Property(p => p.TimeFormat).HasConversion<string>();
        builder.Property(p => p.WeekStart).HasConversion<string>();
    }
}
