using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827159000_AddOwnedNotificationPreferenceSourceFields")]
public partial class AddOwnedNotificationPreferenceSourceFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "notification_type",
            schema: "speed_reading",
            table: "notification_preferences",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "enable_instant",
            schema: "speed_reading",
            table: "notification_preferences",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "enable_daily",
            schema: "speed_reading",
            table: "notification_preferences",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "enable_weekly",
            schema: "speed_reading",
            table: "notification_preferences",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "notification_type", schema: "speed_reading", table: "notification_preferences");
        migrationBuilder.DropColumn(name: "enable_instant", schema: "speed_reading", table: "notification_preferences");
        migrationBuilder.DropColumn(name: "enable_daily", schema: "speed_reading", table: "notification_preferences");
        migrationBuilder.DropColumn(name: "enable_weekly", schema: "speed_reading", table: "notification_preferences");
    }
}
