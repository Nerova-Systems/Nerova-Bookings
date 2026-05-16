using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260516120000_RenameBillingEventProviderColumns")]
public sealed class RenameBillingEventProviderColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn("stripe_event_id", "billing_events", "provider_event_id");
        migrationBuilder.RenameIndex("ix_billing_events_stripe_event_id", "ix_billing_events_provider_event_id", "billing_events");
        migrationBuilder.DropColumn("last_synced_stripe_event_created_at", "subscriptions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>("last_synced_stripe_event_created_at", "subscriptions", "timestamptz", nullable: true);
        migrationBuilder.RenameIndex("ix_billing_events_provider_event_id", "ix_billing_events_stripe_event_id", "billing_events");
        migrationBuilder.RenameColumn("provider_event_id", "billing_events", "stripe_event_id");
    }
}
