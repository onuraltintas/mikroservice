using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260915110100_AddRsvpSessionCount")]
public partial class AddRsvpSessionCount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(
            name: "total_rsvp_sessions_completed",
            schema: "speed_reading",
            table: "user_gamification",
            type: "integer",
            nullable: false,
            defaultValue: 0);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "total_rsvp_sessions_completed",
            schema: "speed_reading",
            table: "user_gamification");
}
