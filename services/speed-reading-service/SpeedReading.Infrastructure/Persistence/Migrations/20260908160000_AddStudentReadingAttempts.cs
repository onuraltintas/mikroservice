using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260908160000_AddStudentReadingAttempts")]
public partial class AddStudentReadingAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "student_reading_attempts",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                reading_text_id = table.Column<Guid>(type: "uuid", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_student_reading_attempts", x => x.id);
                table.ForeignKey(
                    name: "fk_student_reading_attempts_reading_texts_reading_text_id",
                    column: x => x.reading_text_id,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_student_reading_attempts_reading_text_id",
            schema: "speed_reading",
            table: "student_reading_attempts",
            column: "reading_text_id");
        migrationBuilder.CreateIndex(
            name: "ix_student_reading_attempts_user_id_reading_text_id_started_at",
            schema: "speed_reading",
            table: "student_reading_attempts",
            columns: new[] { "user_id", "reading_text_id", "started_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "student_reading_attempts",
            schema: "speed_reading");
    }
}
