using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260501180000_AddTenantMessagingFoundation")]
public sealed class AddTenantMessagingFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_messaging_profiles",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                app_slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                owner_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                owner_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                country_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                twilio_subaccount_sid = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                twilio_subaccount_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                twilio_messaging_service_sid = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                provisioning_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                whats_app_approval_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                business_category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                website_url = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                support_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                logo_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenant_messaging_profiles", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "tenant_phone_number_assignments",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                messaging_profile_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                twilio_phone_number_sid = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                country_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                sms_capable = table.Column<bool>(type: "boolean", nullable: false),
                whats_app_capable = table.Column<bool>(type: "boolean", nullable: false),
                webhook_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                assignment_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenant_phone_number_assignments", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "tenant_message_templates",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                messaging_profile_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                template_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                approval_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                external_template_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenant_message_templates", x => x.id);
            }
        );

        migrationBuilder.CreateIndex("ix_tenant_messaging_profiles_tenant_id_app_slug_owner_type_owner_id", "tenant_messaging_profiles", ["tenant_id", "app_slug", "owner_type", "owner_id"], unique: true);
        migrationBuilder.CreateIndex("ix_tenant_phone_number_assignments_phone_number", "tenant_phone_number_assignments", "phone_number", unique: true);
        migrationBuilder.CreateIndex("ix_tenant_phone_number_assignments_tenant_id_messaging_profile_id_assignment_status", "tenant_phone_number_assignments", ["tenant_id", "messaging_profile_id", "assignment_status"]);
        migrationBuilder.CreateIndex("ix_tenant_phone_number_assignments_twilio_phone_number_sid", "tenant_phone_number_assignments", "twilio_phone_number_sid", unique: true);
        migrationBuilder.CreateIndex("ix_tenant_message_templates_tenant_id_messaging_profile_id_template_key_language", "tenant_message_templates", ["tenant_id", "messaging_profile_id", "template_key", "language"], unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("tenant_message_templates");
        migrationBuilder.DropTable("tenant_phone_number_assignments");
        migrationBuilder.DropTable("tenant_messaging_profiles");
    }
}
