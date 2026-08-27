using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827160000_AlignOwnedNotificationPreferenceUniqueness")]
public partial class AlignOwnedNotificationPreferenceUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_notification_preferences_user_id",
            schema: "speed_reading",
            table: "notification_preferences");

        migrationBuilder.CreateIndex(
            name: "ux_notification_preferences_user_id_notification_type",
            schema: "speed_reading",
            table: "notification_preferences",
            columns: new[] { "user_id", "notification_type" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_notification_preferences_user_id_notification_type",
            schema: "speed_reading",
            table: "notification_preferences");

        migrationBuilder.CreateIndex(
            name: "ux_notification_preferences_user_id",
            schema: "speed_reading",
            table: "notification_preferences",
            column: "user_id",
            unique: true);
    }
}
