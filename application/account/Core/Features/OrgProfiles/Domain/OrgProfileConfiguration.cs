using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.OrgProfiles.Domain;

public sealed class OrgProfileConfiguration : IEntityTypeConfiguration<OrgProfile>
{
    public void Configure(EntityTypeBuilder<OrgProfile> builder)
    {
        builder.MapStronglyTypedUuid<OrgProfile, OrgProfileId>(p => p.Id);
        builder.MapStronglyTypedUuid<OrgProfile, UserId>(p => p.UserId);
        builder.MapStronglyTypedLongId<OrgProfile, TenantId>(p => p.OrgTenantId);

        // User: Restrict — cannot delete a user who has active org profiles.
        // Commands must remove or deactivate org profiles before deleting a user.
        // Mirrors the Membership.UserId FK behaviour (also Restrict).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Org tenant: Cascade — deleting an Organization removes all its member profiles.
        // Mirrors the Membership.TenantId FK behaviour (also Cascade).
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.OrgTenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: a user has at most one profile per organization.
        builder.HasIndex(p => new { p.UserId, p.OrgTenantId })
            .IsUnique()
            .HasDatabaseName("uix_org_profiles_user_id_org_tenant_id");

        // Unique: within an org, no two users share a username (URL slug).
        builder.HasIndex(p => new { p.OrgTenantId, p.Username })
            .IsUnique()
            .HasDatabaseName("uix_org_profiles_org_tenant_id_username");

        // Lookup: all org profiles for a given user ("all my org profiles").
        builder.HasIndex(p => p.UserId)
            .HasDatabaseName("ix_org_profiles_user_id");
    }
}
