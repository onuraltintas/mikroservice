using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Review H4 (finding 21): the platform-wide reading benchmark filters by completion date. Index only; Down drops it.
    /// </summary>
    public partial class ReadingSessionCompletedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_reading_sessions_completed_at",
                schema: "speed_reading",
                table: "reading_sessions",
                column: "completed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reading_sessions_completed_at",
                schema: "speed_reading",
                table: "reading_sessions");
        }
    }
}
