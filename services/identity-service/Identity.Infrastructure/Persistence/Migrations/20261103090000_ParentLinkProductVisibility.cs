using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// V1 (parent panel): the student chooses per product what a linked parent sees. Existing links keep showing both
    /// products (the database default is true). Down drops the two choices.
    /// </summary>
    public partial class ParentLinkProductVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "coaching_visible",
                schema: "identity",
                table: "parent_student_relationships",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "speed_reading_visible",
                schema: "identity",
                table: "parent_student_relationships",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "coaching_visible", schema: "identity", table: "parent_student_relationships");
            migrationBuilder.DropColumn(name: "speed_reading_visible", schema: "identity", table: "parent_student_relationships");
        }
    }
}
