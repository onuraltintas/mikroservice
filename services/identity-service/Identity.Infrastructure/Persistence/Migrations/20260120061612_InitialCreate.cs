using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "institutions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LicenseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxStudents = table.Column<int>(type: "integer", nullable: false),
                    MaxTeachers = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PhoneConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "institution_admins",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Permissions = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_admins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_institution_admins_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalSchema: "identity",
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_institution_admins_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parent_profiles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NotificationPreferences = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parent_profiles_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_profiles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    Certifications = table.Column<string>(type: "jsonb", nullable: false),
                    IsIndependent = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewAllInstitutionStudents = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_profiles_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalSchema: "identity",
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_teacher_profiles_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_profiles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GradeLevel = table.Column<int>(type: "integer", nullable: true),
                    SchoolName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SchoolCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    LearningStyle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DailyGoalMinutes = table.Column<int>(type: "integer", nullable: false),
                    Preferences = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ParentProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_profiles_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalSchema: "identity",
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_profiles_parent_profiles_ParentProfileId",
                        column: x => x.ParentProfileId,
                        principalSchema: "identity",
                        principalTable: "parent_profiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_student_profiles_users_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_profiles_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_student_assignments",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_student_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_student_assignments_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalSchema: "identity",
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_teacher_student_assignments_student_profiles_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "identity",
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_teacher_student_assignments_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalSchema: "identity",
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_institution_admins_InstitutionId",
                schema: "identity",
                table: "institution_admins",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_institution_admins_UserId_InstitutionId",
                schema: "identity",
                table: "institution_admins",
                columns: new[] { "UserId", "InstitutionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutions_City",
                schema: "identity",
                table: "institutions",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_institutions_Type",
                schema: "identity",
                table: "institutions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_parent_profiles_UserId",
                schema: "identity",
                table: "parent_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_GradeLevel",
                schema: "identity",
                table: "student_profiles",
                column: "GradeLevel");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_InstitutionId",
                schema: "identity",
                table: "student_profiles",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_ParentId",
                schema: "identity",
                table: "student_profiles",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_ParentProfileId",
                schema: "identity",
                table: "student_profiles",
                column: "ParentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_UserId",
                schema: "identity",
                table: "student_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_profiles_InstitutionId",
                schema: "identity",
                table: "teacher_profiles",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_profiles_UserId",
                schema: "identity",
                table: "teacher_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_assignments_InstitutionId",
                schema: "identity",
                table: "teacher_student_assignments",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_assignments_StudentId",
                schema: "identity",
                table: "teacher_student_assignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_assignments_TeacherId",
                schema: "identity",
                table: "teacher_student_assignments",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_assignments_TeacherId_StudentId_Subject",
                schema: "identity",
                table: "teacher_student_assignments",
                columns: new[] { "TeacherId", "StudentId", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_UserId_Role",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "identity",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "institution_admins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "teacher_student_assignments",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "student_profiles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "teacher_profiles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "parent_profiles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "institutions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
