using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260613002000_AddVerticalFields")]
public sealed class AddVerticalFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("vertical", "scheduling_profiles", "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>("vertical_fields", "clients", "jsonb", nullable: false, defaultValue: "{}");
        migrationBuilder.AddColumn<string>("sensitive_fields", "clients", "text", nullable: true);
    }
}
