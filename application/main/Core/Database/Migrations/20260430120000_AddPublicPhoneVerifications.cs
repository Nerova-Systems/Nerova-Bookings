using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260430120000_AddPublicPhoneVerifications")]
public sealed class AddPublicPhoneVerifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "public_phone_verifications",
            table => new
            {
                id = table.Column<string>("character varying(255)", nullable: false),
                tenant_id = table.Column<long>(nullable: false),
                phone = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                masked_phone = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                provider = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                provider_sid = table.Column<string>("character varying(80)", maxLength: 80, nullable: true),
                status = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                verification_token_hash = table.Column<string>("character varying(128)", maxLength: 128, nullable: true),
                expires_at = table.Column<DateTimeOffset>(nullable: false),
                verified_at = table.Column<DateTimeOffset>(nullable: true),
                consumed_at = table.Column<DateTimeOffset>(nullable: true),
                created_at = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_public_phone_verifications", x => x.id)
        );

        migrationBuilder.CreateIndex(
            "ix_public_phone_verifications_tenant_id_phone_status",
            "public_phone_verifications",
            ["tenant_id", "phone", "status"]
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("public_phone_verifications");
    }
}
