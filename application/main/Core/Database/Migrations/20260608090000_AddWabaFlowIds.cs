using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260608090000_AddWabaFlowIds")]
public sealed class AddWabaFlowIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Stores the Meta Flow IDs created for each tenant's WABA during embedded signup.
        migrationBuilder.AddColumn<string>("booking_flow_id", "whats_app_business_accounts", "text", nullable: true);
        migrationBuilder.AddColumn<string>("login_flow_id", "whats_app_business_accounts", "text", nullable: true);
    }
}
