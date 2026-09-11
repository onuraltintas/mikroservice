using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260911120000_AddHistoricalUserProfileDisplay")]
public partial class AddHistoricalUserProfileDisplay : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "HistoricalDisplayName",
            schema: "speed_reading",
            table: "user_profiles",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "HistoricalEmail",
            schema: "speed_reading",
            table: "user_profiles",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HistoricalDisplayName", schema: "speed_reading", table: "user_profiles");
        migrationBuilder.DropColumn(name: "HistoricalEmail", schema: "speed_reading", table: "user_profiles");
    }
}
