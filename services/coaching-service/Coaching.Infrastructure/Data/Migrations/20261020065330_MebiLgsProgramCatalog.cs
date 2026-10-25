using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MebiLgsProgramCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Field",
                schema: "coaching",
                table: "target_schools",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "coaching",
                table: "target_schools",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolType",
                schema: "coaching",
                table: "target_schools",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Field",
                schema: "coaching",
                table: "target_schools");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "coaching",
                table: "target_schools");

            migrationBuilder.DropColumn(
                name: "SchoolType",
                schema: "coaching",
                table: "target_schools");
        }
    }
}
