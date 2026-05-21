using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260517190000_AddTeamScoping")]
public sealed class AddTeamScoping : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>("team_id", "event_types", "bigint", nullable: true);
        migrationBuilder.AddColumn<long>("team_id", "schedules", "bigint", nullable: true);
        migrationBuilder.AddColumn<long>("team_id", "scheduling_profiles", "bigint", nullable: true);
        migrationBuilder.AddColumn<long>("team_id", "bookings", "bigint", nullable: true);

        migrationBuilder.CreateIndex("ix_event_types_team_id", "event_types", "team_id");
        migrationBuilder.CreateIndex("ix_schedules_team_id", "schedules", "team_id");
        migrationBuilder.CreateIndex("ix_scheduling_profiles_team_id", "scheduling_profiles", "team_id");
        migrationBuilder.CreateIndex("ix_bookings_team_id", "bookings", "team_id");
    }
}
