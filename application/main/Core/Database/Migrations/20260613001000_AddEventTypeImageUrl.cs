using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260613001000_AddEventTypeImageUrl")]
public sealed class AddEventTypeImageUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("image_url", "event_types", "character varying(500)", maxLength: 500, nullable: true);
    }
}
