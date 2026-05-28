using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260527000000_AddBookingReports")]
public sealed class AddBookingReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "booking_reports",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                booking_id = table.Column<string>("text", nullable: false),
                reported_by_user_id = table.Column<string>("text", nullable: false),
                reason_code = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                notes = table.Column<string>("character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_booking_reports", report => report.id);
                table.ForeignKey(
                    "fk_booking_reports_bookings_booking_id",
                    report => report.booking_id,
                    "bookings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            "ix_booking_reports_tenant_id_created_at",
            "booking_reports",
            ["tenant_id", "created_at"]
        );

        migrationBuilder.CreateIndex("ix_booking_reports_booking_id", "booking_reports", "booking_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("booking_reports");
    }
}
