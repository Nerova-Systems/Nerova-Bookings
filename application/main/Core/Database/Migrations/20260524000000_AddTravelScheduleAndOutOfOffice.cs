using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260524000000_AddTravelScheduleAndOutOfOffice")]
public sealed class AddTravelScheduleAndOutOfOffice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // --- 1. travel_schedules table ---

        migrationBuilder.CreateTable(
            "travel_schedules",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                user_id = table.Column<string>("text", nullable: false),
                start_date = table.Column<DateOnly>("date", nullable: false),
                end_date = table.Column<DateOnly>("date", nullable: false),
                time_zone = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
                schedule_id = table.Column<string>("text", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_travel_schedules", travel => travel.id)
        );

        migrationBuilder.CreateIndex(
            "ix_travel_schedules_tenant_id_user_id",
            "travel_schedules",
            ["tenant_id", "user_id"]
        );
        migrationBuilder.CreateIndex(
            "ix_travel_schedules_user_id_start_date_end_date",
            "travel_schedules",
            ["user_id", "start_date", "end_date"]
        );

        // --- 2. out_of_offices table ---

        migrationBuilder.CreateTable(
            "out_of_offices",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                user_id = table.Column<string>("text", nullable: false),
                start_date = table.Column<DateOnly>("date", nullable: false),
                end_date = table.Column<DateOnly>("date", nullable: false),
                to_user_id = table.Column<string>("text", nullable: true),
                reason = table.Column<string>("character varying(120)", maxLength: 120, nullable: true),
                notes = table.Column<string>("character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_out_of_offices", ooo => ooo.id)
        );

        migrationBuilder.CreateIndex(
            "ix_out_of_offices_tenant_id_user_id",
            "out_of_offices",
            ["tenant_id", "user_id"]
        );
        migrationBuilder.CreateIndex(
            "ix_out_of_offices_user_id_start_date_end_date",
            "out_of_offices",
            ["user_id", "start_date", "end_date"]
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("out_of_offices");
        migrationBuilder.DropTable("travel_schedules");
    }
}
