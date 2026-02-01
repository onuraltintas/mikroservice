using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.CreateTable(
                name: "SupportRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    AdminNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportRequests");

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Body", "Category", "CreatedAt", "IsActive", "Subject", "TemplateName", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "<div style='font-family: Arial;'><h1>EduPlatform</h1><p>Merhaba <strong>{{FirstName}} {{LastName}}</strong>,</p><p>Hesabınız <strong>{{Role}}</strong> rolüyle oluşturuldu.</p><p>Şifreniz: <b>{{TemporaryPassword}}</b></p><br><a href='http://localhost:4200/auth/login'>Giriş Yap</a></div>", "Auth", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Hoş Geldiniz, {{FirstName}}! 🚀", "Auth_Welcome", null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "<div style='font-family: Arial;'><h2>Yeni Kurs Bildirimi</h2><p>Merhaba {{FirstName}},</p><p>Koçunuz size yeni bir kurs atadı: <strong>{{CourseName}}</strong>.</p></div>", "Coaching", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Yeni Bir Kurs Atandı: {{CourseName}}", "Coaching_NewCourse", null }
                });
        }
    }
}
