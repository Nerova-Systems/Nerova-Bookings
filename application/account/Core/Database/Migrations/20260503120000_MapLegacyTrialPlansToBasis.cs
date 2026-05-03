using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260503120000_MapLegacyTrialPlansToBasis")]
public sealed class MapLegacyTrialPlansToBasis : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE tenants SET plan = 'Basis' WHERE plan = 'Trial';");
        migrationBuilder.Sql("UPDATE subscriptions SET plan = 'Basis' WHERE plan = 'Trial';");
        migrationBuilder.Sql("UPDATE subscriptions SET scheduled_plan = NULL WHERE scheduled_plan = 'Trial';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
