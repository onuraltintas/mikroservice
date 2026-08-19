# HTTP API sözleşmesi

Bu belge, servislerin gateway arkasındaki ortak HTTP davranışını tanımlar. Yeni
bir servis veya endpoint eklenirken önce bu sözleşme ve ilgili contract testleri
güncellenir.

## API versioning

Identity, Coaching ve Notification API'leri v1 olarak işaretlidir. Mevcut
route'lar geriye dönük olarak çalışmaya devam eder; sürüm belirtilmezse `1.0`
varsayılır. İstemciler sürüm seçmek için aşağıdaki okuyuculardan birini
kullanabilir:

- `X-Api-Version: 1.0` header'ı,
- `?api-version=1.0` query parametresi,

Servisler `api-supported-versions` ve `api-deprecated-versions` response
header'larını versioning middleware'i üzerinden raporlar. Gateway route'ları
domain servislerinin mevcut path'lerini korur; migration sırasında istemci
path'lerini tek seferde kırmamak için default version kullanılır. API explorer
metadata'sı Swagger ve gelecekteki v2 dokümantasyonunun aynı sürüm gruplarını
kullanabilmesi için etkindir; URL-segment versioning şu an route sözleşmesinin
parçası değildir.

## Hata formatı

Tüm MVC validation ve unhandled exception cevapları `application/problem+json`
olarak döner. Alanlar:

```json
{
  "type": "https://eduplatform.dev/problems/validation-error",
  "title": "Validation Error",
  "status": 400,
  "instance": "/api/users",
  "traceId": "00-...",
  "errors": {
    "email": ["Email is required."]
  }
}
```

`traceId` kullanıcıya stack trace veya secret vermeden gateway → servis →
dependency log korelasyonu sağlar. Production cevaplarında stack trace,
connection string, token veya request body bulunmaz. Bilinen exception türleri
`validation-error`, `not-found`, `forbidden`, `business-rule`,
`concurrency-conflict` ve `unexpected-error` problem type'larına eşlenir.

## Pagination

Liste endpoint'leri sınırsız `ToListAsync()` çalıştırmamalıdır. Identity user
listesi `pageNumber` 1–1000, `pageSize` 1–100 sınırlarını uygular. Notification
listesi de aynı sınırları uygular ve mevcut array response'unu korurken
`X-Total-Count`, `X-Unread-Count`, `X-Page-Number` ve `X-Page-Size` header'larını
döner. `X-Unread-Count` tüm user filtresindeki okunmamış kayıtları saydığı
için ilk sayfadaki kayıt sayısına eşit olmak zorunda değildir.

Yeni liste endpoint'leri için kurallar:

1. İstek modellerinde açık page number/page size bulunur.
2. Maksimum sayfa ve sayfa boyutu validator/controller seviyesinde reddedilir.
3. Deterministic ordering (genellikle `CreatedAt` + `Id`) kullanılır.
4. `Skip` hesabı bounded integer/long aritmetiğiyle yapılır.
5. Toplam sayım ve sayfa verisi aynı tenant filtresini kullanır.

## Idempotent public writes

Gateway write yanıtlarını cache'lemez veya replay etmez. Böylece her retry'da
downstream servisinin authentication, tenant ve business authorization kontrolleri
yeniden çalışır; Gateway veri sahibi değildir. Idempotency, write komutunun sahibi
olan serviste, aynı transaction/unique constraint ve outbox sınırında uygulanmalıdır.
Kimlik veya tenant kapsamı olan write'larda yalnızca Gateway katmanına konan genel bir
cache/replay mekanizması kullanılmamalıdır.

`POST /api/support/submit` requires an `Idempotency-Key` header containing a
16–128 character `[A-Za-z0-9._~-]` value. The key is scoped to the submitted
email address. Repeating the same request with the same key returns the
original `SupportRequestId` and does not create a second acknowledgement or
admin notification. Reusing a key with a different canonical request payload
returns `409 Conflict`. The support row, acknowledgement delivery row and
Identity-forward delivery row are committed together. Durable workers retry
those two side effects, so transient SMTP or Identity failures do not make the
idempotent record permanently lose its notification work. Clients should reuse
the key when retrying a timed-out request and generate a new key for a distinct
support request.

## Contract testleri

`tests/Integration/Identity.API.IntegrationTests` altında ProblemDetails,
versioning, notification pagination ve security metadata testleri bu davranışı
korur. Gateway üzerinden gerçek HTTP smoke testleri ayrıca CI'de çalıştırılır.
