using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// K4 (31.10.2026): a resubmission no longer moves the first hand-in time; the latest hand-in is kept separately.
    /// Existing rows start with the latest time equal to the recorded one. Down drops the column (the first time stays).
    /// </summary>
    public partial class AssignmentLastSubmittedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_submitted_at",
                schema: "coaching",
                table: "assignment_students",
                type: "timestamp with time zone",
                nullable: true);
            migrationBuilder.Sql("UPDATE coaching.assignment_students SET last_submitted_at = submitted_at WHERE submitted_at IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "last_submitted_at", schema: "coaching", table: "assignment_students");
        }
    }
}
