# Speed Reading Service

Speed Reading, Coaching'e bağlı olmayan ayrı bir bounded context olarak çalışır.
`masterhizliokuma.com` gibi bağımsız bir uygulama veya Eduİvme platformunun
Gateway'i arkasında aynı servis kullanılabilir.

Production'da bağımsız yayın için `speed-reading-service`,
`speed-reading-frontend` ve `speed-reading-edge` ayrı container'lar olarak
çalışır. LiteSpeed yalnızca `masterhizliokuma.com` vhost'unu loopback'teki
SpeedReading edge'ine yönlendirir; `eduivme.com` vhost'u platform edge'inde
kalır.

Bağımsız edge, Gateway'in tamamını dışarı açmaz. `/api/speed-reading` ve
SpeedReading'in kimlik/rol ile ilgili sınırlı `/api/v1` uyumluluk yolları
allowlist'tedir; edge bu yolları Gateway'in canonical `/api` sözleşmesine
çevirir. Coaching, ödeme, bildirim, yedekleme ve diğer platform yönetim
uçları bu domain üzerinden kapalıdır.

## Veri sahipliği ve geçiş güvenliği

Kaynak frontend `clients/speed-reading` altında bağımsız istemci olarak
izlenir. Production derlemesi statik bir Nginx image'ı ile paketlenebilir ve
okuma/ilerleme sözleşmeleri için `/api/speed-reading` kullanır. Eski yazma
kontratlarının okuma metni/egzersiz yolları da merkezi uçlara taşınmıştır;
idempotency ve audit sözleşmeleri tüm içerik komutlarında korunur.

- Owned modda servis `ConnectionStrings:SpeedReadingOwned` veya
  `SPEED_READING_OWNED_CONNECTION_STRING` ile kendi `speed_reading` şemasına
  bağlanır; legacy `SpeedReadingDbContext` ve legacy bağlantı runtime'a hiç
  kaydedilmez. `speed-reading-migrations` owned modda yalnızca owned EF
  migration geçmişini uygular.
- `ConnectionStrings:SpeedReading` / `SPEED_READING_CONNECTION_STRING` yalnızca
  legacy fallback veya açıkça çalıştırılan backfill/migration komutları için
  gereklidir. Owned modda boş bırakılabilir.
- Yeni servis veri sahibi olarak devreye alınmadan önce tüm backfill/parity,
  eski yazma yollarının kapatılması, bağımsız backup/restore ve production E2E
  doğrulaması tamamlanmalıdır.

Kaynak repo ve tablo eşleştirmesi için [Hızlı Okuma Veri Taşıma ve Uyumluluk
Planı](./SPEED_READING_DATA_MIGRATION.md) takip edilir. İlk compatibility
sürümü mevcut `ExerciseTypes`, `Exercises`, `ReadingTexts` ve
`ReadingQuestions` tablolarını değiştirmeden okuyan geçiş aşamasıydı; güncel
owned runtime kendi migration geçmişi ve tabloları üzerinden çalışır.

## Çalışma modları

`SpeedReading:Mode` iki değerden biridir:

- `Standalone`: Coaching entegrasyonu kapalıdır; hızlı okuma uygulaması tek
  başına çalışır.
- `Platform`: Eduİvme Gateway ve yetki sözleşmeleri üzerinden platforma
  bağlanır. Coaching, Notification veya Subscription entegrasyonları ayrıca
  açıkça etkinleştirilir.

Varsayılan mod `Standalone`'dır. Standalone modda Coaching entegrasyonu
etkinleştirilirse servis açılışta durur; böylece bağımsız uygulama yanlışlıkla
platform bağımlılığına dönüşmez.

## Gateway ve Compose

Gateway dışarıya `/api/speed-reading` rotasını sunar ve iç ağda
`speed-reading-service:8080` hedefine yönlendirir. Geliştirme ortamında servis
`localhost:5004` üzerinde çalıştırılabilir.

Base Compose'da servis `speed-reading` profiliyle isteğe bağlıdır:

```powershell
docker compose --profile speed-reading up -d speed-reading-service
```

Staging ve production overlay'leri profili kaldırır. Owned modda
`SPEED_READING_OWNED_CONNECTION_STRING` gerekir; legacy/backfill aşamasında
`SPEED_READING_CONNECTION_STRING` da ayrıca verilir.
Öğretmen/veli kurum kapsamı Identity üzerinden doğrulandığı için aynı
ortamlarda `INTERNAL_SERVICE_API_KEY` zorunludur; Compose bu anahtarı
`http://identity-service:8080` adresine giden çağrılar için sağlar. Servis
container'ında `localhost` Identity adresi olarak kullanılmaz.
Hızlı okuma veritabanı platform PostgreSQL container'ına taşınmadığı sürece
business tabloları platform migration zincirine dahil edilmez; one-shot
container owned Speed Reading veritabanı hazır olduktan sonra kendi EF migration
geçmişini uygular. Backfill ve parity komutları iki bağlantıyı açıkça kullanır;
normal owned runtime legacy veritabanına bağlanmaz.

İlk içerik API'leri:

- `GET /api/speed-reading/exercise-types` ve `/exercise-types/categories`
- `GET /api/speed-reading/exercises`
- `GET /api/speed-reading/reading-texts` — `category`, `difficultyLevel`,
  `targetAgeGroupId`, `searchTerm`, `onlyWithQuestions` ve `isActive` filtreleri
- `GET /api/speed-reading/reading-texts/categories` ve `/levels`
- `GET /api/speed-reading/reading-texts/short?limit=10` — aktif, 200 kelimeyi
  geçmeyen RSVP metinleri (limit 1–50 aralığında sınırlandırılır)
- `GET /api/speed-reading/reading-texts/{id}?includeQuestions=true` — içerik
  yönetim yetkisi olmayan kullanıcılar pasif metinleri göremez.

- `POST /api/speed-reading/reading-texts/import/csv`, `/import/excel` ve
  `/import/bulk` — yönetici yetkisiyle 10 MB/500 satır sınırları, soru kolonları,
  satır bazlı sonuç ve idempotency desteğiyle merkezi içe aktarma.
- `GET /api/speed-reading/reading-texts/{id}/export/pdf|docx` ve
  `POST /api/speed-reading/reading-texts/export/pdf|docx` — içerik yönetim
  yetkisiyle gerçek PDF/DOCX çıktısı, tekli/toplu seçim ve dosya adı temizleme.
- `GET /api/speed-reading/progress/reading-history`
- `GET /api/speed-reading/progress/reading-statistics`
- `GET /api/speed-reading/progress/exercise-results`
- `POST /api/speed-reading/progress/exercise-results` (`Idempotency-Key`
  başlığı ile; tekrar istekler güvenli biçimde replay edilir)
- `POST|PUT|DELETE /api/speed-reading/exercise-types` (`ContentManage`)
- `POST|PUT|DELETE /api/speed-reading/exercises` (`ContentManage`)
- `POST|PUT|DELETE /api/speed-reading/reading-texts` (`ContentManage`; sorusu
  bulunan metinler silinemez)
- `POST|PUT|DELETE /api/speed-reading/reading-questions` (`ContentManage`; dört
  seçenek ve A-D doğru cevap doğrulaması)
- `GET /api/speed-reading/program-templates/admin` ve
  `POST|PUT|DELETE /api/speed-reading/program-templates` (`ProgramManage`;
  ilerlemesi bulunan şablonlar silinemez)
- `GET /api/speed-reading/learning-paths/templates/admin` ve
  `POST|PUT|DELETE /api/speed-reading/learning-paths/templates` (`ProgramManage`;
  düğüm veya ilerlemesi bulunan şablonlar silinemez)
- `GET /api/speed-reading/learning-paths/templates/{id}/admin` ve
  `POST|PUT|DELETE /api/speed-reading/learning-paths/nodes` (`ProgramManage`;
  ebeveyn şablon/döngü doğrulaması)
- `POST|PUT|DELETE /api/speed-reading/learning-paths/node-contents` ve
  `POST|DELETE /api/speed-reading/learning-paths/prerequisites` (`ProgramManage`;
  tek içerik referansı ve önkoşul grafiği doğrulaması)
- `GET /api/speed-reading/progress/active-exercise-sessions`
- `GET /api/speed-reading/program-templates`
- `GET /api/speed-reading/progress/programs`
- `GET /api/speed-reading/progress/daily-exercise-logs`
- `GET /api/speed-reading/analytics/student/summary` — oturum açmış öğrencinin
  en fazla 366 günlük okuma/egzersiz özetini ve gün bazlı serisini döndürür;
  başka kullanıcı kimliği kabul etmez. Özet ayrıca son WPM/anlama değeri,
  gamification seviyesi/serisi/XP ve kazanım sayısını, öğrencinin günlük hedefi
  ile tarih aralığındaki hedef tamamlama oranını ve son beş başarımı içerir.
- `GET /api/speed-reading/analytics/student/reading-speed` ve
  `GET /api/speed-reading/analytics/student/comprehension` — token sahibinin
  en fazla 366 günlük okuma hızı/anlama ayrıntılarını, trendlerini, kurum ve
  platform karşılaştırmalarını döndürür. Soru türü bazlı anlama verisi, legacy
  `ReadingSessions` tablosunda cevap ayrıntısı bulunmadığı için açıkça boş
  döner; merkezi cevap olayı taşınmadan sahte değer üretilmez. Kurum/platform
  benchmarkları 5 dakikalık zaman kovası anahtarı ve 5 dakikalık TTL ile sınırlı
  memory cache’te korunur (4.096 kayıt üst sınırı); analytics indexleri
  `003_analytics_indexes.sql` ve öğrenci kapsamlı sorgu indexleri
  `004_student_analytics_indexes.sql` ile migration-only başlangıçta
  idempotent olarak hazırlanır.
- `GET /api/speed-reading/analytics/student/series` — token sahibinin en fazla
  366 günlük program ilerlemesini, aktif programlarını ve tamamlanma trendini
  döndürür. Seri verisi `StudentProgramProgresses`, program şablonları ve
  `DailyExerciseLogs` içindeki gerçek gün ilerlemelerinden hesaplanır; arayüzde
  birimler egzersiz sayısı değil tamamlanan/toplam program günü olarak
  gösterilir. Legacy şemada seri-özel kilometre taşı ilişkisi bulunmadığı için
  kilometre taşı listesi boş ve açıkça eksik veri olarak kalır; genel
  başarımlar seri kilometre taşı gibi gösterilmez.
- `GET /api/speed-reading/analytics/student/activity` — token sahibinin en
  fazla 366 günlük okuma oturumları ile günlük egzersiz kayıtlarını birleştirir;
  aktivite ısı haritası, saat/gün dağılımı, gerçek çalışma süresi ve aralık içi
  streak değerlerini döndürür. Veri yoksa `DataAvailable=false` ve açıklama
  döner; boş grafikler başarı gibi yorumlanmaz. Her iki uç da öğrenci kimliğini
  query parametresinden almaz.
- `GET /api/speed-reading/analytics/teacher/students/{studentId}/reading-speed`
  ve `/comprehension` — Identity tarafından öğrenci okumaya yetkilendirilmiş
  öğretmen/kurum/veli kapsamının seçtiği öğrencinin en fazla 366 günlük raporunu
  döndürür. `studentId` tek başına yetki sağlamaz; servis her istekte token
  sahibini Identity'nin `authorize-student-read` sözleşmesiyle doğrular.
- `GET /api/speed-reading/analytics/teacher/students/{studentId}/activity` —
  aynı Identity kapsam kontrolüyle öğretmen/kurum/veli için öğrencinin okuma ve
  günlük egzersiz aktivitesini döndürür. Öğrenci kimliği yalnızca route filtresidir;
  yetki kontrolü tamamlanmadan analitik sorgu çalıştırılmaz.
- `GET /api/speed-reading/analytics/teacher/class-overview` — token sahibinin
  Identity'den çözülen kurum/atama öğrenci kapsamındaki sınıf özetini döndürür.
  İstemciden `teacherId` alınmaz; tarih aralığı en fazla 366 gündür. Legacy hızlı
  okuma `Users` tablosunda rol bulunmadığı için dönemsel aktif öğrenci metriği
  `ActiveStudentsDataAvailable=false` ile açıkça veri yokluğu olarak gösterilir;
  okuma/anlama oturumu olmayan aralıklarda sınıf ortalaması ve performans dağılımı
  da `ClassAverageWpmDataAvailable` ve
  `ClassAverageComprehensionDataAvailable` bayraklarıyla `—` gösterilir.
- `GET /api/speed-reading/analytics/teacher/assignments` — aynı merkezi öğretmen
  kapsamıyla mevcut öğretmen atama raporu sözleşmesini döndürür. Atama CRUD
  akışı `/api/speed-reading/assignments` altında taşınmış olsa da bu eski
  rapor sözleşmesinin kaynak `ReportsController` karşılığı bulunmadığından
  `DataAvailable=false` döner; eksik rapor verisi tahmin edilmez.
- `GET /api/speed-reading/analytics/teacher/content-analysis` ve
  `/time-progress` — Identity öğrenci kapsamı içinde gerçek egzersiz/okuma içerik
  analizini ve günlük/haftalık/aylık ilerleme trendlerini döndürür. Her iki uçta
  da `teacherId` query parametresi yoktur; servis erişimi token + `INTERNAL_SERVICE_API_KEY`
  ile doğrulanır.
- `GET /api/speed-reading/analytics/admin/teachers/{teacherId}/class-overview`,
  `/assignments`, `/content-analysis` ve `/time-progress` — `ReportView` yetkili
  kurum yöneticisi/SystemAdmin'ın seçtiği öğretmen için raporları döndürür.
  Identity hedef öğretmenin aktif kurumunu ve yöneticinin kurum üyeliğini ayrıca
  doğrular; öğretmenler yalnızca kendi kimlikleri için bu kapsamı kullanabilir.
- `GET /api/speed-reading/analytics/admin/platform-usage` — `PlatformAnalyticsView`
  yetkisiyle platform genelindeki gerçek hızlı okuma kullanım metriklerini
  döndürür: toplam/aktif kullanıcı, okuma oturumu, aktivite hacmi, günlük ve
  saatlik dağılım, elde tutma oranı ve popüler okuma metinleri. Tarih aralığı
  en fazla 366 gündür ve kimlik/kurum kapsamı query parametresinden alınmaz;
  admin yetkisi access token'dan değerlendirilir. Legacy `Users` tablosunda
  kayıt oluşturma zamanı ve kurum adı bulunmadığı için yeni kullanıcı büyümesi
  `NewUserDataAvailable=false`; kayıt tarihi Identity'den ayrıca taşınmadığı
  için bu alan tahmin edilmez veya uydurulmaz.
- `GET /api/speed-reading/analytics/admin/content-analysis` —
  `PlatformAnalyticsView` ile korunan içerik analizi; aktif egzersiz/okuma
  katalog sayılarını, seçilen aralıktaki gerçek kullanım ve performanslarını,
  zorluk/tür kırılımlarını, en çok/en az kullanılan içerikleri ve popüler
  okuma kategorilerini döndürür. Atama ve eğitim serisi tabloları bu bounded
  context'in legacy şemasında bulunmadığı için `AssignmentDataAvailable=false`
  ve ilgili desteklenmeyen alanlar tahmin edilmeden boş/0 döner.
- `GET /api/speed-reading/analytics/admin/system-health` —
  `PlatformAnalyticsView` ile korunan öğrenme performansı özeti; WPM/anlama,
  tamamlanan egzersiz, cevaplanan soru, başarı oranı ve günlük performans
  trendini döndürür. Hızlı okuma legacy veritabanında operasyonel hata,
  kullanıcı memnuniyeti veya sağlık telemetrisi tutulmadığı için bu alanlar
  `...DataAvailable=false` ile açıkça işaretlenir; sahte bir sağlık skoru,
  hata oranı veya uyarı üretilmez.
- `GET /api/speed-reading/analytics/admin/institutions` —
  `PlatformAnalyticsView` ile korunan kurum karşılaştırma raporu. Kurum adı,
  aktiflik ve öğrenci/öğretmen/admin sayıları Identity'nin yalnızca iç ağda
  `GET /api/internal/reporting/speed-reading/institutions` sözleşmesinden;
  kullanıcı, aktivite ve performans metrikleri ise hızlı okuma veritabanından
  gelir. Kurum adı veya kullanıcı rolü hızlı okuma veritabanından türetilmez;
  Identity çağrısı `INTERNAL_SERVICE_API_KEY` ile doğrulanır. Tarih aralığı
  en fazla 366 gündür. Legacy `Users` tablosunda rol bulunmadığı için dönemsel
  aktif öğrenci sayısı veri yokluğu olarak işaretlenir; arayüzde sahte toplam
  öğrenci değeri gösterilmez.
- `GET /api/speed-reading/analytics/admin/programs` — `PlatformAnalyticsView`
  ile korunan program analitiği. Aktif öğrenci, program dağılımı, haftalık
  ilerleme ve son öğrenci aktiviteleri merkezi servisten hesaplanır; kaynak
  projedeki `/student-progress/analytics/dashboard` çağrısı bu endpoint'e
  taşınmıştır.
- `GET /api/speed-reading/student-progress` ve `/{id}` — `ReportView` ile
  korunan sayfalı admin ilerleme listesi ve son 30 günlük egzersiz detayları;
  `POST /{id}/reset` yalnızca `ProgramManage` yetkisiyle ilerlemeyi sıfırlar.
- `POST /api/speed-reading/assignments`, `GET /assignments/my-assignments` ve
  `GET /assignments/teacher-assignments` — öğretmen/öğrenci ödev akışı;
  assignment ve student-assignment kayıtları mevcut legacy şemada korunur.
  Öğretmen detay, silme ve öğrenci ekleme/çıkarma uçları da aynı merkezi
  route altında çalışır.
- `GET /api/speed-reading/reports/templates` ve
  `GET /api/speed-reading/reports/templates/{id}` — `ReportView` yetkisiyle
  mevcut rapor şablonlarını değiştirmeden merkezi servisten okur; SystemAdmin
  dışındaki rollere Admin türü şablonlar gösterilmez, özel şablonlar yalnızca
  sahibine görünür ve liste üst sınırı 100'dür.
- `GET /api/speed-reading/reports/snapshots` ve
  `GET /api/speed-reading/reports/snapshots/{id}` — `ReportView` yetkisi olan
  oturum açmış kullanıcının yalnızca kendi snapshot geçmişini ve ayrıntısını
  döndürür; kullanıcı kimliği route/query üzerinden alınmaz. Çok büyük
  `DataJson` cevapları 1 MB sınırında açıkça `DataJsonTruncated` olarak işaretlenir.
  Speed-reading istemcisinin snapshot okuma çağrıları bu merkezi uçları kullanır;
  `POST|DELETE /api/speed-reading/reports/snapshots` ise kullanıcının kendi
  snapshot kayıtlarını idempotency ve sahiplik kontrolüyle yönetir. Rapor
  snapshot PDF/DOCX üretimi ise eski rapor pipeline'ına bağlı kaldığı için
  frontend'de hâlâ legacy uyumluluk yolundadır.
- `POST /api/speed-reading/reports/export/pdf` ve
  `POST /api/speed-reading/reports/export/excel` — `ReportView` yetkisiyle
  istemcinin rapor verisini gerçek PDF veya OpenXML XLSX dosyasına dönüştürür;
  veri 1 MB ve 1.000 alan sınırlarıyla işlenir.
- `POST /api/speed-reading/cms/newsletter/unsubscribe` — public unsubscribe
  linkindeki subscriber `Guid` token'ını doğrular ve kaydı pasifleştirir;
  tekrar istekleri idempotenttir.
- `GET /api/speed-reading/reports/scheduled` — `ReportView` yetkisi olan
  kullanıcının kendi zamanlanmış raporlarını döndürür.
- `GET /api/speed-reading/reports/scheduled/{id}` — token sahibinin tek
  zamanlanmış raporu; başka kullanıcıların kayıtları `404` döner.
- `POST|PUT|DELETE /api/speed-reading/reports/scheduled` ve
  `PATCH /api/speed-reading/reports/scheduled/{id}/status` — `ReportManage`
  yetkisi, kullanıcı sahipliği, aktif şablon kontrolü ve `Idempotency-Key`
  ile zamanlama yönetimi. Zamanlar Europe/Istanbul yerel saati olarak alınır
  ve UTC saklanır; soft-delete edilen kayıtlar geçmişi korur.
- `GET /api/speed-reading/learning-paths/templates`
- `GET /api/speed-reading/learning-paths/progress`
- `GET /api/speed-reading/learning-paths/personalized`
- `GET /api/speed-reading/learning-paths/personalized/next` — ilk sıradaki aktif,
  kilidi açılmış ve tamamlanmamış kişiselleştirilmiş içerik; uygun kayıt yoksa `204`.
- `GET /api/speed-reading/gamification/user` — oturum açmış öğrencinin XP,
  seviye ve streak özeti; kayıt yoksa varsayılan boş özet döner, tabloya yazmaz.
- `GET /api/speed-reading/gamification/achievements` ve
  `GET /api/speed-reading/gamification/achievements/user` — aktif kazanım
  kataloğu ve öğrencinin açtığı kazanımlar.
- `GET /api/speed-reading/gamification/leaderboard` — `LeaderboardView` yetkisi ile
  sayfalı XP/seviye/streak liderlik tablosu. SystemAdmin global listeyi görür;
  diğer kullanıcılar yalnızca legacy `Users.InstitutionId` ile çözülen kendi
  kurumlarının listesini görür. Kurum kapsamı çözülemeyen kullanıcıya global
  veri döndürülmez.
- `GET /api/speed-reading/achievements/admin` ve
  `POST /api/speed-reading/achievements`, `PUT|DELETE
  /api/speed-reading/achievements/{id}` —
  `GamificationManage` yetkisi ile kazanım yönetimi; idempotency ve audit
  kuralları uygulanır, öğrenci tarafından açılmış kazanımlar fiziksel olarak
  silinmez.

## Yetki sınırı

Identity permission seed'i aşağıdaki bağımsız anahtarları sağlar:

- `Permissions.SpeedReading.View`
- `Permissions.SpeedReading.ContentManage`
- `Permissions.SpeedReading.ProgramManage`
- `Permissions.SpeedReading.ProgressView`
- `Permissions.SpeedReading.ReportView`
- `Permissions.SpeedReading.PlatformAnalyticsView` (yalnızca SystemAdmin)
- `Permissions.SpeedReading.ReportManage`
- `Permissions.SpeedReading.LeaderboardView`
- `Permissions.SpeedReading.GamificationManage`
- `Permissions.SpeedReading.SettingsManage`

Kurum rolleri varsayılan olarak yalnızca görünürlük, ilerleme, rapor ve kurum
kapsamlı liderlik tablosu okuma
yetkilerini alır; içerik/program/ayar değişiklikleri SystemAdmin veya açıkça
atanmış yetki gerektirir.

Gamification yazma uç noktaları idempotency ve audit ile korunur. Öğrenci
istemcisindeki `awardXP`, `checkAchievements`, streak ve showcase çağrıları da
`/api/speed-reading/gamification` altındaki merkezi uçları kullanır; eski
`/v1/gamification` yolu için Caddy uyumluluk alias'ı yalnızca geçişteki eski
istemcileri destekler. Gamification okuma yolları (`user`, `achievements`,
`achievements/user`, `leaderboard`) ve admin kazanım çağrıları yeni servisi
kullanır.
