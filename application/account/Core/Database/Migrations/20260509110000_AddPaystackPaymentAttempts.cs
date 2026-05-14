using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260509110000_AddPaystackPaymentAttempts")]
public sealed class AddPaystackPaymentAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "paystack_payment_attempts",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                subscription_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                paystack_reference = table.Column<string>("text", nullable: false),
                paystack_customer_code = table.Column<string>("text", nullable: false),
                paystack_authorization_code = table.Column<string>("text", nullable: true),
                purpose = table.Column<string>("text", nullable: false),
                plan = table.Column<string>("text", nullable: true),
                amount = table.Column<decimal>("numeric(18,2)", nullable: false),
                currency = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                completed_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                failure_reason = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_paystack_payment_attempts", x => x.id);
                table.ForeignKey("fk_paystack_payment_attempts_subscriptions_subscription_id", x => x.subscription_id, "subscriptions", "id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("fk_paystack_payment_attempts_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
            }
        );

        migrationBuilder.CreateIndex("ix_paystack_payment_attempts_tenant_id", "paystack_payment_attempts", "tenant_id");
        migrationBuilder.CreateIndex("ix_paystack_payment_attempts_subscription_id", "paystack_payment_attempts", "subscription_id");
        migrationBuilder.CreateIndex("ix_paystack_payment_attempts_paystack_reference", "paystack_payment_attempts", "paystack_reference", unique: true);
        migrationBuilder.CreateIndex("ix_paystack_payment_attempts_tenant_id_status", "paystack_payment_attempts", ["tenant_id", "status"]);
    }
}
