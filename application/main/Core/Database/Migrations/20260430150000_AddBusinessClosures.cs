using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260430150000_AddBusinessClosures")]
public sealed class AddBusinessClosures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "business_closures",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                start_date = table.Column<DateOnly>("date", nullable: false),
                end_date = table.Column<DateOnly>("date", nullable: false),
                label = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                type = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_business_closures", x => x.id)
        );

        migrationBuilder.CreateIndex(
            "ix_business_closures_tenant_id_start_date_end_date",
            "business_closures",
            new[] { "tenant_id", "start_date", "end_date" }
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("business_closures");
    }
}
