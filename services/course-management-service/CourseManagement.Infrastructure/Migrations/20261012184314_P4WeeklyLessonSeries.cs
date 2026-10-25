using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P4WeeklyLessonSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "LessonSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LessonSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonSeries_CourseGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CourseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonSeries_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonSeries_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonSeries_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonSessions_SeriesId",
                table: "LessonSessions",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSeries_CourseId",
                table: "LessonSeries",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSeries_GroupId",
                table: "LessonSeries",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSeries_StudentId",
                table: "LessonSeries",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSeries_WorkspaceId_StartsOn",
                table: "LessonSeries",
                columns: new[] { "WorkspaceId", "StartsOn" });

            migrationBuilder.AddForeignKey(
                name: "FK_LessonSessions_LessonSeries_SeriesId",
                table: "LessonSessions",
                column: "SeriesId",
                principalTable: "LessonSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LessonSessions_LessonSeries_SeriesId",
                table: "LessonSessions");

            migrationBuilder.DropTable(
                name: "LessonSeries");

            migrationBuilder.DropIndex(
                name: "IX_LessonSessions_SeriesId",
                table: "LessonSessions");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "LessonSessions");
        }
    }
}
