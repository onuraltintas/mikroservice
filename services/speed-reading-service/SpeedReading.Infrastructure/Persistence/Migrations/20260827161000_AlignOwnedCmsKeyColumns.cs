using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827161000_AlignOwnedCmsKeyColumns")]
public partial class AlignOwnedCmsKeyColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        RenameCmsKeyColumn(migrationBuilder, "cms_content_blocks");
        RenameCmsKeyColumn(migrationBuilder, "cms_pages");
        RenameCmsKeyColumn(migrationBuilder, "cms_blog_posts");
        RenameCmsKeyColumn(migrationBuilder, "cms_contact_messages");
        RenameCmsKeyColumn(migrationBuilder, "cms_newsletter_subscribers");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RenameCmsKeyColumn(migrationBuilder, "cms_content_blocks", "id", "Id");
        RenameCmsKeyColumn(migrationBuilder, "cms_pages", "id", "Id");
        RenameCmsKeyColumn(migrationBuilder, "cms_blog_posts", "id", "Id");
        RenameCmsKeyColumn(migrationBuilder, "cms_contact_messages", "id", "Id");
        RenameCmsKeyColumn(migrationBuilder, "cms_newsletter_subscribers", "id", "Id");
    }

    private static void RenameCmsKeyColumn(
        MigrationBuilder migrationBuilder,
        string table,
        string name = "Id",
        string newName = "id")
    {
        migrationBuilder.RenameColumn(
            name,
            schema: "speed_reading",
            table: table,
            newName: newName);
    }
}
