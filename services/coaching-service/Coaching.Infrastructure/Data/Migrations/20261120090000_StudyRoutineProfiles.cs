using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// H3 (user decision K2, 08.11.2026): the student's "günlük düzen" answers are kept as a study routine profile, one per
    /// student. The answers (jsonb object, SchemaVersion 1) produce the study availability windows; AvailabilityVersion is the
    /// availability version the profile wrote, so a later hand edit of the free hours can be detected. A new table only: no
    /// existing row changes. Down drops the table (the saved answers are lost; the availability they wrote stays).
    /// </summary>
    public partial class StudyRoutineProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_routine_profiles",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    AnswersJson = table.Column<string>(type: "jsonb", nullable: false),
                    AvailabilityVersion = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_routine_profiles", x => x.Id);
                    table.CheckConstraint("ck_study_routine_answers_object", "jsonb_typeof(\"AnswersJson\") = 'object'");
                    table.CheckConstraint("ck_study_routine_availability_version", "\"AvailabilityVersion\" >= 0");
                    table.CheckConstraint("ck_study_routine_schema_version", "\"SchemaVersion\" >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_routine_profiles_StudentId",
                schema: "coaching",
                table: "study_routine_profiles",
                column: "StudentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_routine_profiles",
                schema: "coaching");
        }
    }
}
