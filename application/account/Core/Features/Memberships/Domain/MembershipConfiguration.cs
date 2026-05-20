using Account.Features.Permissions.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Memberships.Domain;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.MapStronglyTypedUuid<Membership, MembershipId>(m => m.Id);
        builder.MapStronglyTypedLongId<Membership, TenantId>(m => m.TenantId);
        builder.MapStronglyTypedUuid<Membership, UserId>(m => m.UserId);
        builder.MapStronglyTypedNullableId<Membership, UserId, string>(m => m.InvitedBy);
        builder.MapStronglyTypedNullableId<Membership, RoleId, string>(m => m.CustomRoleId);

        // Tenant (Team/Org): Cascade — deleting a Team/Org removes all its memberships.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Custom PBAC role: Restrict — prevent deletion of a role while memberships reference it.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(m => m.CustomRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Boolean columns need HasDefaultValue(false) so that EnsureCreated() generates DEFAULT 0
        // in the SQLite test schema. Without this, raw-SQL inserts that omit these columns fail
        // with a NOT NULL constraint.
        builder.Property(m => m.Accepted).HasDefaultValue(false);
        builder.Property(m => m.DisableImpersonation).HasDefaultValue(false);

        // Unique: a user can only have one membership per team/org.
        builder.HasIndex(m => new { m.UserId, m.TenantId })
            .IsUnique()
            .HasDatabaseName("uix_memberships_user_id_tenant_id");

        // Lookup: all members of a given team/org.
        builder.HasIndex(m => m.TenantId)
            .HasDatabaseName("ix_memberships_tenant_id");

        // Lookup: all teams/orgs a given user belongs to.
        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("ix_memberships_user_id");

        // Lookup: accept-invite flow by token.
        builder.HasIndex(m => m.InviteToken)
            .HasDatabaseName("ix_memberships_invite_token");
    }
}
