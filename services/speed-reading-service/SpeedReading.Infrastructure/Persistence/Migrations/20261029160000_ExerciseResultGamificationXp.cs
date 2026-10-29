using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Review H5 (finding 18): each exercise result stores the XP it earned and whether it counted, so rebuilding
    /// gamification from history matches what students received. Nullable columns only; Down drops them.
    /// </summary>
    public partial class ExerciseResultGamificationXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "counts_for_gamification",
                schema: "speed_reading",
                table: "exercise_session_results",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "gamification_xp",
                schema: "speed_reading",
                table: "exercise_session_results",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "counts_for_gamification", schema: "speed_reading", table: "exercise_session_results");
            migrationBuilder.DropColumn(name: "gamification_xp", schema: "speed_reading", table: "exercise_session_results");
        }
    }
}
