using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Wave 3 — EE Microsoft SSO: creates the <c>org_sso_configs</c> table.
///     <list type="bullet">
///         <item>One row per organization per SSO provider; enforced by a unique index on <c>(tenant_id, provider)</c>.</item>
///         <item>The provider credentials are stored encrypted via ASP.NET Core Data Protection.</item>
///         <item>Cascade-deletes when the parent organization tenant is removed.</item>
///     </list>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260526000000_AddOrgSsoConfig")]
public sealed class AddOrgSsoConfig : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "org_sso_configs",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                provider = table.Column<string>("text", nullable: false),
                encrypted_provider_config = table.Column<string>("text", nullable: false),
                allowed_domains_json = table.Column<string>("jsonb", nullable: false),
                is_enabled = table.Column<bool>("boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_org_sso_configs", x => x.id);
                table.ForeignKey(
                    "fk_org_sso_configs_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Unique: one SSO config per organization per provider.
        migrationBuilder.CreateIndex(
            "uix_org_sso_configs_tenant_id_provider",
            "org_sso_configs",
            ["tenant_id", "provider"],
            unique: true
        );
    }
}
