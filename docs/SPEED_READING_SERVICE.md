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
- `GET /api/speed-reading/progress/active-exercise-sessions`
- `GET /api/speed-reading/program-templates`
- `GET /api/speed-reading/progress/programs`
- `GET /api/speed-reading/progress/daily-exercise-logs`
- `GET /api/speed-reading/learning-paths/templates`
- `GET /api/speed-reading/learning-paths/progress`
- `GET /api/speed-reading/learning-paths/personalized`

## Yetki sınırı

Identity permission seed'i aşağıdaki bağımsız anahtarları sağlar:

- `Permissions.SpeedReading.View`
- `Permissions.SpeedReading.ContentManage`
- `Permissions.SpeedReading.ProgramManage`
- `Permissions.SpeedReading.ProgressView`
- `Permissions.SpeedReading.ReportView`
- `Permissions.SpeedReading.GamificationManage`
- `Permissions.SpeedReading.SettingsManage`

Kurum rolleri varsayılan olarak yalnızca görünürlük, ilerleme ve rapor okuma
yetkilerini alır; içerik/program/ayar değişiklikleri SystemAdmin veya açıkça
atanmış yetki gerektirir.
