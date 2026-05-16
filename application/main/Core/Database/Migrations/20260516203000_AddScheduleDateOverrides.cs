using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260516203000_AddScheduleDateOverrides")]
public sealed class AddScheduleDateOverrides : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "date_overrides",
            "schedules",
            "jsonb",
            nullable: false,
            defaultValue: "[]"
        );
    }
}
