using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyGradeResultRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GradeRevision",
                schema: "coaching",
                table: "assignment_students",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.assignment_students WHERE "GradeRevision" IS NOT NULL)
                  THEN RAISE EXCEPTION 'Grade result revisions exist; preserve review freshness before downgrade.'; END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "GradeRevision",
                schema: "coaching",
                table: "assignment_students");
        }
    }
}
