using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSubjectUserLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubjectUserId",
                table: "SupportRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectUserId",
                table: "EmailDeliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportRequests_SubjectUserId",
                table: "SupportRequests",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_SubjectUserId",
                table: "EmailDeliveries",
                column: "SubjectUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportRequests_SubjectUserId",
                table: "SupportRequests");

            migrationBuilder.DropIndex(
                name: "IX_EmailDeliveries_SubjectUserId",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "SubjectUserId",
                table: "SupportRequests");

            migrationBuilder.DropColumn(
                name: "SubjectUserId",
                table: "EmailDeliveries");
        }
    }
}
