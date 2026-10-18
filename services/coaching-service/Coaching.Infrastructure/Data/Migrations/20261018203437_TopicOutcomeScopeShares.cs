using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TopicOutcomeScopeShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_catalog_topic_outcome_shares",
                schema: "coaching",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningMinutes = table.Column<int>(type: "integer", nullable: false),
                    PracticeMinutes = table.Column<int>(type: "integer", nullable: false),
                    CheckMinutes = table.Column<int>(type: "integer", nullable: false),
                    ScopeDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_catalog_topic_outcome_shares", x => new { x.TopicId, x.OutcomeId, x.EstimateId });
                    table.CheckConstraint("ck_topic_outcome_share_minutes", "\"LearningMinutes\" >= 0 AND \"PracticeMinutes\" >= 0 AND \"CheckMinutes\" >= 0 AND (\"LearningMinutes\"::bigint + \"PracticeMinutes\" + \"CheckMinutes\") BETWEEN 1 AND 1440");
                    table.ForeignKey(
                        name: "FK_study_catalog_topic_outcome_shares_outcome_duration_estimat~",
                        columns: x => new { x.EstimateId, x.OutcomeId },
                        principalSchema: "coaching",
                        principalTable: "outcome_duration_estimates",
                        principalColumns: new[] { "Id", "OutcomeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_catalog_topic_outcome_shares_study_catalog_topic_outc~",
                        columns: x => new { x.TopicId, x.OutcomeId },
                        principalSchema: "coaching",
                        principalTable: "study_catalog_topic_outcomes",
                        principalColumns: new[] { "TopicId", "OutcomeId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_catalog_topic_outcome_shares_EstimateId_OutcomeId",
                schema: "coaching",
                table: "study_catalog_topic_outcome_shares",
                columns: new[] { "EstimateId", "OutcomeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_catalog_topic_outcome_shares",
                schema: "coaching");
        }
    }
}
