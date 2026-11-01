using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// V6a (parent panel, user decision 01.11.2026): drops the two legacy parent fields.
    /// <list type="bullet">
    /// <item><c>parent_profiles.NotificationPreferences</c>: the weekly-summary switch is now only the notification-service
    /// category ParentSummary. Every non-default value is kept in <c>identity.legacy_parent_notification_preferences_20261101</c>
    /// first, so the operator can still find the parents who had turned the summary off
    /// (content-packs/parent-weekly-summary-mute/v1) and Down can put the values back.</item>
    /// <item><c>student_profiles.ParentId</c> (FK, index): the verified links are ParentStudentRelationship rows, which are
    /// untouched. The non-null pairs go to <c>identity.legacy_student_parent_ids_20261101</c> first, for Down.</item>
    /// </list>
    /// Down re-adds both columns (the preferences with their old default), restores the kept values and drops the backups.
    /// </summary>
    public partial class RemoveLegacyParentFields : Migration
    {
        private const string OldPreferencesDefault =
            """{"dailyReport": false, "weeklyProgress": true, "examResults": true, "lowActivityAlert": true}""";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                CREATE TABLE identity.legacy_parent_notification_preferences_20261101 (
                    parent_user_id uuid PRIMARY KEY,
                    notification_preferences jsonb NOT NULL,
                    backed_up_at timestamp with time zone NOT NULL DEFAULT now());
                INSERT INTO identity.legacy_parent_notification_preferences_20261101 (parent_user_id, notification_preferences)
                SELECT "UserId", "NotificationPreferences" FROM identity.parent_profiles
                WHERE "NotificationPreferences" IS DISTINCT FROM '{OldPreferencesDefault}'::jsonb;

                CREATE TABLE identity.legacy_student_parent_ids_20261101 (
                    student_user_id uuid PRIMARY KEY,
                    parent_id uuid NOT NULL,
                    backed_up_at timestamp with time zone NOT NULL DEFAULT now());
                INSERT INTO identity.legacy_student_parent_ids_20261101 (student_user_id, parent_id)
                SELECT "UserId", "ParentId" FROM identity.student_profiles WHERE "ParentId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_student_profiles_users_ParentId",
                schema: "identity",
                table: "student_profiles");

            migrationBuilder.DropIndex(
                name: "IX_student_profiles_ParentId",
                schema: "identity",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "identity",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "NotificationPreferences",
                schema: "identity",
                table: "parent_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationPreferences",
                schema: "identity",
                table: "parent_profiles",
                type: "jsonb",
                nullable: false,
                defaultValue: OldPreferencesDefault);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                schema: "identity",
                table: "student_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_ParentId",
                schema: "identity",
                table: "student_profiles",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_student_profiles_users_ParentId",
                schema: "identity",
                table: "student_profiles",
                column: "ParentId",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // The ParentId restore skips a parent account erased after the Up (the old FK would have set it null then).
            migrationBuilder.Sql("""
                UPDATE identity.parent_profiles AS profile SET "NotificationPreferences" = backup.notification_preferences
                FROM identity.legacy_parent_notification_preferences_20261101 AS backup
                WHERE profile."UserId" = backup.parent_user_id;
                DROP TABLE identity.legacy_parent_notification_preferences_20261101;

                UPDATE identity.student_profiles AS profile SET "ParentId" = backup.parent_id
                FROM identity.legacy_student_parent_ids_20261101 AS backup
                WHERE profile."UserId" = backup.student_user_id
                    AND EXISTS (SELECT 1 FROM identity.users AS parent WHERE parent."Id" = backup.parent_id);
                DROP TABLE identity.legacy_student_parent_ids_20261101;
                """);
        }
    }
}
