using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260911110000_AddAssessmentStudyEnrollments")]
public partial class AddAssessmentStudyEnrollments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "study_code", schema: "speed_reading", table: "assessment_attempts", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "study_protocol_version", schema: "speed_reading", table: "assessment_attempts", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "study_cohort_code", schema: "speed_reading", table: "assessment_attempts", type: "character varying(100)", maxLength: 100, nullable: true);

        migrationBuilder.CreateTable(
            name: "assessment_study_enrollments",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                study_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                protocol_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                cohort_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                consent_recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_assessment_study_enrollments", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ux_assessment_study_enrollments_active",
            schema: "speed_reading",
            table: "assessment_study_enrollments",
            columns: new[] { "student_id", "study_code" },
            unique: true,
            filter: "is_active");
        migrationBuilder.CreateIndex(
            name: "ix_assessment_study_enrollments_study_code_protocol_version_cohort_code",
            schema: "speed_reading",
            table: "assessment_study_enrollments",
            columns: new[] { "study_code", "protocol_version", "cohort_code" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "assessment_study_enrollments", schema: "speed_reading");
        migrationBuilder.DropColumn(name: "study_code", schema: "speed_reading", table: "assessment_attempts");
        migrationBuilder.DropColumn(name: "study_protocol_version", schema: "speed_reading", table: "assessment_attempts");
        migrationBuilder.DropColumn(name: "study_cohort_code", schema: "speed_reading", table: "assessment_attempts");
    }
}
