using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260522000001_AddCollectiveScheduling")]
public sealed class AddCollectiveScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "scheduling_type",
            table: "event_types",
            type: "text",
            nullable: false,
            defaultValue: "Default"
        );

        migrationBuilder.CreateTable(
            name: "hosts",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                event_type_id = table.Column<string>(type: "text", nullable: false),
                user_id = table.Column<string>(type: "text", nullable: false),
                is_fixed = table.Column<bool>(type: "boolean", nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                weight = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_hosts", x => x.id);
                table.ForeignKey(
                    name: "fk_hosts_event_types_event_type_id",
                    column: x => x.event_type_id,
                    principalTable: "event_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_hosts_event_type_id_user_id",
            table: "hosts",
            columns: ["event_type_id", "user_id"],
            unique: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "hosts");

        migrationBuilder.DropColumn(
            name: "scheduling_type",
            table: "event_types"
        );
    }
}
