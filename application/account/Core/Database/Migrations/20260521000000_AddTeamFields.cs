using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Adds team-specific branding and scheduling columns to the <c>tenants</c> table, ported from the
///     cal.com Prisma <c>Team</c> model.
///     <list type="bullet">
///         <item><c>slug</c> — URL-friendly identifier for team/org profile pages.</item>
///         <item><c>bio</c> — Short description shown on team/org profile pages.</item>
///         <item><c>hide_branding</c> — Hides Nerova branding on booking pages when true.</item>
///         <item><c>hide_team_profile_link</c> — Hides the public link to the team profile page.</item>
///         <item><c>is_private</c> — Marks the team as not publicly discoverable.</item>
///         <item><c>hide_book_a_team_member</c> — Hides the "Book a team member" option on the team page.</item>
///         <item><c>theme</c> — UI theme override (light/dark/null).</item>
///         <item><c>brand_color</c> — Primary brand color hex string.</item>
///         <item><c>dark_brand_color</c> — Brand color for dark mode.</item>
///         <item><c>time_format</c> — Preferred time format (12 or 24 hours).</item>
///         <item><c>time_zone</c> — IANA time-zone identifier for scheduling.</item>
///         <item><c>week_start</c> — First day of the week for calendar display.</item>
///     </list>
///     Adds two partial unique indexes for slug scoping:
///     <list type="bullet">
///         <item><c>uix_tenants_slug_org</c> — Globally unique slug among organizations.</item>
///         <item><c>uix_tenants_slug_parent</c> — Slug unique within a parent organization for teams.</item>
///     </list>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260521000000_AddTeamFields")]
public sealed class AddTeamFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "slug",
            "tenants",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "bio",
            "tenants",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<bool>(
            "hide_branding",
            "tenants",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            "hide_team_profile_link",
            "tenants",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            "is_private",
            "tenants",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            "hide_book_a_team_member",
            "tenants",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<string>(
            "theme",
            "tenants",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "brand_color",
            "tenants",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "dark_brand_color",
            "tenants",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<int>(
            "time_format",
            "tenants",
            "integer",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "time_zone",
            "tenants",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "week_start",
            "tenants",
            "text",
            nullable: true
        );

        // Partial unique index: organization slugs are globally unique.
        // Using raw SQL because the WHERE condition references a non-key column value ('Organization').
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX uix_tenants_slug_org ON tenants (slug) WHERE slug IS NOT NULL AND kind = 'Organization'"
        );

        // Partial unique index: team slug is unique within its parent organization.
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX uix_tenants_slug_parent ON tenants (slug, parent_tenant_id) WHERE slug IS NOT NULL AND kind = 'Team'"
        );
    }
}
