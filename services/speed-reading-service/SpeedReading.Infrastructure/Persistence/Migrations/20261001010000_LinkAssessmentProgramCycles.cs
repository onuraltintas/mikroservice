using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261001010000_LinkAssessmentProgramCycles")]
public sealed class LinkAssessmentProgramCycles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve historical attempts without guessing which program they measured.
        migrationBuilder.AddColumn<Guid>("program_progress_id", "assessment_attempts",
            schema: "speed_reading", type: "uuid", nullable: true);
        migrationBuilder.CreateIndex("IX_assessment_attempts_program_progress_id", "assessment_attempts",
            "program_progress_id", schema: "speed_reading");
        migrationBuilder.CreateIndex("IX_assessment_attempts_student_id_program_progress_id_phase", "assessment_attempts",
            new[] { "student_id", "program_progress_id", "phase" }, schema: "speed_reading");
        migrationBuilder.AddForeignKey("fk_assessment_program_progress",
            "assessment_attempts", "program_progress_id", "student_program_progress",
            schema: "speed_reading", principalSchema: "speed_reading", principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("fk_assessment_program_progress",
            "assessment_attempts", schema: "speed_reading");
        migrationBuilder.DropIndex("IX_assessment_attempts_student_id_program_progress_id_phase", "assessment_attempts",
            schema: "speed_reading");
        migrationBuilder.DropIndex("IX_assessment_attempts_program_progress_id", "assessment_attempts", schema: "speed_reading");
        migrationBuilder.DropColumn("program_progress_id", "assessment_attempts", schema: "speed_reading");
    }
}
