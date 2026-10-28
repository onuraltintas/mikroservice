using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Roadmap O3: teacher–student and teacher–parent conversations, their messages and files, and the record of
    /// institution reviews. New tables only; existing data is not touched.
    /// </summary>
    public partial class CoachingMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coaching_conversations",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TeacherReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CounterpartReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coaching_conversations", x => x.Id);
                    table.CheckConstraint("ck_coaching_conversation_kind", "(\"Kind\" = 'TeacherStudent' AND \"CounterpartUserId\" = \"StudentUserId\") OR (\"Kind\" = 'TeacherParent' AND \"CounterpartUserId\" <> \"StudentUserId\")");
                    table.CheckConstraint("ck_coaching_conversation_participants", "\"TeacherUserId\" <> \"CounterpartUserId\"");
                });

            migrationBuilder.CreateTable(
                name: "coaching_conversation_accesses",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coaching_conversation_accesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coaching_conversation_accesses_coaching_conversations_Conve~",
                        column: x => x.ConversationId,
                        principalSchema: "coaching",
                        principalTable: "coaching_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "coaching_messages",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coaching_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coaching_messages_coaching_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "coaching",
                        principalTable: "coaching_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "coaching_message_attachments",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploaderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coaching_message_attachments", x => x.Id);
                    table.CheckConstraint("ck_coaching_message_attachment_size", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 10485760");
                    table.ForeignKey(
                        name: "FK_coaching_message_attachments_coaching_conversations_Convers~",
                        column: x => x.ConversationId,
                        principalSchema: "coaching",
                        principalTable: "coaching_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_coaching_message_attachments_coaching_messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "coaching",
                        principalTable: "coaching_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_coaching_conversation_accesses_ConversationId_AccessedAt",
                schema: "coaching",
                table: "coaching_conversation_accesses",
                columns: new[] { "ConversationId", "AccessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_coaching_conversation_accesses_ViewerUserId",
                schema: "coaching",
                table: "coaching_conversation_accesses",
                column: "ViewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_coaching_conversations_CounterpartUserId_LastMessageAt",
                schema: "coaching",
                table: "coaching_conversations",
                columns: new[] { "CounterpartUserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_coaching_conversations_InstitutionId_LastMessageAt",
                schema: "coaching",
                table: "coaching_conversations",
                columns: new[] { "InstitutionId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_coaching_conversations_LastMessageAt",
                schema: "coaching",
                table: "coaching_conversations",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_coaching_conversations_StudentUserId",
                schema: "coaching",
                table: "coaching_conversations",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_coaching_conversations_TeacherUserId_CounterpartUserId_Stud~",
                schema: "coaching",
                table: "coaching_conversations",
                columns: new[] { "TeacherUserId", "CounterpartUserId", "StudentUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coaching_conversations_TeacherUserId_LastMessageAt",
                schema: "coaching",
                table: "coaching_conversations",
                columns: new[] { "TeacherUserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_coaching_message_attachments_ConversationId_MessageId",
                schema: "coaching",
                table: "coaching_message_attachments",
                columns: new[] { "ConversationId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_coaching_message_attachments_MessageId_CreatedAt",
                schema: "coaching",
                table: "coaching_message_attachments",
                columns: new[] { "MessageId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_coaching_message_attachments_StorageKey",
                schema: "coaching",
                table: "coaching_message_attachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coaching_messages_ConversationId_SentAt",
                schema: "coaching",
                table: "coaching_messages",
                columns: new[] { "ConversationId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "coaching_conversation_accesses", schema: "coaching");
            migrationBuilder.DropTable(name: "coaching_message_attachments", schema: "coaching");
            migrationBuilder.DropTable(name: "coaching_messages", schema: "coaching");
            migrationBuilder.DropTable(name: "coaching_conversations", schema: "coaching");
        }
    }
}
