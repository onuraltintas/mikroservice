using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalScoreScale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "target_max_score",
                schema: "coaching",
                table: "academic_goals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_goal_score_scale",
                schema: "coaching",
                table: "academic_goals",
                sql: "target_max_score IS NULL OR (target_max_score > 0 AND target_max_score <= 999.99 AND target_score IS NOT NULL AND target_score > 0 AND target_score <= target_max_score AND target_exam_type IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.academic_goals WHERE target_max_score IS NOT NULL) THEN
                        RAISE EXCEPTION 'Configured score targets exist. Restore a verified backup or explicitly clear configurations before rollback.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_goal_score_scale",
                schema: "coaching",
                table: "academic_goals");

            migrationBuilder.DropColumn(
                name: "target_max_score",
                schema: "coaching",
                table: "academic_goals");
        }
    }
}
