using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827154000_AddOwnedReview")]
public partial class AddOwnedReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "review_items",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                next_review_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                easiness_factor = table.Column<double>(type: "double precision", nullable: false),
                last_score = table.Column<double>(type: "double precision", nullable: true),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                program_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                review_count = table.Column<int>(type: "integer", nullable: false),
                interval_days = table.Column<int>(type: "integer", nullable: false),
                is_mastered = table.Column<bool>(type: "boolean", nullable: false),
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
                table.PrimaryKey("pk_review_items", x => x.id);
                table.ForeignKey(
                    name: "fk_review_items_exercises_exercise_id",
                    column: x => x.exercise_id,
                    principalSchema: "speed_reading",
                    principalTable: "exercises",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_review_items_program_templates_program_template_id",
                    column: x => x.program_template_id,
                    principalSchema: "speed_reading",
                    principalTable: "program_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_review_items_user_id_next_review_date_is_deleted",
            schema: "speed_reading",
            table: "review_items",
            columns: new[] { "user_id", "next_review_date", "is_deleted" });
        migrationBuilder.CreateIndex(
            name: "ux_review_items_user_id_exercise_id_is_deleted",
            schema: "speed_reading",
            table: "review_items",
            columns: new[] { "user_id", "exercise_id", "is_deleted" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_review_items_exercise_id",
            schema: "speed_reading",
            table: "review_items",
            column: "exercise_id");
        migrationBuilder.CreateIndex(
            name: "ix_review_items_program_template_id",
            schema: "speed_reading",
            table: "review_items",
            column: "program_template_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "review_items", schema: "speed_reading");
    }
}
