using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// E3 (user decisions 06.11.2026).
    /// #14 "Şu an durumun ne?": the education statuses become Student (1, with the grade: ortaokul 5–8, lise 9–12),
    /// HighSchoolGraduate (2, the earlier "graduate", same number), UniversityStudent (3) and UniversityGraduate (4). A graduate
    /// preparing for MEB-AGS (purpose 5) can only be a university graduate, so such profiles (and their study settings) become 4;
    /// every other graduate stays a lise graduate and every student keeps the grade. The check constraints allow 0–4, a grade only
    /// for students, a graduation year for both graduate statuses; the study settings' copy of the status is checked too.
    /// #34 no time zone choice: every stored zone (study hours, weekly draft, a plan's weekly snapshot) becomes Europe/Istanbul
    /// and check constraints keep it there. Planned days and minutes are not moved (wall clock: an 18:00 slot stays 18:00).
    /// Down refuses, changing nothing, while a university status (3, 4) is stored: the old model has no place for it. Release
    /// note: back up first; to roll back, set those students to 2 by hand. Down keeps the zones in Istanbul and only lifts the
    /// zone constraints.
    /// </summary>
    public partial class EducationStatusesAndIstanbulTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_student_education", schema: "coaching", table: "student_profiles");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_student_graduation", schema: "coaching", table: "student_profiles");
            migrationBuilder.Sql("""
                UPDATE coaching.student_profiles p SET "EducationStatus" = 4
                WHERE p."EducationStatus" = 2
                  AND EXISTS (SELECT 1 FROM coaching.student_learning_contexts c WHERE c."StudentUserId" = p."UserId" AND c."Purpose" = 5);
                UPDATE coaching.student_learning_contexts c SET "EducationStatus" = 4
                FROM coaching.student_profiles p
                WHERE p."UserId" = c."StudentUserId" AND p."EducationStatus" = 4 AND c."EducationStatus" = 2;
                """);
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_student_education", schema: "coaching", table: "student_profiles",
                sql: "\"EducationStatus\" BETWEEN 0 AND 4 AND ((\"EducationStatus\" = 1 AND \"GradeLevel\" BETWEEN 1 AND 12 AND \"GradeLevel\" IS NOT NULL) OR (\"EducationStatus\" <> 1 AND \"GradeLevel\" IS NULL))");
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_student_graduation", schema: "coaching", table: "student_profiles",
                sql: "\"GraduationYear\" IS NULL OR (\"EducationStatus\" IN (2, 4) AND \"GraduationYear\" BETWEEN 1900 AND 9999)");
            migrationBuilder.AddCheckConstraint(name: "ck_learning_education", schema: "coaching", table: "student_learning_contexts",
                sql: "\"EducationStatus\" BETWEEN 0 AND 4");

            migrationBuilder.Sql("""
                UPDATE coaching.study_availability SET "TimeZoneId" = 'Europe/Istanbul' WHERE "TimeZoneId" <> 'Europe/Istanbul';
                UPDATE coaching.study_weekly_drafts SET "TimeZoneId" = 'Europe/Istanbul' WHERE "TimeZoneId" <> 'Europe/Istanbul';
                UPDATE coaching.study_plan_revisions SET "WeeklySource" = jsonb_set("WeeklySource", '{TimeZoneId}', '"Europe/Istanbul"')
                WHERE "WeeklySource" IS NOT NULL AND "WeeklySource" ? 'TimeZoneId' AND "WeeklySource"->>'TimeZoneId' IS DISTINCT FROM 'Europe/Istanbul';
                """);
            migrationBuilder.AddCheckConstraint(name: "ck_study_availability_zone", schema: "coaching", table: "study_availability",
                sql: "\"TimeZoneId\" = 'Europe/Istanbul'");
            migrationBuilder.AddCheckConstraint(name: "ck_weekly_draft_zone", schema: "coaching", table: "study_weekly_drafts",
                sql: "\"TimeZoneId\" = 'Europe/Istanbul'");
            migrationBuilder.AddCheckConstraint(name: "ck_study_plan_weekly_zone", schema: "coaching", table: "study_plan_revisions",
                sql: "\"WeeklySource\" IS NULL OR coalesce(\"WeeklySource\"->>'TimeZoneId', 'Europe/Istanbul') = 'Europe/Istanbul'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE profiles bigint; contexts bigint;
                BEGIN
                  SELECT count(*) INTO profiles FROM coaching.student_profiles WHERE "EducationStatus" IN (3, 4);
                  SELECT count(*) INTO contexts FROM coaching.student_learning_contexts WHERE "EducationStatus" IN (3, 4);
                  IF profiles + contexts > 0 THEN
                    RAISE EXCEPTION 'EducationStatusesAndIstanbulTime down refused: % student profile(s) and % study setting(s) are university students or graduates. Back them up and set them to 2 first. Nothing changed.', profiles, contexts;
                  END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(name: "ck_study_plan_weekly_zone", schema: "coaching", table: "study_plan_revisions");
            migrationBuilder.DropCheckConstraint(name: "ck_weekly_draft_zone", schema: "coaching", table: "study_weekly_drafts");
            migrationBuilder.DropCheckConstraint(name: "ck_study_availability_zone", schema: "coaching", table: "study_availability");
            migrationBuilder.DropCheckConstraint(name: "ck_learning_education", schema: "coaching", table: "student_learning_contexts");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_student_graduation", schema: "coaching", table: "student_profiles");
            migrationBuilder.DropCheckConstraint(name: "ck_coaching_student_education", schema: "coaching", table: "student_profiles");
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_student_education", schema: "coaching", table: "student_profiles",
                sql: "\"EducationStatus\" BETWEEN 0 AND 2 AND ((\"EducationStatus\" = 1 AND \"GradeLevel\" BETWEEN 1 AND 12 AND \"GradeLevel\" IS NOT NULL) OR (\"EducationStatus\" <> 1 AND \"GradeLevel\" IS NULL))");
            migrationBuilder.AddCheckConstraint(name: "ck_coaching_student_graduation", schema: "coaching", table: "student_profiles",
                sql: "\"GraduationYear\" IS NULL OR (\"EducationStatus\" = 2 AND \"GraduationYear\" BETWEEN 1900 AND 9999)");
        }
    }
}
