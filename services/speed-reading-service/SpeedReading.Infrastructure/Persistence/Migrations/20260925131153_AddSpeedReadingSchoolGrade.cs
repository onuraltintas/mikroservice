using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeedReadingSchoolGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GradeLevel",
                schema: "speed_reading",
                table: "user_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_GradeLevel",
                schema: "speed_reading",
                table: "user_profiles",
                column: "GradeLevel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_profiles_GradeLevel",
                schema: "speed_reading",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "GradeLevel",
                schema: "speed_reading",
                table: "user_profiles");
        }
    }
}
