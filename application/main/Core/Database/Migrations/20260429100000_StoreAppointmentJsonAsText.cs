using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260429100000_StoreAppointmentJsonAsText")]
public sealed class StoreAppointmentJsonAsText : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            "answers_json",
            "appointments",
            "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "jsonb"
        );

        migrationBuilder.AlterColumn<string>(
            "payload_json",
            "appointment_flow_events",
            "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "jsonb"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            "answers_json",
            "appointments",
            "jsonb",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            "payload_json",
            "appointment_flow_events",
            "jsonb",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );
    }
}
