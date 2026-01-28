using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Migrations
{
    public partial class AddEmailTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_TemplateName",
                table: "EmailTemplates",
                column: "TemplateName",
                unique: true);

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Body", "Category", "CreatedAt", "IsActive", "Subject", "TemplateName", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), @"
                <div style='font-family:""Segoe UI"",Helvetica,Arial,sans-serif;background-color:#f3f4f6;padding:40px 0'>
                  <div style='max-width:600px;margin:0 auto;background-color:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 6px rgba(0,0,0,0.05)'>
                    <div style='background:linear-gradient(135deg,#2563eb 0%,#1d4ed8 100%);padding:40px 20px;text-align:center'>
                      <h1 style='color:#ffffff;margin:0;font-size:28px;font-weight:700'>EduPlatform</h1>
                    </div>
                    <div style='padding:40px 30px;color:#374151'>
                      <h2 style='color:#111827;font-size:22px;margin-top:0'>Merhaba {{FirstName}}! 👋</h2>
                      <p style='font-size:16px;line-height:24px'>Aramıza hoş geldiniz! Hesabınız <strong>{{Role}}</strong> yetkisiyle başarıyla oluşturuldu.</p>
                      
                      <div style='background-color:#f0f9ff;border-left:4px solid #0ea5e9;padding:15px;margin:25px 0;border-radius:4px'>
                        <div style='font-size:12px;text-transform:uppercase;color:#0369a1;font-weight:700'>Geçici Şifreniz</div>
                        <div style='font-size:20px;color:#0c4a6e;font-weight:700;margin-top:5px;font-family:monospace'>{{TemporaryPassword}}</div>
                      </div>

                      <p style='font-size:16px;line-height:24px'>Güvenliğiniz için lütfen ilk girişinizde şifrenizi değiştirin.</p>

                      <div style='text-align:center;margin-top:35px'>
                        <a href='http://localhost:4200/auth/login' style='background-color:#2563eb;color:#ffffff;padding:14px 32px;text-decoration:none;border-radius:8px;font-weight:600;display:inline-block'>Giriş Yap</a>
                      </div>
                    </div>
                    <div style='background-color:#f9fafb;padding:20px;text-align:center;font-size:12px;color:#9ca3af;border-top:1px solid #e5e7eb'>
                      &copy; 2026 EduPlatform. Tüm hakları saklıdır.
                    </div>
                  </div>
                </div>", "Auth", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EduPlatform'a Hoş Geldiniz! 🚀", "Auth_Welcome", null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), @"<div style='font-family: Arial;'><h2>Yeni Kurs Bildirimi</h2><p>Merhaba {{FirstName}},</p><p>Koçunuz size yeni bir kurs atadı: <strong>{{CourseName}}</strong>.</p></div>", "Coaching", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Yeni Bir Kurs Atandı: {{CourseName}}", "Coaching_NewCourse", null }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailTemplates");
        }
    }
}
