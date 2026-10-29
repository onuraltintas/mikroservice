using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Review H5 (finding 16, decision 29.10.2026): marks readings rejected for an invalid speed so they stay out of
    /// comprehension averages. Existing rows default to not rejected (comprehension-only readings cannot be told apart
    /// afterwards). Down drops the column.
    /// </summary>
    public partial class ReadingSessionRejected : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_rejected",
                schema: "speed_reading",
                table: "reading_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_rejected", schema: "speed_reading", table: "reading_sessions");
        }
    }
}
