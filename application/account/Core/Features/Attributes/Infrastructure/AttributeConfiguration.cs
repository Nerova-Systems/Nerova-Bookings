using System.Text.Json;
using Account.Features.Attributes.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Attributes.Infrastructure;

public sealed class AttributeConfiguration : IEntityTypeConfiguration<Domain.Attribute>
{
    public void Configure(EntityTypeBuilder<Domain.Attribute> builder)
    {
        builder.ToTable("attributes");

        builder.MapStronglyTypedUuid<Domain.Attribute, AttributeId>(a => a.Id);
        builder.MapStronglyTypedLongId<Domain.Attribute, TenantId>(a => a.TenantId);

        builder.Property(a => a.Name);
        builder.Property(a => a.Slug);

        // Referential integrity: cascade-delete attributes when their owning org is deleted.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── Options collection ───────────────────────────────────────────────
        // AttributeOption is an owned entity stored in a separate table via OwnsMany.
        builder.OwnsMany(a => a.Options, optBuilder =>
        {
            optBuilder.ToTable("attribute_options");

            // Shadow FK: "attribute_id" links each option row back to its owning attribute.
            optBuilder.WithOwner().HasForeignKey("attribute_id");

            // Surrogate PK — without this, EF treats non-null client-generated string keys as
            // existing rows and issues UPDATE instead of INSERT for new options, causing a
            // DbUpdateConcurrencyException. A shadow long key (value 0 = default = new entity)
            // lets EF correctly identify new options as Added. Mirrors RoleConfiguration.Permission.
            optBuilder.Property<long>("id").ValueGeneratedOnAdd();
            optBuilder.HasKey("id");

            // AttributeOptionId is a regular column for stable, API-visible option identity.
            optBuilder.Property(o => o.Id)
                .HasConversion(v => v.Value, v => new AttributeOptionId(v))
                .HasColumnName("attribute_option_id");

            optBuilder.Property(o => o.Value);
            optBuilder.Property(o => o.Slug);
            optBuilder.Property(o => o.IsGroup).HasDefaultValue(false);

            // Contains: JSON array of child option slugs. Stored as TEXT (SQLite compat).
            optBuilder.Property(o => o.Contains)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>()
                )
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                    v => v.ToArray()
                ));

            // Slug must be unique within an attribute.
            optBuilder.HasIndex("attribute_id", nameof(AttributeOption.Slug))
                .IsUnique()
                .HasDatabaseName("uix_attribute_options_attribute_id_slug");
        });

        builder.Navigation(a => a.Options)
            .HasField("_options")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ─── Uniqueness constraints ───────────────────────────────────────────

        // Slug must be unique per organisation.
        builder.HasIndex(a => new { a.TenantId, a.Slug })
            .IsUnique()
            .HasDatabaseName("uix_attributes_tenant_id_slug");

        // Lookup: all attributes for a given org.
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("ix_attributes_tenant_id");
    }
}
