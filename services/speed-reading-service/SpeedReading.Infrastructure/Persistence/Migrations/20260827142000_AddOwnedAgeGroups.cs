using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827142000_AddOwnedAgeGroups")]
public partial class AddOwnedAgeGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "age_group_configurations",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                MinAge = table.Column<int>(type: "integer", nullable: false),
                MaxAge = table.Column<int>(type: "integer", nullable: true),
                RecommendedWPM = table.Column<int>(type: "integer", nullable: false),
                MinWPM = table.Column<int>(type: "integer", nullable: false),
                MaxWPM = table.Column<int>(type: "integer", nullable: false),
                RecommendedComprehension = table.Column<int>(type: "integer", nullable: false),
                RecommendedDailyMinutes = table.Column<int>(type: "integer", nullable: false),
                DefaultDifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                OrderIndex = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_age_group_configurations", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_age_group_configurations_name",
            schema: "speed_reading",
            table: "age_group_configurations",
            column: "Name",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_age_group_configurations_is_active_min_age_max_age",
            schema: "speed_reading",
            table: "age_group_configurations",
            columns: new[] { "IsActive", "MinAge", "MaxAge" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "age_group_configurations",
            schema: "speed_reading");
    }
}
