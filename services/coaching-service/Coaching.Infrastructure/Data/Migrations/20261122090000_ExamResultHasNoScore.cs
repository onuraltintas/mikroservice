using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Paket I4 (user decision 14.11.2026): a student may save a practice exam without the score of a result document.
    /// One flag on the result: existing rows keep their score (false); no row changes. Down drops the column, after which
    /// a result saved without a score reads as a score of 0.
    /// </summary>
    public partial class ExamResultHasNoScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_no_score",
                schema: "coaching",
                table: "exam_results",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_no_score",
                schema: "coaching",
                table: "exam_results");
        }
    }
}
