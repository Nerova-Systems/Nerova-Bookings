using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Creates the <c>memberships</c> table, which models the explicit membership of a user in a
///     <c>Team</c> or <c>Organization</c> tenant. Ported from the cal.com Prisma <c>Membership</c>
///     model with the following additions:
///     <list type="bullet">
///         <item><c>invite_token</c> — cryptographically random 64-char hex token for the email-invite flow.</item>
///         <item><c>accepted_at</c> — timestamp when the invitee accepted; null for pending memberships.</item>
///         <item><c>invited_by</c> — nullable FK to the user who created the invite.</item>
///     </list>
///     <para>
///         Membership is intentionally NOT tenant-scoped: a user's personal (Solo) tenant is separate
///         from any Team/Org they belong to, so scoping by a single tenant context would break
///         cross-tenant queries such as "all teams I belong to".
///     </para>
///     <para>
///         Foreign keys:
///         <list type="bullet">
///             <item><c>user_id → users.id</c> — RESTRICT: prevents deleting a user who has active memberships.</item>
///             <item><c>tenant_id → tenants.id</c> — CASCADE: deleting a Team/Org removes all its memberships.</item>
///             <item><c>invited_by → users.id</c> — RESTRICT: preserves invite audit trail.</item>
///         </list>
///     </para>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260521120000_AddMemberships")]
public sealed class AddMemberships : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "memberships",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                user_id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                invited_by = table.Column<string>("text", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                role = table.Column<string>("text", nullable: false),
                accepted = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                invite_token = table.Column<string>("text", nullable: true),
                accepted_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                disable_impersonation = table.Column<bool>("boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_memberships", x => x.id);
                table.ForeignKey(
                    "fk_memberships_users_user_id",
                    x => x.user_id,
                    "users",
                    "id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    "fk_memberships_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    "fk_memberships_users_invited_by",
                    x => x.invited_by,
                    "users",
                    "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        // Unique: a user can only have one membership per team/org.
        migrationBuilder.CreateIndex(
            "uix_memberships_user_id_tenant_id",
            "memberships",
            ["user_id", "tenant_id"],
            unique: true
        );

        // Lookup: all members of a given team/org.
        migrationBuilder.CreateIndex(
            "ix_memberships_tenant_id",
            "memberships",
            "tenant_id"
        );

        // Lookup: all teams/orgs a given user belongs to.
        migrationBuilder.CreateIndex(
            "ix_memberships_user_id",
            "memberships",
            "user_id"
        );

        // Lookup: accept-invite flow by token. Partial to exclude rows with no token.
        migrationBuilder.CreateIndex(
            "ix_memberships_invite_token",
            "memberships",
            "invite_token",
            filter: "invite_token IS NOT NULL"
        );
    }
}
