using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260915110200_AddVisualizationSceneMode")]
public partial class AddVisualizationSceneMode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "mode",
            schema: "speed_reading",
            table: "visualization_scenes",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "assessment");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "mode",
            schema: "speed_reading",
            table: "visualization_scenes");
}
