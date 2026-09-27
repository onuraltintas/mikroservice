using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "review_completions",
                schema: "speed_reading",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: false),
                    review_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_completions", x => x.session_id);
                    table.ForeignKey(
                        name: "FK_review_completions_review_items_review_item_id",
                        column: x => x.review_item_id,
                        principalSchema: "speed_reading",
                        principalTable: "review_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_review_completions_review_item_id",
                schema: "speed_reading",
                table: "review_completions",
                column: "review_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_review_completions_user_id_exercise_id_reviewed_at",
                schema: "speed_reading",
                table: "review_completions",
                columns: new[] { "user_id", "exercise_id", "reviewed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_completions",
                schema: "speed_reading");
        }
    }
}
