using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260428120000_AddBillingHardeningLedgers")]
public sealed class AddBillingHardeningLedgers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "pay_fast_itn_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                pf_payment_id = table.Column<string>(type: "text", nullable: false),
                payment_status = table.Column<string>(type: "text", nullable: false),
                event_key = table.Column<string>(type: "text", nullable: false),
                payload_json = table.Column<string>(type: "jsonb", nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pay_fast_itn_events", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "billing_reconciliation_runs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                summary = table.Column<string>(type: "text", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_billing_reconciliation_runs", x => x.id);
            }
        );

        migrationBuilder.Sql(
            """
            UPDATE subscriptions
            SET payment_transactions = COALESCE(
                (
                    SELECT jsonb_agg(
                        CASE
                            WHEN transaction ? 'Provider' THEN transaction
                            ELSE transaction || '{"Provider":"PayFast"}'::jsonb
                        END
                    )
                    FROM jsonb_array_elements(payment_transactions) AS transaction
                ),
                '[]'::jsonb
            )
            WHERE payment_transactions IS NOT NULL;
            """
        );

        migrationBuilder.CreateIndex("ix_pay_fast_itn_events_event_key", "pay_fast_itn_events", "event_key", unique: true);
        migrationBuilder.CreateIndex("ix_pay_fast_itn_events_tenant_id_pf_payment_id", "pay_fast_itn_events", ["tenant_id", "pf_payment_id"]);
        migrationBuilder.CreateIndex("ix_billing_reconciliation_runs_status", "billing_reconciliation_runs", "status");
        migrationBuilder.CreateIndex("ix_billing_reconciliation_runs_tenant_id_started_at", "billing_reconciliation_runs", ["tenant_id", "started_at"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("billing_reconciliation_runs");
        migrationBuilder.DropTable("pay_fast_itn_events");
    }
}
