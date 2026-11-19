using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Site.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MaxkampusSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_entries_Group_Kind_SortOrder",
                schema: "site",
                table: "entries");

            migrationBuilder.DropIndex(
                name: "IX_entries_Kind_Group_Slug",
                schema: "site",
                table: "entries");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                schema: "site",
                table: "entries",
                type: "character varying(40000)",
                maxLength: 40000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                schema: "site",
                table: "entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                schema: "site",
                table: "entries",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "site",
                table: "entries",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndsAt",
                schema: "site",
                table: "entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Site",
                schema: "site",
                table: "entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "eduivme");

            migrationBuilder.CreateTable(
                name: "contact_messages",
                schema: "site",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Site = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ConsentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnsweredByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subscribers",
                schema: "site",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Site = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnsubscribedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscribers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entries_Site_Group_Kind_SortOrder",
                schema: "site",
                table: "entries",
                columns: new[] { "Site", "Group", "Kind", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_entries_Site_Kind_Group_Slug",
                schema: "site",
                table: "entries",
                columns: new[] { "Site", "Kind", "Group", "Slug" },
                unique: true,
                filter: "\"Slug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contact_messages_Site_CreatedAt",
                schema: "site",
                table: "contact_messages",
                columns: new[] { "Site", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_subscribers_Site_Email",
                schema: "site",
                table: "subscribers",
                columns: new[] { "Site", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscribers_Site_Status",
                schema: "site",
                table: "subscribers",
                columns: new[] { "Site", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_subscribers_Token",
                schema: "site",
                table: "subscribers",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_messages",
                schema: "site");

            migrationBuilder.DropTable(
                name: "subscribers",
                schema: "site");

            migrationBuilder.DropIndex(
                name: "IX_entries_Site_Group_Kind_SortOrder",
                schema: "site",
                table: "entries");

            migrationBuilder.DropIndex(
                name: "IX_entries_Site_Kind_Group_Slug",
                schema: "site",
                table: "entries");

            migrationBuilder.DropColumn(
                name: "Audience",
                schema: "site",
                table: "entries");

            migrationBuilder.DropColumn(
                name: "Author",
                schema: "site",
                table: "entries");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "site",
                table: "entries");

            migrationBuilder.DropColumn(
                name: "EndsAt",
                schema: "site",
                table: "entries");

            migrationBuilder.DropColumn(
                name: "Site",
                schema: "site",
                table: "entries");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                schema: "site",
                table: "entries",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40000)",
                oldMaxLength: 40000);

            migrationBuilder.CreateIndex(
                name: "IX_entries_Group_Kind_SortOrder",
                schema: "site",
                table: "entries",
                columns: new[] { "Group", "Kind", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_entries_Kind_Group_Slug",
                schema: "site",
                table: "entries",
                columns: new[] { "Kind", "Group", "Slug" },
                unique: true,
                filter: "\"Slug\" IS NOT NULL");
        }
    }
}
