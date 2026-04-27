using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BackOffice.Database.Migrations;

[DbContext(typeof(BackOfficeDbContext))]
[Migration("20260427090200_AddCatalogAndOutboxMessages")]
public sealed class AddCatalogAndOutboxMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "catalog_tenants",
            table => new
            {
                id = table.Column<long>("bigint", nullable: false),
                name = table.Column<string>("text", nullable: false),
                state = table.Column<string>("text", nullable: false),
                plan = table.Column<string>("text", nullable: false),
                logo_url = table.Column<string>("text", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                deleted_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                source_updated_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_catalog_tenants", x => x.id); }
        );

        migrationBuilder.CreateTable(
            "catalog_users",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                email = table.Column<string>("text", nullable: false),
                role = table.Column<string>("text", nullable: false),
                first_name = table.Column<string>("text", nullable: false),
                last_name = table.Column<string>("text", nullable: false),
                title = table.Column<string>("text", nullable: false),
                email_confirmed = table.Column<bool>("boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                last_seen_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                deleted_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                source_updated_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_catalog_users", x => x.id); }
        );

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

        migrationBuilder.CreateTable(
            "processed_catalog_events",
            table => new
            {
                id = table.Column<Guid>("uuid", nullable: false),
                processed_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_processed_catalog_events", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_catalog_tenants_deleted_at", "catalog_tenants", "deleted_at");
        migrationBuilder.CreateIndex("ix_catalog_users_deleted_at", "catalog_users", "deleted_at");
        migrationBuilder.CreateIndex("ix_catalog_users_email", "catalog_users", "email");
        migrationBuilder.CreateIndex("ix_catalog_users_tenant_id", "catalog_users", "tenant_id");
        migrationBuilder.CreateIndex("ix_outbox_messages_processed_at_next_attempt_at_locked_until_at", "outbox_messages", ["processed_at", "next_attempt_at", "locked_until_at"]);
    }
}
