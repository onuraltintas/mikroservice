using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827155000_AddOwnedContentFeedback")]
public partial class AddOwnedContentFeedback : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "content_feedback",
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
                content_id = table.Column<Guid>(type: "uuid", nullable: false),
                content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                rating = table.Column<int>(type: "integer", nullable: true),
                is_liked = table.Column<bool>(type: "boolean", nullable: false),
                is_bookmarked = table.Column<bool>(type: "boolean", nullable: false),
                skip_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                completion_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                time_spent_seconds = table.Column<int>(type: "integer", nullable: false),
                expected_time_seconds = table.Column<int>(type: "integer", nullable: false),
                comprehension_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                exercise_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                retry_count = table.Column<int>(type: "integer", nullable: false),
                interaction_count = table.Column<int>(type: "integer", nullable: false),
                pause_count = table.Column<int>(type: "integer", nullable: false),
                abandoned_at_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                session_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                time_of_day = table.Column<int>(type: "integer", nullable: false),
                device_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                content_category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                content_difficulty_level = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_content_feedback", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_content_feedback_user_id_session_date",
            schema: "speed_reading",
            table: "content_feedback",
            columns: new[] { "user_id", "session_date" });
        migrationBuilder.CreateIndex(
            name: "ix_content_feedback_user_id_content_type_content_id",
            schema: "speed_reading",
            table: "content_feedback",
            columns: new[] { "user_id", "content_type", "content_id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "content_feedback", schema: "speed_reading");
    }
}
