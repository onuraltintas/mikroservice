using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingCmsEditorialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Author",
                schema: "coaching",
                table: "cms_entries",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                schema: "coaching",
                table: "cms_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Eyebrow",
                schema: "coaching",
                table: "cms_entries",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "coaching",
                table: "cms_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkLabel",
                schema: "coaching",
                table: "cms_entries",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkUrl",
                schema: "coaching",
                table: "cms_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                schema: "coaching",
                table: "cms_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryLinkLabel",
                schema: "coaching",
                table: "cms_entries",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryLinkUrl",
                schema: "coaching",
                table: "cms_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            var systemActor = new Guid("d1820000-0000-4000-8000-000000000099");
            var createdAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            void Block(Guid id, string group, string title, string slug, string summary, string content, int order,
                string eyebrow = null, string linkLabel = null, string linkUrl = null)
            {
                migrationBuilder.InsertData(
                    table: "cms_entries",
                    schema: "coaching",
                    columns: new[] { "Id", "Kind", "Group", "Title", "Slug", "Summary", "Content", "SeoTitle", "SeoDescription", "TagsJson", "IsPublished", "ScheduledPublishAt", "SortOrder", "ViewCount", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "Version", "Eyebrow", "LinkLabel", "LinkUrl" },
                    values: new object[] { id, "Block", group, title, slug, summary, content, null, null, "[]", true, null, order, 0, systemActor, createdAt, null, null, 1, eyebrow, linkLabel, linkUrl });
            }
            void Navigation(Guid id, string menu, string label, string url, int order)
            {
                migrationBuilder.InsertData(
                    table: "cms_navigation_items",
                    schema: "coaching",
                    columns: new[] { "Id", "Menu", "Label", "Url", "Icon", "SortOrder", "IsVisible", "OpenInNewTab", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt" },
                    values: new object[] { id, menu, label, url, null, order, true, false, systemActor, createdAt, null, null });
            }

            // Preserve the existing hero text while making all editorial links editable.
            migrationBuilder.Sql("""
                UPDATE coaching.cms_entries
                SET "Eyebrow" = 'Eduİvme · Öğrenci Koçluğu',
                    "LinkLabel" = 'Koçluk alanına giriş yap',
                    "LinkUrl" = '/auth/login?returnUrl=%2Fcoaching-portal',
                    "SecondaryLinkLabel" = 'Nasıl çalışır?',
                    "SecondaryLinkUrl" = '/coaching/pages/nasil-calisir'
                WHERE "Id" = 'd1810000-0000-4000-8000-000000000001'
                  AND "UpdatedAt" IS NULL AND "Version" = 1;
                """);

            Block(new Guid("d1820000-0000-4000-8000-000000000001"), "HomeAreaHeading",
                "Koçluk sürecinde herkes için net bir alan", "home-area-heading",
                "Öğrenci, öğretmen ve kurumlar için sorumlulukları ve ilerlemeyi anlaşılır hale getirin.", "", 0,
                "Tek platform, farklı sorumluluklar", "Nasıl çalışır?", "/coaching/pages/nasil-calisir");
            Block(new Guid("d1820000-0000-4000-8000-000000000002"), "HomeProcessHeading",
                "Nasıl çalışır?", "home-process-heading",
                "Koçluk; hedef belirleme, planlı çalışma ve düzenli değerlendirme adımlarını bir araya getirir.", "", 0,
                "Sade ve takip edilebilir");
            Block(new Guid("d1820000-0000-4000-8000-000000000003"), "HomePlansHeading",
                "Size uygun planı bulun", "home-plans-heading",
                "Öğrenci ve kurumlar için yayınlanan Koçluk erişim seçeneklerini karşılaştırın.", "", 0,
                "Koçluk için erişim seçenekleri");
            Block(new Guid("d1820000-0000-4000-8000-000000000004"), "HomeBlogHeading",
                "Daha iyi bir çalışma rutini için", "home-blog-heading",
                "Hedef, çalışma planı ve koçluk görüşmeleri için uygulamaya dönük rehberler.", "", 0,
                "Koçluk kaynakları", "Tüm yazılar", "/coaching/blog");
            Block(new Guid("d1820000-0000-4000-8000-000000000005"), "HomeClosingCta",
                "Koçluk alanında hedeflerini düzenle.", "home-closing-cta",
                "Hedeflerini, çalışmalarını ve ilerlemeni tek bir Koçluk alanında takip et.", "", 0,
                "İlk adımını bugün at", "Giriş yap veya kayıt ol", "/auth/login?returnUrl=%2Fcoaching-portal");
            Block(new Guid("d1820000-0000-4000-8000-000000000006"), "HomeFaqHeading",
                "Sıkça sorulan sorular", "home-faq-heading",
                "Koçluk hesapları, paneller, kurum bağlantıları ve erişim hakkında yanıtlar.", "", 0,
                "Yardım ve bilgi");
            Block(new Guid("d1820000-0000-4000-8000-000000000007"), "HomeTestimonialsHeading",
                "Koçluk kullanıcılarından", "home-testimonials-heading",
                "Yalnızca izin alınmış, gerçek kullanıcı geri bildirimleri burada yayınlanır.", "", 0,
                "Koçluk deneyimleri");
            Block(new Guid("d1820000-0000-4000-8000-000000000008"), "BlogLanding",
                "Öğrenme yolculuğuna eşlik eden yazılar", "blog-landing",
                "Hedef belirleme, çalışma planı ve koçluk görüşmeleri için uygulamaya dönük kısa rehberler.", "", 0,
                "Koçluk kaynakları");
            Block(new Guid("d1820000-0000-4000-8000-000000000009"), "HomeBranding",
                "Eduİvme", "home-branding",
                "Hedef belirleme, çalışma planı ve ilerleme değerlendirmesi için öğrenci, öğretmen ve kurumlara özel Koçluk alanı.", "", 0,
                "Öğrenci Koçluğu", "Giriş / kayıt", "/auth/login?returnUrl=%2Fcoaching-portal");

            Block(new Guid("d1820000-0000-4000-8000-000000000101"), "HomeFaq",
                "Koçluk platformunu kimler kullanabilir?", "faq-kimler-kullanabilir",
                null, "Öğrenciler, öğretmenler ve kurum yöneticileri kendi hesap ve rollerine uygun Koçluk alanını kullanır. Kurum bağlantıları ve öğretmen-öğrenci eşleştirmeleri davet veya yetkili yönetim akışlarıyla yapılır.", 0);
            Block(new Guid("d1820000-0000-4000-8000-000000000102"), "HomeFaq",
                "Öğrenci ve öğretmen nasıl bağlanır?", "faq-ogrenci-ogretmen-baglantisi",
                null, "Öğretmen öğrenciyi davet edebilir veya kurum yöneticisi kurum kapsamındaki eşleştirmeyi yönetebilir. Davet, ilgili kullanıcı hesabıyla kabul edilir.", 1);
            Block(new Guid("d1820000-0000-4000-8000-000000000103"), "HomeFaq",
                "Hızlı Okuma sonuçları Koçluk panelinde görünür mü?", "faq-hizli-okuma-verisi",
                null, "Hayır. Hızlı Okuma ayrı bir platformdur; profili ve sonuçları kendi veri alanında tutulur. Koçluk paneli bu sonuçları göstermez.", 2);
            Block(new Guid("d1820000-0000-4000-8000-000000000104"), "HomeFaq",
                "Koçluk belirli bir akademik sonucu garanti eder mi?", "faq-akademik-sonuc-garantisi",
                null, "Hayır. Platform planlama, takip ve değerlendirme araçları sağlar; belirli bir sınav puanı veya akademik sonuç garantisi vermez.", 3);
            Block(new Guid("d1820000-0000-4000-8000-000000000105"), "HomeFaq",
                "Plan ve ücret bilgilerini nereden öğrenebilirim?", "faq-plan-ve-ucretler",
                null, "Aktif ve herkese açık planları ana sayfada görebilirsiniz. Planların fiyat ve özellikleri Koçluk abonelik yönetiminden güncellenir.", 4);

            Navigation(new Guid("d1820000-0000-4000-8000-000000000201"), "Main", "Yazılar", "/coaching/blog", 10);
            Navigation(new Guid("d1820000-0000-4000-8000-000000000301"), "Footer", "Nasıl çalışır?", "/coaching/pages/nasil-calisir", 0);
            Navigation(new Guid("d1820000-0000-4000-8000-000000000302"), "Footer", "Sıkça sorulan sorular", "/coaching/faq", 1);
            Navigation(new Guid("d1820000-0000-4000-8000-000000000303"), "Footer", "Yazılar", "/coaching/blog", 2);
            Navigation(new Guid("d1820000-0000-4000-8000-000000000304"), "Footer", "Planlar", "/coaching#planlar", 3);

            migrationBuilder.Sql("""
                UPDATE coaching.cms_navigation_items
                SET "Url" = '/coaching/faq'
                WHERE "Id" = 'd1810000-0000-4000-8000-000000000105' AND "UpdatedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM coaching.cms_entries
                WHERE "Id" IN (
                    'd1820000-0000-4000-8000-000000000001', 'd1820000-0000-4000-8000-000000000002',
                    'd1820000-0000-4000-8000-000000000003', 'd1820000-0000-4000-8000-000000000004',
                    'd1820000-0000-4000-8000-000000000005', 'd1820000-0000-4000-8000-000000000006',
                    'd1820000-0000-4000-8000-000000000007', 'd1820000-0000-4000-8000-000000000008',
                    'd1820000-0000-4000-8000-000000000009', 'd1820000-0000-4000-8000-000000000101',
                    'd1820000-0000-4000-8000-000000000102', 'd1820000-0000-4000-8000-000000000103',
                    'd1820000-0000-4000-8000-000000000104', 'd1820000-0000-4000-8000-000000000105'
                ) AND "UpdatedAt" IS NULL AND "Version" = 1;

                DELETE FROM coaching.cms_navigation_items
                WHERE "Id" IN (
                    'd1820000-0000-4000-8000-000000000201', 'd1820000-0000-4000-8000-000000000301',
                    'd1820000-0000-4000-8000-000000000302', 'd1820000-0000-4000-8000-000000000303',
                    'd1820000-0000-4000-8000-000000000304'
                ) AND "UpdatedAt" IS NULL;
                UPDATE coaching.cms_navigation_items
                SET "Url" = '/coaching/pages/sikca-sorulan-sorular'
                WHERE "Id" = 'd1810000-0000-4000-8000-000000000105' AND "UpdatedAt" IS NULL;
                """);
            migrationBuilder.DropColumn(
                name: "Author",
                schema: "coaching",
                table: "cms_entries");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                schema: "coaching",
                table: "cms_entries");

            migrationBuilder.DropColumn(
                name: "Eyebrow",
                schema: "coaching",
                table: "cms_entries");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "coaching",
                table: "cms_entries");

            migrationBuilder.DropColumn(
                name: "LinkLabel",
                schema: "coaching",
                table: "cms_entries");

            migrationBuilder.DropColumn(
                name: "LinkUrl",
                schema: "coaching",
                table: "cms_entries");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                schema: "coaching",
                table: "cms_entries");

            migrationBuilder.DropColumn(
                name: "SecondaryLinkLabel",
                schema: "coaching",
                table: "cms_entries");

            migrationBuilder.DropColumn(
                name: "SecondaryLinkUrl",
                schema: "coaching",
                table: "cms_entries");
        }
    }
}
