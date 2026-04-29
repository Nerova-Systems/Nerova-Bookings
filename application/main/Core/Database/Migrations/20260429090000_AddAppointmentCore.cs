using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Main.Features.Appointments;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260429090000_AddAppointmentCore")]
public sealed class AddAppointmentCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "business_profiles",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                name = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                slug = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                time_zone = table.Column<string>("text", nullable: false),
                currency = table.Column<string>("text", nullable: false),
                address = table.Column<string>("text", nullable: false),
                public_booking_enabled = table.Column<bool>("boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_business_profiles", x => x.id)
        );

        migrationBuilder.CreateTable(
            "service_categories",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                name = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                sort_order = table.Column<int>("integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_service_categories", x => x.id)
        );

        migrationBuilder.CreateTable(
            "bookable_services",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                category_id = table.Column<string>("text", nullable: false),
                name = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                description = table.Column<string>("text", nullable: false),
                mode = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                duration_minutes = table.Column<int>("integer", nullable: false),
                price_cents = table.Column<int>("integer", nullable: false),
                deposit_cents = table.Column<int>("integer", nullable: false),
                buffer_before_minutes = table.Column<int>("integer", nullable: false),
                buffer_after_minutes = table.Column<int>("integer", nullable: false),
                location = table.Column<string>("text", nullable: false),
                is_active = table.Column<bool>("boolean", nullable: false),
                sort_order = table.Column<int>("integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_bookable_services", x => x.id)
        );

        migrationBuilder.CreateTable(
            "staff_members",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                name = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                email = table.Column<string>("text", nullable: false),
                phone = table.Column<string>("text", nullable: false),
                is_active = table.Column<bool>("boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_staff_members", x => x.id)
        );

        migrationBuilder.CreateTable(
            "availability_rules",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                staff_member_id = table.Column<string>("text", nullable: true),
                day_of_week = table.Column<string>("text", nullable: false),
                start_time = table.Column<TimeOnly>("time without time zone", nullable: false),
                end_time = table.Column<TimeOnly>("time without time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_availability_rules", x => x.id)
        );

        migrationBuilder.CreateTable(
            "clients",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                name = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                phone = table.Column<string>("character varying(64)", maxLength: 64, nullable: false),
                email = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                alert = table.Column<string>("text", nullable: true),
                internal_note = table.Column<string>("text", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_clients", x => x.id)
        );

        migrationBuilder.CreateTable(
            "appointments",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                public_reference = table.Column<string>("text", nullable: false),
                client_id = table.Column<string>("text", nullable: false),
                service_id = table.Column<string>("text", nullable: false),
                staff_member_id = table.Column<string>("text", nullable: false),
                start_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                end_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                status = table.Column<string>("text", nullable: false),
                payment_status = table.Column<string>("text", nullable: false),
                source = table.Column<string>("text", nullable: false),
                answers_json = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_appointments", x => x.id)
        );

        migrationBuilder.CreateTable(
            "appointment_payment_intents",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                appointment_id = table.Column<string>("text", nullable: false),
                provider = table.Column<string>("text", nullable: false),
                reference = table.Column<string>("text", nullable: false),
                amount_cents = table.Column<int>("integer", nullable: false),
                status = table.Column<string>("text", nullable: false),
                authorization_url = table.Column<string>("text", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                confirmed_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_appointment_payment_intents", x => x.id)
        );

        migrationBuilder.CreateTable(
            "appointment_flow_events",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                appointment_id = table.Column<string>("text", nullable: false),
                type = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                scheduled_for = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                payload_json = table.Column<string>("text", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_appointment_flow_events", x => x.id)
        );

        migrationBuilder.CreateTable(
            "external_busy_blocks",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                provider = table.Column<string>("text", nullable: false),
                start_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                end_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                label = table.Column<string>("text", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_external_busy_blocks", x => x.id)
        );

        migrationBuilder.CreateTable(
            "integration_connections",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                provider = table.Column<string>("text", nullable: false),
                capability = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                last_synced_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_integration_connections", x => x.id)
        );

        migrationBuilder.CreateIndex("ix_business_profiles_slug", "business_profiles", "slug", unique: true);
        migrationBuilder.CreateIndex("ix_appointments_public_reference", "appointments", "public_reference", unique: true);
        migrationBuilder.CreateIndex("ix_appointment_payment_intents_reference", "appointment_payment_intents", "reference", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("appointment_flow_events");
        migrationBuilder.DropTable("appointment_payment_intents");
        migrationBuilder.DropTable("appointments");
        migrationBuilder.DropTable("availability_rules");
        migrationBuilder.DropTable("bookable_services");
        migrationBuilder.DropTable("business_profiles");
        migrationBuilder.DropTable("clients");
        migrationBuilder.DropTable("external_busy_blocks");
        migrationBuilder.DropTable("integration_connections");
        migrationBuilder.DropTable("service_categories");
        migrationBuilder.DropTable("staff_members");
    }
}
