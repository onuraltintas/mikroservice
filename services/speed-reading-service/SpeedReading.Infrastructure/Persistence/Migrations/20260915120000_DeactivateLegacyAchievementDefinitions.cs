using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260915120000_DeactivateLegacyAchievementDefinitions")]
public partial class DeactivateLegacyAchievementDefinitions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            UPDATE speed_reading.achievements
            SET "IsDeleted" = TRUE,
                "IsActive" = FALSE,
                "DeletedAt" = CURRENT_TIMESTAMP,
                "DeletedBy" = 'system:legacy-cutover',
                updated_at = CURRENT_TIMESTAMP,
                updated_by = 'system:legacy-cutover'
            WHERE NOT "IsDeleted"
              AND (
                lower(btrim("CriteriaType")) NOT IN (
                    'streak', 'level_reached', 'activity_count', 'total_xp',
                    'reading_minutes', 'wpm_reached', 'comprehension_score',
                    'reading_count', 'rsvp_count', 'exercise_count',
                    'vocabulary_learned', 'vocabulary_box', 'vocabulary_streak',
                    'vocabulary_categories', 'rsvp_wpm', 'rsvp_comprehension',
                    'exercise_type_first', 'exercise_variety')
                OR "CriteriaValue" !~ '^[[:space:]]*[{]'
              );
            """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
