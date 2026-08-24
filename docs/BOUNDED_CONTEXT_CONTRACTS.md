# Bounded context sözleşmeleri

Servisler veri sahibi sınırlarını korur. Bir servis başka bir servisin PostgreSQL
şemasına bağlanmaz; çapraz bağ yalnızca versionlanmış HTTP contract veya event
üzerinden kurulur.

| Bounded context | Veri sahibi | Dış sözleşme |
| --- | --- | --- |
| Identity | kullanıcı, rol/permission, kurum ve profile üyelikleri | `/api/auth`, `/api/users`, `/api/institutions`, internal coaching authorization |
| Coaching | assignment, exam/result, coaching session, academic goal | `/api/assignments`, `/api/exams`, `/api/sessions`, `/api/goals` |
| Speed Reading | mevcut hızlı okuma içeriği, programları, ilerleme ve ölçüm verileri | `/api/speed-reading` |
| Notification | notification, support request, e-mail template/delivery, SignalR | `/api/notifications`, `/api/support`, `/api/email-templates` |
| Gateway | route, edge rate limit, maintenance ve trusted proxy | yalnızca dış API entry point; domain verisi yok |

## Coaching command kuralları

Assignment, Exam, Session ve Goal command'ları Coaching Application assembly'sinde
tanımlıdır. `CoachingContractTests` şu sınırları release sırasında sabitler:

- Teacher/student identifier'ları boş olamaz.
- Başlık ve açıklama uzunlukları bounded'dır.
- Due/exam/session/target tarihleri geçmişte olamaz.
- Assignment hedef listesi boş, duplicate veya 100'den büyük olamaz.
- Score, duration ve passing-score değerleri domain sınırları içindedir.
- Book/Mixed assignment'larda kitap başlığı ve geçerli sayfa aralığı zorunludur;
  fotoğraf teslimleri 10 MiB, MIME, hash ve içerik imzası ile sınırlandırılır.
- Fotoğraf bytes'ları PostgreSQL'e yazılmaz. `Local` yalnız Development/test
  içindir; yatay ölçek için `Minio`/S3 uyumlu provider, tarama için `ClamAv`
  seçilir ve Production bu iki seçimi fail-closed zorunlu kılar.
- SystemAdmin Coaching read model'i assignment, session, exam ve goal listelerini
  bounded pagination/filter ile sunar; admin DTO'larında Identity profile PII'si
  veya storage key bulunmaz.
- SystemAdmin yönetim yüzeyi genel tablo CRUD'u değildir: Coaching command'larını
  açık aksiyonlarla çağırır ve `Permissions.Coaching.Manage` + `MfaRequired`
  ister. Seans katılımı ve sınav sonuçları için admin detay query'leri yalnızca
  gerekli öğrenci kimliklerini/ölçüm alanlarını döndürür; tenant ve hedef
  doğrulaması yine command handler'ında yapılır.

Kimlik/tenant authorization Coaching'de tahmin edilmez. Teacher target'ları
Identity internal authorization endpoint'inde aktif user/profile/institution,
student ve assignment ilişkileriyle doğrulanır. Internal çağrılar minimum entropy
kontrollü service key ve güvenli `X-Correlation-ID` ile yapılır.

## Event ve retry kuralları

Event payload'ları `shared/EduPlatform.Shared.Contracts/Events` altında versionlanır.
Consumer'lar inbox/outbox ve bounded retry kullanır; aynı event tekrar geldiğinde
yan etki ikinci kez üretilmez. Kalıcı hatalar dead-letter kuyruğuna gider ve
operasyonel replay kararı olmadan otomatik sonsuz retry yapılmaz.

## Idempotency sahipliği

Gateway domain write sonucunu cache'lemez. Public support submit'te
`(normalized email, Idempotency-Key)` unique constraint ve mevcut response ID'si
ile servis-sahibi idempotency uygulanır. Support kaydı, acknowledgement e-mail
delivery row'u ve Identity forward delivery row'u aynı Notification transaction'ında
oluşur; iki durable worker bounded retry ile yan etkileri tamamlar. Aynı anahtar
farklı canonical payload ile kullanılırsa servis `409 Conflict` döndürür. Böylece aynı
key ile gelen retry mevcut ID'yi döndürürken kaybolan admin bildirimi tekrar
denenebilir. Identity kurum oluşturma komutu ve Coaching assignment, exam,
session, goal ve exam-result komutları aynı prensibi `(scope, key, payload hash,
resource ID)` kaydı ve unique constraint ile uygular. Her kayıt ilgili domain
satırıyla aynı transaction içinde oluşturulur; cross-service genel replay
eklenmez. Exam-result replay'i mevcut sonuç doğrulanırsa no-op'tur.

## Doğrulama

```powershell
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj `
  -c Release --filter FullyQualifiedName~CoachingContractTests
```

Yeni bounded context eklenmeden önce veri sahibi, command/query DTO'su, event
version'ı, authorization sınırı, retry/idempotency davranışı ve contract testleri
bu dokümana eklenmelidir.
