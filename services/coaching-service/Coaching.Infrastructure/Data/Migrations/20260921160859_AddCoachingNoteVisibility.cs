using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingNoteVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "teacher_notes_visibility",
                schema: "coaching",
                table: "coaching_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "CoachPrivate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "teacher_notes_visibility",
                schema: "coaching",
                table: "coaching_sessions");
        }
    }
}
