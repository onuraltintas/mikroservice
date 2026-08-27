using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827148000_AddOwnedQuestionBank")]
public partial class AddOwnedQuestionBank : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "exam_questions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                content = table.Column<string>(type: "text", nullable: false),
                question = table.Column<string>(type: "text", nullable: false),
                option_a = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                option_b = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                option_c = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                option_d = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                option_e = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                correct_option = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                exam_type = table.Column<int>(type: "integer", nullable: false),
                difficulty = table.Column<int>(type: "integer", nullable: false),
                word_count = table.Column<int>(type: "integer", nullable: false),
                topic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                category = table.Column<int>(type: "integer", nullable: false),
                target_age_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exam_questions", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_exam_questions_exam_type_difficulty_category",
            schema: "speed_reading",
            table: "exam_questions",
            columns: new[] { "exam_type", "difficulty", "category" });
        migrationBuilder.CreateIndex(
            name: "ix_exam_questions_target_age_group_id",
            schema: "speed_reading",
            table: "exam_questions",
            column: "target_age_group_id");
        migrationBuilder.CreateIndex(
            name: "ix_exam_questions_created_at",
            schema: "speed_reading",
            table: "exam_questions",
            column: "created_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "exam_questions", schema: "speed_reading");
}
