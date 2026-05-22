using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260522164352_ExpandEventTypeForCalcomParity")]
public sealed class ExpandEventTypeForCalcomParity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            "is_instant_event",
            "event_types",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            "assign_all_team_members",
            "event_types",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            "hide_organizer_email",
            "event_types",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            "booking_requires_authentication",
            "event_types",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<string>(
            "secondary_email_user_id",
            "event_types",
            "text",
            nullable: true
        );

        migrationBuilder.CreateTable(
            "hashed_links",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                event_type_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                hash = table.Column<string>("text", nullable: false),
                expires_after_uses = table.Column<int>("integer", nullable: true),
                expires_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_hashed_links", x => x.id);
                table.ForeignKey(
                    "fk_hashed_links_event_types_event_type_id",
                    x => x.event_type_id,
                    "event_types",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            "ix_hashed_links_tenant_id_hash",
            "hashed_links",
            ["tenant_id", "hash"],
            unique: true
        );

        migrationBuilder.CreateIndex(
            "ix_hashed_links_event_type_id",
            "hashed_links",
            "event_type_id"
        );
    }
}
