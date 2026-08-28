using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpeedReading.Infrastructure.Persistence;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260828130000_AddAssessmentMeasurementFoundation")]
public partial class AddAssessmentMeasurementFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assessment_attempts",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                form_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                age_group_configuration_id = table.Column<Guid>(type: "uuid", nullable: true),
                expected_exercise_count = table.Column<int>(type: "integer", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_assessment_attempts", x => x.id);
                table.ForeignKey(
                    name: "fk_assessment_attempts_age_group_configuration_id",
                    column: x => x.age_group_configuration_id,
                    principalSchema: "speed_reading",
                    principalTable: "age_group_configurations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_assessment_attempts_student_id_phase_status_started_at",
            schema: "speed_reading",
            table: "assessment_attempts",
            columns: new[] { "student_id", "phase", "status", "started_at" });

        migrationBuilder.CreateIndex(
            name: "ix_assessment_attempts_student_id_phase_form_version_active",
            schema: "speed_reading",
            table: "assessment_attempts",
            columns: new[] { "student_id", "phase", "form_version" },
            unique: true,
            filter: "status = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "assessment_attempts",
            schema: "speed_reading");
    }
}
