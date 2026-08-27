using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827151000_AddOwnedCms")]
public partial class AddOwnedCms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "cms_content_blocks", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false), Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), Group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false), Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true), Type = table.Column<int>(type: "integer", nullable: false), Value = table.Column<string>(type: "text", nullable: false), CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), CreatedBy = table.Column<Guid>(type: "uuid", nullable: false), UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true), IsDeleted = table.Column<bool>(type: "boolean", nullable: false), DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            }, constraints: table => table.PrimaryKey("pk_cms_content_blocks", x => x.Id));
        migrationBuilder.CreateTable(
            name: "cms_pages", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false), Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), Content = table.Column<string>(type: "text", nullable: false), IsPublished = table.Column<bool>(type: "boolean", nullable: false), MetaTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true), MetaDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), MetaKeywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), CanonicalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), OgTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true), OgDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), OgImage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), SeoSettings_NoIndex = table.Column<bool>(type: "boolean", nullable: false), CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), CreatedBy = table.Column<Guid>(type: "uuid", nullable: false), UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true), IsDeleted = table.Column<bool>(type: "boolean", nullable: false), DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            }, constraints: table => table.PrimaryKey("pk_cms_pages", x => x.Id));
        migrationBuilder.CreateTable(
            name: "cms_blog_posts", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false), Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), Content = table.Column<string>(type: "text", nullable: false), CoverImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true), ViewCount = table.Column<int>(type: "integer", nullable: false), IsPublished = table.Column<bool>(type: "boolean", nullable: false), PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), MetaTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true), MetaDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), MetaKeywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), CanonicalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), OgTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true), OgDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), OgImage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true), SeoSettings_NoIndex = table.Column<bool>(type: "boolean", nullable: false), CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), CreatedBy = table.Column<Guid>(type: "uuid", nullable: false), UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true), IsDeleted = table.Column<bool>(type: "boolean", nullable: false), DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            }, constraints: table => table.PrimaryKey("pk_cms_blog_posts", x => x.Id));
        migrationBuilder.CreateTable(
            name: "cms_contact_messages", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false), Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false), Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false), Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), Message = table.Column<string>(type: "text", nullable: false), IsRead = table.Column<bool>(type: "boolean", nullable: false), IsReplied = table.Column<bool>(type: "boolean", nullable: false), RepliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), RepliedBy = table.Column<Guid>(type: "uuid", nullable: true), ReplyContent = table.Column<string>(type: "text", nullable: true), CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), CreatedBy = table.Column<Guid>(type: "uuid", nullable: false), UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true), IsDeleted = table.Column<bool>(type: "boolean", nullable: false), DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            }, constraints: table => table.PrimaryKey("pk_cms_contact_messages", x => x.Id));
        migrationBuilder.CreateTable(
            name: "cms_newsletter_subscribers", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false), Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false), IsActive = table.Column<bool>(type: "boolean", nullable: false), Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true), CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), CreatedBy = table.Column<Guid>(type: "uuid", nullable: false), UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true), IsDeleted = table.Column<bool>(type: "boolean", nullable: false), DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            }, constraints: table => table.PrimaryKey("pk_cms_newsletter_subscribers", x => x.Id));
        migrationBuilder.CreateIndex("ix_cms_content_blocks_group", "cms_content_blocks", "Group", "speed_reading");
        migrationBuilder.CreateIndex("ix_cms_content_blocks_key", "cms_content_blocks", "Key", "speed_reading");
        migrationBuilder.CreateIndex("ix_cms_pages_slug", "cms_pages", "Slug", "speed_reading");
        migrationBuilder.CreateIndex("ix_cms_blog_posts_published_at", "cms_blog_posts", "PublishedAt", "speed_reading");
        migrationBuilder.CreateIndex("ix_cms_blog_posts_slug", "cms_blog_posts", "Slug", "speed_reading");
        migrationBuilder.CreateIndex("ix_cms_contact_messages_created_at", "cms_contact_messages", "CreatedAt", "speed_reading");
        migrationBuilder.CreateIndex("ix_cms_contact_messages_is_read", "cms_contact_messages", "IsRead", "speed_reading");
        migrationBuilder.CreateIndex("ix_cms_newsletter_subscribers_email", "cms_newsletter_subscribers", "Email", "speed_reading", true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "cms_newsletter_subscribers", schema: "speed_reading");
        migrationBuilder.DropTable(name: "cms_contact_messages", schema: "speed_reading");
        migrationBuilder.DropTable(name: "cms_blog_posts", schema: "speed_reading");
        migrationBuilder.DropTable(name: "cms_pages", schema: "speed_reading");
        migrationBuilder.DropTable(name: "cms_content_blocks", schema: "speed_reading");
    }
}
