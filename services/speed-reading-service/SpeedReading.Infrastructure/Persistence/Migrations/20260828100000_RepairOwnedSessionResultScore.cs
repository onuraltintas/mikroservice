using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260828100000_RepairOwnedSessionResultScore")]
public partial class RepairOwnedSessionResultScore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE speed_reading.exercise_session_results
            SET score = weighted_kdp
            WHERE score IS DISTINCT FROM weighted_kdp;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The previous score values were not source-owned and cannot be
        // reconstructed safely during a rollback.
    }
}
