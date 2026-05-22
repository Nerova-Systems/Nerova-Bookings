using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Adds the tenant hierarchy columns to support Organizations and Teams (ported from cal.com EE/Teams).
///     <list type="bullet">
///         <item><c>parent_tenant_id</c> — nullable FK to the parent Organization; null for Solo tenants.</item>
///         <item><c>kind</c> — enum-as-text: Solo | Team | Organization. Defaults to 'Solo' for all existing rows.</item>
///     </list>
///     The FK uses <c>RESTRICT</c> so that deleting an Organization with active Teams is blocked at the DB level.
///     A partial index on <c>parent_tenant_id IS NOT NULL</c> keeps child-lookup queries efficient.
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260520231615_AddTenantHierarchy")]
public sealed class AddTenantHierarchy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            "parent_tenant_id",
            "tenants",
            "bigint",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "kind",
            "tenants",
            "text",
            nullable: false,
            defaultValue: "Solo"
        );

        migrationBuilder.AddForeignKey(
            "fk_tenants_tenants_parent_tenant_id",
            "tenants",
            "parent_tenant_id",
            "tenants",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.CreateIndex(
            "ix_tenants_parent_tenant_id",
            "tenants",
            "parent_tenant_id",
            filter: "parent_tenant_id IS NOT NULL"
        );
    }
}
