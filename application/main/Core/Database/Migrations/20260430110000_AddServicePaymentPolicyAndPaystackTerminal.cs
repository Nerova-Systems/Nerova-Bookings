using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260430110000_AddServicePaymentPolicyAndPaystackTerminal")]
public sealed class AddServicePaymentPolicyAndPaystackTerminal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "payment_policy",
            "bookable_services",
            "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "NoPaymentRequired"
        );

        migrationBuilder.Sql("update bookable_services set payment_policy = 'DepositBeforeBooking' where deposit_cents > 0");

        migrationBuilder.AddColumn<string>(
            "channel",
            "appointment_payment_intents",
            "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "HostedCheckout"
        );

        migrationBuilder.AddColumn<string>(
            "virtual_terminal_code",
            "appointment_payment_intents",
            "character varying(80)",
            maxLength: 80,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "split_code",
            "paystack_subaccounts",
            "character varying(80)",
            maxLength: 80,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "virtual_terminal_code",
            "paystack_subaccounts",
            "character varying(80)",
            maxLength: 80,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "primary_contact_name",
            "paystack_subaccounts",
            "character varying(160)",
            maxLength: 160,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "primary_contact_email",
            "paystack_subaccounts",
            "character varying(320)",
            maxLength: 320,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "primary_contact_phone",
            "paystack_subaccounts",
            "character varying(64)",
            maxLength: 64,
            nullable: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("payment_policy", "bookable_services");
        migrationBuilder.DropColumn("channel", "appointment_payment_intents");
        migrationBuilder.DropColumn("virtual_terminal_code", "appointment_payment_intents");
        migrationBuilder.DropColumn("split_code", "paystack_subaccounts");
        migrationBuilder.DropColumn("virtual_terminal_code", "paystack_subaccounts");
        migrationBuilder.DropColumn("primary_contact_name", "paystack_subaccounts");
        migrationBuilder.DropColumn("primary_contact_email", "paystack_subaccounts");
        migrationBuilder.DropColumn("primary_contact_phone", "paystack_subaccounts");
    }
}
