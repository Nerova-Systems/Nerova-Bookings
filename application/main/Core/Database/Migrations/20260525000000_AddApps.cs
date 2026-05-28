using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260525000000_AddApps")]
public sealed class AddApps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // --- 1. apps table (platform-owned registry; no tenant scope) ---

        migrationBuilder.CreateTable(
            "apps",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                name = table.Column<string>("character varying(120)", maxLength: 120, nullable: false),
                category = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                description = table.Column<string>("character varying(2000)", maxLength: 2000, nullable: false),
                logo_url = table.Column<string>("character varying(500)", maxLength: 500, nullable: false),
                is_active = table.Column<bool>("boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_apps", app => app.id)
        );

        migrationBuilder.CreateIndex("ix_apps_category", "apps", "category");
        migrationBuilder.CreateIndex("ix_apps_is_active", "apps", "is_active");

        // --- 2. credentials table (per-user OAuth tokens, encrypted at rest) ---

        migrationBuilder.CreateTable(
            "credentials",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                user_id = table.Column<string>("text", nullable: false),
                app_slug = table.Column<string>("text", nullable: false),
                encrypted_key = table.Column<string>("text", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_credentials", credential => credential.id)
        );

        migrationBuilder.CreateIndex(
            "ix_credentials_tenant_id_user_id_app_slug",
            "credentials",
            ["tenant_id", "user_id", "app_slug"],
            unique: true
        );
        migrationBuilder.CreateIndex("ix_credentials_app_slug", "credentials", "app_slug");

        // --- 3. app_installations table (tenant-level install marker) ---

        migrationBuilder.CreateTable(
            "app_installations",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                app_slug = table.Column<string>("text", nullable: false),
                installed_by_user_id = table.Column<string>("text", nullable: false),
                installed_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_app_installations", installation => installation.id)
        );

        migrationBuilder.CreateIndex(
            "ix_app_installations_tenant_id_app_slug",
            "app_installations",
            ["tenant_id", "app_slug"],
            unique: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("app_installations");
        migrationBuilder.DropTable("credentials");
        migrationBuilder.DropTable("apps");
    }
}
