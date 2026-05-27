using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Adds the WhatsApp Flows Phase 2 columns to <c>waba_configurations</c>: the long-lived WABA
///     access token used to talk to the Meta Graph API, and the cached Meta Flow JSON we last
///     uploaded for the tenant (for debugging / re-upload after a deprecation).
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260530000000_AddFlowJsonAndAccessTokenToWabaConfiguration")]
public sealed class AddFlowJsonAndAccessTokenToWabaConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "waba_access_token",
            "waba_configurations",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "generated_flow_json",
            "waba_configurations",
            "jsonb",
            nullable: true
        );
    }
}
