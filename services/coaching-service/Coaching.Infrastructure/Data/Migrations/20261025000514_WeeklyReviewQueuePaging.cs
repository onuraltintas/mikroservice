using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyReviewQueuePaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_weekly_reflections_WeekStart_Id",
                schema: "coaching",
                table: "weekly_reflections",
                columns: new[] { "WeekStart", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_reflection_reviews_TeacherId_ReflectionId_CreatedAt_~",
                schema: "coaching",
                table: "weekly_reflection_reviews",
                columns: new[] { "TeacherId", "ReflectionId", "CreatedAt", "Id" },
                descending: new[] { false, false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_weekly_reflections_WeekStart_Id",
                schema: "coaching",
                table: "weekly_reflections");

            migrationBuilder.DropIndex(
                name: "IX_weekly_reflection_reviews_TeacherId_ReflectionId_CreatedAt_~",
                schema: "coaching",
                table: "weekly_reflection_reviews");
        }
    }
}
