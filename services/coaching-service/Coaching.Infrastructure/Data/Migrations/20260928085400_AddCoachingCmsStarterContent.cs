using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingCmsStarterContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var createdAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var systemActor = Guid.Empty;

            void Entry(Guid id, string kind, string group, string title, string slug, string summary, string content,
                string seoTitle, string seoDescription, string tags, int sortOrder)
            {
                migrationBuilder.InsertData(
                    table: "cms_entries",
                    schema: "coaching",
                    columns: new[] { "Id", "Kind", "Group", "Title", "Slug", "Summary", "Content", "SeoTitle", "SeoDescription", "TagsJson", "IsPublished", "ScheduledPublishAt", "SortOrder", "ViewCount", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "Version" },
                    values: new object[] { id, kind, group, title, slug, summary, content, seoTitle, seoDescription, tags, true, null, sortOrder, 0, systemActor, createdAt, null, null, 1 });
            }

            Entry(new Guid("d1810000-0000-4000-8000-000000000001"), "Block", "HomeHero",
                "Hedefinden başla, adım adım ilerle.", "home-hero",
                "Öğrenci, öğretmen ve kurumların hedeflerini, çalışma planlarını ve ilerleme değerlendirmelerini aynı Koçluk alanında buluşturun.",
                "Koçluk alanı, öğrencinin hedeflerini öğretmen desteği ve kurum düzeyindeki görünürlükle bir araya getirir.\n\n- Öğrenci hedeflerini ve çalışma adımlarını takip eder\n- Öğretmenler ödev ve seansları yönetir\n- Kurumlar ekiplerinin ilerlemesini ve raporlarını inceler",
                "Öğrenci Koçluğu | Eduİvme", "Hedef, plan, ödev ve seans takibi için öğrenci, öğretmen ve kurumlara özel Koçluk platformu.",
                "[\"Koçluk\",\"Öğrenci\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000002"), "Block", "HomePage",
                "Öğrenciler için görünür ilerleme", "home-ogrenciler",
                "Hedeflerini, ödevlerini, görüşmelerini ve gelişim değerlendirmelerini kendi panelinden takip et.",
                "Öğrenci panelinde hedeflerini ve sana atanan çalışmaları bir arada görebilirsin.\n\n- Aktif hedef ve çalışma adımlarını incele\n- Ödev durumunu ve yaklaşan seanslarını takip et\n- Öğretmeninle gelişimini düzenli olarak gözden geçir",
                "Öğrenci Koçluk Paneli", "Öğrenci panelinde hedef, ödev, seans ve gelişim bilgilerini takip edin.",
                "[\"Öğrenci\",\"Hedef\",\"Ödev\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000003"), "Block", "HomePage",
                "Öğretmenler için odaklı takip", "home-ogretmenler",
                "Bağlı öğrencilerin hedeflerini ve koçluk çalışmalarını tek öğretmen panelinden planla.",
                "Öğretmen paneli, sorumluluğunuzdaki öğrenciler için planlama ve değerlendirme araçları sunar.\n\n- Öğrenci hedeflerini ve gelişim raporlarını incele\n- Ödev ve seans süreçlerini yönet\n- Her öğrenci için sonraki adımları netleştir",
                "Öğretmen Koçluk Paneli", "Öğretmenler için öğrenci, ödev, seans ve ilerleme takibi.",
                "[\"Öğretmen\",\"Öğrenci takibi\"]", 1);

            Entry(new Guid("d1810000-0000-4000-8000-000000000004"), "Block", "HomePage",
                "Kurumlar için ekip görünürlüğü", "home-kurumlar",
                "Öğretmen ve öğrenci süreçlerini kurum ölçeğinde yönet, raporları ve ilerleme özetlerini incele.",
                "Kurum paneli, yetkili yöneticilere kendi kurumlarının Koçluk süreçlerini yönetme olanağı verir.\n\n- Kurum öğretmen ve öğrencilerini düzenle\n- Öğrenci-öğretmen eşleşmelerini ve çalışmaları yönet\n- Kurum düzeyindeki rapor ve ilerleme özetlerini incele",
                "Kurum Koçluk Paneli", "Kurum yöneticileri için öğretmen, öğrenci, çalışma ve rapor yönetimi.",
                "[\"Kurum\",\"Raporlama\"]", 2);

            Entry(new Guid("d1810000-0000-4000-8000-000000000013"), "Block", "HomeHowItWorks",
                "Önce hedefi belirleyin", "home-process-hedef",
                "Öğrenci ve öğretmen, odaklanılacak hedefi ve mevcut ihtiyacı birlikte netleştirir.",
                "Hedefi öğrencinin koşullarına, dönem içindeki önceliklerine ve mevcut çalışma düzenine göre tanımlayın.",
                "Koçluk Süreci: Hedef Belirleme", "Koçluk sürecinde öğrenci ve öğretmen birlikte odaklanılacak hedefi belirler.",
                "[\"Koçluk süreci\",\"Hedef\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000014"), "Block", "HomeHowItWorks",
                "Planı uygulanabilir adımlara ayırın", "home-process-plan",
                "Hedefe yönelik çalışma ve görüşme adımlarını öğrencinin takvimine göre planlayın.",
                "Ödevleri ve seansları açık, takip edilebilir adımlara dönüştürün; planı öğrencinin günlük koşullarına göre ayarlayın.",
                "Koçluk Süreci: Çalışma Planı", "Koçluk hedeflerini öğrencinin koşullarına uygun çalışma adımlarıyla planlayın.",
                "[\"Planlama\",\"Ödev\",\"Seans\"]", 1);

            Entry(new Guid("d1810000-0000-4000-8000-000000000015"), "Block", "HomeHowItWorks",
                "Birlikte gözden geçirin", "home-process-review",
                "Ödev, hedef ve seans bilgilerini kullanarak sonraki adımı öğrenciyle birlikte belirleyin.",
                "Tamamlanan çalışmaları ve zorlanılan noktaları değerlendirin; yeni döneme gerçekçi bir sonraki adımla başlayın.",
                "Koçluk Süreci: İlerlemeyi Değerlendirme", "Koçluk ilerleme değerlendirmesinde öğrenci ve öğretmen sonraki adımları birlikte belirler.",
                "[\"İlerleme\",\"Değerlendirme\"]", 2);

            Entry(new Guid("d1810000-0000-4000-8000-000000000005"), "Page", null,
                "Koçluk nasıl çalışır?", "nasil-calisir",
                "Hedef belirlemeden düzenli değerlendirmeye kadar Koçluk sürecinin temel adımlarını keşfedin.",
                "## 1. Hedefi netleştirin\n\nÖğrenci ve öğretmen, üzerinde çalışılacak hedefi ve mevcut ihtiyacı birlikte tanımlar. Hedef öğrencinin koşullarına ve dönem içindeki önceliklerine göre belirlenir.\n\n## 2. Çalışma adımlarını planlayın\n\nBüyük hedef, izlenebilir ve yapılabilir adımlara ayrılır. Öğretmen gerekli ödevleri ve görüşmeleri planlar; öğrenci kendi panelinden atanan çalışmaları takip eder.\n\n## 3. İlerlemeyi gözden geçirin\n\nÖğrenci ve öğretmen; tamamlanan ödevleri, hedefleri ve seans notlarını gözden geçirerek sonraki adımları belirler. Değerlendirme, tek bir puana değil süreçteki bilgilere dayanır.\n\n## 4. Kurum düzeyinde takip\n\nKurum yöneticileri, yetkileri kapsamında kurum öğretmenlerini, öğrencilerini ve raporlarını yönetir. Öğrenci verileri kurum kapsamı ve atanmış roller doğrultusunda görüntülenir.",
                "Koçluk Süreci Nasıl İşler?", "Koçluk hedef belirleme, planlama ve ilerleme değerlendirmesi adımlarıyla çalışır.",
                "[\"Koçluk süreci\",\"Hedef\",\"Planlama\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000006"), "Page", null,
                "Öğrenci Koçluğu", "ogrenciler",
                "Öğrenci panelinde hedeflerini, ödevlerini, seanslarını ve ilerleme bilgilerini takip et.",
                "## Öğrenci panelinde neler yapabilirsin?\n\nKoçluk hesabınla giriş yaptığında sana atanan çalışma ve seansları, hedeflerini ve raporlarını kendi öğrenci panelinde görüntüleyebilirsin.\n\n- Hedeflerinin durumunu ve üzerinde çalıştığın adımları takip et\n- Öğretmenin tarafından atanan ödevleri görüntüle ve tamamlanma durumunu güncelle\n- Yaklaşan ve geçmiş seans bilgilerini incele\n- İlerleme raporundaki güncel özetleri gözden geçir\n\n## Hesap ve veri alanı\n\nKoçluk profili ve Koçluk çalışmaları bu platformun kendi veri alanında yönetilir. Hızlı Okuma ayrı bir platformdur; Hızlı Okuma profili ve sonuçları Koçluk panelinde gösterilmez.",
                "Öğrenciler İçin Koçluk Paneli", "Koçluk öğrenci panelinde hedef, ödev, seans ve gelişim bilgilerinizi takip edin.",
                "[\"Öğrenci\",\"Öğrenci paneli\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000007"), "Page", null,
                "Öğretmen Koçluğu", "ogretmenler",
                "Öğretmen paneli üzerinden sorumluluğunuzdaki öğrencileri, çalışmaları ve raporları yönetin.",
                "## Öğretmen paneli\n\nÖğretmen hesabı, atama veya kurum eşleştirmesiyle sorumluluğunuza bağlanan öğrencilerin Koçluk süreçlerini takip etmeniz için araçlar sunar.\n\n- Öğrenci profillerini ve gelişim raporlarını incele\n- Ödev oluştur, güncelle ve tamamlanma durumunu takip et\n- Koçluk seanslarını planla ve geçmiş görüşmeleri gözden geçir\n- Öğrenci hedeflerini düzenli olarak değerlendirmeye al\n\n## Erişim sınırı\n\nÖğretmen görünümü, yetkiniz ve size bağlanan öğrenci kapsamıyla sınırlıdır. Kurum yöneticisi rolü varsa kurum kapsamındaki ek yönetim ve rapor araçları ayrıca görünür.",
                "Öğretmenler İçin Koçluk Paneli", "Öğretmenler öğrencileri, ödevleri, seansları ve ilerleme raporlarını yönetebilir.",
                "[\"Öğretmen\",\"Öğretmen paneli\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000008"), "Page", null,
                "Kurum Koçluğu", "kurumlar",
                "Kurum yöneticileri öğretmen ve öğrenci bağlantılarını, koçluk çalışmalarını ve raporları kurum kapsamında yönetir.",
                "## Kurum yöneticileri için ortak panel\n\nKurum yöneticisi, kurumuna bağlı Koçluk kullanıcılarını ve süreçlerini tek panelden yönetebilir. Paneldeki kayıtlar, kurum ve rol yetkileri doğrultusunda sunulur.\n\n- Kurum öğretmen ve öğrenci kayıtlarını görüntüle\n- Öğretmen ve öğrenci eşleşmelerini yönet\n- Kurum kapsamındaki ödev ve seans süreçlerini incele\n- Öğrenci ve öğretmen raporları ile kurum ilerleme özetlerine ulaş\n\n## Kurum sınırı\n\nKurum yöneticisinin liste ve raporları kendi kurumunun Koçluk kapsamıyla sınırlıdır. Öğrenci başka bir kuruma geçerse geçmiş Koçluk kayıtlarının görünürlüğü ürünün tanımlı rol ve veri erişim kurallarına göre belirlenir.",
                "Kurumlar İçin Koçluk Paneli", "Kurum panelinde öğretmen, öğrenci, eşleştirme ve Koçluk raporlarını yönetin.",
                "[\"Kurum\",\"Kurum yöneticisi\",\"Raporlama\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000009"), "Page", null,
                "Sıkça sorulan sorular", "sikca-sorulan-sorular",
                "Koçluk hesabı, paneller, kurum bağlantıları, veri kapsamı ve planlarla ilgili kısa yanıtlar.",
                "## Koçluk platformunu kimler kullanabilir?\n\nÖğrenciler, öğretmenler ve kurum yöneticileri kendi hesap ve rollerine uygun Koçluk panelini kullanır. Kurum bağlantısı ve öğretmen-öğrenci eşleştirmeleri davet veya yetkili yönetim akışlarıyla yapılır.\n\n## Öğrenci ve öğretmen nasıl bağlanır?\n\nÖğretmen öğrenciyi davet edebilir veya kurum yöneticisi kurum kapsamındaki eşleştirmeyi yönetebilir. Davetin kabul edilmesi için ilgili hesapla oturum açılması gerekir.\n\n## Hızlı Okuma sonuçları Koçluk panelinde görünür mü?\n\nHayır. Hızlı Okuma ayrı bir platformdur; profil ve sonuç verileri kendi platformunda tutulur. Koçluk paneli Hızlı Okuma sonuçlarını göstermez.\n\n## Koçluk belirli bir akademik sonuç garantisi verir mi?\n\nHayır. Platform planlama, takip ve değerlendirme araçları sağlar; belirli bir sınav puanı veya akademik sonuç garantisi vermez. Sonuçlar öğrencinin koşullarına ve yürütülen çalışmaya bağlıdır.\n\n## Plan ve ücret bilgilerini nereden öğrenebilirim?\n\nYalnızca Koçluk yöneticisi tarafından aktif ve herkese açık olarak yayınlanan planlar ana sayfada gösterilir. Kurum planı için kurum çözüm sayfasını inceleyin.",
                "Koçluk Sıkça Sorulan Sorular", "Koçluk hesapları, paneller, veri ayrımı ve planlarla ilgili sıkça sorulan sorular.",
                "[\"SSS\",\"Koçluk\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000010"), "Blog", null,
                "Bir hedefi uygulanabilir çalışma planına dönüştürmek", "hedefi-calisma-planina-donusturmek",
                "Belirsiz bir hedefi günlük hayata sığan küçük adımlara bölmek için pratik bir başlangıç rehberi.",
                "## Hedefi görünür hale getirin\n\n‘Daha iyi çalışmak’ gibi geniş bir hedef, ne zaman başlayacağınızı veya ilerlemeyi nasıl gözden geçireceğinizi söylemez. Önce neyi, hangi dönemde ve hangi işaretlere bakarak takip edeceğinizi yazın.\n\n## Küçük ve somut adımlara bölün\n\nBir çalışma adımı, takvime konabilecek kadar açık olmalıdır. Örneğin ‘konuyu tekrar et’ yerine ‘Salı günü 20 dakika konu özeti çıkar ve üç örnek soru çöz’ gibi bir plan kullanılabilir. Süre ve kapsam öğrencinin gerçek programına göre ayarlanmalıdır.\n\n## Takvime yerleştirin ve gözden geçirin\n\nPlanı haftalık programa yerleştirin. Haftanın sonunda neyin tamamlandığını, neyin zor geldiğini ve gelecek hafta neyin değiştirileceğini öğrenciyle birlikte değerlendirin. Bir adım tamamlanmadıysa önce engeli anlayın; planı otomatik olarak daha yoğun hale getirmeyin.\n\n## Koçlukta kullanın\n\nHedef ve çalışma adımlarını öğrenci panelinde izleyin, öğretmen görüşmesinde ilerlemeyi konuşun ve yeni adımı birlikte belirleyin. Bu rehber genel planlama önerisidir; kişiye özel değerlendirme yerine geçmez.",
                "Hedefi Çalışma Planına Dönüştürme", "Öğrenci hedefini somut, takvimlenebilir ve düzenli gözden geçirilebilir çalışma adımlarına bölün.",
                "[\"Hedef belirleme\",\"Çalışma planı\",\"Öğrenci koçluğu\"]", 0);

            Entry(new Guid("d1810000-0000-4000-8000-000000000011"), "Blog", null,
                "Koçluk görüşmesine birlikte hazırlanmak", "kocluk-gorusmesine-hazirlanmak",
                "Öğrenci ve öğretmenin görüşmeyi açık hedefler ve somut örneklerle yürütmesine yardımcı olacak bir kontrol listesi.",
                "## Görüşmeden önce\n\nÖğrenci, son görüşmeden beri tamamladığı çalışmaları ve takıldığı noktaları kısaca not edebilir. Öğretmen de önceki hedefleri, ödev durumunu ve konuşulması gereken konuları gözden geçirebilir.\n\n- Görüşmede yanıtlanacak bir veya iki soruyu belirleyin\n- Tamamlanan ve tamamlanamayan çalışmaları örnekleriyle getirin\n- Yeni dönemdeki sınav, proje veya zaman kısıtlarını paylaşın\n\n## Görüşme sırasında\n\nÖnce öğrencinin kendi değerlendirmesini dinleyin. Planın hangi kısmının işe yaradığını ve hangi kısmında engel çıktığını anlamaya çalışın. Ardından bir sonraki görüşmeye kadar izlenecek net ve gerçekçi adımları birlikte belirleyin.\n\n## Görüşmeden sonra\n\nKararlaştırılan hedefi ve ödevleri Koçluk panelinde güncelleyin. Bir sonraki görüşmede bu adımların durumunu gözden geçirin. Görüşme, öğrenciyi yargılamak için değil destek ihtiyacını ve sonraki adımı netleştirmek için kullanılmalıdır.",
                "Koçluk Görüşmesine Hazırlık", "Öğrenci ve öğretmen için Koçluk seansından önce, sırasında ve sonra kullanılabilecek kısa kontrol listesi.",
                "[\"Koçluk seansı\",\"Öğretmen\",\"Öğrenci\"]", 1);

            Entry(new Guid("d1810000-0000-4000-8000-000000000012"), "Blog", null,
                "Haftalık ilerlemeyi anlamlı biçimde gözden geçirmek", "haftalik-ilerlemeyi-gozden-gecirme",
                "Haftalık kontrolü yalnızca tamamlanan işlerin sayısına değil öğrenme sürecine de odaklayın.",
                "## Birkaç farklı işarete bakın\n\nHaftalık gözden geçirmede yalnızca kaç ödevin tamamlandığını saymak yerine hedefe yönelik ilerlemeyi, çalışma düzenini ve karşılaşılan güçlükleri birlikte ele alın.\n\n## Kısa bir değerlendirme akışı kullanın\n\n- Bu hafta hangi adımlar tamamlandı?\n- Hangi konu veya görev beklenenden zor geldi?\n- Planı etkileyen zaman, ortam veya kaynak değişikliği oldu mu?\n- Gelecek hafta için en önemli ve gerçekçi adım nedir?\n\n## Notları bir sonraki adıma bağlayın\n\nGözden geçirme sonunda bir sonraki adımı ve gerekiyorsa destek ihtiyacını kaydedin. Bir hafta içindeki değişim tek başına kalıcı gelişim veya başarısızlık anlamına gelmez; değerlendirmeyi öğrencinin bağlamı ve zaman içindeki kayıtlarıyla birlikte yapın.\n\nKoçluk panelindeki hedef, ödev, seans ve rapor alanları bu görüşmelerdeki bilgileri düzenli biçimde takip etmeye yardımcı olur.",
                "Haftalık Öğrenme İlerlemesi", "Haftalık öğrenci ilerlemesini hedef, çalışma, engel ve sonraki adım üzerinden değerlendirin.",
                "[\"İlerleme\",\"Öğrenme\",\"Raporlama\"]", 2);

            void Navigation(Guid id, string label, string url, int sortOrder)
            {
                migrationBuilder.InsertData(
                    table: "cms_navigation_items",
                    schema: "coaching",
                    columns: new[] { "Id", "Menu", "Label", "Url", "Icon", "SortOrder", "IsVisible", "OpenInNewTab", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt" },
                    values: new object[] { id, "Main", label, url, null, sortOrder, true, false, systemActor, createdAt, null, null });
            }

            Navigation(new Guid("d1810000-0000-4000-8000-000000000101"), "Nasıl çalışır?", "/coaching/pages/nasil-calisir", 0);
            Navigation(new Guid("d1810000-0000-4000-8000-000000000102"), "Öğrenciler", "/coaching/pages/ogrenciler", 1);
            Navigation(new Guid("d1810000-0000-4000-8000-000000000103"), "Öğretmenler", "/coaching/pages/ogretmenler", 2);
            Navigation(new Guid("d1810000-0000-4000-8000-000000000104"), "Kurumlar", "/coaching/pages/kurumlar", 3);
            Navigation(new Guid("d1810000-0000-4000-8000-000000000105"), "Sıkça sorulan sorular", "/coaching/pages/sikca-sorulan-sorular", 4);
            Navigation(new Guid("d1810000-0000-4000-8000-000000000106"), "Planlar", "/coaching#planlar", 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM coaching.cms_entries
                WHERE "Id" IN (
                    'd1810000-0000-4000-8000-000000000001', 'd1810000-0000-4000-8000-000000000002',
                    'd1810000-0000-4000-8000-000000000003', 'd1810000-0000-4000-8000-000000000004',
                    'd1810000-0000-4000-8000-000000000005', 'd1810000-0000-4000-8000-000000000006',
                    'd1810000-0000-4000-8000-000000000007', 'd1810000-0000-4000-8000-000000000008',
                    'd1810000-0000-4000-8000-000000000009', 'd1810000-0000-4000-8000-000000000010',
                    'd1810000-0000-4000-8000-000000000011', 'd1810000-0000-4000-8000-000000000012',
                    'd1810000-0000-4000-8000-000000000013', 'd1810000-0000-4000-8000-000000000014',
                    'd1810000-0000-4000-8000-000000000015'
                ) AND "UpdatedAt" IS NULL AND "Version" = 1;

                DELETE FROM coaching.cms_navigation_items
                WHERE "Id" IN (
                    'd1810000-0000-4000-8000-000000000101', 'd1810000-0000-4000-8000-000000000102',
                    'd1810000-0000-4000-8000-000000000103', 'd1810000-0000-4000-8000-000000000104',
                    'd1810000-0000-4000-8000-000000000105', 'd1810000-0000-4000-8000-000000000106'
                ) AND "UpdatedAt" IS NULL;
                """);
        }
    }
}
