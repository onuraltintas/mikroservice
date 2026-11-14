using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Paket I5 (user decision 14.11.2026): spaced reviews are the student's choice. One nullable text on her plan preferences
    /// (for example "7,30"; empty: none; null: not asked yet). No row changes. Down drops the column and with it the choices.
    /// </summary>
    public partial class SpacedReviewChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpacedReviewDays",
                schema: "coaching",
                table: "study_plan_preferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpacedReviewDays",
                schema: "coaching",
                table: "study_plan_preferences");
        }
    }
}
