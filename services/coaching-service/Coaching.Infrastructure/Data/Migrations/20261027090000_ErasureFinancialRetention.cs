using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ErasureFinancialRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinancialAnonymizedAt",
                schema: "coaching",
                table: "CoachingErasureExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinancialRetentionUntil",
                schema: "coaching",
                table: "CoachingErasureExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetainedFinancialRecordCount",
                schema: "coaching",
                table: "CoachingErasureExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectUserId",
                schema: "coaching",
                table: "CoachingErasureExecutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetainedFinancialRecordCount",
                schema: "coaching",
                table: "CoachingErasureAssessments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CoachingErasureExecutions_FinancialRetentionUntil",
                schema: "coaching",
                table: "CoachingErasureExecutions",
                column: "FinancialRetentionUntil",
                filter: "\"FinancialAnonymizedAt\" IS NULL AND \"FinancialRetentionUntil\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CoachingErasureExecutions_FinancialRetentionUntil",
                schema: "coaching",
                table: "CoachingErasureExecutions");

            migrationBuilder.DropColumn(
                name: "FinancialAnonymizedAt",
                schema: "coaching",
                table: "CoachingErasureExecutions");

            migrationBuilder.DropColumn(
                name: "FinancialRetentionUntil",
                schema: "coaching",
                table: "CoachingErasureExecutions");

            migrationBuilder.DropColumn(
                name: "RetainedFinancialRecordCount",
                schema: "coaching",
                table: "CoachingErasureExecutions");

            migrationBuilder.DropColumn(
                name: "SubjectUserId",
                schema: "coaching",
                table: "CoachingErasureExecutions");

            migrationBuilder.DropColumn(
                name: "RetainedFinancialRecordCount",
                schema: "coaching",
                table: "CoachingErasureAssessments");
        }
    }
}
