using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827156000_AddOwnedAdaptiveLearning")]
public partial class AddOwnedAdaptiveLearning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "adaptive_learning_profiles",
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
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                proficiency_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                preferred_content_types = table.Column<string>(type: "text", nullable: true),
                learning_pace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                weak_areas = table.Column<string>(type: "text", nullable: true),
                strong_areas = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_adaptive_learning_profiles", x => x.id));

        migrationBuilder.CreateTable(
            name: "adaptive_content_recommendations",
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
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                reading_text_id = table.Column<Guid>(type: "uuid", nullable: false),
                confidence_score = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                recommendation_reason = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_adaptive_content_recommendations", x => x.id);
                table.ForeignKey(
                    name: "fk_adaptive_content_recommendations_reading_texts_reading_text_id",
                    column: x => x.reading_text_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "adaptive_daily_goals",
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
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                target_minutes = table.Column<int>(type: "integer", nullable: false),
                actual_minutes = table.Column<int>(type: "integer", nullable: false),
                is_completed = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_adaptive_daily_goals", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_adaptive_learning_profiles_student_id_is_deleted",
            schema: "speed_reading",
            table: "adaptive_learning_profiles",
            columns: new[] { "student_id", "is_deleted" });
        migrationBuilder.CreateIndex(
            name: "ix_adaptive_content_recommendations_student_id_is_deleted_confidence_score",
            schema: "speed_reading",
            table: "adaptive_content_recommendations",
            columns: new[] { "student_id", "is_deleted", "confidence_score" });
        migrationBuilder.CreateIndex(
            name: "ix_adaptive_content_recommendations_reading_text_id",
            schema: "speed_reading",
            table: "adaptive_content_recommendations",
            column: "reading_text_id");
        migrationBuilder.CreateIndex(
            name: "ix_adaptive_daily_goals_student_id_date_is_deleted",
            schema: "speed_reading",
            table: "adaptive_daily_goals",
            columns: new[] { "student_id", "date", "is_deleted" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "adaptive_content_recommendations", schema: "speed_reading");
        migrationBuilder.DropTable(name: "adaptive_daily_goals", schema: "speed_reading");
        migrationBuilder.DropTable(name: "adaptive_learning_profiles", schema: "speed_reading");
    }
}
