using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Account.Database.Migrations;

[DbContext(typeof(AccountDbContext))]
[Migration("20260427185546_AddMassTransitOutboxState")]
public sealed class AddMassTransitOutboxState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "masstransit_inbox_states",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                consumer_id = table.Column<Guid>(type: "uuid", nullable: false),
                lock_id = table.Column<Guid>(type: "uuid", nullable: false),
                row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                receive_count = table.Column<int>(type: "integer", nullable: false),
                expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_sequence_number = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_masstransit_inbox_states", x => x.id);
                table.UniqueConstraint("ak_inbox_state_message_id_consumer_id", x => new { x.message_id, x.consumer_id });
            }
        );

        migrationBuilder.CreateTable(
            name: "masstransit_outbox_states",
            columns: table => new
            {
                outbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                lock_id = table.Column<Guid>(type: "uuid", nullable: false),
                row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_sequence_number = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_masstransit_outbox_states", x => x.outbox_id); }
        );

        migrationBuilder.CreateTable(
            name: "masstransit_outbox_messages",
            columns: table => new
            {
                sequence_number = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                enqueue_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                sent_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                headers = table.Column<string>(type: "text", nullable: true),
                properties = table.Column<string>(type: "text", nullable: true),
                inbox_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                inbox_consumer_id = table.Column<Guid>(type: "uuid", nullable: true),
                outbox_id = table.Column<Guid>(type: "uuid", nullable: true),
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                content_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                message_type = table.Column<string>(type: "text", nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                initiator_id = table.Column<Guid>(type: "uuid", nullable: true),
                request_id = table.Column<Guid>(type: "uuid", nullable: true),
                source_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                destination_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                response_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                fault_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_masstransit_outbox_messages", x => x.sequence_number);
                table.ForeignKey(
                    name: "fk_masstransit_outbox_messages_masstransit_inbox_states_inbox_",
                    columns: x => new { x.inbox_message_id, x.inbox_consumer_id },
                    principalTable: "masstransit_inbox_states",
                    principalColumns: new[] { "message_id", "consumer_id" }
                );
                table.ForeignKey(
                    name: "fk_masstransit_outbox_messages_outbox_state_outbox_id",
                    column: x => x.outbox_id,
                    principalTable: "masstransit_outbox_states",
                    principalColumn: "outbox_id"
                );
            }
        );

        migrationBuilder.CreateIndex("ix_masstransit_inbox_states_delivered", "masstransit_inbox_states", "delivered");
        migrationBuilder.CreateIndex("ix_masstransit_outbox_messages_enqueue_time", "masstransit_outbox_messages", "enqueue_time");
        migrationBuilder.CreateIndex("ix_masstransit_outbox_messages_expiration_time", "masstransit_outbox_messages", "expiration_time");
        migrationBuilder.CreateIndex(
            "ix_masstransit_outbox_messages_inbox_message_id_inbox_consumer",
            "masstransit_outbox_messages",
            ["inbox_message_id", "inbox_consumer_id", "sequence_number"],
            unique: true
        );
        migrationBuilder.CreateIndex(
            "ix_masstransit_outbox_messages_outbox_id_sequence_number",
            "masstransit_outbox_messages",
            ["outbox_id", "sequence_number"],
            unique: true
        );
        migrationBuilder.CreateIndex("ix_masstransit_outbox_states_created", "masstransit_outbox_states", "created");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("masstransit_outbox_messages");
        migrationBuilder.DropTable("masstransit_inbox_states");
        migrationBuilder.DropTable("masstransit_outbox_states");
    }
}
