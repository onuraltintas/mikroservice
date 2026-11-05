using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Paket B (user decision 05.11.2026): score estimates for every exam. score_models and score_model_versions accept the
    /// MEB-AGS score types (P1, P2_&lt;field&gt;, P2_16, P2_17) and keep the model's own ± range (UncertaintyShare: share of the
    /// distance to the top score; UncertaintyCap: optional cap in points). Existing rows get 0 / null, so YKS estimates are
    /// unchanged. Down refuses, changing nothing, while a MEB-AGS model or version exists: the previous release cannot read it.
    /// </summary>
    public partial class ScoreModelsAllExams : Migration
    {
        private const string Old = "\"ScoreType\" IN ('LGS', 'TYT', 'SAY', 'EA', 'SOZ', 'DIL')";
        private const string New = "\"ScoreType\" IN ('LGS', 'TYT', 'SAY', 'EA', 'SOZ', 'DIL', 'P1', 'P2_TUR', 'P2_IMAT', 'P2_FEN', 'P2_SOS', 'P2_TDE', 'P2_TAR', 'P2_COG', 'P2_MAT', 'P2_FIZ', 'P2_KIM', 'P2_BIO', 'P2_REH', 'P2_SINIF', 'P2_OKUL', 'P2_BED', 'P2_OZEL', 'P2_16', 'P2_17')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "score_models", "score_model_versions" })
            {
                migrationBuilder.AddColumn<decimal>(name: "UncertaintyShare", schema: "coaching", table: table, type: "numeric(6,4)", precision: 6, scale: 4,
                    nullable: false, defaultValue: 0m);
                migrationBuilder.AddColumn<decimal>(name: "UncertaintyCap", schema: "coaching", table: table, type: "numeric(8,3)", precision: 8, scale: 3,
                    nullable: true);
            }
            migrationBuilder.DropCheckConstraint(name: "ck_score_model_type", schema: "coaching", table: "score_models");
            migrationBuilder.AddCheckConstraint(name: "ck_score_model_type", schema: "coaching", table: "score_models", sql: New);
            migrationBuilder.DropCheckConstraint(name: "ck_score_model_version_type", schema: "coaching", table: "score_model_versions");
            migrationBuilder.AddCheckConstraint(name: "ck_score_model_version_type", schema: "coaching", table: "score_model_versions", sql: New);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE models bigint; versions bigint;
                BEGIN
                  SELECT count(*) INTO models FROM coaching.score_models WHERE "ScoreType" NOT IN ('LGS', 'TYT', 'SAY', 'EA', 'SOZ', 'DIL');
                  SELECT count(*) INTO versions FROM coaching.score_model_versions WHERE "ScoreType" NOT IN ('LGS', 'TYT', 'SAY', 'EA', 'SOZ', 'DIL');
                  IF models + versions > 0 THEN
                    RAISE EXCEPTION 'ScoreModelsAllExams down refused: % MEB-AGS score model(s) and % version(s) exist. Nothing changed.', models, versions;
                  END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(name: "ck_score_model_type", schema: "coaching", table: "score_models");
            migrationBuilder.AddCheckConstraint(name: "ck_score_model_type", schema: "coaching", table: "score_models", sql: Old);
            migrationBuilder.DropCheckConstraint(name: "ck_score_model_version_type", schema: "coaching", table: "score_model_versions");
            migrationBuilder.AddCheckConstraint(name: "ck_score_model_version_type", schema: "coaching", table: "score_model_versions", sql: Old);
            foreach (var table in new[] { "score_models", "score_model_versions" })
            {
                migrationBuilder.DropColumn(name: "UncertaintyShare", schema: "coaching", table: table);
                migrationBuilder.DropColumn(name: "UncertaintyCap", schema: "coaching", table: table);
            }
        }
    }
}
