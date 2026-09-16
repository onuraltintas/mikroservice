using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Identity.Infrastructure.Persistence;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[Migration("20260916140000_AddStudentProgressSharing")]
[DbContext(typeof(IdentityDbContext))]
public partial class AddStudentProgressSharing : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ShareProgressWithTeachers",
            schema: "identity",
            table: "student_profiles",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ShareProgressWithTeachers",
            schema: "identity",
            table: "student_profiles");
    }
}
