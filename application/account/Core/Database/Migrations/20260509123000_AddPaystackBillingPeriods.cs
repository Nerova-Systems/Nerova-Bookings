using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260509123000_AddPaystackBillingPeriods")]
public sealed class AddPaystackBillingPeriods : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>("current_period_start", "subscriptions", "timestamptz", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("next_billing_at", "subscriptions", "timestamptz", nullable: true);
        migrationBuilder.Sql("UPDATE subscriptions SET next_billing_at = current_period_end WHERE current_period_end IS NOT NULL");
    }
}
