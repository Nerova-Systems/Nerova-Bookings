using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260501120000_AddFeatureFlagsAndTeams")]
public sealed class AddFeatureFlagsAndTeams : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "feature_flags",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                user_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_feature_flags", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "teams",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_teams", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "team_members",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                team_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                user_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_team_members", x => x.id);
            }
        );

        migrationBuilder.CreateIndex("ix_feature_flags_tenant_id_key_user_id", "feature_flags", ["tenant_id", "key", "user_id"], unique: true);
        migrationBuilder.CreateIndex("ix_teams_tenant_id_name", "teams", ["tenant_id", "name"]);
        migrationBuilder.CreateIndex("ix_team_members_tenant_id_team_id_user_id", "team_members", ["tenant_id", "team_id", "user_id"], unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("team_members");
        migrationBuilder.DropTable("teams");
        migrationBuilder.DropTable("feature_flags");
    }
}
