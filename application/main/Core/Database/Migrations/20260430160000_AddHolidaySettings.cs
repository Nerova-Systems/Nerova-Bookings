using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260430160000_AddHolidaySettings")]
public sealed class AddHolidaySettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "holiday_country_code",
            "business_profiles",
            "character varying(8)",
            maxLength: 8,
            nullable: false,
            defaultValue: "ZA"
        );

        migrationBuilder.AddColumn<string>(
            "open_public_holiday_ids_json",
            "business_profiles",
            "text",
            nullable: false,
            defaultValue: "[]"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("holiday_country_code", "business_profiles");
        migrationBuilder.DropColumn("open_public_holiday_ids_json", "business_profiles");
    }
}
