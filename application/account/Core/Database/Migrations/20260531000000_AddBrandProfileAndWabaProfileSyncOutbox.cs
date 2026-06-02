using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Phase 7a — WABA Brand Profile.
///     <list type="bullet">
///         <item>
///             Adds <c>brand_profile</c> (jsonb, nullable) to <c>tenants</c>. The column is the
///             serialized form of the owned-type <c>BrandProfile</c> value object configured via
///             <c>OwnsOne(...).ToJson()</c> in <c>TenantConfiguration</c>.
///         </item>
///         <item>
///             Creates <c>waba_profile_sync_outboxes</c>, an append-only outbox table written by the
///             <c>UpdateTenantBrandProfileCommand</c> and drained by the Phase 7b TickerQ sync job
///             that pushes the brand profile to Meta. Polling index is on
///             <c>(status, next_attempt_at)</c> to match the sync job's "next due" query.
///         </item>
///     </list>
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

        migrationBuilder.CreateTable(
            "waba_profile_sync_outboxes",
            table => new
            {
                id = table.Column<long>("bigint", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                phone_number_id = table.Column<string>("text", nullable: false),
                requested_payload = table.Column<string>("jsonb", nullable: false),
                brand_logo_url = table.Column<string>("text", nullable: true),
                logo_upload_handle = table.Column<string>("text", nullable: true),
                last_synced_logo_hash = table.Column<string>("text", nullable: true),
                status = table.Column<string>("text", nullable: false),
                attempts = table.Column<int>("integer", nullable: false),
                last_error = table.Column<string>("text", nullable: true),
                next_attempt_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                synced_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_waba_profile_sync_outboxes", x => x.id);
                table.ForeignKey(
                    "fk_waba_profile_sync_outboxes_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Polling index for the Phase 7b sync job: "pending rows whose NextAttemptAt has elapsed".
        migrationBuilder.CreateIndex(
            "ix_waba_profile_sync_outboxes_status_next_attempt_at",
            "waba_profile_sync_outboxes",
            new[] { "status", "next_attempt_at" }
        );
    }
}
