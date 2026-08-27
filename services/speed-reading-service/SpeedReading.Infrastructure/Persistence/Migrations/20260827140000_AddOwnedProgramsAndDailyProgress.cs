using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827140000_AddOwnedProgramsAndDailyProgress")]
public partial class AddOwnedProgramsAndDailyProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "program_templates",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                TargetAgeGroupConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                MinAssessmentScore = table.Column<int>(type: "integer", nullable: false),
                MaxAssessmentScore = table.Column<int>(type: "integer", nullable: false),
                WeeklyPatternJson = table.Column<string>(type: "jsonb", nullable: false),
                InitialDifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                WeeksPerDifficultyIncrease = table.Column<int>(type: "integer", nullable: false),
                MaxDifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                TotalWeeks = table.Column<int>(type: "integer", nullable: false),
                TotalDays = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                ProgramType = table.Column<int>(type: "integer", nullable: false),
                ExamType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                IsAssessment = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_program_templates", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "student_program_progress",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ProgramTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                AssignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CurrentDay = table.Column<int>(type: "integer", nullable: false),
                CurrentWeek = table.Column<int>(type: "integer", nullable: false),
                CurrentDifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                DaysCompleted = table.Column<int>(type: "integer", nullable: false),
                ExercisesCompleted = table.Column<int>(type: "integer", nullable: false),
                LastCompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AverageSuccessRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                LongestStreak = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_student_program_progress", x => x.id);
                table.ForeignKey(
                    name: "fk_student_program_progress_program_templates_program_template_id",
                    column: x => x.ProgramTemplateId,
                    principalSchema: "speed_reading",
                    principalTable: "program_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "daily_exercise_logs",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentProgramProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                ExerciseTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                DayNumber = table.Column<int>(type: "integer", nullable: false),
                WeekNumber = table.Column<int>(type: "integer", nullable: false),
                DifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                TimeSpentSeconds = table.Column<int>(type: "integer", nullable: false),
                SuccessRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                IsPassed = table.Column<bool>(type: "boolean", nullable: false),
                ResultDataJson = table.Column<string>(type: "jsonb", nullable: false),
                AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                IsRetry = table.Column<bool>(type: "boolean", nullable: false),
                DevicePlatform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CorrectCount = table.Column<int>(type: "integer", nullable: false),
                IncorrectCount = table.Column<int>(type: "integer", nullable: false),
                TotalAttempts = table.Column<int>(type: "integer", nullable: false),
                AverageWPM = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                AverageComprehension = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                TimeOfDay = table.Column<TimeSpan>(type: "interval", nullable: false),
                AverageResponseTimeMs = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                MedianResponseTimeMs = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                StdDevResponseTimeMs = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                PauseCount = table.Column<int>(type: "integer", nullable: false),
                TotalPausedSeconds = table.Column<int>(type: "integer", nullable: false),
                PerformanceTrend = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                IsPersonalBest = table.Column<bool>(type: "boolean", nullable: false),
                PreviousAverageScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                EngagementScore = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                FrustrationScore = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                LearningRate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                ConsistencyScore = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_daily_exercise_logs", x => x.id);
                table.ForeignKey(
                    name: "fk_daily_exercise_logs_exercises_exercise_id",
                    column: x => x.ExerciseId,
                    principalSchema: "speed_reading",
                    principalTable: "exercises",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_daily_exercise_logs_exercise_types_exercise_type_id",
                    column: x => x.ExerciseTypeId,
                    principalSchema: "speed_reading",
                    principalTable: "exercise_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_daily_exercise_logs_student_program_progress_progress_id",
                    column: x => x.StudentProgramProgressId,
                    principalSchema: "speed_reading",
                    principalTable: "student_program_progress",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_program_templates_is_active_display_order",
            schema: "speed_reading",
            table: "program_templates",
            columns: new[] { "IsActive", "DisplayOrder" });
        migrationBuilder.CreateIndex(
            name: "ix_student_program_progress_program_template_id",
            schema: "speed_reading",
            table: "student_program_progress",
            column: "ProgramTemplateId");
        migrationBuilder.CreateIndex(
            name: "ix_student_program_progress_user_id_is_active_assigned_date",
            schema: "speed_reading",
            table: "student_program_progress",
            columns: new[] { "UserId", "IsActive", "AssignedDate" });
        migrationBuilder.CreateIndex(
            name: "ix_daily_exercise_logs_exercise_id",
            schema: "speed_reading",
            table: "daily_exercise_logs",
            column: "ExerciseId");
        migrationBuilder.CreateIndex(
            name: "ix_daily_exercise_logs_student_program_progress_id_week_number_day_number",
            schema: "speed_reading",
            table: "daily_exercise_logs",
            columns: new[] { "StudentProgramProgressId", "WeekNumber", "DayNumber" });
        migrationBuilder.CreateIndex(
            name: "ix_daily_exercise_logs_user_id_completed_date",
            schema: "speed_reading",
            table: "daily_exercise_logs",
            columns: new[] { "UserId", "CompletedDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "daily_exercise_logs", schema: "speed_reading");
        migrationBuilder.DropTable(name: "student_program_progress", schema: "speed_reading");
        migrationBuilder.DropTable(name: "program_templates", schema: "speed_reading");
    }
}
