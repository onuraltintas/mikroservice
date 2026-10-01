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
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS speed_reading.ix_assessment_attempts_student_id_phase_form_version_active;
            DROP INDEX IF EXISTS speed_reading."IX_assessment_attempts_student_id_phase_form_version";
            """);
        migrationBuilder.CreateIndex("ux_assessment_baseline_active", "assessment_attempts",
            new[] { "student_id", "phase", "form_version" }, schema: "speed_reading", unique: true,
            filter: "status = 1 AND phase = 1");
        migrationBuilder.CreateIndex("ux_assessment_program_phase_active", "assessment_attempts",
            new[] { "student_id", "program_progress_id", "phase", "form_version" }, schema: "speed_reading",
            unique: true, filter: "status = 1 AND program_progress_id IS NOT NULL");
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
        migrationBuilder.DropIndex("ux_assessment_baseline_active", "assessment_attempts", schema: "speed_reading");
        migrationBuilder.DropIndex("ux_assessment_program_phase_active", "assessment_attempts", schema: "speed_reading");
        // Older code supports only one open attempt per phase/form. Preserve
        // all history and close only older conflicting open attempts on rollback.
        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT id, ROW_NUMBER() OVER (
                    PARTITION BY student_id, phase, form_version ORDER BY started_at DESC, id
                ) AS rank FROM speed_reading.assessment_attempts WHERE status = 1
            )
            UPDATE speed_reading.assessment_attempts AS attempt
            SET status = 3, updated_at = NOW()
            FROM ranked WHERE attempt.id = ranked.id AND ranked.rank > 1;
            """);
        migrationBuilder.CreateIndex("ix_assessment_attempts_student_id_phase_form_version_active", "assessment_attempts",
            new[] { "student_id", "phase", "form_version" }, schema: "speed_reading", unique: true,
            filter: "status = 1");
        migrationBuilder.DropForeignKey("fk_assessment_program_progress",
            "assessment_attempts", schema: "speed_reading");
        migrationBuilder.DropIndex("IX_assessment_attempts_student_id_program_progress_id_phase", "assessment_attempts",
            schema: "speed_reading");
        migrationBuilder.DropIndex("IX_assessment_attempts_program_progress_id", "assessment_attempts", schema: "speed_reading");
        migrationBuilder.DropColumn("program_progress_id", "assessment_attempts", schema: "speed_reading");
    }
}
