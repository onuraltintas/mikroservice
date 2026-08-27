using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827120000_AddOwnedReadingSessionHistory")]
public partial class AddOwnedReadingSessionHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "reading_sessions",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                reading_text_id = table.Column<Guid>(type: "uuid", nullable: false),
                reading_time_seconds = table.Column<int>(type: "integer", nullable: false),
                calculated_wpm = table.Column<int>(type: "integer", nullable: false),
                correct_answers = table.Column<int>(type: "integer", nullable: false),
                total_questions = table.Column<int>(type: "integer", nullable: false),
                comprehension_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                efficiency_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_reading_sessions", x => x.id);
                table.ForeignKey(
                    name: "fk_reading_sessions_reading_texts_reading_text_id",
                    column: x => x.reading_text_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_reading_sessions_reading_text_id",
            schema: "speed_reading",
            table: "reading_sessions",
            column: "reading_text_id");
        migrationBuilder.CreateIndex(
            name: "ix_reading_sessions_user_id_completed_at",
            schema: "speed_reading",
            table: "reading_sessions",
            columns: new[] { "user_id", "completed_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "reading_sessions", schema: "speed_reading");
    }
}
