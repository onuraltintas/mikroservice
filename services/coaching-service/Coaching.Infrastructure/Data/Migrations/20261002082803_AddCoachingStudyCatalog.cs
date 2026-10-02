using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingStudyCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_catalog_lessons",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    GradeNumber = table.Column<int>(type: "integer", nullable: true),
                    ExamCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_lessons", x => x.Id);
                    table.CheckConstraint("ck_catalog_lesson_grade", "\"GradeNumber\" IS NULL OR \"GradeNumber\" BETWEEN 1 AND 12");
                });

            migrationBuilder.CreateTable(
                name: "study_catalog_units",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_units", x => x.Id);
                    table.UniqueConstraint("AK_study_catalog_units_Id_LessonId", x => new { x.Id, x.LessonId });
                    table.CheckConstraint("ck_catalog_unit_order", "\"DisplayOrder\" IS NULL OR \"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_study_catalog_units_study_catalog_lessons_LessonId",
                        column: x => x.LessonId,
                        principalSchema: "coaching",
                        principalTable: "study_catalog_lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_catalog_topics",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_topics", x => x.Id);
                    table.UniqueConstraint("AK_study_catalog_topics_Id_UnitId_LessonId", x => new { x.Id, x.UnitId, x.LessonId });
                    table.CheckConstraint("ck_catalog_topic_minutes", "\"EstimatedMinutes\" IS NULL OR \"EstimatedMinutes\" > 0");
                    table.CheckConstraint("ck_catalog_topic_order", "\"DisplayOrder\" IS NULL OR \"DisplayOrder\" >= 0");
                    table.CheckConstraint("ck_catalog_topic_parent", "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_study_catalog_topics_study_catalog_topics_ParentId_UnitId_L~",
                        columns: x => new { x.ParentId, x.UnitId, x.LessonId },
                        principalSchema: "coaching",
                        principalTable: "study_catalog_topics",
                        principalColumns: new[] { "Id", "UnitId", "LessonId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_catalog_topics_study_catalog_units_UnitId_LessonId",
                        columns: x => new { x.UnitId, x.LessonId },
                        principalSchema: "coaching",
                        principalTable: "study_catalog_units",
                        principalColumns: new[] { "Id", "LessonId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_lessons_IsActive_GradeNumber_ExamCode",
                schema: "coaching",
                table: "study_catalog_lessons",
                columns: new[] { "IsActive", "GradeNumber", "ExamCode" });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_lessons_Source_SourceId",
                schema: "coaching",
                table: "study_catalog_lessons",
                columns: new[] { "Source", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_topics_LessonId_IsActive_DisplayOrder",
                schema: "coaching",
                table: "study_catalog_topics",
                columns: new[] { "LessonId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_topics_ParentId_UnitId_LessonId",
                schema: "coaching",
                table: "study_catalog_topics",
                columns: new[] { "ParentId", "UnitId", "LessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_topics_Source_SourceId",
                schema: "coaching",
                table: "study_catalog_topics",
                columns: new[] { "Source", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_topics_UnitId_LessonId",
                schema: "coaching",
                table: "study_catalog_topics",
                columns: new[] { "UnitId", "LessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_units_LessonId",
                schema: "coaching",
                table: "study_catalog_units",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_units_Source_SourceId",
                schema: "coaching",
                table: "study_catalog_units",
                columns: new[] { "Source", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_catalog_topics",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "study_catalog_units",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "study_catalog_lessons",
                schema: "coaching");
        }
    }
}
