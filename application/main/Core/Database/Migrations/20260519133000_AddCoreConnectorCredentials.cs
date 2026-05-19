using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260519133000_AddCoreConnectorCredentials")]
public sealed class AddCoreConnectorCredentials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "connector_credentials",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                owner_user_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                integration = table.Column<string>("text", nullable: false),
                external_account_id = table.Column<string>("text", nullable: false),
                account_email = table.Column<string>("text", nullable: false),
                display_name = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                secret_reference = table.Column<string>("text", nullable: false),
                calendars_json = table.Column<string>("jsonb", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_connector_credentials", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_connector_credentials_tenant_id_owner_user_id_id", "connector_credentials", ["tenant_id", "owner_user_id", "id"]);
        migrationBuilder.CreateIndex("ix_connector_credentials_tenant_id_owner_user_id_integration", "connector_credentials", ["tenant_id", "owner_user_id", "integration"]);
    }
}
