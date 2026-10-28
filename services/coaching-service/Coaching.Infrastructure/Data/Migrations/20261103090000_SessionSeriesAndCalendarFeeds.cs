using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap O6: a series id on sessions (recurring sessions are separate rows sharing it) and the private calendar
    /// links (only token hashes). Existing sessions belong to no series. Down refuses while series or links exist.
    /// </summary>
    public partial class SessionSeriesAndCalendarFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "series_id",
                schema: "coaching",
                table: "coaching_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "calendar_feeds",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_feeds", x => x.Id);
                    table.CheckConstraint("ck_coaching_calendar_feed_role", "\"Role\" IN ('Teacher', 'Student')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_coaching_sessions_series_scheduled",
                schema: "coaching",
                table: "coaching_sessions",
                columns: new[] { "series_id", "scheduled_date" },
                filter: "series_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feeds_TokenHash",
                schema: "coaching",
                table: "calendar_feeds",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feeds_UserId_Role",
                schema: "coaching",
                table: "calendar_feeds",
                columns: new[] { "UserId", "Role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM coaching.calendar_feeds) OR EXISTS (SELECT 1 FROM coaching.coaching_sessions WHERE series_id IS NOT NULL)
                  THEN RAISE EXCEPTION 'Recurring sessions or calendar links exist; export them before downgrade.'; END IF;
                END $$;
                """);
            migrationBuilder.DropTable(name: "calendar_feeds", schema: "coaching");
            migrationBuilder.DropIndex(name: "ix_coaching_sessions_series_scheduled", schema: "coaching", table: "coaching_sessions");
            migrationBuilder.DropColumn(name: "series_id", schema: "coaching", table: "coaching_sessions");
        }
    }
}
