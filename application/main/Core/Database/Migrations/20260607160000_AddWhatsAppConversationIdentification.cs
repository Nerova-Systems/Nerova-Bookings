using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260607160000_AddWhatsAppConversationIdentification")]
public sealed class AddWhatsAppConversationIdentification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Tracks whether the customer has been matched to a Client record (by phone) or completed the
        // login/registration Flow. Defaults to false for all existing rows.
        migrationBuilder.AddColumn<bool>(
            "is_identified",
            "whats_app_conversations",
            "boolean",
            nullable: false,
            defaultValue: false
        );

        // Rename AwaitingFlowCompletion -> AwaitingBookingFlow so existing in-flight sessions are
        // transparently upgraded to the new state name.
        migrationBuilder.Sql("""
            UPDATE whats_app_conversations
            SET state = 'AwaitingBookingFlow'
            WHERE state = 'AwaitingFlowCompletion';
            """);
    }
}
