using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260522000004_UseTickerQTextStatuses")]
public sealed class UseTickerQTextStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE ticker."TimeTickers"
                ALTER COLUMN status TYPE text USING status::text;

            ALTER TABLE ticker."CronTickerOccurrences"
                ALTER COLUMN status TYPE text USING status::text;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE ticker."TimeTickers"
                ALTER COLUMN status TYPE integer USING 0;

            ALTER TABLE ticker."CronTickerOccurrences"
                ALTER COLUMN status TYPE integer USING 0;
            """
        );
    }
}
