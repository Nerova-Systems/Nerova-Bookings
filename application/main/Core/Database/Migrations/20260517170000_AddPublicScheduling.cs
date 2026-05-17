using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260517170000_AddPublicScheduling")]
public sealed class AddPublicScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "scheduling_profiles",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                owner_user_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                handle = table.Column<string>("text", nullable: false),
                display_name = table.Column<string>("text", nullable: false),
                avatar_url = table.Column<string>("text", nullable: true),
                deleted_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_scheduling_profiles", profile => profile.id); }
        );

        migrationBuilder.CreateTable(
            "bookings",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                event_type_id = table.Column<string>("text", nullable: false),
                owner_user_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                start_time = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                end_time = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                before_event_buffer_minutes = table.Column<int>("integer", nullable: false),
                after_event_buffer_minutes = table.Column<int>("integer", nullable: false),
                booker_name = table.Column<string>("text", nullable: false),
                booker_email = table.Column<string>("text", nullable: false),
                time_zone = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                responses_json = table.Column<string>("jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bookings", booking => booking.id);
                table.ForeignKey(
                    "fk_bookings_event_types_event_type_id",
                    booking => booking.event_type_id,
                    "event_types",
                    "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            "ix_scheduling_profiles_handle",
            "scheduling_profiles",
            "handle",
            unique: true,
            filter: "deleted_at IS NULL"
        );

        migrationBuilder.CreateIndex(
            "ix_scheduling_profiles_tenant_id_owner_user_id",
            "scheduling_profiles",
            ["tenant_id", "owner_user_id"],
            unique: true,
            filter: "deleted_at IS NULL"
        );

        migrationBuilder.CreateIndex(
            "ix_bookings_event_type_id",
            "bookings",
            "event_type_id"
        );

        migrationBuilder.CreateIndex(
            "ix_bookings_tenant_id_owner_user_id_start_time_end_time",
            "bookings",
            ["tenant_id", "owner_user_id", "start_time", "end_time"]
        );

        migrationBuilder.CreateIndex(
            "ix_bookings_tenant_id_event_type_id_start_time_end_time",
            "bookings",
            ["tenant_id", "event_type_id", "start_time", "end_time"]
        );
    }
}
