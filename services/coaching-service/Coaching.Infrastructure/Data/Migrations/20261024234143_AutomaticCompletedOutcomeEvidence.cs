using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AutomaticCompletedOutcomeEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutomaticOutcomeEvidence",
                schema: "coaching",
                table: "study_plan_revisions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.study_plan_revisions WHERE "AutomaticOutcomeEvidence" IS NOT NULL)
                  THEN RAISE EXCEPTION 'Automatic outcome evidence exists; preserve plan evidence before downgrade.'; END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "AutomaticOutcomeEvidence",
                schema: "coaching",
                table: "study_plan_revisions");
        }
    }
}
