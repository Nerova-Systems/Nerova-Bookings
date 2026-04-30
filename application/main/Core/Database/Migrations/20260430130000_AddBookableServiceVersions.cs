using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260430130000_AddBookableServiceVersions")]
public sealed class AddBookableServiceVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "bookable_service_versions",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                service_id = table.Column<string>("text", nullable: false),
                version_number = table.Column<int>("integer", nullable: false),
                category_id = table.Column<string>("text", nullable: false),
                name = table.Column<string>("character varying(160)", maxLength: 160, nullable: false),
                description = table.Column<string>("text", nullable: false),
                mode = table.Column<string>("character varying(32)", maxLength: 32, nullable: false),
                duration_minutes = table.Column<int>("integer", nullable: false),
                price_cents = table.Column<int>("integer", nullable: false),
                deposit_cents = table.Column<int>("integer", nullable: false),
                payment_policy = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                buffer_before_minutes = table.Column<int>("integer", nullable: false),
                buffer_after_minutes = table.Column<int>("integer", nullable: false),
                location = table.Column<string>("character varying(240)", maxLength: 240, nullable: false),
                is_active = table.Column<bool>("boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bookable_service_versions", x => x.id);
            }
        );

        migrationBuilder.CreateIndex(
            "ix_bookable_service_versions_service_id_version_number",
            "bookable_service_versions",
            new[] { "service_id", "version_number" },
            unique: true
        );

        migrationBuilder.AddColumn<string>(
            "service_version_id",
            "appointments",
            "character varying(64)",
            maxLength: 64,
            nullable: true
        );

        migrationBuilder.Sql(
            """
            insert into bookable_service_versions (
                id,
                tenant_id,
                service_id,
                version_number,
                category_id,
                name,
                description,
                mode,
                duration_minutes,
                price_cents,
                deposit_cents,
                payment_policy,
                buffer_before_minutes,
                buffer_after_minutes,
                location,
                is_active,
                created_at
            )
            select
                id || '_v1',
                tenant_id,
                id,
                1,
                category_id,
                name,
                description,
                mode,
                duration_minutes,
                price_cents,
                deposit_cents,
                payment_policy,
                buffer_before_minutes,
                buffer_after_minutes,
                location,
                is_active,
                now()
            from bookable_services
            """
        );

        migrationBuilder.Sql("update appointments set service_version_id = service_id || '_v1'");

        migrationBuilder.AlterColumn<string>(
            "service_version_id",
            "appointments",
            "character varying(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(64)",
            oldMaxLength: 64,
            oldNullable: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("service_version_id", "appointments");
        migrationBuilder.DropTable("bookable_service_versions");
    }
}
