using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260501120000_AddSchedulingFoundation")]
public sealed class AddSchedulingFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "business_locations",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                time_zone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                address = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                is_default = table.Column<bool>(type: "boolean", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_business_locations", x => x.id);
            }
        );

        migrationBuilder.AddColumn<string>(
            "location_id",
            "bookable_services",
            "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            "location_id",
            "bookable_service_versions",
            "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            "location_id",
            "staff_members",
            "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            "user_id",
            "staff_members",
            "character varying(32)",
            maxLength: 32,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "location_id",
            "appointments",
            "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            "owner_type",
            "integration_connections",
            "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Tenant"
        );

        migrationBuilder.AddColumn<string>(
            "owner_id",
            "integration_connections",
            "character varying(80)",
            maxLength: 80,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            "external_connection_id",
            "integration_connections",
            "character varying(160)",
            maxLength: 160,
            nullable: true
        );

        migrationBuilder.CreateTable(
            name: "scheduling_resources",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                location_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_scheduling_resources", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "bookable_service_resources",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                service_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                resource_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bookable_service_resources", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "resource_reservations",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                resource_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                appointment_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_resource_reservations", x => x.id);
            }
        );

        migrationBuilder.CreateIndex("ix_business_locations_tenant_id_is_default", "business_locations", ["tenant_id", "is_default"]);
        migrationBuilder.CreateIndex("ix_bookable_service_resources_tenant_id_service_id_resource_id", "bookable_service_resources", ["tenant_id", "service_id", "resource_id"], unique: true);
        migrationBuilder.CreateIndex("ix_resource_reservations_tenant_id_resource_id_start_at_end_at", "resource_reservations", ["tenant_id", "resource_id", "start_at", "end_at"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("resource_reservations");
        migrationBuilder.DropTable("bookable_service_resources");
        migrationBuilder.DropTable("scheduling_resources");
        migrationBuilder.DropTable("business_locations");
        migrationBuilder.DropColumn("location_id", "bookable_services");
        migrationBuilder.DropColumn("location_id", "bookable_service_versions");
        migrationBuilder.DropColumn("location_id", "staff_members");
        migrationBuilder.DropColumn("user_id", "staff_members");
        migrationBuilder.DropColumn("location_id", "appointments");
        migrationBuilder.DropColumn("owner_type", "integration_connections");
        migrationBuilder.DropColumn("owner_id", "integration_connections");
        migrationBuilder.DropColumn("external_connection_id", "integration_connections");
    }
}
