using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

partial class AddSpeedReadingProfilePersonalData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DateOfBirth",
            schema: "speed_reading",
            table: "user_profiles",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LearningStyle",
            schema: "speed_reading",
            table: "user_profiles",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LearningStyle",
            schema: "speed_reading",
            table: "user_profiles");

        migrationBuilder.DropColumn(
            name: "DateOfBirth",
            schema: "speed_reading",
            table: "user_profiles");
    }
}
