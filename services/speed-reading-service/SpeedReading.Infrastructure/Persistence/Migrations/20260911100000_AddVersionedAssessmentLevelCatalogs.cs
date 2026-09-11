using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260911100000_AddVersionedAssessmentLevelCatalogs")]
public partial class AddVersionedAssessmentLevelCatalogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assessment_level_catalogs",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                catalog_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                definitions_json = table.Column<string>(type: "text", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_assessment_level_catalogs", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_assessment_level_catalogs_catalog_version",
            schema: "speed_reading",
            table: "assessment_level_catalogs",
            column: "catalog_version",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ux_assessment_level_catalogs_published",
            schema: "speed_reading",
            table: "assessment_level_catalogs",
            column: "status",
            unique: true,
            filter: "status = 2");

        migrationBuilder.AddColumn<string>(
            name: "level_catalog_version",
            schema: "speed_reading",
            table: "assessment_attempts",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "tr-standard-v1");

        migrationBuilder.Sql(
            """
            INSERT INTO speed_reading.assessment_level_catalogs
                (id, catalog_version, name, definitions_json, status, published_at, created_at, created_by, version)
            VALUES
                ('8d315f73-859a-4ed8-9ee7-90da62f9c6a1', 'tr-standard-v1', 'Standart Seviye Kataloğu',
                 '[{"level":1,"code":"beginner","displayName":"Başlangıç","minimumWpm":0,"minimumComprehension":0},{"level":2,"code":"basic","displayName":"Temel","minimumWpm":100,"minimumComprehension":40},{"level":3,"code":"lower_intermediate","displayName":"Orta-Alt","minimumWpm":150,"minimumComprehension":55},{"level":4,"code":"intermediate","displayName":"Orta","minimumWpm":200,"minimumComprehension":65},{"level":5,"code":"upper_intermediate","displayName":"Orta-Üst","minimumWpm":250,"minimumComprehension":70},{"level":6,"code":"advanced","displayName":"İleri","minimumWpm":300,"minimumComprehension":75},{"level":7,"code":"expert","displayName":"Uzman","minimumWpm":400,"minimumComprehension":80},{"level":8,"code":"elite","displayName":"Elit","minimumWpm":500,"minimumComprehension":90}]',
                 2, NOW(), NOW(), 'migration:20260911100000', 0);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "level_catalog_version",
            schema: "speed_reading",
            table: "assessment_attempts");
        migrationBuilder.DropTable(name: "assessment_level_catalogs", schema: "speed_reading");
    }
}
