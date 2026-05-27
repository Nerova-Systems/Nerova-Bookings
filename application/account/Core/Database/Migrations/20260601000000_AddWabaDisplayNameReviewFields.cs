using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Phase 7c — WABA display-name review state.
///     <list type="bullet">
///         <item>
///             Adds the five columns that back <c>WabaConfiguration.RequestDisplayNameChange</c>
///             and <c>MarkDisplayNameReviewResult</c>:
///             <c>requested_display_name</c>, <c>display_name_status</c>,
///             <c>display_name_review_requested_at</c>, <c>display_name_last_checked_at</c>,
///             and <c>verified_name</c>.
///         </item>
///         <item>
///             <c>display_name_status</c> is non-nullable; existing rows seed to <c>"None"</c>
///             so the poller's "pending review" filter sees the historical fleet as not in
///             review (matching the in-domain default for the enum).
///         </item>
///     </list>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260601000000_AddWabaDisplayNameReviewFields")]
public sealed class AddWabaDisplayNameReviewFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "requested_display_name",
            "waba_configurations",
            "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "display_name_status",
            "waba_configurations",
            "text",
            nullable: false,
            defaultValue: "None"
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            "display_name_review_requested_at",
            "waba_configurations",
            "timestamptz",
            nullable: true
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            "display_name_last_checked_at",
            "waba_configurations",
            "timestamptz",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "verified_name",
            "waba_configurations",
            "text",
            nullable: true
        );
    }
}
