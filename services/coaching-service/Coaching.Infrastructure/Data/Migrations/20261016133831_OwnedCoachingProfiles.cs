using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnedCoachingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "institution_memberships",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_memberships", x => x.Id);
                    table.CheckConstraint("ck_coaching_member_role", "\"Role\" IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "student_profiles",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationStatus = table.Column<int>(type: "integer", nullable: false),
                    GradeLevel = table.Column<int>(type: "integer", nullable: true),
                    SchoolName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SchoolCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SchoolDistrict = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SchoolType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClassBranch = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    GraduationYear = table.Column<int>(type: "integer", nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ShareProgressWithTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_profiles", x => x.Id);
                    table.UniqueConstraint("ak_coaching_student_profile_user", x => x.UserId);
                    table.CheckConstraint("ck_coaching_student_education", "\"EducationStatus\" BETWEEN 0 AND 2 AND ((\"EducationStatus\" = 1 AND \"GradeLevel\" BETWEEN 1 AND 12 AND \"GradeLevel\" IS NOT NULL) OR (\"EducationStatus\" <> 1 AND \"GradeLevel\" IS NULL))");
                    table.CheckConstraint("ck_coaching_student_graduation", "\"GraduationYear\" IS NULL OR (\"EducationStatus\" = 2 AND \"GraduationYear\" BETWEEN 1900 AND 9999)");
                });

            migrationBuilder.CreateTable(
                name: "teacher_profiles",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubjectsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: true),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CanViewAllInstitutionStudents = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_profiles", x => x.Id);
                    table.UniqueConstraint("ak_coaching_teacher_profile_user", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "teacher_student_assignments",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_student_assignments", x => x.Id);
                    table.CheckConstraint("ck_coaching_assignment_end", "(\"IsActive\" AND \"EndDate\" IS NULL) OR (NOT \"IsActive\" AND \"EndDate\" IS NOT NULL)");
                    table.CheckConstraint("ck_coaching_assignment_users", "\"TeacherUserId\" <> \"StudentUserId\"");
                    table.ForeignKey(
                        name: "FK_teacher_student_assignments_student_profiles_StudentUserId",
                        column: x => x.StudentUserId,
                        principalSchema: "coaching",
                        principalTable: "student_profiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_teacher_student_assignments_teacher_profiles_TeacherUserId",
                        column: x => x.TeacherUserId,
                        principalSchema: "coaching",
                        principalTable: "teacher_profiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_institution_memberships_InstitutionId_Role_IsActive",
                schema: "coaching",
                table: "institution_memberships",
                columns: new[] { "InstitutionId", "Role", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_institution_memberships_InstitutionId_UserId_Role",
                schema: "coaching",
                table: "institution_memberships",
                columns: new[] { "InstitutionId", "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institution_memberships_UserId_Role",
                schema: "coaching",
                table: "institution_memberships",
                columns: new[] { "UserId", "Role" },
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_IsActive_GradeLevel",
                schema: "coaching",
                table: "student_profiles",
                columns: new[] { "IsActive", "GradeLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_assignments_InstitutionId_IsActive",
                schema: "coaching",
                table: "teacher_student_assignments",
                columns: new[] { "InstitutionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_assignments_StudentUserId_IsActive",
                schema: "coaching",
                table: "teacher_student_assignments",
                columns: new[] { "StudentUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_assignments_TeacherUserId_StudentUserId_Ins~",
                schema: "coaching",
                table: "teacher_student_assignments",
                columns: new[] { "TeacherUserId", "StudentUserId", "InstitutionId", "Subject" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "institution_memberships",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "teacher_student_assignments",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "student_profiles",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "teacher_profiles",
                schema: "coaching");
        }
    }
}
