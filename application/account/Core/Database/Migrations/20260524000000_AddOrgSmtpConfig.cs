using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Wave 2 — EE Custom SMTP: creates the <c>org_smtp_configs</c> table.
///     <list type="bullet">
///         <item>One row per organization; enforced by a unique index on <c>tenant_id</c>.</item>
///         <item>The SMTP password is stored encrypted via ASP.NET Core Data Protection.</item>
///         <item>Cascade-deletes when the parent organization tenant is removed.</item>
///     </list>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260524000000_AddOrgSmtpConfig")]
public sealed class AddOrgSmtpConfig : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "org_smtp_configs",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                host = table.Column<string>("text", nullable: false),
                port = table.Column<int>("integer", nullable: false),
                use_ssl = table.Column<bool>("boolean", nullable: false),
                username = table.Column<string>("text", nullable: false),
                encrypted_password = table.Column<string>("text", nullable: false),
                from_email = table.Column<string>("text", nullable: false),
                from_name = table.Column<string>("text", nullable: true),
                reply_to_email = table.Column<string>("text", nullable: true),
                is_enabled = table.Column<bool>("boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_org_smtp_configs", x => x.id);
                table.ForeignKey(
                    "fk_org_smtp_configs_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Unique: one SMTP config per organization.
        migrationBuilder.CreateIndex(
            "uix_org_smtp_configs_tenant_id",
            "org_smtp_configs",
            "tenant_id",
            unique: true
        );
    }
}
