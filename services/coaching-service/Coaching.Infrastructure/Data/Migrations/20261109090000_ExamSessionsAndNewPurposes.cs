using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// KM1 (01.11.2026): an exam can have several dated sessions (YDS and YÖKDİL twice a year, e-YDS many times). The single
    /// study_catalog_exams."ExamDate" becomes the exam's first session (label: the year) and the column is dropped, so there is
    /// one source of truth. Students may aim for one session (student_learning_contexts."TargetExamSessionId"), and the
    /// learning purposes Ags (5), Yds (6) and Yokdil (7) are allowed. Down puts each exam's next upcoming session date (else its
    /// latest one) back into "ExamDate"; it refuses while a student uses one of the new purposes, because the old code cannot
    /// read them.
    /// </summary>
    public partial class ExamSessionsAndNewPurposes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_catalog_exam_sessions",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApplicationStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ApplicationEnd = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_exam_sessions", x => x.Id);
                    table.CheckConstraint("ck_exam_session_application", "(\"ApplicationStart\" IS NULL) = (\"ApplicationEnd\" IS NULL) AND (\"ApplicationEnd\" IS NULL OR (\"ApplicationStart\" <= \"ApplicationEnd\" AND \"ApplicationEnd\" < \"Date\"))");
                    table.ForeignKey(
                        name: "FK_study_catalog_exam_sessions_study_catalog_exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_exam_sessions_ExamId_Date",
                schema: "coaching",
                table: "study_catalog_exam_sessions",
                columns: new[] { "ExamId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_exam_sessions_ExamId_Label",
                schema: "coaching",
                table: "study_catalog_exam_sessions",
                columns: new[] { "ExamId", "Label" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO coaching.study_catalog_exam_sessions ("Id", "ExamId", "Date", "Label")
                SELECT gen_random_uuid(), "Id", "ExamDate", to_char("ExamDate", 'YYYY')
                FROM coaching.study_catalog_exams WHERE "ExamDate" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(name: "ExamDate", schema: "coaching", table: "study_catalog_exams");

            migrationBuilder.AddColumn<Guid>(
                name: "TargetExamSessionId",
                schema: "coaching",
                table: "student_learning_contexts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_learning_contexts_TargetExamSessionId",
                schema: "coaching",
                table: "student_learning_contexts",
                column: "TargetExamSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_student_learning_contexts_study_catalog_exam_sessions_TargetExamSessionId",
                schema: "coaching",
                table: "student_learning_contexts",
                column: "TargetExamSessionId",
                principalSchema: "coaching",
                principalTable: "study_catalog_exam_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropCheckConstraint(name: "ck_learning_purpose", schema: "coaching", table: "student_learning_contexts");
            migrationBuilder.AddCheckConstraint(name: "ck_learning_purpose", schema: "coaching", table: "student_learning_contexts", sql: "\"Purpose\" BETWEEN 0 AND 7");

            // The goal's single net target may use the new practice scopes (AGS 80, ÖABT 50, YDS/YÖKDİL 80 questions).
            migrationBuilder.DropCheckConstraint(name: "ck_goal_net_target", schema: "coaching", table: "academic_goals");
            migrationBuilder.AddCheckConstraint(name: "ck_goal_net_target", schema: "coaching", table: "academic_goals",
                sql: "(target_net IS NULL AND target_practice_exam_code IS NULL) OR (target_net IS NOT NULL AND target_practice_exam_code IS NOT NULL AND target_net >= 0 AND target_net <= CASE target_practice_exam_code WHEN 'LGS' THEN 90 WHEN 'TYT' THEN 120 WHEN 'AYT_SAY' THEN 80 WHEN 'AYT_EA' THEN 80 WHEN 'AYT_SOZ' THEN 80 WHEN 'YDT_EN' THEN 80 WHEN 'AGS' THEN 80 WHEN 'OABT_TUR' THEN 50 WHEN 'OABT_IMAT' THEN 50 WHEN 'OABT_FEN' THEN 50 WHEN 'OABT_SOS' THEN 50 WHEN 'OABT_TDE' THEN 50 WHEN 'OABT_TAR' THEN 50 WHEN 'OABT_COG' THEN 50 WHEN 'OABT_MAT' THEN 50 WHEN 'OABT_FIZ' THEN 50 WHEN 'OABT_KIM' THEN 50 WHEN 'OABT_BIO' THEN 50 WHEN 'OABT_DIN' THEN 50 WHEN 'OABT_REH' THEN 50 WHEN 'OABT_SINIF' THEN 50 WHEN 'OABT_OKUL' THEN 50 WHEN 'OABT_BED' THEN 50 WHEN 'OABT_OZEL' THEN 50 WHEN 'YDS_EN' THEN 80 WHEN 'YOKDIL_FEN' THEN 80 WHEN 'YOKDIL_SAG' THEN 80 WHEN 'YOKDIL_SOS' THEN 80 ELSE -1 END)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE students integer;
                BEGIN
                    SELECT count(*) INTO students FROM coaching.student_learning_contexts WHERE "Purpose" > 4;
                    IF students > 0 THEN
                        RAISE EXCEPTION '% students prepare for AGS, YDS or YÖKDİL; the previous version cannot read these purposes. Nothing was rolled back.', students;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(name: "ck_learning_purpose", schema: "coaching", table: "student_learning_contexts");
            migrationBuilder.AddCheckConstraint(name: "ck_learning_purpose", schema: "coaching", table: "student_learning_contexts", sql: "\"Purpose\" BETWEEN 0 AND 4");

            migrationBuilder.Sql("""
                DO $$
                DECLARE goals integer;
                BEGIN
                    SELECT count(*) INTO goals FROM coaching.academic_goals
                    WHERE target_practice_exam_code IS NOT NULL AND target_practice_exam_code NOT IN ('LGS','TYT','AYT_SAY','AYT_EA','AYT_SOZ','YDT_EN');
                    IF goals > 0 THEN
                        RAISE EXCEPTION '% goals target an AGS, ÖABT, YDS or YÖKDİL net; the previous version cannot read them. Nothing was rolled back.', goals;
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(name: "ck_goal_net_target", schema: "coaching", table: "academic_goals");
            migrationBuilder.AddCheckConstraint(name: "ck_goal_net_target", schema: "coaching", table: "academic_goals",
                sql: "(target_net IS NULL AND target_practice_exam_code IS NULL) OR (target_net IS NOT NULL AND target_practice_exam_code IS NOT NULL AND target_net >= 0 AND target_net <= CASE target_practice_exam_code WHEN 'LGS' THEN 90 WHEN 'TYT' THEN 120 WHEN 'AYT_SAY' THEN 80 WHEN 'AYT_EA' THEN 80 WHEN 'AYT_SOZ' THEN 80 WHEN 'YDT_EN' THEN 80 ELSE -1 END)");

            migrationBuilder.DropForeignKey(
                name: "FK_student_learning_contexts_study_catalog_exam_sessions_TargetExamSessionId",
                schema: "coaching",
                table: "student_learning_contexts");

            migrationBuilder.DropIndex(
                name: "IX_student_learning_contexts_TargetExamSessionId",
                schema: "coaching",
                table: "student_learning_contexts");

            migrationBuilder.DropColumn(name: "TargetExamSessionId", schema: "coaching", table: "student_learning_contexts");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExamDate",
                schema: "coaching",
                table: "study_catalog_exams",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE coaching.study_catalog_exams AS e SET "ExamDate" = (
                    SELECT s."Date" FROM coaching.study_catalog_exam_sessions AS s WHERE s."ExamId" = e."Id"
                    ORDER BY (s."Date" <= CURRENT_DATE), CASE WHEN s."Date" > CURRENT_DATE THEN s."Date" END, s."Date" DESC
                    LIMIT 1);
                """);

            migrationBuilder.DropTable(name: "study_catalog_exam_sessions", schema: "coaching");
        }
    }
}
