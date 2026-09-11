using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260912000000_AddAssessmentStudyDefinitions")]
public partial class AddAssessmentStudyDefinitions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assessment_study_definitions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                study_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                protocol_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                cohort_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                consent_document_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                consent_document_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_assessment_study_definitions", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_assessment_study_definitions_study_code_cohort_code",
            schema: "speed_reading",
            table: "assessment_study_definitions",
            columns: new[] { "study_code", "cohort_code" },
            unique: true);

        migrationBuilder.AddColumn<Guid>(name: "study_definition_id", schema: "speed_reading", table: "assessment_study_enrollments", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<string>(name: "consent_document_version", schema: "speed_reading", table: "assessment_study_enrollments", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "consent_document_reference", schema: "speed_reading", table: "assessment_study_enrollments", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.CreateIndex(name: "ix_assessment_study_enrollments_study_definition_id", schema: "speed_reading", table: "assessment_study_enrollments", column: "study_definition_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ix_assessment_study_enrollments_study_definition_id", schema: "speed_reading", table: "assessment_study_enrollments");
        migrationBuilder.DropColumn(name: "study_definition_id", schema: "speed_reading", table: "assessment_study_enrollments");
        migrationBuilder.DropColumn(name: "consent_document_version", schema: "speed_reading", table: "assessment_study_enrollments");
        migrationBuilder.DropColumn(name: "consent_document_reference", schema: "speed_reading", table: "assessment_study_enrollments");
        migrationBuilder.DropTable(name: "assessment_study_definitions", schema: "speed_reading");
    }
}
