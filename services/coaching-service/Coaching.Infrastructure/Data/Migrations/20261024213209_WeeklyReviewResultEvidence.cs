using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyReviewResultEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReflectionHistoryJson",
                schema: "coaching",
                table: "weekly_reflections",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ResultsJson",
                schema: "coaching",
                table: "weekly_reflections",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.weekly_reflections WHERE "ResultsJson" IS NOT NULL OR "ReflectionHistoryJson" <> '[]'::jsonb)
                  THEN RAISE EXCEPTION 'Weekly result evidence or reflection history exists; export and migrate it explicitly before downgrade.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "ReflectionHistoryJson",
                schema: "coaching",
                table: "weekly_reflections");

            migrationBuilder.DropColumn(
                name: "ResultsJson",
                schema: "coaching",
                table: "weekly_reflections");
        }
    }
}
