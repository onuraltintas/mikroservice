using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>Roadmap O5: yearly score models (coefficients and score-to-rank tables) for LGS and YKS estimates. New table only.</summary>
    public partial class ScoreModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "score_models",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ScoreType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BaseScore = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    DiplomaWeight = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    CoefficientsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RanksJson = table.Column<string>(type: "jsonb", nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_models", x => x.Id);
                    table.CheckConstraint("ck_score_model_type", "\"ScoreType\" IN ('LGS', 'TYT', 'SAY', 'EA', 'SOZ', 'DIL')");
                    table.CheckConstraint("ck_score_model_year", "\"Year\" BETWEEN 2018 AND 2100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_score_models_ScoreType_Year",
                schema: "coaching",
                table: "score_models",
                columns: new[] { "ScoreType", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "score_models", schema: "coaching");
        }
    }
}
