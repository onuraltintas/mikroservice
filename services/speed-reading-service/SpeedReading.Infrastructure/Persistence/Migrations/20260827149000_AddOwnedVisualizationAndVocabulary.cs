using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827149000_AddOwnedVisualizationAndVocabulary")]
public partial class AddOwnedVisualizationAndVocabulary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "visualization_scenes", schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                duration = table.Column<int>(type: "integer", nullable: false),
                display_order = table.Column<int>(type: "integer", nullable: false),
                difficulty_level = table.Column<int>(type: "integer", nullable: false),
                target_age_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_visualization_scenes", x => x.id);
                table.ForeignKey("fk_visualization_scenes_exercises_exercise_id", x => x.exercise_id,
                    principalSchema: "speed_reading", principalTable: "exercises", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "vocabulary_items", schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                word = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                definition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                example_sentence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                synonyms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                antonyms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                target_age_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                difficulty_level = table.Column<int>(type: "integer", nullable: false),
                category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_vocabulary_items", x => x.id));

        migrationBuilder.CreateTable(
            name: "visualization_questions", schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                scene_id = table.Column<Guid>(type: "uuid", nullable: false),
                question_text = table.Column<string>(type: "text", nullable: false),
                options_json = table.Column<string>(type: "jsonb", nullable: false),
                correct_answer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                question_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                display_order = table.Column<int>(type: "integer", nullable: false),
                hint_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_visualization_questions", x => x.id);
                table.ForeignKey("fk_visualization_questions_visualization_scenes_scene_id", x => x.scene_id,
                    principalSchema: "speed_reading", principalTable: "visualization_scenes", principalColumn: "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_vocabulary_progress", schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                vocabulary_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                box = table.Column<int>(type: "integer", nullable: false),
                consecutive_correct_count = table.Column<int>(type: "integer", nullable: false),
                next_review_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_user_vocabulary_progress", x => x.id);
                table.ForeignKey("fk_user_vocabulary_progress_vocabulary_items_vocabulary_item_id", x => x.vocabulary_item_id,
                    principalSchema: "speed_reading", principalTable: "vocabulary_items", principalColumn: "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "ix_visualization_scenes_exercise_id_is_deleted_display_order", schema: "speed_reading", table: "visualization_scenes", columns: new[] { "exercise_id", "is_deleted", "display_order" });
        migrationBuilder.CreateIndex(name: "ix_visualization_scenes_target_age_group_id", schema: "speed_reading", table: "visualization_scenes", column: "target_age_group_id");
        migrationBuilder.CreateIndex(name: "ix_visualization_questions_scene_id_is_deleted_display_order", schema: "speed_reading", table: "visualization_questions", columns: new[] { "scene_id", "is_deleted", "display_order" });
        migrationBuilder.CreateIndex(name: "ix_vocabulary_items_category_difficulty_level_is_deleted", schema: "speed_reading", table: "vocabulary_items", columns: new[] { "category", "difficulty_level", "is_deleted" });
        migrationBuilder.CreateIndex(name: "ix_vocabulary_items_target_age_group_id", schema: "speed_reading", table: "vocabulary_items", column: "target_age_group_id");
        migrationBuilder.CreateIndex(name: "ix_user_vocabulary_progress_user_id_vocabulary_item_id_is_deleted", schema: "speed_reading", table: "user_vocabulary_progress", columns: new[] { "user_id", "vocabulary_item_id", "is_deleted" });
        migrationBuilder.CreateIndex(name: "ix_user_vocabulary_progress_user_id_next_review_date_is_deleted", schema: "speed_reading", table: "user_vocabulary_progress", columns: new[] { "user_id", "next_review_date", "is_deleted" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_vocabulary_progress", schema: "speed_reading");
        migrationBuilder.DropTable(name: "visualization_questions", schema: "speed_reading");
        migrationBuilder.DropTable(name: "vocabulary_items", schema: "speed_reading");
        migrationBuilder.DropTable(name: "visualization_scenes", schema: "speed_reading");
    }
}
