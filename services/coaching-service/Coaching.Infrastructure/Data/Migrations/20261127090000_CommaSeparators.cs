using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// 22.11.2026 (design rule: no "·" lines in what people read): the texts the service wrote with " · " get the new separators.
    /// Only generated texts change: the spaced-review task titles ("Tekrar · Konu" → "Tekrar: Konu"), the plan engine's reasons
    /// (one sentence each, "a · b" → "a. b") and the titles of goals made from a university programme ("Üniversite · Bölüm" →
    /// "Üniversite, Bölüm"). Catalog records (exam names of the content packs) and accepted legal documents are left as they are.
    /// </summary>
    public partial class CommaSeparators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE coaching.study_plan_tasks SET "Title" = 'Tekrar: ' || substr("Title", 10) WHERE "Title" LIKE 'Tekrar · %';
                UPDATE coaching.study_plan_tasks SET "PlanReason" = replace("PlanReason", ' · ', '. ') WHERE "PlanReason" LIKE '% · %';
                UPDATE coaching.academic_goals SET title = replace(title, ' · ', ', ') WHERE target_university_program_id IS NOT NULL AND title LIKE '% · %';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The separators are wording only; the earlier texts are not written back (a ", " in a goal title may be the student's own).
        }
    }
}
