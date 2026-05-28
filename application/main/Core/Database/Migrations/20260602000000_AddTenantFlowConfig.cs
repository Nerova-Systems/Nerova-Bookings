using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

/// <summary>
///     Creates the <c>tenant_flow_configs</c> table — the questionnaire output for the WhatsApp
///     Flows feature. One row per tenant; the list of pre-booking custom questions is stored as a
///     single <c>jsonb</c> column.
/// </summary>
[DbContext(typeof(MainDbContext))]
[Migration("20260602000000_AddTenantFlowConfig")]
public sealed class AddTenantFlowConfig : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "tenant_flow_configs",
            table => new
            {
                id = table.Column<long>("bigint", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                business_vertical = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                staff_assignment = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                payment_timing = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                deposit_amount_cents = table.Column<long>("bigint", nullable: true),
                booking_window_days = table.Column<int>("integer", nullable: false),
                default_session_minutes = table.Column<int>("integer", nullable: false),
                has_multiple_services = table.Column<bool>("boolean", nullable: false),
                allow_same_day_bookings = table.Column<bool>("boolean", nullable: false),
                confirmation_message_template = table.Column<string>("character varying(1000)", maxLength: 1000, nullable: false),
                cancellation_contact = table.Column<string>("character varying(500)", maxLength: 500, nullable: false),
                custom_pre_booking_questions = table.Column<string>("jsonb", nullable: false),
                config_version_hash = table.Column<string>("character varying(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_tenant_flow_configs", x => x.id); }
        );

        // 1:1 — each tenant has at most one flow config.
        migrationBuilder.CreateIndex(
            "uix_tenant_flow_configs_tenant_id",
            "tenant_flow_configs",
            "tenant_id",
            unique: true
        );
    }
}
