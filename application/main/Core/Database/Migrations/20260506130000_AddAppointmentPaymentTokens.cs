using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260506130000_AddAppointmentPaymentTokens")]
public sealed class AddAppointmentPaymentTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "appointment_payment_tokens",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                appointment_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                token_hash = table.Column<string>("text", nullable: false),
                payment_intent_id = table.Column<string>("text", nullable: true),
                status = table.Column<string>("text", nullable: false),
                expires_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                used_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_appointment_payment_tokens", x => x.id);
            }
        );

        migrationBuilder.AddColumn<string>(
            "payment_token_id",
            "appointment_payment_intents",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "provider_access_code",
            "appointment_payment_intents",
            "text",
            nullable: true
        );

        migrationBuilder.CreateIndex("ix_appointment_payment_tokens_token_hash", "appointment_payment_tokens", "token_hash", unique: true);
        migrationBuilder.CreateIndex("ix_appointment_payment_tokens_tenant_id_appointment_id_status", "appointment_payment_tokens", ["tenant_id", "appointment_id", "status"]);
        migrationBuilder.CreateIndex("ix_appointment_payment_intents_payment_token_id", "appointment_payment_intents", "payment_token_id");
    }
}
