using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Brand Profile.
///     Adds <c>brand_profile</c> (jsonb, nullable) to <c>tenants</c>. The column is the
///     serialized form of the owned-type <c>BrandProfile</c> value object configured via
///     <c>OwnsOne(...).ToJson()</c> in <c>TenantConfiguration</c>.
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260531000000_AddBrandProfileAndWabaProfileSyncOutbox")]
public sealed class AddBrandProfileAndWabaProfileSyncOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "brand_profile",
            "tenants",
            "jsonb",
            nullable: true
        );
    }
}
