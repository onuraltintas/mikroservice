using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Paket C (#3, user decisions 05.11.2026): the coaching student's first-login wizard.
    /// student_profiles gains the student's home city and district (ids and names from the shared Identity location directory),
    /// the YKS diploma grade (50–100, with whether it is an expected grade) and the wizard state (saved steps as bits 1–7 and
    /// the completion time). academic_goals gains score_targets, the student's own target scores (YKS: TYT and the field's score type).
    /// Students who already finished the old setup (an exam purpose and complete education) are marked as having completed the
    /// wizard, so they never see it; everyone else starts or resumes it. Down refuses, changing nothing, while any profile has wizard answers (home city or district, diploma grade, a partial wizard
    /// state) or any goal has target scores; otherwise it drops the new columns. Release note: back up before rolling back.
    /// </summary>
    public partial class StudentOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "HomeProvinceId", schema: "coaching", table: "student_profiles", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "HomeCity", schema: "coaching", table: "student_profiles", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "HomeDistrictId", schema: "coaching", table: "student_profiles", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "HomeDistrict", schema: "coaching", table: "student_profiles", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "DiplomaGrade", schema: "coaching", table: "student_profiles", type: "numeric(5,2)", precision: 5, scale: 2, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "DiplomaGradeEstimated", schema: "coaching", table: "student_profiles", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>(name: "OnboardingSteps", schema: "coaching", table: "student_profiles", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateTime>(name: "OnboardingCompletedAt", schema: "coaching", table: "student_profiles", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_student_diploma", schema: "coaching", table: "student_profiles",
                sql: "\"DiplomaGrade\" IS NULL OR \"DiplomaGrade\" BETWEEN 50 AND 100");
            migrationBuilder.AddColumn<string>(name: "score_targets", schema: "coaching", table: "academic_goals", type: "jsonb", nullable: false, defaultValue: "[]");
            migrationBuilder.AddCheckConstraint(name: "ck_goal_score_targets", schema: "coaching", table: "academic_goals",
                sql: "jsonb_typeof(score_targets) = 'array' AND jsonb_array_length(score_targets) <= 2");

            // Existing students with an exam purpose (LGS 1, YKS 2, AGS 5, YDS 6, YÖKDİL 7) and complete education finished the
            // old setup: all seven wizard steps (bits 1–7 = 254) are marked done.
            migrationBuilder.Sql("""
                UPDATE coaching.student_profiles p
                SET "OnboardingSteps" = 254, "OnboardingCompletedAt" = now()
                WHERE p."OnboardingCompletedAt" IS NULL
                  AND (p."EducationStatus" = 2 OR (p."EducationStatus" = 1 AND p."GradeLevel" IS NOT NULL))
                  AND EXISTS (SELECT 1 FROM coaching.student_learning_contexts c
                              WHERE c."StudentUserId" = p."UserId" AND c."Purpose" IN (1, 2, 5, 6, 7));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The wizard's answers live only in these columns: going back would lose them. Back up and clear them by hand first.
            migrationBuilder.Sql("""
                DO $$
                DECLARE profiles bigint; goals bigint;
                BEGIN
                  SELECT count(*) INTO profiles FROM coaching.student_profiles
                  WHERE "HomeProvinceId" IS NOT NULL OR "HomeCity" IS NOT NULL OR "HomeDistrictId" IS NOT NULL OR "HomeDistrict" IS NOT NULL
                     OR "DiplomaGrade" IS NOT NULL OR "OnboardingSteps" NOT IN (0, 254);
                  SELECT count(*) INTO goals FROM coaching.academic_goals WHERE score_targets <> '[]'::jsonb;
                  IF profiles + goals > 0 THEN
                    RAISE EXCEPTION 'StudentOnboarding down refused: % student profile(s) with first-login wizard answers and % goal(s) with target scores exist. Back them up and clear them first. Nothing changed.', profiles, goals;
                  END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(name: "ck_goal_score_targets", schema: "coaching", table: "academic_goals");
            migrationBuilder.DropColumn(name: "score_targets", schema: "coaching", table: "academic_goals");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_student_diploma", schema: "coaching", table: "student_profiles");
            foreach (var column in new[] { "HomeProvinceId", "HomeCity", "HomeDistrictId", "HomeDistrict", "DiplomaGrade", "DiplomaGradeEstimated",
                         "OnboardingSteps", "OnboardingCompletedAt" })
                migrationBuilder.DropColumn(name: column, schema: "coaching", table: "student_profiles");
        }
    }
}
