using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827157000_AddOwnedAdaptiveText")]
public partial class AddOwnedAdaptiveText : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "adaptive_reading_profiles",
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
                current_reading_level = table.Column<int>(type: "integer", nullable: false),
                average_comprehension_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                average_reading_speed = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                total_texts_read = table.Column<int>(type: "integer", nullable: false),
                total_reading_time_seconds = table.Column<int>(type: "integer", nullable: false),
                preferred_categories = table.Column<string[]>(type: "text[]", nullable: false),
                difficult_categories = table.Column<string[]>(type: "text[]", nullable: false),
                last_calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_adaptive_reading_profiles", x => x.id));

        migrationBuilder.CreateTable(
            name: "adaptive_text_recommendation_history",
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
                recommended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                was_accepted = table.Column<bool>(type: "boolean", nullable: false),
                confidence_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                reasoning_json = table.Column<string>(type: "text", nullable: false),
                student_level_at_time = table.Column<int>(type: "integer", nullable: false),
                result_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_adaptive_text_recommendation_history", x => x.id);
                table.ForeignKey(
                    name: "fk_adaptive_text_recommendation_history_reading_texts_reading_text_id",
                    column: x => x.reading_text_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_adaptive_reading_profiles_student_id_is_deleted",
            schema: "speed_reading",
            table: "adaptive_reading_profiles",
            columns: new[] { "student_id", "is_deleted" });
        migrationBuilder.CreateIndex(
            name: "ix_adaptive_text_recommendation_history_reading_text_id",
            schema: "speed_reading",
            table: "adaptive_text_recommendation_history",
            column: "reading_text_id");
        migrationBuilder.CreateIndex(
            name: "ix_adaptive_text_recommendation_history_student_id_recommended_at",
            schema: "speed_reading",
            table: "adaptive_text_recommendation_history",
            columns: new[] { "student_id", "recommended_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "adaptive_text_recommendation_history", schema: "speed_reading");
        migrationBuilder.DropTable(name: "adaptive_reading_profiles", schema: "speed_reading");
    }
}
