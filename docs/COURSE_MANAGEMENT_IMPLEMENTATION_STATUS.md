# Kurs yönetimi geliştirme durumu

Tarih: 12 Ekim 2026. İlk geliştirme teslimi; kullanılabilir MVP veya üretim sürümü değildir.

## Uygulananlar

- .NET 10 servis: Domain, Application, Infrastructure ve API projeleri; ana solution'a eklendi.
- Ayrı PostgreSQL modeli ve ilk EF migration: `UserProfiles`, `Workspaces`. Identity kimliği referans olarak tutulur; Identity veya diğer ürün tabloları değiştirilmez.
- API yalnızca geçerli, imzalı, `platform_product=course-management` ve boş olmayan GUID `sub` içeren Identity tokenını kabul eder. Eski ürün tokenı SystemAdmin olsa da bu API'ye giremez.
- `GET/PUT /api/course-management/v1/me/profile`: kullanıcının kendi yerel profilini okuma/yazma. Kullanıcı kimliği request body'den alınmaz.
- `GET /api/course-management/v1/me/workspaces`: yalnızca sahibi olduğu alanları listeler. Çalışma alanı oluşturma/üyelik yönetimi henüz yoktur.
- `/health/live`, veritabanını kontrol eden `/health/ready` ve Gateway'de yeni namespaced route/cluster.
- Identity'de `CourseManagement=3`, ürün route/parsing ve izin filtreleri. Ayrı ürün login controller'ı ve staff ürün değiştirme endpoint'i. Eski ürün kayıt endpoint'leri genişletilmedi.
- Personel portalında üçüncü ürün seçimi ve ayrı kurs çalışma alanı iskeleti. Eski hesap/profil ve bildirim bileşenleri kurs ürününde açılmaz; yeni profil servise aittir.
- Admin'de `/dashboard/course-management` ve SystemAdmin'e özel hazırlık ekranı. Henüz veri yöneten admin işlemleri yoktur; gerçek işlemler ürün izinleriyle P2/P7'de açılacaktır.
- Ayrı Angular öğrenci uygulaması ve kendi package-lock dosyası. Geliştirme durumunu gösterir; giriş, takvim, ödeme ve ödev işlevleri henüz açık değildir.
- Ayrı Dockerfile ve isteğe bağlı geliştirme Compose overlay'i. Mevcut Compose servis ayarları değiştirilmedi. Üretime yayın yapılmadı.

## Doğrulamalar

- 20 .NET alan modeli/izin ayrımı/profil saklama/sahiplik testi geçti.
- Personel portalında giriş/MFA/rol, token yenileme ve CAPTCHA dahil 28 mevcut test ve 4 yeni ürün izolasyonu/CAPTCHA testi geçti (toplam 32).
- Admin sidebar'ın 3 mevcut izin testi geçti.
- Identity tarafında ortak CAPTCHA ve personel ürün erişimi için seçilen 33 regresyon/entegrasyon testi geçti.
- 4 koruma/sözleşme testi geçti: 1.589 eski ürün dosyasının SHA256 baseline'ı, eski Gateway ayarları ve ayrı öğrenci uygulaması.
- Gerçek JWT ile çalışan API testi: anonim 401; eski ürün ve geçersiz sub 403; sahte imza 401; geçerli ürün 200; geçersiz profil 400.
- İzole PostgreSQL konteynerinde ilk migration ve ikinci kez uygulama geçti. Gerçek DB üzerinde profil oluşturma/güncelleme, başka kullanıcının profil ayrımı ve başka sahibin çalışma alanının gizlenmesi geçti. Konteyner test sonunda kaldırıldı.
- Kurs API ve Identity API derlemeleri geçti. Yeni öğrenci ve staff üretim derlemeleri geçti. Admin Angular/TypeScript template derlemesi geçti.
- Mevcut Compose ile yeni geliştirme overlay'inin birleştirilmiş şema doğrulaması geçti.
- Admin tam bundle doğrulaması tamamlanamadı: sandbox'ta Google Fonts DNS erişim hatası; tekrar denemelerde Node süreci çıktı vermeden kapandı (bir denemede Windows `0xC0000005`). Bu nedenle admin üretim build kapısı açık kalır.
- NuGet güvenlik danışma kaynağı erişiminde NU1900 uyarısı var; derleme başarısını etkilemedi. Staff'ın mevcut SCSS bütçe uyarısı devam ediyor.

## Sonraki geliştirme kapısı

1. P0 aktarım alan matrisi ve çok sekmeli ortak oturum davranışını doğrula; kurs profilini eski ürün profillerinden ayrı tut.
2. P2: davet, ürün katılımı, kurum/bağımsız öğretmen çalışma alanı oluşturma, üyelik/rol/kurum kapsamı, tekrar güvenliği ve audit.
3. Öğrenci için davet kabulü ve gerçek Identity girişi; kurum/öğretmen için yerel profil ve çalışma alanı ekranları.
4. P3 öğrenci/veli/kurs/grup; ardından P4 takvim/yoklama, P5 ödeme/paket, P6 ödev ve bildirimler.
5. Admin bundle problemini izole et; CI build/test, yayın ayrımı, yedek/restore ve gerçek pilot doğrulamaları tamamlanmadan yayına çıkma.

Mevcut hesapların kursa otomatik aktarımı veya yetki kazanması uygulanmadı. Tam katılım akışı kurulmadan bu iskelet canlı kullanıcı kaydı için açılmamalıdır.
