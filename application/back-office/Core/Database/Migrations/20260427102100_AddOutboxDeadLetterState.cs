using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BackOffice.Database.Migrations;

[DbContext(typeof(BackOfficeDbContext))]
[Migration("20260427102100_AddOutboxDeadLetterState")]
public sealed class AddOutboxDeadLetterState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            "dead_lettered_at",
            "outbox_messages",
            "timestamptz",
            nullable: true
        );

        migrationBuilder.CreateIndex("ix_outbox_messages_dead_lettered_at", "outbox_messages", "dead_lettered_at");
    }
}
