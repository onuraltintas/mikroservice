# Event güvenilirliği sözleşmesi

EduPlatform servisleri olayları doğrudan RabbitMQ'ya gönderip veritabanı
işlemini ayrı bir transaction olarak bırakamaz. Olay üreten write işlemi ile
outbox kaydı aynı PostgreSQL transaction'ında tamamlanır.

## Uygulanan desen

- Identity ve Notification DbContext'leri EF Core MassTransit inbox/outbox
  tablolarını içerir.
- Coaching de `InboxState`, `OutboxMessage` ve `OutboxState` tablolarını
  `AddMassTransitOutbox` migration'ı ile içerir.
- `UseBusOutbox()` publish çağrılarını transaction sonrasında broker'a taşır.
- Receive endpoint'leri EF inbox/outbox middleware'i kullanır; aynı mesajın
  tekrar teslim edilmesi idempotent işlenir.
- Notification consumer'ları ve support reply/acknowledgement command'ları
  e-postayı SMTP'ye doğrudan göndermez; `EmailDeliveries`
  tablosuna unique `(MessageId, ConsumerType)` anahtarıyla durable work item
  yazar. Insert PostgreSQL `ON CONFLICT DO NOTHING` ile atomiktir; iki replica
  aynı event'i aynı anda işlese bile tek work item oluşur.
- Background worker kayıtları lease + exponential retry ile teslim eder.
  Her lease'in UUID fencing token'ı vardır; lease süresi dolan veya başka worker
  tarafından yeniden sahiplenilen eski worker, kaydı `Sent`/`Failed` durumuna
  çeviremez. Gönderim gövdeleri ASP.NET Data Protection ile veritabanında
  şifreli tutulur; worker yalnız SMTP çağrısı anında çözer.
- `Sent` gövdeleri 7 gün, kalıcı `Failed` gövdeleri 30 gün sonra worker tarafından
  temizlenir; `MessageId + ConsumerType` tombstone satırları korunur. Böylece
  eski bir event replay edilse bile unique idempotency anahtarı duplicate email
  oluşmasını engeller. Başarısız işler `Failed` durumunda kalır ve mevcut satır
  kontrollü olarak yeniden `Pending` yapılarak replay edilir.
- Worker claim sorguları `Status + NextAttemptAt/LeaseUntil + CreatedAt`
  bileşik index'lerini kullanır; backlog büyümesi bu nedenle tablo taramasına
  dönüşmemelidir.
- SignalR notification ID'si event `MessageId` ile deterministikleştirilir.
  Retry aynı kaydı ve aynı client ID'sini kullanır; frontend duplicate ID'leri
  yeniden eklemez.
- Consumer'larda 5 denemeli exponential retry uygulanır (1 saniyeden 1
  dakikaya, 5 saniye jitter/delay ile). Kalıcı hatalar MassTransit'in `_error`
  dead-letter kuyruğuna gider ve monitoring ile izlenir.

## Operasyon

Production Compose overlay'ı web container'larından önce Identity, Coaching ve
Notification için one-shot `*-migrations` container'ları çalıştırır. Bu
container'lar yalnızca ilgili `dotnet <Service>.API.dll --migrate-only`
komutunu yürütür ve yalnız DbContext kaydeder; RabbitMQ, Redis, SMTP veya HTTP
bağımlılıklarını başlatmaz. Migration başarılı olmadan web replica'ları
başlamaz.
Rolling deploy veya Kubernetes gibi Compose dışı ortamlarda aynı komutlar release
job/init job olarak tek instance çalıştırılmalıdır; her web replica'sına
otomatik migration yetkisi verilmez.

Outbox ve `EmailDeliveries` tabloları migration ile oluşturulmadan ilgili servisin production'a
çıkarılması yasaktır. `OutboxMessage` birikimi, RabbitMQ ready/unacked sayısı,
redelivery ve `_error` kuyrukları dashboard/alert kapsamındadır. Retry, kalıcı
bir iş kuralı hatasını sonsuza kadar döndürmek için kullanılmaz; hata kuyruğu
incelenip düzeltildikten sonra kontrollü replay yapılır.

Email backlog alarmı için Notification PostgreSQL üzerinde aşağıdaki sorgu
dashboard'a eklenir; `Failed` sayısı veya yaşlanan `Pending/Processing` kaydı
runbook'taki replay/SMTP incelemesini başlatır:

```sql
SELECT "Status", COUNT(*) AS "Count", MIN("CreatedAt") AS "Oldest"
FROM "EmailDeliveries"
GROUP BY "Status"
ORDER BY "Status";
```

Yeni event eklerken:

1. Contract `shared/EduPlatform.Shared.Contracts/Events` altında versionlanır.
2. Producer write transaction'ı outbox üzerinden publish eder.
3. Consumer duplicate delivery'ye karşı idempotent olur.
4. En az bir publish/consume veya duplicate-delivery test senaryosu eklenir.
5. Şema migration'ı ve rollback etkisi dokümante edilir.
