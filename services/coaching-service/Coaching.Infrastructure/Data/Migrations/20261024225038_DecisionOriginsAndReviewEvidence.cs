using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DecisionOriginsAndReviewEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_weekly_reflection_reviews_ReflectionId_TeacherId_Reflection~",
                schema: "coaching",
                table: "weekly_reflection_reviews");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceFingerprint",
                schema: "coaching",
                table: "weekly_reflection_reviews",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceSnapshotJson",
                schema: "coaching",
                table: "weekly_reflection_reviews",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousDecisionId",
                schema: "coaching",
                table: "weekly_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousDecisionSnapshotJson",
                schema: "coaching",
                table: "weekly_decisions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceReflectionId",
                schema: "coaching",
                table: "weekly_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReflectionSnapshotJson",
                schema: "coaching",
                table: "weekly_decisions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceReflectionVersion",
                schema: "coaching",
                table: "weekly_decisions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_reflection_reviews_ReflectionId_TeacherId_Reflection~",
                schema: "coaching",
                table: "weekly_reflection_reviews",
                columns: new[] { "ReflectionId", "TeacherId", "ReflectionVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_decisions_PreviousDecisionId",
                schema: "coaching",
                table: "weekly_decisions",
                column: "PreviousDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_decisions_SourceReflectionId",
                schema: "coaching",
                table: "weekly_decisions",
                column: "SourceReflectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.weekly_decisions WHERE "SourceReflectionId" IS NOT NULL OR "SourceReflectionVersion" IS NOT NULL OR "SourceReflectionSnapshotJson" IS NOT NULL OR "PreviousDecisionId" IS NOT NULL OR "PreviousDecisionSnapshotJson" IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM coaching.weekly_reflection_reviews WHERE "EvidenceFingerprint" IS NOT NULL OR "EvidenceSnapshotJson" IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM coaching.weekly_reflection_reviews GROUP BY "ReflectionId","TeacherId","ReflectionVersion" HAVING count(*)>1)
                  THEN RAISE EXCEPTION 'Decision origins or repeated review evidence exist; preserve these records before downgrade.'; END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_weekly_reflection_reviews_ReflectionId_TeacherId_Reflection~",
                schema: "coaching",
                table: "weekly_reflection_reviews");

            migrationBuilder.DropIndex(
                name: "IX_weekly_decisions_PreviousDecisionId",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropIndex(
                name: "IX_weekly_decisions_SourceReflectionId",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "EvidenceFingerprint",
                schema: "coaching",
                table: "weekly_reflection_reviews");

            migrationBuilder.DropColumn(
                name: "EvidenceSnapshotJson",
                schema: "coaching",
                table: "weekly_reflection_reviews");

            migrationBuilder.DropColumn(
                name: "PreviousDecisionId",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "PreviousDecisionSnapshotJson",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "SourceReflectionId",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "SourceReflectionSnapshotJson",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.DropColumn(
                name: "SourceReflectionVersion",
                schema: "coaching",
                table: "weekly_decisions");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_reflection_reviews_ReflectionId_TeacherId_Reflection~",
                schema: "coaching",
                table: "weekly_reflection_reviews",
                columns: new[] { "ReflectionId", "TeacherId", "ReflectionVersion" },
                unique: true);
        }
    }
}
