using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P4BranchesAndRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "LessonSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "LessonSeries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseBranches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseBranches_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseRooms_CourseBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "CourseBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseRooms_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonSessions_RoomId",
                table: "LessonSessions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSessions_WorkspaceId_RoomId_Status_StartsAt",
                table: "LessonSessions",
                columns: new[] { "WorkspaceId", "RoomId", "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonSeries_RoomId",
                table: "LessonSeries",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseBranches_WorkspaceId_Name",
                table: "CourseBranches",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseRooms_BranchId_Name",
                table: "CourseRooms",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseRooms_WorkspaceId_BranchId",
                table: "CourseRooms",
                columns: new[] { "WorkspaceId", "BranchId" });

            migrationBuilder.AddForeignKey(
                name: "FK_LessonSeries_CourseRooms_RoomId",
                table: "LessonSeries",
                column: "RoomId",
                principalTable: "CourseRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonSessions_CourseRooms_RoomId",
                table: "LessonSessions",
                column: "RoomId",
                principalTable: "CourseRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LessonSeries_CourseRooms_RoomId",
                table: "LessonSeries");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonSessions_CourseRooms_RoomId",
                table: "LessonSessions");

            migrationBuilder.DropTable(
                name: "CourseRooms");

            migrationBuilder.DropTable(
                name: "CourseBranches");

            migrationBuilder.DropIndex(
                name: "IX_LessonSessions_RoomId",
                table: "LessonSessions");

            migrationBuilder.DropIndex(
                name: "IX_LessonSessions_WorkspaceId_RoomId_Status_StartsAt",
                table: "LessonSessions");

            migrationBuilder.DropIndex(
                name: "IX_LessonSeries_RoomId",
                table: "LessonSeries");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "LessonSessions");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "LessonSeries");
        }
    }
}
