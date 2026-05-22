using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260522000005_AddTickerQCronOccurrenceConflictIndex")]
public sealed class AddTickerQCronOccurrenceConflictIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_cron_ticker_occurrences_execution_time_cron_ticker_id
                ON ticker."CronTickerOccurrences" (execution_time, cron_ticker_id);
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS ticker.ux_cron_ticker_occurrences_execution_time_cron_ticker_id;
            """
        );
    }
}
