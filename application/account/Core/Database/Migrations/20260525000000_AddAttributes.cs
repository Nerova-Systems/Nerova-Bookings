using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Implements the l2-attributes slice:
///     <list type="bullet">
///         <item>Creates the <c>attributes</c> table — org-defined custom member fields.</item>
///         <item>
///             Creates the <c>attribute_options</c> owned table for SingleSelect / MultiSelect options.
///             Uses a surrogate <c>BIGSERIAL</c> PK so EF Core correctly inserts new options added to a
///             tracked aggregate (mirrors the same pattern used in <c>role_permissions</c>).
///         </item>
///         <item>Creates the <c>attribute_assignments</c> table — membership→attribute value links.</item>
///     </list>
///     <para>
///         All attribute repository methods bypass the tenant query filter with
///         <c>IgnoreQueryFilters([QueryFilterNames.Tenant])</c> because the attribute
///         <c>tenant_id</c> is an <em>org</em> tenant ID, not the executing user's solo tenant ID.
///     </para>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260525000000_AddAttributes")]
public sealed class AddAttributes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── attributes table ─────────────────────────────────────────────────

        migrationBuilder.CreateTable(
            "attributes",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                name = table.Column<string>("text", nullable: false),
                slug = table.Column<string>("text", nullable: false),
                type = table.Column<string>("text", nullable: false),
                is_weights_enabled = table.Column<bool>("boolean", nullable: false),
                enabled = table.Column<bool>("boolean", nullable: false),
                is_locked = table.Column<bool>("boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_attributes", x => x.id);
                table.ForeignKey(
                    "fk_attributes_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Slug unique within an organisation.
        migrationBuilder.CreateIndex(
            "uix_attributes_tenant_id_slug",
            "attributes",
            ["tenant_id", "slug"],
            unique: true
        );

        // Lookup: all attributes for a given org.
        migrationBuilder.CreateIndex(
            "ix_attributes_tenant_id",
            "attributes",
            "tenant_id"
        );

        // ─── attribute_options table ──────────────────────────────────────────
        // Owned by Attribute via OwnsMany. Surrogate BIGSERIAL PK so EF Core
        // treats newly-added options as Added (INSERT) rather than Modified (UPDATE).

        migrationBuilder.CreateTable(
            "attribute_options",
            table => new
            {
                id = table.Column<long>("bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                attribute_id = table.Column<string>("text", nullable: false),
                attribute_option_id = table.Column<string>("text", nullable: false),
                value = table.Column<string>("text", nullable: false),
                slug = table.Column<string>("text", nullable: false),
                is_group = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                contains = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_attribute_options", x => x.id);
                table.ForeignKey(
                    "fk_attribute_options_attributes_attribute_id",
                    x => x.attribute_id,
                    "attributes",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Slug unique within an attribute.
        migrationBuilder.CreateIndex(
            "uix_attribute_options_attribute_id_slug",
            "attribute_options",
            ["attribute_id", "slug"],
            unique: true
        );

        // ─── attribute_assignments table ──────────────────────────────────────

        migrationBuilder.CreateTable(
            "attribute_assignments",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                membership_id = table.Column<string>("text", nullable: false),
                attribute_id = table.Column<string>("text", nullable: false),
                attribute_option_id = table.Column<string>("text", nullable: true),
                value = table.Column<string>("text", nullable: true),
                weight = table.Column<int>("integer", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_attribute_assignments", x => x.id);
                table.ForeignKey(
                    "fk_attribute_assignments_attributes_attribute_id",
                    x => x.attribute_id,
                    "attributes",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    "fk_attribute_assignments_memberships_membership_id",
                    x => x.membership_id,
                    "memberships",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Unique: one row per (membership, attribute, option) — enforces no duplicate assignments.
        migrationBuilder.CreateIndex(
            "uix_attribute_assignments_membership_attribute_option",
            "attribute_assignments",
            ["membership_id", "attribute_id", "attribute_option_id"],
            unique: true
        );

        // Lookup: all assignments for a given membership.
        migrationBuilder.CreateIndex(
            "ix_attribute_assignments_membership_id",
            "attribute_assignments",
            "membership_id"
        );

        // Lookup: all assignments for a given attribute.
        migrationBuilder.CreateIndex(
            "ix_attribute_assignments_attribute_id",
            "attribute_assignments",
            "attribute_id"
        );

        // Lookup: all assignments in an org.
        migrationBuilder.CreateIndex(
            "ix_attribute_assignments_tenant_id",
            "attribute_assignments",
            "tenant_id"
        );
    }
}
