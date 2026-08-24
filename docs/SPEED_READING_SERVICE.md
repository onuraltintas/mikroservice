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
  başka kullanıcı kimliği kabul etmez.
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
- `GET /api/speed-reading/reports/scheduled` — `ReportView` yetkisi olan
  kullanıcının kendi zamanlanmış raporlarını döndürür.
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
