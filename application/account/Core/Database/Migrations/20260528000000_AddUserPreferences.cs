using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Creates the <c>user_preferences</c> table, a 1:1 per-user store for display and locale
///     preferences (time format, week start, language, time zone). Ported loosely from the cal.com
///     Prisma <c>User</c> preference fields, kept as a separate aggregate so the settings surface
///     evolves independently from the auth/identity model.
///     <para>
///         Foreign key: <c>user_id → users.id</c> — CASCADE. Deleting a user drops their preferences
///         row automatically (preferences carry no value once the user is gone).
///     </para>
///     <para>
///         Unique constraint on <c>user_id</c> enforces the 1:1 invariant at the database level.
///     </para>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260528000000_AddUserPreferences")]
public sealed class AddUserPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "user_preferences",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                user_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                time_format = table.Column<string>("text", nullable: false),
                week_start = table.Column<string>("text", nullable: false),
                language = table.Column<string>("text", nullable: false),
                time_zone = table.Column<string>("text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_preferences", x => x.id);
                table.ForeignKey(
                    "fk_user_preferences_users_user_id",
                    x => x.user_id,
                    "users",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // 1:1: a user has at most one preferences row.
        migrationBuilder.CreateIndex(
            "uix_user_preferences_user_id",
            "user_preferences",
            "user_id",
            unique: true
        );
    }
}
