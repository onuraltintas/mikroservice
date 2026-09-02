using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260902110000_AddOwnedCmsNavigation")]
public partial class AddOwnedCmsNavigation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "cms_navigation_items",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                menu = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                fragment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_visible = table.Column<bool>(type: "boolean", nullable: false),
                open_in_new_tab = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_cms_navigation_items", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_cms_navigation_items_menu_sort_order",
            schema: "speed_reading",
            table: "cms_navigation_items",
            columns: new[] { "menu", "sort_order" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "cms_navigation_items",
            schema: "speed_reading");
    }
}
