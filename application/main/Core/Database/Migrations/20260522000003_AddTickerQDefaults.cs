using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260522000003_AddTickerQDefaults")]
public sealed class AddTickerQDefaults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE ticker."CronTickers"
                ALTER COLUMN is_system_paused SET DEFAULT FALSE;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE ticker."CronTickers"
                ALTER COLUMN is_system_paused DROP DEFAULT;
            """
        );
    }
}
