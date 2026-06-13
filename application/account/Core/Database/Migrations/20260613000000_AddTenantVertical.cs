using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260613000000_AddTenantVertical")]
public sealed class AddTenantVertical : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("vertical", "tenants", "character varying(20)", maxLength: 20, nullable: true);
    }
}
