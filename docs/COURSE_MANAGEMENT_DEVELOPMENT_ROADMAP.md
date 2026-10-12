# Kurs Yönetimi — Ürün Planı ve Geliştirme Yol Haritası

Tarih: 12 Ekim 2026

Durum: Geliştirme başladı. P0 koruma baseline'ı ve P1 ürün iskeleti uygulandı; yerel profil ve sahip olunan çalışma alanı okuma API'si eklendi. P0 aktarım/session doğrulaması, P1 yayın/işletim kapıları ve P2 katılım akışları henüz tamamlanmadı. Güncel teslim ve doğrulamalar: `docs/COURSE_MANAGEMENT_IMPLEMENTATION_STATUS.md`.

## 1. Kabul edilen kapsam ve mimari kararlar

- Kurs yönetimi, koçluk ve hızlı okumadan bağımsız üçüncü üründür.
- Öğrenci paneli sıfırdan, ayrı Angular uygulaması olarak geliştirilir: `clients/course-management`.
- Öğretmen ve kurum alanları mevcut `staff-portal` uygulamasına yeni ürün modülü olarak eklenir.
- Merkezi yönetim mevcut `admin-panel` uygulamasına ayrı modül olarak eklenir.
- Backend: `services/course-management-service`; kendi veritabanı, migration, depolama ve mesajları bulunur.
- Identity hesabı ortak kalır; kurs yönetimi profilleri ve çalışma alanı ilişkileri yeni servise aittir.
- Başka üründeki kişi/kurum, yetkili katılım veya aktarım gerçekleştiğinde burada yerel kayıt kazanır.
- Mevcut ürünlerin özellikleri, iş kuralları, kayıtları, API sözleşmeleri ve ekranları değiştirilmez.
- Mevcut bileşenler ortaklaştırılmaz; yeni modül kendi bileşenlerini kullanır. Görsel yaklaşım referans alınabilir.

Varsayımlar: İlk sürüm Türkçe ve TRY odaklıdır; öğretmen ve kurum modları birlikte desteklenir. Öğrencinin giriş hesabı olmadan operasyon kaydı açılabilir. Online tahsilat, resmi e-belge entegrasyonu ve genel muhasebe ilk sürüme dahil değildir. Öğrenci uygulaması mobil uyumlu hazırlanır; native mobil uygulama planlanmaz. Bunlar ürün varsayımlarıdır, uygulanmış kararlar değildir.

## 2. Mevcut ürünleri koruma sözleşmesi

### 2.1 Korunan alanlar

- `services/coaching-service/**`, `services/speed-reading-service/**` ve mevcut migration'ları.
- `clients/speed-reading/**`.
- Personel portalındaki `features/coaching/**` ve `features/speed-reading/**`.
- Admin panelindeki mevcut koçluk, hızlı okuma ve koçluk öğrenci portalı ekranları.
- Mevcut ürünlerin veri aktarımı, üyelikleri, abonelikleri, rol izinleri ve bildirim akışları.
- Ortak CSS, mevcut tasarım değişkenleri, paket sürümleri ve mevcut ekran metinleri; yeni ürünle ilgili ekleme gereken ürün seçici metinleri hariç.

Bu alanlarda yeni ürün gerekçesiyle refactor, yeniden adlandırma veya işlev değişikliği yapılmaz. Yeni geliştirme için mevcut ürün tablolarına sütun eklenmez; eski veriler üzerinde toplu backfill/migration çalıştırılmaz.

### 2.2 Gerekli ortak kayıt noktaları

Tamamen sıfır ortak dosya değişikliği, mevcut panellerde üçüncü ürünün görünmesiyle bağdaşmaz. Kullanıcının kabul ettiği panel genişlemesi kapsamında aşağıdaki noktalara yalnızca yeni ürünü tanıtan eklemeler yapılır:

| Nokta | İzin verilen ekleme | Koruma şartı |
|---|---|---|
| Identity ürün enum/route değerleri | `CourseManagement = 3`, `course-management` | Mevcut enum değerleri, ürün URL'leri korunur |
| Identity giriş, kayıt, erişim, MFA/oturum geçişi | Üçüncü ürüne ait yol ve sözleşmeler | Koçluk/hızlı okuma taleplerinin davranışı değişmez |
| Identity izin kapsamı | Yeni ürün izinlerinin diğer ürünlere sızmasını önleyen sınırlama | Eski ürünlerin mevcut izin kararları korunur |
| Staff portal ürün tipi, seçici ve kök bileşen | Üçüncü ürün ve yeni bileşenin bağlanması | Eski çalışma alanı bileşenleri değiştirilmez |
| Staff davet/bildirim yönlendirme noktaları | Yeni ürün için açık API seçimi | Eski ürünlerin URL ve HTTP metotları korunur |
| Admin rota/menü/izin kayıtları | Yeni kurs yönetimi rotaları ve izinleri | Eski rotalar ve ekranlar korunur |
| Gateway | Yeni `/api/course-management/**` yolu ve cluster | Mevcut route/transform/policy değerleri korunur |
| Solution, Compose, CI ve yayın ayarları | Yeni projeler ve ayrı build/test/deploy hedefleri | Mevcut servis ayarları değiştirilmez |
| Ortak event sözleşmeleri | Ayrı `CourseManagement` namespace'i | Mevcut event tipleri ve tüketicileri değiştirilmez |
| Notification | Yeni ürüne özel tüketici ve şablon kaydı | Mevcut bildirim tipleri/şablonları korunur |
| Identity salt okunur aktarım sözleşmesi | Gerekirse yeni ürüne ait ayrı internal endpoint/DTO | Eski endpoint, profil ve üyelikler değiştirilmez; yetkisiz toplu dizin erişimi verilmez |

Yeni ürüne ait izinlerin eski ürün token'larına eklenmesi engellenir. Mevcut `PlatformPermissionScope` diğer ürünün izinlerini dışlayarak çalıştığından üçüncü üründe bu alan özellikle sözleşme testleriyle korunur. Genel izin sistemini yeniden tasarlamak bu kapsamda değildir.

Yeni modül devre dışıyken eski kullanıcılar için yeni menü, gereksiz istek veya hata oluşmaması hedeflenir. Kurs kullanıcıları dışında yeni ürün erişimi otomatik verilmez.

Kök bileşenin CSS'i yeni modülün stilleriyle değişmez; yeni stiller modül kapsamındadır. Öğrenci uygulamasının session/cookie davranışı mevcut ürünleri oturumdan çıkarmamalıdır; aynı tarayıcıda ayrı sekmelerle eski/yeni ürün kullanımı regresyon senaryosudur. Bağımlılık sürümlerini yükseltmek veya mevcut paket kilidini yeniden çözmek bu geliştirme kapsamına dahil değildir; zorunlu yeni paket ihtiyacı P0'da ayrıca listelenir.

### 2.3 Her teslimin kanıtı

- Korunan dosyalarda diff bulunmaması; ortak kayıt dosyalarının izin verilen eklemelerle sınırlı olması.
- Eski ürünlerin giriş, MFA, ürün geçişi, öğretmen/kurum görünümü ve kritik öğrenci akışlarının geçmesi.
- Eski ürün API'lerinin route, DTO ve izin kararlarının korunması.
- Yeni ürün kapalı ve açık durumlarında personel/admin panellerinin kontrol edilmesi.
- Yeni ürün migration'larının yalnızca yeni veritabanı ile gerekli Identity ürün/izin kayıtlarına etkisi.
- Test öncesi var olan arızaların baseline olarak ayrılması; yeni arızaların teslim edilmemesi.

## 3. Uygulama sınırları ve dosya yerleşimi

```text
services/course-management-service/
  CourseManagement.Domain/
  CourseManagement.Application/
  CourseManagement.Infrastructure/
  CourseManagement.API/

clients/course-management/                         # yeni öğrenci uygulaması
clients/admin-panel/projects/staff-portal/src/app/features/course-management/
clients/admin-panel/src/app/features/course-management-admin/

shared/EduPlatform.Shared.Contracts/CourseManagement/
tests/Unit/CourseManagement.Application.UnitTests/
tests/Integration/CourseManagement.API.IntegrationTests/
tests/E2E/specs/course-management-*.spec.mjs
```

Adlar öneridir; mevcut repository standardıyla son hali temel aşamada belirlenir. Yeni servis .NET katmanlı yapıyı, PostgreSQL ve mevcut RabbitMQ/MassTransit outbox yaklaşımını takip eder. Yeni ürün için mevcut ürün servislerine runtime bağımlılığı oluşturulmaz. Identity ve Notification entegrasyonları kullanılır.

Kendi `course_management_db` veritabanı kullanılır. Aynı PostgreSQL sunucusunda ayrı veritabanı olabilir; uygulama kullanıcısının diğer ürün veritabanlarına yetkisi olmaz. Dosyalar ayrı storage alanında tutulur. Önbellek anahtarları, mesaj kuyrukları ve metrikler ürün adıyla ayrılır.

## 4. Kimlik, profil, çalışma alanı ve roller

### 4.1 Veri sahipliği

| Veri | Sahibi |
|---|---|
| Kullanıcı hesabı, parola, MFA, doğrulanmış giriş adresi, oturum | Identity |
| Ürün erişimi ve ürün kapsamlı personel rolü | Identity |
| Kurs öğrenci/öğretmen/veli profili | CourseManagement |
| Kurumun kurs yönetimi profili, şube ve derslikleri | CourseManagement |
| Ortak kurum kimliği mevcutsa referansı | Identity referansı; yerel operasyon profili CourseManagement |
| Çalışma alanı üyeliği, yerel rol ve kayıt erişimi | CourseManagement |
| Kurs/grup kaydı, ders, yoklama, ödev ve finans | CourseManagement |

Ortak hesap ayarları ile ürün profili ayrı ekranlardır. Yeni ürün profili güncellemesi Identity'deki hesap veya diğer ürün profillerini güncellemez. Doğrulanmış hesap e-postası ile kurumun kullandığı operasyon iletişim adresi ayrı anlamlar taşır.

### 4.2 Çalışma alanı

`Workspace` türleri: `Institution`, `IndependentTeacher`.

Kullanıcı birden fazla çalışma alanına katılabilir. Kurum çalışanı olan öğretmen ayrıca kişisel alan açabilir. Her istek ürün erişimini, çalışma alanı üyeliğini, işlemin iznini ve hedef kaydın kapsamını sunucuda doğrular. İstemcinin gönderdiği `workspaceId` tek başına yetki sağlamaz.

Önerilen ilk sürüm rolleri:

| Rol | Temel yetki |
|---|---|
| Kurum sahibi | Çalışma alanı, üyelik ve kurum operasyonları |
| Kurum yöneticisi | Öğrenci, kurs, grup ve ders yönetimi; verilen finans izinleri |
| Öğretmen | Atandığı öğrenciler/dersler, yoklama ve ödev |
| Bağımsız öğretmen | Kişisel alanının öğrencileri, dersleri ve finansı |
| Öğrenci | Kendi dersleri, ödevleri, paketleri ve izin verilen bilgiler |
| Veli | Doğrulanmış ilişkiyle bağlı öğrenciler; verilen ödeme ve eğitim görünürlüğü |
| Platform yöneticisi | Ürün işletimi ve yetkili destek; kurum finansını düzeltme otomatik yetkisi yok |

Sekreter ve finans görevlisi sonraki fazda yerel izinlerle eklenebilir. MVP'de sırf bu roller için eski personel erişim politikasının kapsamı genişletilmez. Platform yöneticisinin hassas kayıtlara erişimi ayrıca denetlenir.

Yeni kurum ve bağımsız öğretmen için self-servis katılım akışları ayrıca bulunur; yalnızca davetli kullanıcı desteklenmez. Mevcut hesap yeni ürüne katılır, tamamen yeni kişi Identity hesabı açar; her iki durumda yerel ürün profili ve çalışma alanı oluşur. Ürün rolü portala giriş sağlar; çalışma alanındaki izinler yerel üyelikten çözülür. Son üyelik kaldırıldığında yerel erişim kapanır; diğer ürün erişimleri iptal edilmez. Identity hesabı pasifleştirilirse yeni ürün erişimi de geçersizleşir; bu yaşam döngüsü mevcut ortak güvenlik sözleşmesiyle ve ayrı yeni ürün testleriyle doğrulanır.

### 4.3 Hesapsız öğrenci

Yerel öğrenci kaydı `StudentRecordId` ile oluşturulur; `IdentityUserId` başlangıçta boş olabilir. Kurs kaydı, ödeme ve yoklama bu yerel kayıtla çalışır. Portal daveti kabul edilince doğrulanmış hesap bağlanır. Ödev öğrenciye atanabilir; portal üzerinden teslim için hesap bağlantısı gerekir.

Bir veli birden fazla öğrenciye bağlanabilir. Veli olmak ve ödeme sorumlusu olmak ayrıdır. Aynı kişinin farklı çalışma alanlarındaki borçları, notları ve ilişkileri birbirine açılmaz.

## 5. Ürünler arası katılım ve aktarım

1. Ortak hesaba giriş veya doğrulanmış davet kabulü.
2. Kurs yönetimi ürün erişiminin oluşturulması/doğrulanması.
3. Yeni üründe yerel profil oluşturulması.
4. Davetteki çalışma alanı ve rol için yetkili üyelik oluşturulması.
5. Yalnızca ihtiyaç duyulan ve aktarımı yetkilendirilmiş alanların alınması.
6. Kaynak, aktör, zaman ve eşleştirme sonucunun kaydedilmesi.

Başka üründe kayıtlı olmak otomatik katılım veya yetki vermez. Ad-soyad veya benzer e-posta üzerinden hesap birleştirilmez. Öğrenciler ve öğretmenler ortak `UserId` ile doğrulanır; kurum eşleştirmesi ortak kurum kimliği ve yetkili yönetici doğrulamasıyla yapılır.

İlk sürüm tek kişinin katılımını ve mevcut hesapla davet kabulünü destekler. Kurumun seçili üyelerini toplu aktarma ve CSV içe aktarma pilot öncesinde tamamlanır. P0'da kurum/öğretmen/öğrenci temel alanlarının hangilerinin Identity'deki mevcut yetkili sözleşmelerden alınabildiği alan bazında doğrulanır. Eksik temel Identity verisi için gerekirse yeni ürüne ait ayrı, salt okunur internal sözleşme eklenir; mevcut sözleşmeler değiştirilmez. Kaynak ürün servislerinde export endpoint açılmaz. Yalnızca koçluk/hızlı okuma yerel profilinde bulunan bir alanı eski servise dokunmadan almak mümkün değilse bu sınırlama aktarım önizlemesinde açıkça gösterilir; isteğe bağlı ek alanlar CSV veya doğrulanmış manuel girişle tamamlanır. Temel ortak kimliğin bağlanması CSV'ye bırakılmaz.

Aktarım alan matrisi P0 teslimidir: kaynak alan, veri sahibi, kullanılacak endpoint, yetki kapsamı, hedef alan, kullanıcıya gösterilen aktarım seçimi ve eksik veri davranışı. P2 kabul koşulu mevcut kurum, öğretmen ve öğrencinin ortak kimliğiyle yeni ürüne katılabilmesidir. P7 kabul koşulu yetkili kurum yöneticisinin seçtiği üyeleri mükerrer kayıt oluşturmadan aktarabilmesidir. Mevcut kurum üyelikleri diğer ürünlerden taşınmaz veya silinmez; burada yeni yerel üyelikler oluşturulur.

Aktarım önizlemesi, alan eşleştirme, satır bazlı hata raporu ve tekrar çalıştırmada mükerrer kayıt önleme bulunur. `ImportMapping` kaynak sistem/kayıt/çalışma alanı eşleştirmesini saklar. Ders geçmişi, borç, koçluk özel notları ve hızlı okuma sonuçları varsayılan olarak aktarılmaz. Yerel profil sonraki düzenlemelerde kaynak ürüne yazılmaz; sürekli çift yönlü senkronizasyon yoktur.

Identity ve yeni servis arasında ortak veritabanı transaction'ı kurulmaz. Katılım işlemi durum kaydı ve idempotency ile tekrar denenebilir. Identity erişimi oluşup yerel profil oluşturulamazsa kullanıcıya katılımın tamamlanmadığı gösterilir; retry eksik adımı tamamlar, eski üyelikleri silmez.

## 6. MVP fonksiyonları ve veri modeli

### 6.1 Öğrenci ve kurs kaydı

- Öğrenci kartı, veli bağlantısı, aktif/pasif operasyon durumu.
- Kurs/program ve grup tanımı; özel ders gruba ihtiyaç duymadan açılır.
- Bir öğrencinin birden fazla kaydı ve öğretmenle ilişkisi.
- Kayıt tarihleri, dönem, ücret anlaşması ve ödeme planı.
- Kayıt durumları: taslak, aktif, dondurulmuş, tamamlanmış ve iptal. Grup kapasitesi ve kayıt tarihleri doğrulanır; kurs kaydı sona erse de geçmiş korunur.
- MVP'de temel şube/derslik tanımı, dersin şubesi ve liste/takvim filtreleri bulunur. Şubeye özel personel yetkileri, franchise hiyerarşisi ve şubeler arası finans konsolidasyonu sonraki fazdadır.
- Kurum içi notlar ile öğrenci/veli görünür geri bildirim ayrı tutulur.

Varlıklar: `Workspace`, `Membership`, `StudentProfile`, `TeacherProfile`, `StudentRecord`, `GuardianLink`, `Course`, `Group`, `Enrollment`, `Branch`, `Room`.

### 6.2 Takvim ve yoklama

- Tek ders ve tekrarlayan ders serisi; kurum, öğretmen ve öğrenci görünümü.
- Öğretmen/oda/grup/öğrenci için tanımlanan çakışma kuralları.
- Serinin tek oturumunu veya gelecekteki oturumlarını değiştirme.
- Ders durumları: planlandı, tamamlandı, iptal edildi; erteleme eski/yeni oturum bağlantısıyla izlenir.
- Öğrenci yoklaması: katıldı, katılmadı, mazeretli, geç katıldı.
- Öğretmen izin/müsaitlik aralıkları ve tatil istisnaları.
- İptal ve telafi kayıtları; geçmiş tamamlanmış dersler yeniden yazılmaz.

Varlıklar: `LessonSeries`, `LessonSession`, `LessonParticipant`, `Attendance`, `Availability`, `MakeupCredit`.

Zamanlar UTC olarak saklanır; çalışma alanı IANA zaman dilimini saklar. Tekrarlayan plan yerel saat ve zaman dilimiyle tanımlanır. Eşzamanlı ders kayıtlarında çakışma yalnızca ekrandan kontrol edilmez; sunucuda transaction/uygun kilitleme veya veritabanı kısıtıyla yarış koşulu engellenir.

Kişisel ve kurumsal takvimler MVP'de ayrı kalır. Ürün genelinde veya harici takvimler arasında müsaitlik paylaşımı daha sonra, ders ayrıntısını açmadan eklenir. MVP kurum dışındaki takvimleri kapsayan çakışmasızlık garantisi vermez.

### 6.3 Paket ve finans

- Dört temel ücret modeli: dönemlik/taksitli anlaşma, aylık sabit ücret, ders/saat başına ücret ve adet bazlı özel ders paketi.
- Aylık modelde aktif anlaşmanın dönem borcu bir kez oluşturulur; kısmi ay/dondurma hesabı P0'da belirlenir. Ders/saat modelinde tamamlanan veya politika gereği ücretlenen ders borçlandırılır; süre birimi, oran ve yuvarlama anlaşmada saklanır. Geçmiş borçlar güncel fiyat değişikliğiyle yeniden hesaplanmaz.
- Paket geçerlilik süresi, kullanım, kullanımın geri alınması ve telafi hareketleri.
- Nakit ve EFT/havale tahsilat kaydı; doğrulanmış harici kart tahsilatı manuel kaydedilebilir.
- Kısmi tahsilat, bir tahsilatın birden fazla taksite dağıtılması.
- İndirim, düzeltme, ödeme iptali ve iade hareketleri.
- Vadesi gelen/geciken borç, öğrenci hesap hareketi ve tahsilat raporu.
- Öğrencinin ders hakkı ile para bakiyesi ayrı tutulur.

Varlıklar: `FeeAgreement`, `PaymentPlan`, `Installment`, `Payment`, `PaymentAllocation`, `FinancialAdjustment`, `Refund`, `LessonPackage`, `PackageMovement`.

Tutarlar decimal ve para birimiyle saklanır; ilk sürüm TRY kullanır. Finansal hareketler silinerek düzeltilmez; yetkili ters/düzeltme hareketi oluşur. Toplam tahsilat dağılımı tahsilatı aşamaz. Paket kullanımı ve dersin finansal sonucu aynı servis transaction'ında, tekrar çalıştırmada ikinci etki yaratmadan uygulanır. Tahsilat oluşturma ve iade işlemleri idempotent olur. Raporlarda sözleşme tutarı, tahakkuk, gerçek tahsilat ve kalan alacak ayrı gösterilir; tahsilat doğrudan muhasebesel gelir/kâr olarak sunulmaz.

Başlangıçta genel kural motoru yerine birkaç tanımlı iptal politikası kullanılır. Politikanın sürümü kayıt anlaşmasına bağlanır. Öğretmen iptali, öğrenci erken/geç iptali ve gelmeme için ders hakkı, borç ve telafi sonuçları açıkça tanımlanır. Takvimde ders oluşturmak tek başına borç doğurmaz; ücret modeli ve ilgili iş olayı belirleyicidir.

### 6.4 Ödev ve öğrenci portalı

- Öğrenci/gruba ödev, açıklama, son tarih ve öğretmen materyali.
- Öğrenci metin/fotoğraf/PDF teslimi; izin verilen tür, boyut ve saklama sınırları.
- Teslim durumu, geç teslim, puan ve geri bildirim.
- Yetkili dosya indirme, tarama/karantina ve saklama süresi.
- Dersler, devamsızlık, kalan paket hakkı ve izin verilen ödeme bilgileri.
- Öğrenci/veli için yeni ürünün kendi onboarding, profil ve bildirim ekranları.

Varlıklar: `Assignment`, `AssignmentRecipient`, `Submission`, `Attachment`, `NotificationPreference`.

Koçluk dosya servisinin uygulaması çağrılmaz; yeni storage namespace'i ve yeni endpoint'ler kullanılır. Fotoğraf/PDF desteği yeni ürünün kendi kontratıdır. MVP'de açık genel sohbet, video konferans veya otomatik ödev değerlendirme yoktur.

## 7. Ekran planı

### 7.1 Yeni öğrenci uygulaması

Önerilen adres: ayrı host veya `/course/` altında ayrı statik uygulama; yayın ortamı kararı hazırlık fazında verilir. Personel ve admin uygulamalarına öğrenci ekranı gömülmez.

- Giriş/kayıt/davet kabulü; ürün erişimi ve yerel profil tamamlama.
- Ana sayfa: sıradaki dersler, bekleyen ödevler, kalan haklar.
- Ders takvimi ve ders detayı.
- Ödev listesi, detay, teslim ve geri bildirim.
- Devamsızlık ve ders geçmişi.
- Paketler ve ödeme planı; ödeme sorumlusuna göre görünürlük.
- Profil, bildirimler ve hesap ayarlarına geçiş.
- Veli görünümü: yalnızca doğrulanmış bağlı çocuklar arasında seçim.

### 7.2 Personel portalı — yeni üçüncü ürün

Yeni modülde route tabanlı gezinme kullanılır; mevcut ürünlerin sekmeleri/router yapısı değiştirilmez. Boş mevcut router'a yalnızca kurs yönetimi rotaları eklenir; kök ürün seçicisinin yeni modülü göstermesi için küçük kayıt eklenir.

Öğretmen: bugünkü dersler, öğrenciler, takvim, yoklama, ödevler. Kişisel çalışma alanında ayrıca paketler, alacaklar ve tahsilatlar.

Kurum: genel bakış, öğrenci kayıtları, öğretmenler, kurslar/gruplar, şube/derslik, kurum takvimi, yoklamalar, ödevler, ödeme planları/tahsilat, raporlar ve çalışma alanı ayarları.

MVP raporları açıkça şunlardır: aktif kayıt/öğrenci sayısı; gün/hafta öğretmen ve derslik programı; öğrenci/grup devamsızlığı; kullanılmamış ve süresi yaklaşan paket hakları; vadesi gelen/geciken taksitler; tarih/ödeme yöntemi bazlı tahsilat ve iade; öğrenci hesap ekstresi; ödev teslim oranı. Yetkili kullanıcı için CSV/PDF çıktıları P7'de tamamlanır. Öğrenci sayısı ve ders yükü raporları öğretmen hakedişi veya kâr hesabı olarak sunulmaz.

Platform aboneliği ayrı ekrandır. Kurumda çalışan öğretmen varsayılan olarak kurum tahsilatlarını göremez. Öğretmen kendi kişisel alanında finans yetkisine sahiptir.

Platform aboneliğinin ilk sürümü: admin tarafından öğretmen/kurum planı, aktif öğrenci limiti, başlangıç-bitiş ve erişim durumu yönetimi; kullanıcının kendi planını görmesi. Otomatik abonelik satışı, kart tahsilatı ve tahsilat komisyonu MVP dışındadır. Abonelik bittiğinde kayıtlar silinmez; yetkili geçmiş görüntüleme ve veri dışa aktarma korunur, yeni operasyon oluşturma politikası P0'da sabitlenir.

### 7.3 Admin modülü

- Kurs yönetimine katılan kurumlar ve bağımsız öğretmen çalışma alanları.
- Ürün erişimi/davet destek işlemleri; yerel profil ve üyelik durumu.
- Ürün aboneliği ve limit yönetimi; kurum öğrenci borçlarından ayrı.
- İşletim özeti, başarısız aktarım ve mesaj işlemleri.
- Yeni ürüne ait audit ve destek görünümü.

Global yönetici görünümü açık bir global scope ile çalışır. Kurum API'sinin tenant sınırı devre dışı bırakılmaz. Gereken hassas destek erişimi izin ve audit ile sınırlanır.

## 8. API ve mesajlaşma yaklaşımı

Yeni domain API'leri `/api/course-management/v1` altında toplanır. Mevcut `/api/students`, `/api/teachers`, `/api/sessions`, `/api/assignments` yolları yeniden kullanılmaz.

Örnek kaynaklar:

```text
/me/profile
/me/workspaces
/workspaces/{workspaceId}/members
/workspaces/{workspaceId}/students
/workspaces/{workspaceId}/courses
/workspaces/{workspaceId}/groups
/workspaces/{workspaceId}/enrollments
/workspaces/{workspaceId}/lessons
/workspaces/{workspaceId}/lesson-series
/workspaces/{workspaceId}/payments
/workspaces/{workspaceId}/packages
/workspaces/{workspaceId}/assignments
/workspaces/{workspaceId}/imports
/admin/workspaces
```

Öğrenci self-service endpoint'leri hesabından öğrenciyi çözer; URL'deki öğrenci kimliğine güvenmez. Listeler sayfalı ve scope filtrelidir. Beklenen iş kuralı hataları belirgin kodlarla döner; eşzamanlı düzenleme için sürüm/optimistic concurrency kullanılır.

Yeni event örnekleri: `CourseManagementLessonScheduledV1`, `CourseManagementLessonRescheduledV1`, `CourseManagementAssignmentPublishedV1`, `CourseManagementPaymentRecordedV1`. Bildirim için gerekli minimum payload gönderilir. Event servis outbox'ıyla yazılır; tüketici inbox/idempotency ile ikinci bildirim oluşturmaz. Vade/ödev/ders hatırlatmaları yeni ürüne ait worker tarafından planlanır; ders ertelenince eski hatırlatma geçersizleşir. Mesaj gecikmesi veya Notification kesintisi domain işlemini geri almaz.

MVP bildirim kanalları uygulama içi ve mevcut altyapının desteklediği e-postadır. Ders değişikliği/iptali, yaklaşan ders, ödev atama/son tarih, ödeme vadesi ve devamsızlık için alıcı ve tercih kuralları tanımlanır. SMS/WhatsApp/push ayrı sağlayıcı ve maliyet gerektirdiğinden sonraki faza bırakılır; piyasada bulunması ilk sürümde hazır olduğu anlamına gelmez.

## 9. Aşamalar, bağımlılıklar ve çıkış koşulları

Tahminler 1 deneyimli geliştirici, mevcut altyapının kullanılabilir olması, kararlı kapsam ve düzenli geri bildirim varsayımıyladır. Takvim taahhüdü değildir. İlk iki faz sonunda tahmin yeniden yapılır. Test, inceleme ve temel dokümantasyon faz sürelerine dahildir.

| Faz | Süre aralığı | Bağımlılık | Teslim ve doğrulama |
|---|---|---|---|
| P0 — Hazırlık ve kontratlar | 3–5 iş günü | Yok | Korunan alan listesi, baseline, rol/veri sahipliği, ekran taslakları, aktarım alan matrisi, API/event ve iptal/ücret politikaları |
| P1 — Servis ve ayrı uygulama temeli | 4–6 iş günü | P0 | Yeni API/DB/migration, öğrenci uygulaması, staff/admin modül iskeleti, ayrı build ve health check |
| P2 — Identity ve çalışma alanları | 6–9 iş günü | P1 | Yeni/mevcut kurum ve öğretmen katılımı, öğrenci daveti, hesap bağlama, yerel profil, kurum/kişisel alan, izolasyon ve erişim iptali testleri |
| P3 — Öğrenci/kurs/grup kayıtları | 5–8 iş günü | P2 | Hesapsız öğrenci, veli ilişkisi, kayıt, öğretmen atama, kurum ve öğretmen listeleri |
| P4 — Takvim, tekrar ve yoklama | 8–12 iş günü | P3 | Tekrarlayan/tek ders, çakışma koruması, iptal/telafi, öğrenci takvimi ve yoklama |
| P5 — Finans ve ders paketleri | 10–15 iş günü | P3, P4 | Dönemlik/aylık/ders-saat/paket ücretleri, kısmi tahsilat, dağılım, paket hareketleri, iade ve raporlar |
| P6 — Ödev, portal ve bildirimler | 6–9 iş günü | P3; ders ekranları için P4, ödeme ekranları için P5 | Teslim/geri bildirim/dosya, öğrenci/veli görünümü, yeniden planlanan hatırlatmalar |
| P7 — Admin, aktarım ve pilot hazırlığı | 5–8 iş günü | P2–P6 | Merkezi ürün yönetimi, CSV aktarım, audit, export, yedek/restore ve uçtan uca doğrulama |
| P8 — Pilot ve sürüm kararı | 5–10 iş günü | P7 | Gerçek kurum/öğretmen senaryoları, engelleyici hataların kapanması, işletim runbook'u |

Revizyon sonrası toplam mühendislik tahmini: 52–82 iş günü, yaklaşık 11–17 çalışma haftası. Bu aralık doğrulanmış görev eforlarının toplamı değildir; aylık/ders bazlı modeller eklendiği için P5 için ek pay ayrılmış ön tahmindir. P0 sözleşme incelemesi ve P1–P2 gerçekleşen eforlarıyla yeniden hesaplanır. Pilotun gerçek gözlem süresi ayrıca 2–4 takvim haftası olabilir; P8 eforu bu gözlem süresine yayılır. Tatiller, entegrasyon beklemeleri ve kapsam değişiklikleri takvim süresini uzatabilir.

Kritik yol: P0 → P1 → P2 → P3 → P4 → P5 → P6'nın finans/ders ekranları → P7 → P8. Ödev geliştirmesi P3 sonrasında başlayabilir; aynı geliştirici için bu durum toplam eforu azaltmaz. Admin iskeleti P1, erişim yönetimi P2, final işletim ekranları P7'de tamamlanır. Öğrenci uygulaması P1'den itibaren vardır; son fazda açılmaz.

## 10. İlk dört somut teslim

### Teslim A — Ürün iskeleti ve koruma

- Yeni servis ve öğrenci uygulaması ayrı derlenir.
- Yeni domain yolu Gateway'den doğru servise gider.
- Staff/admin modülleri erişimsiz kullanıcılara görünmez.
- Yeni ürün kapalıyken eski ürünlerin baseline akışları geçer.

### Teslim B — Gerçek katılım

- Mevcut Identity hesabı yeni ürün davetini kabul eder; ikinci hesap oluşmaz.
- Yerel profil ve kişisel/kurum çalışma alanı oluşur.
- Aynı kullanıcı iki alan arasında geçer; veriler karışmaz.
- Tekrarlanan kabul/aktarım mükerrer profil veya üyelik üretmez.

### Teslim C — Dersin günlük yönetimi

- Kurum hesapsız öğrenci kaydeder, kurs/grup kaydı ve ders planı açar.
- Öğretmen aynı öğrenciye atanmış dersi görür ve yoklama alır.
- Öğrenci davet kabulünden sonra kendi takvimini görür.
- Çakışma, iptal, serinin tek oturumunu değiştirme ve telafi senaryoları geçer.

### Teslim D — Kullanılabilir MVP

- Taksitli kurs ve özel ders paketi uçtan uca çalışır.
- Aylık ücret bir dönemde bir kez borçlandırılır; ders/saat bazlı özel ders doğru süre ve anlaşma oranıyla borçlandırılır.
- Kısmi ödeme/dağılım/iade ve paket tüketimi doğru hesaplanır.
- Ödev verilir, öğrenci teslim eder, öğretmen değerlendirir.
- Kurum, bağımsız öğretmen, öğrenci/veli ve admin senaryoları birlikte geçer.
- Veri aktarımı, audit, yedek/restore ve ürünler arası izolasyon doğrulanır.

## 11. Test ve sürüm kapıları

### Domain testleri

- İptal politikası × katılım × ücret modeli sonucunun doğrulanması.
- Paket tüketimi, tekrar işleme, telafi ve geçerlilik süresi.
- Taksit yuvarlama, kısmi tahsilat, dağılım limiti, iade/ters işlem.
- Tekrarlayan planın tek/gelecek oturum değişikliği.

### Integration/contract testleri

- Eski ürün token'ıyla yeni ürüne ve yeni ürün token'ıyla eski ürüne erişim reddi.
- Yetkisiz workspace/student/lesson/payment/attachment kimliğiyle erişim reddi.
- Kurumun öğretmenin kişisel öğrencilerini ve finansını görememesi.
- Öğretmenin kurum finansını varsayılan olarak görememesi.
- Veli ilişkisinin ve öğrencinin self-service kapsamının doğrulanması.
- Aynı anda çakışan ders, çift tahsilat ve çift paket kullanımının engellenmesi.
- Katılımın kısmi başarısızlığında güvenli tekrar deneme.
- Yeni kurum/öğretmen self-servis katılımı, mevcut kurum/öğretmen/öğrenci kimlik eşleştirmesi ve seçili üyelerin aktarımı.
- Aylık borcun çift oluşmaması; iptal/dondurma ve fiyat değişikliğinin geçmiş borcu değiştirmemesi.
- Grup kapasitesi, üyelik iptali, öğretmen ayrılması ve abonelik sonlanmasının kayıt geçmişini koruması.
- Aynı tarayıcıda eski ve yeni ürün sekmeleri arasında session/cookie izolasyonu.
- Broker kesintisi, event yeniden teslimi ve hatırlatma iptali.
- Dosya erişimi, karantina, limit ve yetki kaldırıldığında erişim reddi.

### E2E

1. Kurum → öğrenci kaydı → kurs kaydı → taksit → ders → yoklama → tahsilat.
2. Bağımsız öğretmen → paket → tekrarlayan ders → iptal/telafi → kalan hak.
3. Öğretmen → ödev → öğrenci teslimi → geri bildirim.
4. Veli → çocuk seçimi → ders/ödeme görünürlüğü.
5. Aynı kişi → ürün geçişi → kurum/kişisel alan geçişi → eski ürüne dönüş.
6. Admin → yeni ürün yönetimi; eski admin ekranlarının erişim ve davranış kontrolü.

Her fazda ilgili testler çalışır. Ortak auth/gateway/panel kökü değiştiğinde ilgili eski ürün regresyonları da çalışır. Son pilot kapısında tam kritik akış paketi, mobil görünüm ve erişilebilirlik kontrolleri yapılır. Mevcut testlerin varlığı başarı kanıtı sayılmaz; çalıştırılmış sonuç kaydedilir.

## 12. Yayın ve işletim

- Yeni servis, worker, öğrenci uygulaması, staff modülü ve admin modülü için ayrı sürüm kayıtları.
- Yeni servis/öğrenci uygulaması ayrı deploy edilir; staff/admin modülleri host uygulamalarıyla birlikte derlenir. Bu iki modül için fiziksel olarak ayrı deploy garanti edilmez.
- Önce test ortamında Identity/Gateway eklemeleri, yeni servis ve frontend'ler doğrulanır; üretimde eksik backend varken menü açılmaz.
- Yeni ürün yayın etkinleştirmesi ortak servislerde geri uyumlu eklemelerden sonra yapılır.
- Kurs DB/storage backup ve restore senaryosu pilot öncesi denenir.
- Ders çakışması reddi, tahsilat hatası, event gecikmesi ve aktarım hatası metrikleri.
- Rollback: yeni ürün giriş/menüsünü kapatma ve yeni uygulama/servisi geri alma. Eski ürün tabloları değiştirilmediği için onlara veri rollback'i gerekmez.
- Ortak ürün kayıtları geri alınırken yeni ürünle üretilmiş Identity satırları nedeniyle enum tanımsızlığı yaratılmaz; çoğunlukla kayıtları koruyup erişim/yayın kapatılır.
- Profil silme, hesap kapatma, veri export'u ve finans kayıtlarının saklama kuralları ayrıdır. Hesap kapatma, gerekli finansal geçmişi otomatik silmez; yasal saklama ve anonimleştirme kararları yayın öncesi netleştirilir.

## 13. MVP sonrası geliştirme sırası

1. Öğretmen hakedişi: ücret anlaşması, tamamlanan derslerden dönemlik hesap, onay ve ödeme; geçmiş oran snapshot'ı.
2. Online ödeme: kurum/öğretmenin kendi tahsilat hesabı, webhook doğrulama, idempotency ve mutabakat. Platformun parayı toplaması/dağıtması ayrı ürün ve hukuki karardır.
3. Sekreter/finans rolleri, şube bazlı yetkiler, franchise/şube konsolidasyonu ve ayrıntılı yetkiler. Temel şube/derslik tanımı MVP'dedir.
4. Öğrenci rezervasyonu, kontenjan/bekleme listesi ve telafi talebi.
5. Aday öğrenci CRM ve kayıt yenileme.
6. Harici takvim ve gizlilik koruyan alanlar arası müsaitlik.
7. Resmi e-belge/muhasebe entegrasyonları ve gelişmiş yönetim raporları.
8. SMS/WhatsApp/push kanalları, isteğe bağlı kurum markası ve online başvuru formları.

Video altyapısı, soru bankası, yapay zekâ değerlendirmesi, diğer ürünlerin özel raporlarını birleştirme ve tam bordro bu yol haritasının MVP kapsamı dışındadır.

## 14. Başlamadan önce P0'da somutlaştırılacak kararlar

- Öğrenci uygulamasının host/path'i ve güvenli cookie/session paylaşım sınırı.
- Erken/geç iptal eşiği, gelmeme ve öğretmen iptali sonuçları.
- Paket hakkının hangi iş olayında tüketileceği; taksitli kaydın dondurma/iade davranışı.
- Öğrencinin ödeme bilgisi görünürlüğü ve velinin bağlanma/doğrulama akışı.
- Dosya tür/boyut/saklama sınırları, hukuki metinler ve veri işleme dayanakları.
- İlk pilot kurum/öğretmenler, tipik öğrenci/ders hacmi ve ölçülecek operasyon süreleri.

Bu kararların çıktısı ekran taslakları, örnek iş senaryoları ve test edilebilir kabul koşulları olur. Uygulama geliştirmesine bu belgelerden başlanır; ürünler arası ortaklaştırma veya eski özelliklerin yeniden yazımıyla başlanmaz.

## 15. Araştırma ile özellik eşleştirmesi

12 Ekim 2026 kontrolünde ürünlerin resmi sayfaları yeniden okunmuştur. Buradaki bilgiler üretici tarafından açıklanan özelliklerdir; bağımsız kullanım testi, pazar payı, fiyat veya entegrasyon uygunluğu doğrulaması değildir. Özelliklerin MVP/sonraki faz seçimi bizim kapsam kararımızdır.

| Araştırma kaynağı | Doğrulanan özellik grubu | Bu plandaki karşılığı |
|---|---|---|
| [AKINSOFT resmi yardım dosyası](https://bilgibankasi.akinsoft.net/tr/home/makale/1622-akinsoft-kurs-otomasyon-yardim-dosyasi) | Öğrenci/grup/sınıf, ders programı, bakiye/taksit ve öğretmen ödemeleri | Öğrenci, takvim, taksit MVP; öğretmen hakedişi sonraki faz |
| [ArtechSchool özellikleri](https://www.artechschool.com/ozellikler) | Öğrenci, ders, ödeme, CRM ve kurum operasyonları | Öğrenci/ders/ödeme MVP; CRM ve daha geniş entegrasyonlar sonraki faz |
| [EduSMS resmi sayfası](https://edusms.com.tr/) | Öğrenci/veli kaydı, ders programı, yoklama, ödeme planı ve hatırlatmalar | Veli, kurs kaydı, takvim, yoklama ve tahsilat MVP; SMS kanalı sonraki faz |
| [TutorCruncher özellikleri](https://tutorcruncher.com/features) | Müşteri yönetimi, takvim, faturalama/öğretmen ödemeleri, iletişim ve raporlar | Çalışma alanı/takvim/finans/rapor MVP; hakediş, online ödeme, CRM ve marka özelleştirme sonraki faz |
| [Teachworks resmi sayfası](https://www.teachworks.com/) | Bireysel/grup dersleri, öğretmen müsaitliği, oda planlama, öğrenci yönetimi ve öğretmen saat/ücret takibi | Takvim/oda/öğrenci MVP; hakediş ve ileri entegrasyonlar sonraki faz |
| [TutorBird öğrenci portalı](https://www.tutorbird.com/student-portal/) | Öğrenci/veli portalı, rezervasyon, telafi, bakiye ve çalışma dosyaları | Portal/telafi/ödev teslimi MVP; self-servis rezervasyon sonraki faz; çalışma süresi günlüğü kapsam dışında |
| [Classcard resmi sayfası](https://www.classcardapp.com/) | Takvim/yoklama, rezervasyon, üyelik/ücret, materyal ve raporlar | Takvim/yoklama/paket/materyal/rapor MVP; rezervasyon/online ödeme/markalı uygulama sonraki faz |

Araştırmada görülen her özellik ilk sürüm gereksinimi değildir. Çalışma süresi günlüğü, deneme sınavı/optik analizi, kartlı geçiş ve video ders altyapısı talep edilen günlük kurs operasyonlarının dışında tutulur. Sektörel ihtiyaç pilotta doğrulanırsa ayrı kapsam olarak değerlendirilir; koçluk/hızlı okuma özelliği buraya taşınmaz.

## 16. Kullanıcı ihtiyaçları için izlenebilirlik ve kontrol sonucu

| İnsan tarafından istenen ihtiyaç | Plan karşılığı | Faz | Kabul kanıtı |
|---|---|---|---|
| Mikroservis projesine yeni servis | Ayrı servis, DB ve API yolu | P1 | Yeni API bağımsız çalışır; eski servis diff'i yok |
| Kursların öğrencilerini takip etmesi | Öğrenci kartı, kurs/grup kaydı, veli ve geçmiş | P3 | Hesaplı/hesapsız öğrenci kursa kaydedilir ve geçmiş izlenir |
| Ders saatlerini planlama | Tekrarlayan/tek ders, oda ve öğretmen müsaitliği | P4 | Çakışma ve seri değişikliği senaryoları geçer |
| Ödemeleri takip etme | Dört ücret modeli, taksit, tahsilat, bakiye ve iade | P5 | Anlaşma → borç → kısmi ödeme → kalan tutar doğru hesaplanır |
| Öğretmenin özel derslerini takip etmesi | Bağımsız öğretmen çalışma alanı ve finansı | P2–P5 | Kurumda çalışan öğretmenin kişisel alanı ayrı çalışır |
| Öğrenciye ödev verme | Yeni ürün ödev/teslim/değerlendirme modeli | P6 | Öğretmen verir, öğrenci teslim eder, geri bildirim görünür |
| Koçluk ve hızlı okumadan ayrı olması | Korunan alanlar, yeni modeller ve runtime bağımsızlık | Her faz | Veri/API/yetki ve eski akış regresyonları geçer |
| Kullanıcı Identity, profil burada | Ortak hesap; yerel ürün profili/üyelik | P2 | Profil güncellemesi başka ürün veya ortak hesaba yazmaz |
| Mevcut kurum/öğrenci/öğretmenin aktarımı | Ortak kimlik bağlama ve seçili üyelerin yetkili aktarımı | P0, P2, P7 | Alan matrisi, katılım ve mükerrer kayıt önleme doğrulanır |
| Öğrenci panelinin sıfırdan olması | Ayrı `clients/course-management` | P1, P4–P6 | Ayrı build/yayın; eski öğrenci ekranlarında değişiklik yok |
| Kurum/öğretmen portalına üçüncü ürün | Yeni staff feature modülü | P1–P6 | Eski ürünler korunarak yeni alan açılır |
| Admin genişletmesi | Ayrı admin feature modülü | P1, P2, P7 | Yeni ürüne özel yönetim ve audit |
| Diğer kısımların değişmemesi | Korunan dosyalar ve sınırlı ortak kayıt listesi | Her faz | İzin verilen kayıt noktaları dışında eski kod diff'i yok |
| Türkiye ve dünya araştırmasına uygunluk | Kaynak/özellik/faz matrisi | Plan ve pilot | Temel operasyonlar MVP; ertelenen farklar açık |

Kontrol sonucu: İnsan tarafından belirtilen ana ihtiyaçların tamamı revize planda karşılık ve kabul ölçütüne sahiptir. Bu sonuç teknik uygulanabilirliğin veya testlerin tamamlandığı anlamına gelmez. Özellikle kaynak verinin erişilebilirliği, ortak session davranışı, ücret/iptal politikaları ve gerçek kapasite hedefleri P0'da doğrulanacaktır.

Bu denetimde düzeltilen eksikler: araştırma kaynaklarının izlenebilirliği; aylık/ders-saat ücretlendirmesi; yeni kurum/öğretmen self-servis katılımı; mevcut kimlik ve seçili üyelerin aktarımı için açık kapılar; temel/ileri şube desteği ayrımı; rapor ve bildirim kanal kapsamı; platform aboneliği yaşam döngüsü; hesap/üyelik iptali ve çok sekmeli oturum koruması. Veli, hakediş, online ödeme, CRM ve diğer büyüme özellikleri ilk kullanıcı talebiyle araştırma önerilerinden ayrı olarak işaretlenmiştir; ilk sürüme otomatik geniş kapsam eklenmez.

Pilot başlangıcından önce hedef veri hacmi, eşzamanlı kullanıcı sayısı, öğrenci kaydı/ders planlama işlemlerinin kabul edilen süreleri ve performans sınırları yazılı olarak belirlenir. Bu ölçümler henüz verilmediğinden plan belirli bir ölçek veya gecikme garantisi iddia etmez.
