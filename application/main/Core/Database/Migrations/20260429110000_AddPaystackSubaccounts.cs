using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260429110000_AddPaystackSubaccounts")]
public sealed class AddPaystackSubaccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "paystack_subaccounts",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                subaccount_code = table.Column<string>("character varying(80)", maxLength: 80, nullable: false),
                subaccount_id = table.Column<int>("integer", nullable: true),
                business_name = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                settlement_bank_name = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                settlement_bank_code = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                account_name = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                masked_account_number = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                currency = table.Column<string>("character varying(8)", maxLength: 8, nullable: false),
                is_active = table.Column<bool>("boolean", nullable: false),
                is_verified = table.Column<bool>("boolean", nullable: false),
                settlement_schedule = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                last_synced_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_paystack_subaccounts", x => x.id)
        );

        migrationBuilder.CreateIndex("ix_paystack_subaccounts_tenant_id", "paystack_subaccounts", "tenant_id", unique: true);
        migrationBuilder.CreateIndex("ix_paystack_subaccounts_subaccount_code", "paystack_subaccounts", "subaccount_code", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("paystack_subaccounts");
    }
}
