using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Implements the Wave 1 PBAC domain layer:
///     <list type="bullet">
///         <item>Creates the <c>roles</c> table (system and custom PBAC roles).</item>
///         <item>Creates the <c>role_permissions</c> owned join table.</item>
///         <item>
///             Adds <c>custom_role_id</c> to <c>memberships</c> for per-member role overrides.
///         </item>
///         <item>Seeds the three system roles (Owner, Admin, Member) with their permission sets.</item>
///     </list>
///     <para>
///         System roles have <c>tenant_id IS NULL</c> and fixed deterministic IDs so that downstream
///         migrations and application code can reference them via <c>SystemRoles.OwnerId</c> etc.
///         without DB round-trips.
///     </para>
///     <para>
///         Permissions are stored as (resource, action) enum name strings in <c>role_permissions</c>
///         (PascalCase, matching EF Core's <c>EnumToStringConverter</c> output). The table uses a
///         surrogate <c>BIGSERIAL</c> primary key to avoid EF Core deduplication of owned record
///         entities that share the same (resource, action) values across multiple roles; uniqueness
///         is enforced by <c>uix_role_permissions_role_resource_action</c>.
///     </para>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260522100000_AddPbacDomain")]
public sealed class AddPbacDomain : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── roles table ──────────────────────────────────────────────────────

        migrationBuilder.CreateTable(
            "roles",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                name = table.Column<string>("text", nullable: false),
                description = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_roles", x => x.id);
                table.ForeignKey(
                    "fk_roles_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        // Unique: system role names are globally unique (no tenant).
        migrationBuilder.CreateIndex(
            "uix_roles_name_system",
            "roles",
            "name",
            unique: true,
            filter: "tenant_id IS NULL"
        );

        // Unique: custom role names are unique within their org.
        migrationBuilder.CreateIndex(
            "uix_roles_tenant_id_name",
            "roles",
            ["tenant_id", "name"],
            unique: true,
            filter: "tenant_id IS NOT NULL"
        );

        // Lookup: all custom roles for a given org.
        migrationBuilder.CreateIndex(
            "ix_roles_tenant_id",
            "roles",
            "tenant_id",
            filter: "tenant_id IS NOT NULL"
        );

        // ─── role_permissions table ───────────────────────────────────────────

        migrationBuilder.CreateTable(
            "role_permissions",
            table => new
            {
                id = table.Column<long>("bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                role_id = table.Column<string>("text", nullable: false),
                resource = table.Column<string>("text", nullable: false),
                action = table.Column<string>("text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_role_permissions", x => x.id);
                table.ForeignKey(
                    "fk_role_permissions_roles_role_id",
                    x => x.role_id,
                    "roles",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Unique: one (role, resource, action) combination per role.
        migrationBuilder.CreateIndex(
            "uix_role_permissions_role_resource_action",
            "role_permissions",
            ["role_id", "resource", "action"],
            unique: true
        );

        // ─── memberships.custom_role_id column ───────────────────────────────

        migrationBuilder.AddColumn<string>(
            "custom_role_id",
            "memberships",
            "text",
            nullable: true
        );

        migrationBuilder.CreateIndex(
            "ix_memberships_custom_role_id",
            "memberships",
            "custom_role_id",
            filter: "custom_role_id IS NOT NULL"
        );

        migrationBuilder.AddForeignKey(
            "fk_memberships_roles_custom_role_id",
            "memberships",
            "custom_role_id",
            "roles",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict
        );

        // ─── Seed system roles ────────────────────────────────────────────────

        const string ownerId = "rol_00000000000000000000000001";
        const string adminId = "rol_00000000000000000000000002";
        const string memberId = "rol_00000000000000000000000003";

        var seedTime = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero);

        migrationBuilder.InsertData(
            "roles",
            new[] { "id", "tenant_id", "created_at", "modified_at", "name", "description" },
            new object[,]
            {
                { ownerId, null!, seedTime, null!, "Owner", "Full access to all resources." },
                { adminId, null!, seedTime, null!, "Admin", "Full access except billing management and organization deletion." },
                { memberId, null!, seedTime, null!, "Member", "Day-to-day access to bookings, event types and schedules." }
            }
        );

        // All 15 resources × 7 actions = 105 permissions for Owner.
        string[] resources = ["EventType", "Booking", "Team", "Organization", "ApiKey", "Workflow", "Insights", "Member", "Role", "Attribute", "Schedule", "AuditLog", "Sso", "Smtp", "Billing"];
        string[] actions = ["Create", "Read", "Update", "Delete", "Manage", "Invite", "List"];

        // Build object[,] for each role's permissions (required by MigrationBuilder.InsertData overload).
        var permColumns = new[] { "role_id", "resource", "action" };

        var ownerRows = resources.SelectMany(r => actions.Select(a => (r, a))).ToArray();
        var ownerData = new object[ownerRows.Length, 3];
        for (var i = 0; i < ownerRows.Length; i++)
        {
            ownerData[i, 0] = ownerId;
            ownerData[i, 1] = ownerRows[i].r;
            ownerData[i, 2] = ownerRows[i].a;
        }

        migrationBuilder.InsertData("role_permissions", permColumns, ownerData);

        // Admin: all except Billing.Manage and Organization.Delete.
        var adminRows = resources
            .SelectMany(r => actions
                .Where(a => !((r == "Billing" && a == "Manage") || (r == "Organization" && a == "Delete")))
                .Select(a => (r, a)))
            .ToArray();
        var adminData = new object[adminRows.Length, 3];
        for (var i = 0; i < adminRows.Length; i++)
        {
            adminData[i, 0] = adminId;
            adminData[i, 1] = adminRows[i].r;
            adminData[i, 2] = adminRows[i].a;
        }

        migrationBuilder.InsertData("role_permissions", permColumns, adminData);

        // Member: limited set only.
        migrationBuilder.InsertData(
            "role_permissions",
            permColumns,
            new object[,]
            {
                { memberId, "Team", "Read" },
                { memberId, "Member", "Read" },
                { memberId, "Booking", "Create" },
                { memberId, "Booking", "Read" },
                { memberId, "Booking", "Update" },
                { memberId, "EventType", "Create" },
                { memberId, "EventType", "Read" },
                { memberId, "EventType", "Update" },
                { memberId, "Schedule", "Create" },
                { memberId, "Schedule", "Read" },
                { memberId, "Schedule", "Update" }
            }
        );
    }
}
