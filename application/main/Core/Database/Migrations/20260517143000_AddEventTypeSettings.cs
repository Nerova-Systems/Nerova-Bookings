using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260517143000_AddEventTypeSettings")]
public sealed class AddEventTypeSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "settings",
            "event_types",
            "jsonb",
            nullable: false,
            defaultValue: "{}"
        );
    }
}
