using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Creates the <c>waba_configurations</c> table, a 1:1 per-tenant store for WhatsApp Business
///     Account (WABA) onboarding state: Meta WABA IDs, RSA key pair material (encrypted), Paystack
///     subaccount code, and the composite onboarding gate status.
///     <para>
///         Unique indexes on <c>tenant_id</c> and <c>waba_id</c> enforce the 1:1 tenant constraint
///         and global WABA uniqueness at the database level.
///     </para>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260529000000_AddWabaConfiguration")]
public sealed class AddWabaConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "waba_configurations",
            table => new
            {
                id = table.Column<long>("bigint", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                waba_id = table.Column<string>("text", nullable: false),
                phone_number_id = table.Column<string>("text", nullable: true),
                display_phone_number = table.Column<string>("text", nullable: true),
                onboarding_gate_status = table.Column<string>("text", nullable: false),
                encrypted_private_key = table.Column<string>("text", nullable: true),
                private_key_iv = table.Column<string>("text", nullable: true),
                public_key_fingerprint = table.Column<string>("text", nullable: true),
                flow_id = table.Column<string>("text", nullable: true),
                flow_status = table.Column<string>("text", nullable: false),
                subaccount_code = table.Column<string>("text", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_waba_configurations", x => x.id);
                table.ForeignKey(
                    "fk_waba_configurations_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // 1:1: each tenant has at most one WABA configuration.
        migrationBuilder.CreateIndex(
            "uix_waba_configurations_tenant_id",
            "waba_configurations",
            "tenant_id",
            unique: true
        );

        // WABA IDs must be globally unique across tenants.
        migrationBuilder.CreateIndex(
            "uix_waba_configurations_waba_id",
            "waba_configurations",
            "waba_id",
            unique: true
        );
    }
}
