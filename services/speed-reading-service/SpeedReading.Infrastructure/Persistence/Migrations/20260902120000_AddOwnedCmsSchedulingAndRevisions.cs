using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260902120000_AddOwnedCmsSchedulingAndRevisions")]
public partial class AddOwnedCmsSchedulingAndRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ScheduledPublishAt",
            schema: "speed_reading",
            table: "cms_pages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ScheduledPublishAt",
            schema: "speed_reading",
            table: "cms_blog_posts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "cms_content_revisions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                payload_json = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_cms_content_revisions", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_cms_content_revisions_entity_version",
            schema: "speed_reading",
            table: "cms_content_revisions",
            columns: new[] { "entity_type", "entity_id", "version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_cms_pages_scheduled_publish_at",
            schema: "speed_reading",
            table: "cms_pages",
            column: "ScheduledPublishAt");

        migrationBuilder.CreateIndex(
            name: "ix_cms_blog_posts_scheduled_publish_at",
            schema: "speed_reading",
            table: "cms_blog_posts",
            column: "ScheduledPublishAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("ix_cms_blog_posts_scheduled_publish_at", "cms_blog_posts", "speed_reading");
        migrationBuilder.DropIndex("ix_cms_pages_scheduled_publish_at", "cms_pages", "speed_reading");
        migrationBuilder.DropTable("cms_content_revisions", "speed_reading");
        migrationBuilder.DropColumn("ScheduledPublishAt", "cms_blog_posts", "speed_reading");
        migrationBuilder.DropColumn("ScheduledPublishAt", "cms_pages", "speed_reading");
    }
}
