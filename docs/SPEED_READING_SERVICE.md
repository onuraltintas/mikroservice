# Speed Reading Service

Speed Reading, Coaching'e bağlı olmayan ayrı bir bounded context olarak çalışır.
`masterhizliokuma.com` gibi bağımsız bir uygulama veya Eduİvme platformunun
Gateway'i arkasında aynı servis kullanılabilir.

## Veri sahipliği ve geçiş güvenliği

Kaynak frontend `clients/speed-reading` altında bağımsız istemci olarak
izlenir. Production derlemesi statik bir Nginx image'ı ile paketlenebilir ve
okuma/ilerleme sözleşmeleri için `/api/speed-reading` kullanır. Eski yazma
kontratları, idempotency ve audit dilimi tamamlanana kadar ayrı tutulur.

- Mevcut hızlı okuma veritabanı korunur; servis mevcut business tabloları için
  migration çalıştırmaz ve şemayı değiştirmez. Yazma dilimi için ayrı
  `speed-reading-migrations` one-shot container'ı yalnızca
  `SpeedReadingIdempotencyRecords` ve `SpeedReadingAdminAuditRecords`
  tablolarını oluşturur.
- `ConnectionStrings:SpeedReading` veya `SPEED_READING_CONNECTION_STRING`
  zorunludur. Bağlantı bilgisi yoksa servis güvenli şekilde başlamaz.
- Yeni servis veri sahibi olarak devreye alındığında eski uygulamanın yazma
  yolları kapatılmalı, önce salt-okunur doğrulama ve geri dönüş planı
  tamamlanmalıdır.

Kaynak repo ve tablo eşleştirmesi için [Hızlı Okuma Veri Taşıma ve Uyumluluk
Planı](./SPEED_READING_DATA_MIGRATION.md) takip edilir. İlk sürüm mevcut
`ExerciseTypes`, `Exercises`, `ReadingTexts` ve `ReadingQuestions` tablolarını
değiştirmeden okur; yeni migration üretmez.

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

Staging ve production overlay'leri profili kaldırır; bu ortamlar için
`SPEED_READING_CONNECTION_STRING` deployment secret olarak verilmelidir.
Öğretmen/veli kurum kapsamı Identity üzerinden doğrulandığı için aynı
ortamlarda `INTERNAL_SERVICE_API_KEY` zorunludur; Compose bu anahtarı
`http://identity-service:8080` adresine giden çağrılar için sağlar. Servis
container'ında `localhost` Identity adresi olarak kullanılmaz.
Hızlı okuma veritabanı platform PostgreSQL container'ına taşınmadığı sürece
business tabloları platform migration zincirine dahil edilmez; one-shot
container yalnızca platform PostgreSQL'in hazır olmasını bekleyip ek ledger
tablosunu uygular.

İlk içerik API'leri:

- `GET /api/speed-reading/exercise-types`
- `GET /api/speed-reading/exercises`
- `GET /api/speed-reading/reading-texts`
- `GET /api/speed-reading/reading-texts/{id}`
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
  kapsamıyla atama raporu sözleşmesini döndürür. Atama tabloları hızlı okuma
  bounded context'inde henüz bulunmadığından `DataAvailable=false` ve açıklama
  alanı döner; eksik veri tahmin edilmez.
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
  üretim/export çağrısı, rapor üretim bağımlılıkları taşınana kadar legacy
  uyumluluk köprüsünde tutulur.
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
istemcisindeki `awardXP`, `checkAchievements`, streak ve showcase çağrıları bir
sonraki geçiş diliminde merkezi yazma uç noktalarına taşınana kadar eski
`/v1/gamification` uyumluluk köprüsünü kullanır. Eski speed-reading admin
ekranının kazanım servisi de artık `/api/speed-reading/achievements` uçlarını
kullanır. Bu geçici öğrenci yazma köprüsü, merkezi yazma
ve rollback testleri tamamlanmadan kapatılmamalıdır; gamification okuma yolları
(`user`, `achievements`, `achievements/user`, `leaderboard`) artık yalnızca yeni
servisi kullanır.
