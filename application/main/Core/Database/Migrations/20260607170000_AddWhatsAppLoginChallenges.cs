using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260607170000_AddWhatsAppLoginChallenges")]
public sealed class AddWhatsAppLoginChallenges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "whats_app_login_challenges",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                phone_number = table.Column<string>("text", nullable: false),
                email = table.Column<string>("text", nullable: false),
                otp_hash = table.Column<string>("text", nullable: false),
                otp_salt = table.Column<string>("text", nullable: false),
                is_consumed = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                expires_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_whats_app_login_challenges", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_whats_app_login_challenges_tenant_id", "whats_app_login_challenges", "tenant_id");

        // One active challenge per customer phone number within a tenant.
        migrationBuilder.CreateIndex(
            "ix_whats_app_login_challenges_tenant_id_phone_number",
            "whats_app_login_challenges",
            ["tenant_id", "phone_number"],
            unique: true
        );
    }
}
