using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260516090000_AddSchedulingSetup")]
public sealed class AddSchedulingSetup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "schedules",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                owner_user_id = table.Column<string>("text", nullable: false),
                name = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                time_zone = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
                is_default = table.Column<bool>("boolean", nullable: false),
                availability_windows = table.Column<string>("jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                deleted_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_schedules", schedule => schedule.id); }
        );

        migrationBuilder.CreateTable(
            "event_types",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                owner_user_id = table.Column<string>("text", nullable: false),
                title = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                slug = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                description = table.Column<string>("character varying(1000)", maxLength: 1000, nullable: true),
                duration_minutes = table.Column<int>("integer", nullable: false),
                hidden = table.Column<bool>("boolean", nullable: false),
                schedule_id = table.Column<string>("text", nullable: false),
                before_event_buffer_minutes = table.Column<int>("integer", nullable: false),
                after_event_buffer_minutes = table.Column<int>("integer", nullable: false),
                slot_interval_minutes = table.Column<int>("integer", nullable: false),
                minimum_booking_notice_minutes = table.Column<int>("integer", nullable: false),
                location_type = table.Column<string>("character varying(80)", maxLength: 80, nullable: true),
                location_value = table.Column<string>("character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                deleted_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_event_types", eventType => eventType.id);
                table.ForeignKey(
                    "fk_event_types_schedules_schedule_id",
                    eventType => eventType.schedule_id,
                    "schedules",
                    "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            "ix_schedules_tenant_id_owner_user_id_name",
            "schedules",
            ["tenant_id", "owner_user_id", "name"]
        );

        migrationBuilder.CreateIndex(
            "ix_schedules_tenant_id_owner_user_id_is_default",
            "schedules",
            ["tenant_id", "owner_user_id", "is_default"]
        );

        migrationBuilder.CreateIndex(
            "ix_event_types_schedule_id",
            "event_types",
            "schedule_id"
        );

        migrationBuilder.CreateIndex(
            "ix_event_types_tenant_id_owner_user_id_slug",
            "event_types",
            ["tenant_id", "owner_user_id", "slug"],
            unique: true,
            filter: "deleted_at IS NULL"
        );

        migrationBuilder.CreateIndex(
            "ix_event_types_tenant_id_owner_user_id_title",
            "event_types",
            ["tenant_id", "owner_user_id", "title"]
        );
    }
}
