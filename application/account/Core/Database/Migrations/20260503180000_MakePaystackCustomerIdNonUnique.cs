using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260503180000_MakePaystackCustomerIdNonUnique")]
public sealed class MakePaystackCustomerIdNonUnique : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS ix_subscriptions_paystack_customer_id;

            CREATE INDEX IF NOT EXISTS ix_subscriptions_paystack_customer_id
                ON subscriptions (paystack_customer_id)
                WHERE paystack_customer_id IS NOT NULL;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS ix_subscriptions_paystack_customer_id;

            CREATE UNIQUE INDEX IF NOT EXISTS ix_subscriptions_paystack_customer_id
                ON subscriptions (paystack_customer_id)
                WHERE paystack_customer_id IS NOT NULL;
            """
        );
    }
}
