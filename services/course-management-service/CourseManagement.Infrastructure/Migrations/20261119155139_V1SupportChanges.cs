using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V1SupportChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "After",
                table: "SupportAccessLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Before",
                table: "SupportAccessLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsChange",
                table: "SupportAccessLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                table: "SupportAccessLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsChanges",
                table: "SupportAccessGrants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndNoticeSentAt",
                table: "SupportAccessGrants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestNoticeSentAt",
                table: "SupportAccessGrants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartNoticeSentAt",
                table: "SupportAccessGrants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Delivery",
                table: "CourseEmails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveryCheckedAt",
                table: "CourseEmails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedRecipientCount",
                table: "CourseEmails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CourseEmails_Status_Delivery_ProcessedAt",
                table: "CourseEmails",
                columns: new[] { "Status", "Delivery", "ProcessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CourseEmails_Status_Delivery_ProcessedAt",
                table: "CourseEmails");

            migrationBuilder.DropColumn(
                name: "After",
                table: "SupportAccessLogs");

            migrationBuilder.DropColumn(
                name: "Before",
                table: "SupportAccessLogs");

            migrationBuilder.DropColumn(
                name: "IsChange",
                table: "SupportAccessLogs");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "SupportAccessLogs");

            migrationBuilder.DropColumn(
                name: "AllowsChanges",
                table: "SupportAccessGrants");

            migrationBuilder.DropColumn(
                name: "EndNoticeSentAt",
                table: "SupportAccessGrants");

            migrationBuilder.DropColumn(
                name: "RequestNoticeSentAt",
                table: "SupportAccessGrants");

            migrationBuilder.DropColumn(
                name: "StartNoticeSentAt",
                table: "SupportAccessGrants");

            migrationBuilder.DropColumn(
                name: "Delivery",
                table: "CourseEmails");

            migrationBuilder.DropColumn(
                name: "DeliveryCheckedAt",
                table: "CourseEmails");

            migrationBuilder.DropColumn(
                name: "FailedRecipientCount",
                table: "CourseEmails");
        }
    }
}
