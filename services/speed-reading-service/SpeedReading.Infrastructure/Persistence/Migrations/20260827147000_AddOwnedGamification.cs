using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827147000_AddOwnedGamification")]
public partial class AddOwnedGamification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "achievements",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                IconEmoji = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                CriteriaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CriteriaValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                TriggerType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                TriggerValue = table.Column<int>(type: "integer", nullable: true),
                IsRepeatable = table.Column<bool>(type: "boolean", nullable: false),
                XPReward = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_achievements", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_gamification",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TotalXP = table.Column<long>(type: "bigint", nullable: false),
                CurrentLevel = table.Column<int>(type: "integer", nullable: false),
                CurrentLevelXP = table.Column<int>(type: "integer", nullable: false),
                NextLevelXP = table.Column<int>(type: "integer", nullable: false),
                LevelTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                LevelIcon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                LongestStreak = table.Column<int>(type: "integer", nullable: false),
                LastActivityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                StreakFreezeCount = table.Column<int>(type: "integer", nullable: false),
                TotalActivitiesCompleted = table.Column<int>(type: "integer", nullable: false),
                TotalReadingMinutes = table.Column<int>(type: "integer", nullable: false),
                MaxWPM = table.Column<int>(type: "integer", nullable: false),
                MaxComprehensionScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TotalExercisesCompleted = table.Column<int>(type: "integer", nullable: false),
                TotalReadingSessionsCompleted = table.Column<int>(type: "integer", nullable: false),
                CompletedExerciseTypesJson = table.Column<string>(type: "jsonb", nullable: false),
                MaxRSVPWPM = table.Column<int>(type: "integer", nullable: false),
                MaxRSVPComprehension = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TotalVocabularyWordsLearned = table.Column<int>(type: "integer", nullable: false),
                MaxVocabularyBoxReached = table.Column<int>(type: "integer", nullable: false),
                TotalVocabularyQuestionsAnswered = table.Column<int>(type: "integer", nullable: false),
                VocabularyMasteryLevel = table.Column<int>(type: "integer", nullable: false),
                MaxVocabularyStreak = table.Column<int>(type: "integer", nullable: false),
                LearnedVocabularyCategoriesJson = table.Column<string>(type: "jsonb", nullable: false),
                LearnedVocabularyCategoriesMapJson = table.Column<string>(type: "jsonb", nullable: false),
                LearnedVocabularyDifficultiesJson = table.Column<string>(type: "jsonb", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_gamification", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_achievements",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AchievementId = table.Column<Guid>(type: "uuid", nullable: false),
                UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsShowcased = table.Column<bool>(type: "boolean", nullable: false),
                ShowcaseOrder = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_achievements", x => x.id);
                table.ForeignKey(
                    name: "fk_user_achievements_achievements_achievement_id",
                    column: x => x.AchievementId,
                    principalSchema: "speed_reading",
                    principalTable: "achievements",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_achievements_category",
            schema: "speed_reading",
            table: "achievements",
            column: "Category");
        migrationBuilder.CreateIndex(
            name: "ix_achievements_tier",
            schema: "speed_reading",
            table: "achievements",
            column: "Tier");
        migrationBuilder.CreateIndex(
            name: "ix_achievements_is_active",
            schema: "speed_reading",
            table: "achievements",
            column: "IsActive");
        migrationBuilder.CreateIndex(
            name: "ix_achievements_sort_order",
            schema: "speed_reading",
            table: "achievements",
            column: "SortOrder");
        migrationBuilder.CreateIndex(
            name: "ix_user_gamification_user_id",
            schema: "speed_reading",
            table: "user_gamification",
            column: "UserId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_user_gamification_total_xp",
            schema: "speed_reading",
            table: "user_gamification",
            column: "TotalXP");
        migrationBuilder.CreateIndex(
            name: "ix_user_gamification_current_level",
            schema: "speed_reading",
            table: "user_gamification",
            column: "CurrentLevel");
        migrationBuilder.CreateIndex(
            name: "ix_user_gamification_current_streak",
            schema: "speed_reading",
            table: "user_gamification",
            column: "CurrentStreak");
        migrationBuilder.CreateIndex(
            name: "ix_user_achievements_user_id_achievement_id_is_deleted",
            schema: "speed_reading",
            table: "user_achievements",
            columns: new[] { "UserId", "AchievementId", "IsDeleted" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_user_achievements_user_id",
            schema: "speed_reading",
            table: "user_achievements",
            column: "UserId");
        migrationBuilder.CreateIndex(
            name: "ix_user_achievements_unlocked_at",
            schema: "speed_reading",
            table: "user_achievements",
            column: "UnlockedAt");
        migrationBuilder.CreateIndex(
            name: "ix_user_achievements_user_id_is_showcased",
            schema: "speed_reading",
            table: "user_achievements",
            columns: new[] { "UserId", "IsShowcased" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_achievements", schema: "speed_reading");
        migrationBuilder.DropTable(name: "user_gamification", schema: "speed_reading");
        migrationBuilder.DropTable(name: "achievements", schema: "speed_reading");
    }
}
