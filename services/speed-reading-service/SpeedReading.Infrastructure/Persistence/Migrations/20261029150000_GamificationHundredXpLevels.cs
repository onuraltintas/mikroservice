using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Review H5 (finding 14, decision 29.10.2026): every level is 100 XP. Recomputes the stored level, level progress,
    /// title and icon from total XP (no schema change). Down restores the former 0–199 first level.
    /// </summary>
    public partial class GamificationHundredXpLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE speed_reading.user_gamification SET \"CurrentLevel\" = (GREATEST(\"TotalXP\", 0) / 100 + 1), \"CurrentLevelXP\" = GREATEST(\"TotalXP\", 0) % 100, \"NextLevelXP\" = 100, \"LevelTitle\" = CASE WHEN (GREATEST(\"TotalXP\", 0) / 100 + 1) <= 5 THEN 'Başlangıç Okuyucu' WHEN (GREATEST(\"TotalXP\", 0) / 100 + 1) <= 10 THEN 'Gelişen Okuyucu' WHEN (GREATEST(\"TotalXP\", 0) / 100 + 1) <= 15 THEN 'İleri Okuyucu' ELSE 'Master Okuyucu' END, \"LevelIcon\" = CASE WHEN (GREATEST(\"TotalXP\", 0) / 100 + 1) <= 5 THEN '📖' WHEN (GREATEST(\"TotalXP\", 0) / 100 + 1) <= 10 THEN '📗' WHEN (GREATEST(\"TotalXP\", 0) / 100 + 1) <= 15 THEN '📘' ELSE '📕' END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE speed_reading.user_gamification SET \"CurrentLevel\" = GREATEST(1, GREATEST(\"TotalXP\", 0) / 100), \"CurrentLevelXP\" = LEAST(100, GREATEST(0, GREATEST(\"TotalXP\", 0) - (GREATEST(1, GREATEST(\"TotalXP\", 0) / 100) - 1) * 100)), \"NextLevelXP\" = 100, \"LevelTitle\" = CASE WHEN GREATEST(1, GREATEST(\"TotalXP\", 0) / 100) <= 5 THEN 'Başlangıç Okuyucu' WHEN GREATEST(1, GREATEST(\"TotalXP\", 0) / 100) <= 10 THEN 'Gelişen Okuyucu' WHEN GREATEST(1, GREATEST(\"TotalXP\", 0) / 100) <= 15 THEN 'İleri Okuyucu' ELSE 'Master Okuyucu' END, \"LevelIcon\" = CASE WHEN GREATEST(1, GREATEST(\"TotalXP\", 0) / 100) <= 5 THEN '📖' WHEN GREATEST(1, GREATEST(\"TotalXP\", 0) / 100) <= 10 THEN '📗' WHEN GREATEST(1, GREATEST(\"TotalXP\", 0) / 100) <= 15 THEN '📘' ELSE '📕' END;");
        }
    }
}
