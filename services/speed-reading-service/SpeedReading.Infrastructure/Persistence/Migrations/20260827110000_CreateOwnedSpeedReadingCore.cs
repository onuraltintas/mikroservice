using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeedReading.Infrastructure.Persistence;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827110000_CreateOwnedSpeedReadingCore")]
public partial class CreateOwnedSpeedReadingCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "speed_reading");

        migrationBuilder.CreateTable(
            name: "exercise_type_categories",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exercise_type_categories", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "exercise_types",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                icon_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                color_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                engine_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                category_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exercise_types", x => x.id);
                table.ForeignKey(
                    name: "fk_exercise_types_exercise_type_categories_category_id",
                    column: x => x.category_id,
                    principalSchema: "speed_reading",
                    principalTable: "exercise_type_categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "exercises",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                exercise_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                type_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                difficulty_level = table.Column<int>(type: "integer", nullable: false),
                configuration_json = table.Column<string>(type: "jsonb", nullable: false),
                target_age_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exercises", x => x.id);
                table.ForeignKey(
                    name: "fk_exercises_exercise_types_exercise_type_id",
                    column: x => x.exercise_type_id,
                    principalSchema: "speed_reading",
                    principalTable: "exercise_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "reading_texts",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                content = table.Column<string>(type: "text", nullable: false),
                word_count = table.Column<int>(type: "integer", nullable: false),
                category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                difficulty_level = table.Column<int>(type: "integer", nullable: false),
                target_age_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                recommended_min_level = table.Column<int>(type: "integer", nullable: false),
                recommended_max_level = table.Column<int>(type: "integer", nullable: false),
                average_comprehension_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                times_read = table.Column<int>(type: "integer", nullable: false),
                average_reading_time_seconds = table.Column<int>(type: "integer", nullable: false),
                exercise_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_reading_texts", x => x.id);
                table.ForeignKey(
                    name: "fk_reading_texts_exercises_exercise_id",
                    column: x => x.exercise_id,
                    principalSchema: "speed_reading",
                    principalTable: "exercises",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "exercise_sessions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                reading_text_id = table.Column<Guid>(type: "uuid", nullable: true),
                student_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<int>(type: "integer", nullable: false),
                start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                total_paused_seconds = table.Column<int>(type: "integer", nullable: false),
                paused_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                time_limit_seconds = table.Column<int>(type: "integer", nullable: true),
                current_step = table.Column<int>(type: "integer", nullable: false),
                total_steps = table.Column<int>(type: "integer", nullable: false),
                correct_count = table.Column<int>(type: "integer", nullable: false),
                incorrect_count = table.Column<int>(type: "integer", nullable: false),
                session_data_json = table.Column<string>(type: "jsonb", nullable: false),
                custom_data_json = table.Column<string>(type: "jsonb", nullable: true),
                processed_actions_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exercise_sessions", x => x.id);
                table.ForeignKey(
                    name: "fk_exercise_sessions_exercises_exercise_id",
                    column: x => x.exercise_id,
                    principalSchema: "speed_reading",
                    principalTable: "exercises",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_exercise_sessions_reading_texts_reading_text_id",
                    column: x => x.reading_text_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "reading_questions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                reading_text_id = table.Column<Guid>(type: "uuid", nullable: false),
                question_text = table.Column<string>(type: "text", nullable: false),
                type = table.Column<int>(type: "integer", nullable: false),
                bloom_level = table.Column<int>(type: "integer", nullable: false),
                difficulty_level = table.Column<int>(type: "integer", nullable: false),
                explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                option_a = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                option_b = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                option_c = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                option_d = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                correct_answer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                order_index = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_reading_questions", x => x.id);
                table.ForeignKey(
                    name: "fk_reading_questions_reading_texts_reading_text_id",
                    column: x => x.reading_text_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "exercise_session_answers",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: true),
                legacy_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                question_id = table.Column<Guid>(type: "uuid", nullable: false),
                answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                is_correct = table.Column<bool>(type: "boolean", nullable: false),
                time_spent_seconds = table.Column<int>(type: "integer", nullable: false),
                bloom_level = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exercise_session_answers", x => x.id);
                table.ForeignKey(
                    name: "fk_exercise_session_answers_exercise_sessions_session_id",
                    column: x => x.session_id,
                    principalSchema: "speed_reading",
                    principalTable: "exercise_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "exercise_session_results",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: true),
                legacy_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                reading_text_id = table.Column<Guid>(type: "uuid", nullable: true),
                words_read = table.Column<int>(type: "integer", nullable: false),
                time_spent_seconds = table.Column<int>(type: "integer", nullable: false),
                raw_wpm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                comprehension_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                weighted_kdp = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                question_answers_json = table.Column<string>(type: "jsonb", nullable: false),
                reading_movements_json = table.Column<string>(type: "jsonb", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exercise_session_results", x => x.id);
                table.ForeignKey(
                    name: "fk_exercise_session_results_exercise_sessions_session_id",
                    column: x => x.session_id,
                    principalSchema: "speed_reading",
                    principalTable: "exercise_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_exercise_session_results_reading_texts_reading_text_id",
                    column: x => x.reading_text_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_exercises_creator_id",
            schema: "speed_reading",
            table: "exercises",
            column: "creator_id");
        migrationBuilder.CreateIndex(
            name: "ix_exercises_exercise_type_id",
            schema: "speed_reading",
            table: "exercises",
            column: "exercise_type_id");
        migrationBuilder.CreateIndex(
            name: "ix_exercise_type_categories_name",
            schema: "speed_reading",
            table: "exercise_type_categories",
            column: "name",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_exercise_types_name",
            schema: "speed_reading",
            table: "exercise_types",
            column: "name",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_exercise_types_category_id_is_active",
            schema: "speed_reading",
            table: "exercise_types",
            columns: new[] { "category_id", "is_active" });
        migrationBuilder.CreateIndex(
            name: "ix_exercises_type_code_is_active",
            schema: "speed_reading",
            table: "exercises",
            columns: new[] { "type_code", "is_active" });
        migrationBuilder.CreateIndex(
            name: "ix_reading_texts_exercise_id_is_active",
            schema: "speed_reading",
            table: "reading_texts",
            columns: new[] { "exercise_id", "is_active" });
        migrationBuilder.CreateIndex(
            name: "ix_reading_texts_language_is_active",
            schema: "speed_reading",
            table: "reading_texts",
            columns: new[] { "language", "is_active" });
        migrationBuilder.CreateIndex(
            name: "ix_exercise_sessions_student_id_status",
            schema: "speed_reading",
            table: "exercise_sessions",
            columns: new[] { "student_id", "status" });
        migrationBuilder.CreateIndex(
            name: "ix_exercise_sessions_student_id_exercise_id_status",
            schema: "speed_reading",
            table: "exercise_sessions",
            columns: new[] { "student_id", "exercise_id", "status" });
        migrationBuilder.CreateIndex(
            name: "ix_reading_questions_reading_text_id_order_index",
            schema: "speed_reading",
            table: "reading_questions",
            columns: new[] { "reading_text_id", "order_index" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_exercise_session_answers_session_id_question_id",
            schema: "speed_reading",
            table: "exercise_session_answers",
            columns: new[] { "session_id", "question_id" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_exercise_session_results_session_id",
            schema: "speed_reading",
            table: "exercise_session_results",
            column: "session_id",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_exercise_session_results_legacy_session_id",
            schema: "speed_reading",
            table: "exercise_session_results",
            column: "legacy_session_id");
        migrationBuilder.CreateIndex(
            name: "ix_exercise_session_results_student_id_completed_at",
            schema: "speed_reading",
            table: "exercise_session_results",
            columns: new[] { "student_id", "completed_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "exercise_session_answers", schema: "speed_reading");
        migrationBuilder.DropTable(name: "exercise_session_results", schema: "speed_reading");
        migrationBuilder.DropTable(name: "reading_questions", schema: "speed_reading");
        migrationBuilder.DropTable(name: "exercise_sessions", schema: "speed_reading");
        migrationBuilder.DropTable(name: "reading_texts", schema: "speed_reading");
        migrationBuilder.DropTable(name: "exercises", schema: "speed_reading");
        migrationBuilder.DropTable(name: "exercise_types", schema: "speed_reading");
        migrationBuilder.DropTable(name: "exercise_type_categories", schema: "speed_reading");
    }
}
