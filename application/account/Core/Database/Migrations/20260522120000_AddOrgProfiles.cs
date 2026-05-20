using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Creates the <c>org_profiles</c> table, which models a user's per-organization display identity.
///     Ported from the cal.com Prisma <c>Profile</c> model. Each <c>(user_id, org_tenant_id)</c> pair
///     may have its own URL slug (<c>username</c>), display name, avatar, and bio — separate from the
///     user's global profile.
///     <para>
///         <c>OrgProfile</c> is intentionally NOT tenant-scoped: it crosses tenant boundaries (a user's
///         Solo personal tenant differs from any org they belong to). Repository methods accept explicit
///         <c>user_id</c> or <c>org_tenant_id</c> parameters instead.
///     </para>
///     <para>
///         Foreign keys:
///         <list type="bullet">
///             <item>
///                 <c>user_id → users.id</c> — RESTRICT: prevents deleting a user who has active org
///                 profiles; commands must clean up profiles before deleting a user.
///             </item>
///             <item>
///                 <c>org_tenant_id → tenants.id</c> — CASCADE: deleting an Organization removes all
///                 its member profiles.
///             </item>
///         </list>
///     </para>
///     <para>
///         Unique constraints:
///         <list type="bullet">
///             <item><c>(user_id, org_tenant_id)</c> — one profile per user per org.</item>
///             <item><c>(org_tenant_id, username)</c> — no two users share a slug within an org.</item>
///         </list>
///     </para>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260522120000_AddOrgProfiles")]
public sealed class AddOrgProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "org_profiles",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                user_id = table.Column<string>("text", nullable: false),
                org_tenant_id = table.Column<long>("bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                username = table.Column<string>("text", nullable: false),
                name = table.Column<string>("text", nullable: true),
                avatar_url = table.Column<string>("text", nullable: true),
                bio = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_org_profiles", x => x.id);
                table.ForeignKey(
                    "fk_org_profiles_users_user_id",
                    x => x.user_id,
                    "users",
                    "id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    "fk_org_profiles_tenants_org_tenant_id",
                    x => x.org_tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Unique: a user has at most one profile per organization.
        migrationBuilder.CreateIndex(
            "uix_org_profiles_user_id_org_tenant_id",
            "org_profiles",
            ["user_id", "org_tenant_id"],
            unique: true
        );

        // Unique: within an org, no two users share a username (URL slug).
        migrationBuilder.CreateIndex(
            "uix_org_profiles_org_tenant_id_username",
            "org_profiles",
            ["org_tenant_id", "username"],
            unique: true
        );

        // Lookup: all org profiles for a given user ("all my org profiles").
        migrationBuilder.CreateIndex(
            "ix_org_profiles_user_id",
            "org_profiles",
            "user_id"
        );
    }
}
