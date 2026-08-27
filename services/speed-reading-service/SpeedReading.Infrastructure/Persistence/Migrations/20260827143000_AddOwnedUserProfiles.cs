using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827143000_AddOwnedUserProfiles")]
public partial class AddOwnedUserProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_profiles",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CurrentLevel = table.Column<int>(type: "integer", nullable: false),
                TargetWPM = table.Column<int>(type: "integer", nullable: false),
                TargetComprehension = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                DailyGoalMinutes = table.Column<int>(type: "integer", nullable: false),
                AgeGroupConfigurationId = table.Column<Guid>(type: "uuid", nullable: true),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_profiles", x => x.id);
                table.ForeignKey(
                    name: "fk_user_profiles_age_group_configurations_age_group_configuration_id",
                    column: x => x.AgeGroupConfigurationId,
                    principalSchema: "speed_reading",
                    principalTable: "age_group_configurations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_user_profiles_user_id",
            schema: "speed_reading",
            table: "user_profiles",
            column: "UserId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_user_profiles_age_group_configuration_id",
            schema: "speed_reading",
            table: "user_profiles",
            column: "AgeGroupConfigurationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_profiles",
            schema: "speed_reading");
    }
}
