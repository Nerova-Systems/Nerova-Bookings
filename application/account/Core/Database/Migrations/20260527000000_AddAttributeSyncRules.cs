using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Wave 4 — EE IdP Attribute Sync: creates the <c>attribute_sync_rules</c> table.
///     <list type="bullet">
///         <item>
///             One row per rule per organisation; each rule maps a single SAML/OIDC claim key to an
///             <c>Attribute</c> aggregate via three mapping modes: <c>Direct</c>, <c>Lookup</c>,
///             and <c>Group</c>.
///         </item>
///         <item>
///             Cascade-deletes when the parent organisation tenant is removed.
///             <c>attribute_id</c> is intentionally a free-text FK (no referential constraint) so
///             rules survive independent attribute deletion — the sync service skips missing
///             attributes gracefully.
///         </item>
///         <item>Indexes on <c>tenant_id</c> and the composite <c>(tenant_id, is_enabled)</c>
///             so the hot-path per-login query (enabled rules for an org) uses an index-only scan.</item>
///     </list>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260527000000_AddAttributeSyncRules")]
public sealed class AddAttributeSyncRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "attribute_sync_rules",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                attribute_id = table.Column<string>("text", nullable: false),
                claim_path = table.Column<string>("text", nullable: false),
                mode = table.Column<string>("text", nullable: false),
                auto_create_options = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                is_enabled = table.Column<bool>("boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_attribute_sync_rules", x => x.id);
                table.ForeignKey(
                    "fk_attribute_sync_rules_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Lookup: all rules for a given org.
        migrationBuilder.CreateIndex(
            "ix_attribute_sync_rules_tenant_id",
            "attribute_sync_rules",
            "tenant_id"
        );

        // Hot-path: enabled rules per org — used on every SSO login.
        migrationBuilder.CreateIndex(
            "ix_attribute_sync_rules_tenant_id_is_enabled",
            "attribute_sync_rules",
            ["tenant_id", "is_enabled"]
        );
    }
}
