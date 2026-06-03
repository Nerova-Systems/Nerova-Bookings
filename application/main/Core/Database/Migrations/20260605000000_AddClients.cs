using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260605000000_AddClients")]
public sealed class AddClients : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "clients",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                first_name = table.Column<string>("character varying(30)", maxLength: 30, nullable: false),
                last_name = table.Column<string>("character varying(30)", maxLength: 30, nullable: false),
                email = table.Column<string>("character varying(100)", maxLength: 100, nullable: true),
                phone_number = table.Column<string>("character varying(30)", maxLength: 30, nullable: true),
                avatar_url = table.Column<string>("character varying(500)", maxLength: 500, nullable: true),
                last_visit_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_clients", client => client.id); }
        );

        migrationBuilder.CreateIndex(
            "ix_clients_tenant_id_created_at",
            "clients",
            ["tenant_id", "created_at"]
        );

        migrationBuilder.CreateIndex(
            "ix_clients_tenant_id_email",
            "clients",
            ["tenant_id", "email"]
        );

        migrationBuilder.CreateIndex(
            "ix_clients_tenant_id_phone_number",
            "clients",
            ["tenant_id", "phone_number"]
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("clients");
    }
}
