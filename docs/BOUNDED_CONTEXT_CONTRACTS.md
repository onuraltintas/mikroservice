# Bounded context sözleşmeleri

Servisler veri sahibi sınırlarını korur. Bir servis başka bir servisin PostgreSQL
şemasına bağlanmaz; çapraz bağ yalnızca versionlanmış HTTP contract veya event
üzerinden kurulur.

| Bounded context | Veri sahibi | Dış sözleşme |
| --- | --- | --- |
| Identity | kullanıcı, rol/permission, kurum ve profile üyelikleri | `/api/auth`, `/api/users`, `/api/institutions`, internal coaching authorization |
| Coaching | assignment, exam/result, coaching session, academic goal | `/api/assignments`, `/api/exams`, `/api/sessions`, `/api/goals` |
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
denenebilir. Gelecekteki Coaching/Identity write komutları da aynı prensiple
kendi transaction/unique constraint sınırında uygulanmalıdır; cross-service genel
replay eklenmemelidir.

## Doğrulama

```powershell
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj `
  -c Release --filter FullyQualifiedName~CoachingContractTests
```

Yeni bounded context eklenmeden önce veri sahibi, command/query DTO'su, event
version'ı, authorization sınırı, retry/idempotency davranışı ve contract testleri
bu dokümana eklenmelidir.
