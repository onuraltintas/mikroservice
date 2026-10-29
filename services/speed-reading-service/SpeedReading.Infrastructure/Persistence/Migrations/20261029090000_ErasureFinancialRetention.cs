using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Review H2b: an erasure receipt remembers, for one year, whose paid records were kept so a daily job can then
    /// anonymise them. Only new nullable columns and an index; Down drops them.
    /// </summary>
    public partial class ErasureFinancialRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinancialAnonymizedAt",
                schema: "speed_reading",
                table: "privacy_erasure_executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinancialRetentionUntil",
                schema: "speed_reading",
                table: "privacy_erasure_executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectUserId",
                schema: "speed_reading",
                table: "privacy_erasure_executions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_privacy_erasure_executions_FinancialRetentionUntil",
                schema: "speed_reading",
                table: "privacy_erasure_executions",
                column: "FinancialRetentionUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_privacy_erasure_executions_FinancialRetentionUntil",
                schema: "speed_reading",
                table: "privacy_erasure_executions");
            migrationBuilder.DropColumn(name: "FinancialAnonymizedAt", schema: "speed_reading", table: "privacy_erasure_executions");
            migrationBuilder.DropColumn(name: "FinancialRetentionUntil", schema: "speed_reading", table: "privacy_erasure_executions");
            migrationBuilder.DropColumn(name: "SubjectUserId", schema: "speed_reading", table: "privacy_erasure_executions");
        }
    }
}
