using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Attributes.Infrastructure;

public sealed class AttributeAssignmentConfiguration : IEntityTypeConfiguration<AttributeAssignment>
{
    public void Configure(EntityTypeBuilder<AttributeAssignment> builder)
    {
        builder.ToTable("attribute_assignments");

        builder.MapStronglyTypedUuid<AttributeAssignment, AttributeAssignmentId>(a => a.Id);

        builder.MapStronglyTypedLongId<AttributeAssignment, TenantId>(a => a.TenantId);
        builder.MapStronglyTypedUuid<AttributeAssignment, MembershipId>(a => a.MembershipId);
        builder.MapStronglyTypedUuid<AttributeAssignment, AttributeId>(a => a.AttributeId);

        // AttributeOptionId is nullable (null for Text / Number attribute types).
        builder.Property(a => a.AttributeOptionId)
            .HasConversion(
                v => v == null ? null : v.Value,
                v => v == null ? null : new AttributeOptionId(v)
            )
            .IsRequired(false);

        builder.Property(a => a.Value).IsRequired(false);
        builder.Property(a => a.Weight);

        // FK: membership (restrict — delete membership explicitly first)
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(a => a.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: attribute (cascade — if an attribute is deleted, its assignments vanish)
        builder.HasOne<Domain.Attribute>()
            .WithMany()
            .HasForeignKey(a => a.AttributeId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── Indexes ──────────────────────────────────────────────────────────

        // Unique per (membership, attribute, option) — one row per option per member.
        // Allows NULL optionId (Text/Number single assignment per member per attribute).
        builder.HasIndex(a => new { a.MembershipId, a.AttributeId, a.AttributeOptionId })
            .IsUnique()
            .HasDatabaseName("uix_attribute_assignments_membership_attribute_option");

        // Lookup: all assignments for a given membership.
        builder.HasIndex(a => a.MembershipId)
            .HasDatabaseName("ix_attribute_assignments_membership_id");

        // Lookup: all assignments for a given attribute (e.g. when deleting an attribute option).
        builder.HasIndex(a => a.AttributeId)
            .HasDatabaseName("ix_attribute_assignments_attribute_id");

        // Lookup: all assignments in an org.
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("ix_attribute_assignments_tenant_id");
    }
}
