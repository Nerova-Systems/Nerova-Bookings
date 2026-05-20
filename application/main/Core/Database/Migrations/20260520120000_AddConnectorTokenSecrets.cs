using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260520120000_AddConnectorTokenSecrets")]
public sealed class AddConnectorTokenSecrets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "connector_token_secrets",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                credential_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                protected_payload = table.Column<string>("text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_connector_token_secrets", x => x.id);
                table.ForeignKey(
                    "fk_connector_token_secrets_connector_credentials_credential_id",
                    x => x.credential_id,
                    "connector_credentials",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            "ix_connector_credentials_tenant_id_owner_user_id_integration_external_account_id",
            "connector_credentials",
            ["tenant_id", "owner_user_id", "integration", "external_account_id"],
            unique: true
        );
        migrationBuilder.CreateIndex("ix_connector_token_secrets_credential_id", "connector_token_secrets", "credential_id");
        migrationBuilder.CreateIndex("ix_connector_token_secrets_tenant_id_id", "connector_token_secrets", ["tenant_id", "id"]);
        migrationBuilder.CreateIndex(
            "ix_connector_token_secrets_tenant_id_credential_id",
            "connector_token_secrets",
            ["tenant_id", "credential_id"],
            unique: true
        );
    }
}
