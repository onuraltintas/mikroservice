using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramDayAdvance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EarlyAdvanceDays",
                schema: "speed_reading",
                table: "student_program_progress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "program_progression_policies",
                schema: "speed_reading",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowEarlyAdvance = table.Column<bool>(type: "boolean", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_progression_policies", x => x.UserId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_progression_policies",
                schema: "speed_reading");

            migrationBuilder.DropColumn(
                name: "EarlyAdvanceDays",
                schema: "speed_reading",
                table: "student_program_progress");
        }
    }
}
