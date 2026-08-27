using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827153000_AddOwnedRsvp")]
public partial class AddOwnedRsvp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rsvp_sessions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                text_id = table.Column<Guid>(type: "uuid", nullable: true),
                text_content = table.Column<string>(type: "text", nullable: true),
                words_per_minute = table.Column<int>(type: "integer", nullable: false),
                font_family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                font_size = table.Column<int>(type: "integer", nullable: false),
                background_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                text_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                total_words = table.Column<int>(type: "integer", nullable: false),
                completed_words = table.Column<int>(type: "integer", nullable: false),
                session_duration = table.Column<int>(type: "integer", nullable: false),
                completed = table.Column<bool>(type: "boolean", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_rsvp_sessions", x => x.id));

        migrationBuilder.CreateIndex("ix_rsvp_sessions_user_id", "rsvp_sessions", "user_id", "speed_reading");
        migrationBuilder.CreateIndex("ix_rsvp_sessions_user_id_created_at", "rsvp_sessions", new[] { "user_id", "created_at" }, "speed_reading");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "rsvp_sessions", schema: "speed_reading");
    }
}
