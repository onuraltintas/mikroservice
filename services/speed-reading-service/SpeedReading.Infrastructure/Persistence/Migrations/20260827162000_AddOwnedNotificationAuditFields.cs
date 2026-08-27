using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827162000_AddOwnedNotificationAuditFields")]
public partial class AddOwnedNotificationAuditFields : Migration
{
    private static readonly string[] Tables =
    [
        "notifications",
        "notification_preferences",
        "notification_type_preferences",
        "push_subscriptions",
        "announcements",
        "announcement_user_interactions",
        "email_templates",
        "email_campaigns",
        "email_campaign_logs"
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in Tables)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "speed_reading",
                table: table,
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "speed_reading",
                table: table,
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                schema: "speed_reading",
                table: table,
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "speed_reading",
                table: table,
                type: "uuid",
                nullable: true);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in Tables)
        {
            migrationBuilder.DropColumn(name: "deleted_by", schema: "speed_reading", table: table);
            migrationBuilder.DropColumn(name: "deleted_at", schema: "speed_reading", table: table);
            migrationBuilder.DropColumn(name: "updated_by", schema: "speed_reading", table: table);
            migrationBuilder.DropColumn(name: "created_by", schema: "speed_reading", table: table);
        }
    }
}
