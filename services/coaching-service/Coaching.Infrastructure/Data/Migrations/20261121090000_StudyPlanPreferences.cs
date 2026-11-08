using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Paket H4a (H4b review, user decision K2 covers this profile): the student's plan preferences, one row per student: the YKS exam
    /// focus (Tyt, Balanced, Ayt) and the wizard's tempo (Relaxed, Balanced, Intense), saved without touching the free hours. A new table
    /// only: no existing row changes (a routine saved before shows its own answers until it or the preferences are saved). Down drops
    /// the table (the saved preferences are lost).
    /// </summary>
    public partial class StudyPlanPreferencesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_plan_preferences",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamFocus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Tempo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_plan_preferences", x => x.Id);
                    table.CheckConstraint("ck_study_plan_preferences_exam_focus", "\"ExamFocus\" IS NULL OR \"ExamFocus\" IN ('Tyt', 'Balanced', 'Ayt')");
                    table.CheckConstraint("ck_study_plan_preferences_tempo", "\"Tempo\" IS NULL OR \"Tempo\" IN ('Relaxed', 'Balanced', 'Intense')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_preferences_StudentId",
                schema: "coaching",
                table: "study_plan_preferences",
                column: "StudentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_plan_preferences",
                schema: "coaching");
        }
    }
}
