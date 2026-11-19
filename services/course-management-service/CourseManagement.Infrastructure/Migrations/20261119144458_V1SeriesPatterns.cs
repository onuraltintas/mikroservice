using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V1SeriesPatterns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonthlyWeekOfMonth",
                table: "LessonSeries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatEveryWeeks",
                table: "LessonSeries",
                type: "integer",
                nullable: false,
                // Existing series repeat every week.
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonthlyWeekOfMonth",
                table: "LessonSeries");

            migrationBuilder.DropColumn(
                name: "RepeatEveryWeeks",
                table: "LessonSeries");
        }
    }
}
