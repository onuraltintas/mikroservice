using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261012020000_SeparateAssessmentExerciseScope")]
public sealed class SeparateAssessmentExerciseScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "is_assessment_only", schema: "speed_reading", table: "exercises",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.Sql("""
            UPDATE speed_reading.exercises SET is_assessment_only = true
            WHERE created_by = 'system:assessment-v1'
                AND id IN ('99b0a038-90b9-5156-b803-f96d30753499','82087c90-eb56-587a-b064-cdc27e15f14b',
                    'dc182755-52fe-5b86-be96-ba98fbcd04b9','4d7c5eb7-2a1e-5900-b4ac-bdf3add28872',
                    '291705e1-254f-5379-aecd-c033901c276d','0f67593d-efd9-5f0d-814b-1f7d2692a48f',
                    'bd64c53c-c379-5a42-bcfc-a0f076189520','4dd52a35-bbee-512a-914a-8f471438c528',
                    '39b33543-b617-5e3e-a6fe-06d82b58137b','705e471b-ec39-508d-aa84-dd5bee4b6bcf',
                    '9ca42784-5cdb-5aaf-be5b-d0f3924ecdeb','c5d9326d-2097-51f7-974f-5b660847c831');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "is_assessment_only", schema: "speed_reading", table: "exercises");
}
