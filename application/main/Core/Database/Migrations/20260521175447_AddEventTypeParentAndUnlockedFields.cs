using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260521175447_AddEventTypeParentAndUnlockedFields")]
public sealed class AddEventTypeParentAndUnlockedFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "parent_event_type_id",
            table: "event_types",
            type: "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "unlocked_fields",
            table: "event_types",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb"
        );

        migrationBuilder.CreateIndex(
            name: "ix_event_types_parent_event_type_id",
            table: "event_types",
            column: "parent_event_type_id"
        );
    }
}
