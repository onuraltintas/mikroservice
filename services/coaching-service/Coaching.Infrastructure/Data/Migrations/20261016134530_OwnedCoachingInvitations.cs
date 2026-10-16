using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnedCoachingInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsInstitutionInvitation = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invitations_InviterUserId_Status",
                schema: "coaching",
                table: "invitations",
                columns: new[] { "InviterUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_invitations_RecipientEmail_Role_InstitutionId_TeacherUserId~",
                schema: "coaching",
                table: "invitations",
                columns: new[] { "RecipientEmail", "Role", "InstitutionId", "TeacherUserId", "IsInstitutionInvitation" },
                unique: true,
                filter: "\"Status\" = 0")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_invitations_RecipientEmail_Status_ExpiresAt",
                schema: "coaching",
                table: "invitations",
                columns: new[] { "RecipientEmail", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitations",
                schema: "coaching");
        }
    }
}
