using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Account.Database.Migrations;

/// <summary>
///     Wave 2 — EE Audit Log: creates the <c>audit_log_entries</c> table.
///     <list type="bullet">
///         <item>Append-only table; rows are never updated or deleted (except via tenant cascade).</item>
///         <item><c>metadata</c> is stored as <c>jsonb</c> for efficient Postgres querying.</item>
///         <item>Three indexes support the expected query patterns: by tenant, by actor, and by date range.</item>
///     </list>
/// </summary>
[DbContext(typeof(AccountDbContext))]
[Migration("20260523000000_AddAuditLog")]
public sealed class AddAuditLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "audit_log_entries",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                actor_user_id = table.Column<string>("text", nullable: true),
                actor_email = table.Column<string>("text", nullable: false),
                resource = table.Column<string>("text", nullable: false),
                action = table.Column<string>("text", nullable: false),
                resource_id = table.Column<string>("text", nullable: true),
                metadata = table.Column<string>("jsonb", nullable: true),
                ip_address = table.Column<string>("text", nullable: true),
                user_agent = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_log_entries", x => x.id);
                table.ForeignKey(
                    "fk_audit_log_entries_tenants_tenant_id",
                    x => x.tenant_id,
                    "tenants",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        // Primary access pattern: all entries for a tenant (supplements the EF global query filter).
        migrationBuilder.CreateIndex(
            "ix_audit_log_entries_tenant_id",
            "audit_log_entries",
            "tenant_id"
        );

        // Supports actor-based filtering (admin investigation).
        migrationBuilder.CreateIndex(
            "ix_audit_log_entries_actor_user_id",
            "audit_log_entries",
            "actor_user_id",
            filter: "actor_user_id IS NOT NULL"
        );

        // Supports time-range queries.
        migrationBuilder.CreateIndex(
            "ix_audit_log_entries_created_at",
            "audit_log_entries",
            "created_at"
        );
    }
}
