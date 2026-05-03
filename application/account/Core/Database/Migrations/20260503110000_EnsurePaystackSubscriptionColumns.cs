using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260503110000_EnsurePaystackSubscriptionColumns")]
public sealed class EnsurePaystackSubscriptionColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS scheduled_plan text;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS paystack_customer_id text;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS paystack_subscription_id text;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS current_price_amount numeric(18,2);
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS current_price_currency text;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS current_period_end timestamptz;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS cancel_at_period_end boolean NOT NULL DEFAULT false;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS first_payment_failed_at timestamptz;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS cancellation_reason text;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS cancellation_feedback text;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS payment_transactions jsonb NOT NULL DEFAULT '[]'::jsonb;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS payment_method jsonb;
            ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS billing_info jsonb;

            DROP INDEX IF EXISTS ix_subscriptions_paystack_customer_id;
            CREATE INDEX IF NOT EXISTS ix_subscriptions_paystack_customer_id
                ON subscriptions (paystack_customer_id)
                WHERE paystack_customer_id IS NOT NULL;

            CREATE TABLE IF NOT EXISTS paystack_events (
                tenant_id bigint NULL,
                id text NOT NULL,
                created_at timestamptz NOT NULL,
                modified_at timestamptz NULL,
                event_type text NOT NULL,
                status text NOT NULL,
                processed_at timestamptz NULL,
                paystack_customer_id text NULL,
                paystack_subscription_id text NULL,
                payload jsonb NULL,
                error text NULL,
                CONSTRAINT pk_paystack_events PRIMARY KEY (id)
            );

            CREATE INDEX IF NOT EXISTS ix_paystack_events_tenant_id
                ON paystack_events (tenant_id);

            CREATE INDEX IF NOT EXISTS ix_paystack_events_paystack_customer_id_status
                ON paystack_events (paystack_customer_id, status);
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
