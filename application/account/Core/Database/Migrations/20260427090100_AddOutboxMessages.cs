using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260427090100_AddOutboxMessages")]
public sealed class AddOutboxMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "outbox_messages",
            table => new
            {
                id = table.Column<Guid>("uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                processed_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                next_attempt_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                locked_until_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                type = table.Column<string>("text", nullable: false),
                payload = table.Column<string>("jsonb", nullable: false),
                attempts = table.Column<int>("integer", nullable: false),
                last_error = table.Column<string>("text", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_outbox_messages", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_outbox_messages_processed_at_next_attempt_at_locked_until_at", "outbox_messages", ["processed_at", "next_attempt_at", "locked_until_at"]);
    }
}
