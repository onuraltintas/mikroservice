using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSubjectRequestScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataSubjectRequests_RequesterUserId_RequestType",
                schema: "identity",
                table: "DataSubjectRequests");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                schema: "identity",
                table: "DataSubjectRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Account");

            migrationBuilder.CreateIndex(
                name: "IX_DataSubjectRequests_RequesterUserId_RequestType_Scope",
                schema: "identity",
                table: "DataSubjectRequests",
                columns: new[] { "RequesterUserId", "RequestType", "Scope" },
                unique: true,
                filter: "\"Status\" IN ('Submitted', 'IdentityVerified', 'Approved', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataSubjectRequests_RequesterUserId_RequestType_Scope",
                schema: "identity",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "identity",
                table: "DataSubjectRequests");

            migrationBuilder.CreateIndex(
                name: "IX_DataSubjectRequests_RequesterUserId_RequestType",
                schema: "identity",
                table: "DataSubjectRequests",
                columns: new[] { "RequesterUserId", "RequestType" },
                unique: true,
                filter: "\"Status\" IN ('Submitted', 'IdentityVerified', 'Approved', 'Processing')");
        }
    }
}
