using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260923120000_RetireLegacyPersonalizedRecommendations")]
public sealed class RetireLegacyPersonalizedRecommendations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Previously generated 30-item batches were not based on a demonstrated support need.
        // Keep completed history; retire only unfinished recommendations.
        migrationBuilder.Sql(
            """
            UPDATE speed_reading.personalized_learning_path_items
            SET "IsDeleted" = true,
                "DeletedAt" = NOW(),
                "DeletedBy" = 'personalized-path-policy-2026-09',
                updated_at = NOW(),
                updated_by = 'personalized-path-policy-2026-09'
            WHERE "IsDeleted" = false AND "IsCompleted" = false;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE speed_reading.personalized_learning_path_items
            SET "IsDeleted" = false,
                "DeletedAt" = NULL,
                "DeletedBy" = NULL
            WHERE "DeletedBy" = 'personalized-path-policy-2026-09';
            """);
    }
}
