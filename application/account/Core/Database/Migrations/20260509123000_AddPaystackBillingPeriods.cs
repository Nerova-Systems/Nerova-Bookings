using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260509123000_AddPaystackBillingPeriods")]
public sealed class AddPaystackBillingPeriods : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // These columns are already included in the regenerated Initial migration.
        // This migration is kept as a no-op to preserve history for databases that
        // had the original Initial migration applied before the regeneration.
    }
}
