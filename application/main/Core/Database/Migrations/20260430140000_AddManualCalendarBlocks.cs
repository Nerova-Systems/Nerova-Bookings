using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260430140000_AddManualCalendarBlocks")]
public sealed class AddManualCalendarBlocks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "manual_calendar_blocks",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                staff_member_id = table.Column<string>("character varying(64)", maxLength: 64, nullable: true),
                title = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                start_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                end_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_manual_calendar_blocks", x => x.id)
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("manual_calendar_blocks");
    }
}
