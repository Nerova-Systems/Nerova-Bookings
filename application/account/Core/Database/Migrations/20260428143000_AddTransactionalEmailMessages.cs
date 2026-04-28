using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260428143000_AddTransactionalEmailMessages")]
public sealed class AddTransactionalEmailMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "transactional_email_messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                html_content = table.Column<string>(type: "text", nullable: false),
                template_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                status = table.Column<string>(type: "text", nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                dead_lettered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_transactional_email_messages", x => x.id);
            }
        );

        migrationBuilder.CreateIndex("ix_transactional_email_messages_correlation_id", "transactional_email_messages", "correlation_id");
        migrationBuilder.CreateIndex("ix_transactional_email_messages_status_next_attempt_at", "transactional_email_messages", ["status", "next_attempt_at"]);
        migrationBuilder.CreateIndex("ix_transactional_email_messages_template_key", "transactional_email_messages", "template_key");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("transactional_email_messages");
    }
}
