using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeInstitutionAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "institution_id",
                schema: "speed_reading",
                table: "assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_assignments_institution_id_created_at",
                schema: "speed_reading",
                table: "assignments",
                columns: new[] { "institution_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_assignments_institution_id_created_at",
                schema: "speed_reading",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "institution_id",
                schema: "speed_reading",
                table: "assignments");
        }
    }
}
