using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827152000_AddOwnedNotifications")]
public partial class AddOwnedNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notifications",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<int>(type: "integer", nullable: false),
                channel = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                data = table.Column<string>(type: "text", nullable: true),
                action_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                priority = table.Column<int>(type: "integer", nullable: false),
                user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                user_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                user_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_notifications", x => x.id));

        migrationBuilder.CreateTable(
            name: "notification_preferences",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                push_enabled = table.Column<bool>(type: "boolean", nullable: false),
                in_app_enabled = table.Column<bool>(type: "boolean", nullable: false),
                sms_enabled = table.Column<bool>(type: "boolean", nullable: false),
                achievements_enabled = table.Column<bool>(type: "boolean", nullable: false),
                level_up_enabled = table.Column<bool>(type: "boolean", nullable: false),
                daily_reminder_enabled = table.Column<bool>(type: "boolean", nullable: false),
                streak_milestone_enabled = table.Column<bool>(type: "boolean", nullable: false),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_notification_preferences", x => x.id));

        migrationBuilder.CreateTable(
            name: "notification_type_preferences",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                notification_type = table.Column<int>(type: "integer", nullable: false),
                enable_in_app = table.Column<bool>(type: "boolean", nullable: false),
                enable_email = table.Column<bool>(type: "boolean", nullable: false),
                enable_push = table.Column<bool>(type: "boolean", nullable: false),
                preferred_time = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_notification_type_preferences", x => x.id));

        migrationBuilder.CreateTable(
            name: "push_subscriptions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                p256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                auth = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                user_agent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_push_subscriptions", x => x.id));

        migrationBuilder.CreateTable(
            name: "announcements",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                content = table.Column<string>(type: "text", nullable: false),
                type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                target_audience = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                target_institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                target_roles = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_pinned = table.Column<bool>(type: "boolean", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                action_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                plain_text_content = table.Column<string>(type: "text", nullable: true),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                display_type = table.Column<int>(type: "integer", nullable: false),
                icon = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                color_theme = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                action_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                send_email_notification = table.Column<bool>(type: "boolean", nullable: false),
                create_in_app_notification = table.Column<bool>(type: "boolean", nullable: false),
                email_campaign_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_announcements", x => x.id));

        migrationBuilder.CreateTable(
            name: "email_templates",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                variables = table.Column<string>(type: "text", nullable: true),
                is_system = table.Column<bool>(type: "boolean", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                available_variables = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_email_templates", x => x.id));

        migrationBuilder.CreateTable(
            name: "email_campaigns",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                target_roles = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                target_institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                template_id = table.Column<Guid>(type: "uuid", nullable: true),
                scheduled_for = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                total_recipients = table.Column<int>(type: "integer", nullable: false),
                sent_count = table.Column<int>(type: "integer", nullable: false),
                failed_count = table.Column<int>(type: "integer", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                plain_text_body = table.Column<string>(type: "text", nullable: true),
                include_all_users = table.Column<bool>(type: "boolean", nullable: false),
                include_subscribers = table.Column<bool>(type: "boolean", nullable: false),
                opened_count = table.Column<int>(type: "integer", nullable: false),
                clicked_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_email_campaigns", x => x.id));

        migrationBuilder.CreateTable(
            name: "announcement_user_interactions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                viewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                clicked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_announcement_user_interactions", x => x.id));

        migrationBuilder.CreateTable(
            name: "email_campaign_logs",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                recipient_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_email_campaign_logs", x => x.id));

        migrationBuilder.CreateIndex("ix_notifications_user_id", "notifications", "user_id", "speed_reading");
        migrationBuilder.CreateIndex("ix_notifications_user_id_status", "notifications", new[] { "user_id", "status" }, "speed_reading");
        migrationBuilder.CreateIndex("ux_notification_preferences_user_id", "notification_preferences", "user_id", "speed_reading", unique: true);
        migrationBuilder.CreateIndex("ux_notification_type_preferences_user_id_notification_type", "notification_type_preferences", new[] { "user_id", "notification_type" }, "speed_reading", unique: true);
        migrationBuilder.CreateIndex("ux_push_subscriptions_endpoint", "push_subscriptions", "endpoint", "speed_reading", unique: true);
        migrationBuilder.CreateIndex("ix_push_subscriptions_user_id", "push_subscriptions", "user_id", "speed_reading");
        migrationBuilder.CreateIndex("ix_announcements_is_active", "announcements", "is_active", "speed_reading");
        migrationBuilder.CreateIndex("ix_announcements_created_at", "announcements", "created_at", "speed_reading");
        migrationBuilder.CreateIndex("ix_email_templates_code", "email_templates", "code", "speed_reading", unique: true, filter: "code IS NOT NULL");
        migrationBuilder.CreateIndex("ix_email_campaigns_template_id", "email_campaigns", "template_id", "speed_reading");
        migrationBuilder.CreateIndex("ix_email_campaigns_status", "email_campaigns", "status", "speed_reading");
        migrationBuilder.CreateIndex("ux_announcement_user_interactions_announcement_id_user_id", "announcement_user_interactions", new[] { "announcement_id", "user_id" }, "speed_reading", unique: true);
        migrationBuilder.CreateIndex("ix_email_campaign_logs_campaign_id", "email_campaign_logs", "campaign_id", "speed_reading");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "email_campaign_logs", schema: "speed_reading");
        migrationBuilder.DropTable(name: "announcement_user_interactions", schema: "speed_reading");
        migrationBuilder.DropTable(name: "email_campaigns", schema: "speed_reading");
        migrationBuilder.DropTable(name: "email_templates", schema: "speed_reading");
        migrationBuilder.DropTable(name: "announcements", schema: "speed_reading");
        migrationBuilder.DropTable(name: "push_subscriptions", schema: "speed_reading");
        migrationBuilder.DropTable(name: "notification_type_preferences", schema: "speed_reading");
        migrationBuilder.DropTable(name: "notification_preferences", schema: "speed_reading");
        migrationBuilder.DropTable(name: "notifications", schema: "speed_reading");
    }
}
