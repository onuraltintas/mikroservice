using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// KM7 (01.11.2026): catalog administration.
    /// - study_catalog_topic_outcomes."IsRejected": a rejected topic–outcome link stays for audit, but it no longer counts as a
    ///   link for planning (planning reads approved links only) and no longer blocks deleting the topic. A link is pending,
    ///   approved or rejected, never approved and rejected at once.
    /// - score_model_versions: every applied, restored or deleted score model is kept so the administrator can see the upload
    ///   history and restore an earlier version. The existing models are copied in as their first version.
    /// Down refuses while a rejected link exists: the old schema would read it as a pending link again.
    /// </summary>
    public partial class CatalogAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                schema: "coaching",
                table: "study_catalog_topic_outcomes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_topic_outcome_review_state",
                schema: "coaching",
                table: "study_catalog_topic_outcomes",
                sql: "NOT (\"IsApproved\" AND \"IsRejected\")");

            migrationBuilder.CreateTable(
                name: "score_model_versions",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ScoreType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BaseScore = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    DiplomaWeight = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    CoefficientsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RanksJson = table.Column<string>(type: "jsonb", nullable: false),
                    RankRows = table.Column<int>(type: "integer", nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RestoredFromId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_model_versions", x => x.Id);
                    table.CheckConstraint("ck_score_model_version_action", "\"Action\" IN ('Upload', 'Restore', 'Delete')");
                    table.CheckConstraint("ck_score_model_version_type", "\"ScoreType\" IN ('LGS', 'TYT', 'SAY', 'EA', 'SOZ', 'DIL')");
                    table.CheckConstraint("ck_score_model_version_year", "\"Year\" BETWEEN 2018 AND 2100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_score_model_versions_UploadId",
                schema: "coaching",
                table: "score_model_versions",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_score_model_versions_Year_ScoreType_CreatedAt",
                schema: "coaching",
                table: "score_model_versions",
                columns: new[] { "Year", "ScoreType", "CreatedAt" });

            // One upload per (year, import time): the models of one file share the upload id.
            migrationBuilder.Sql("""
                INSERT INTO coaching.score_model_versions ("Id","UploadId","Year","ScoreType","Action","BaseScore","DiplomaWeight",
                    "CoefficientsJson","RanksJson","RankRows","FileSha256","Reason","ActorUserId","CreatedAt","RestoredFromId")
                SELECT gen_random_uuid(),
                       md5('score-model-upload:' || m."Year" || ':' || m."ImportedAt"::text)::uuid,
                       m."Year", m."ScoreType", 'Upload', m."BaseScore", m."DiplomaWeight", m."CoefficientsJson", m."RanksJson",
                       jsonb_array_length(m."RanksJson"), NULL, NULL, m."ImportedByUserId", m."ImportedAt", NULL
                FROM coaching.score_models AS m;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.study_catalog_topic_outcomes WHERE "IsRejected") THEN
                        RAISE EXCEPTION 'Reddedilmiş konu–kazanım bağlantıları var; eski şema bunları bekleyen bağlantı sayar. Önce bağlantıları silin.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "score_model_versions",
                schema: "coaching");

            migrationBuilder.DropCheckConstraint(
                name: "ck_topic_outcome_review_state",
                schema: "coaching",
                table: "study_catalog_topic_outcomes");

            migrationBuilder.DropColumn(
                name: "IsRejected",
                schema: "coaching",
                table: "study_catalog_topic_outcomes");
        }
    }
}
