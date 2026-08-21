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

Registration endpoints `POST /api/auth/register/student`, `/teacher`, `/parent`
and `/institution` return a `userId` containing the Identity user ID. Profile or
institution resource IDs are not returned under that field. The same user ID is
included in the e-mail verification link and is required by
`POST /api/auth/confirm-email`.

Institution-managed `POST /api/institution/teachers` and
`POST /api/institution/students` follow the same cross-service identifier rule:
`TeacherId`/`StudentId` is the Identity user ID used by Coaching, while
`ProfileId` is the Identity-owned profile ID used only by profile/invitation
operations. Neither response contains a password. The notification event carries
only a short-lived password-setup token; credentials never cross service boundaries.
These routes and the invitation routes are exposed by the Gateway under the
singular `/api/institution/{**catch-all}` and `/api/invitations/{**catch-all}`
patterns; clients must not call the internal Identity service port directly.

`POST /api/institutions` (SystemAdmin tenant creation) also requires an
`Idempotency-Key` header. Identity scopes the key to
`identity.institutions.create`, stores the canonical payload hash and the created
institution ID under a unique `(Scope, Key)` constraint, and commits that record
with the institution row. A retry with the same payload returns the original
`institutionId`; reusing the key with a different name, type, city or e-mail
returns `409 Conflict`. The admin panel generates the key once per create action
so transport-level retries do not create duplicate tenants.

`POST /api/assignments` (Coaching) requires the same 16–128 character
`Idempotency-Key` header. Coaching scopes the key to
`coaching.assignments.create`, stores a canonical request hash and the created
assignment ID under a unique `(Scope, Key)` constraint, and commits that record
with the assignment and student links. Retrying the same payload returns the
original assignment response; reusing the key with a different teacher,
institution, target list, score or assignment detail returns `409 Conflict`.
The key is owned by the caller and must be reused for transport-level retries;
clients must generate a new key for a distinct assignment.

### Kitap ödevi ve fotoğraf teslimi

`POST /api/assignments` accepts `AssignmentSource` values `Digital`, `Book` or
`Mixed`. For `Book`/`Mixed`, `BookTitle`, `BookStartPage` and `BookEndPage` are
required; the optional question range must contain both start and end values.
ISBN, edition and chapter are metadata only and are bounded by the Coaching
validator.

Students create an attachment metadata row first:

```http
POST /api/assignments/{assignmentId}/students/{studentId}/attachments
Content-Type: application/json
```

```json
{
  "assignmentId": "...",
  "studentId": "...",
  "fileName": "matematik-01.jpg",
  "contentType": "image/jpeg",
  "sizeBytes": 183421,
  "sha256": "<64 hexadecimal characters>"
}
```

The response contains an opaque attachment ID and a short-lived upload path.
The expiry is persisted with the attachment and enforced by the `PUT` endpoint;
an expired path returns `Attachment.UploadExpired` and cannot write bytes. The
client then sends the raw bytes with `PUT` to that path and supplies the same
`Content-Type`, `Content-Length` and `X-Content-SHA256` values. Only JPEG, PNG
and WebP images up to 10 MiB are accepted; the service verifies size, hash and
the image magic signature. A submission cannot be completed while any
attachment is not clean. Original filenames are metadata only; storage keys
are server-generated and are never exposed in the response.

Clean attachment bytes are streamed through the authorized endpoint
`GET /api/assignments/{assignmentId}/students/{studentId}/attachments/{attachmentId}/content`.
The same student/teacher/system-admin scope checks are applied on every read;
pending-scan or rejected files return `409` and are never served. SystemAdmin
can inspect the aggregate through `GET /api/coaching-admin/assignments/{id}`;
the response contains metadata and attachment status, never a storage key.

The Coaching admin read model also exposes bounded, read-only operational lists:
`GET /api/coaching-admin/sessions`, `GET /api/coaching-admin/exams` and
`GET /api/coaching-admin/goals`. Each endpoint supports `pageNumber` (1–1000),
`pageSize` (1–100) and bounded search/filter parameters; student identifiers
are returned only as identifiers needed for administration, not as Identity
profile data.

The current local adapter is for Development/test environments and writes to a
dedicated mounted directory. The scanner is explicitly selected with
`ATTACHMENT_SCANNER_PROVIDER=Local|ClamAv`; `Local` is a deterministic
development scanner and does not claim malware detection. The optional OSS
Compose profile starts ClamAV:

```powershell
docker compose --env-file .env --profile security-scan up -d clamav
```

Before Production, use `ATTACHMENT_SCANNER_PROVIDER=ClamAv` together with a
MinIO/S3-compatible storage adapter. The repository includes a MinIO profile
for local scale testing:

```powershell
docker compose --env-file .env --profile object-storage up -d minio
```

Set `ATTACHMENT_STORAGE_PROVIDER=Minio` and the `ATTACHMENT_MINIO_*` values
before starting Coaching. Production configuration rejects both the `Local`
storage provider and the `Local` scanner, and remains fail-closed if the
configured dependencies cannot be reached. The application interface is
storage-provider agnostic, so another S3-compatible service can be substituted
without changing the API contract.

The same contract applies to Coaching `POST /api/exams`, `POST /api/sessions`,
`POST /api/goals` and `POST /api/exams/{id}/results`, with scopes
`coaching.exams.create`, `coaching.sessions.create`, `coaching.goals.create`
and `coaching.exam-results.create`. A replay of an exam, session or goal
returns its original resource ID; a replay of an exam result is a no-op after
the existing result is verified. A changed payload always returns `409
Conflict`. The key is read from the HTTP header, not from the JSON body, and
each domain row plus its idempotency record is committed in one Coaching
transaction.

## Contract testleri

`tests/Integration/Identity.API.IntegrationTests` altında ProblemDetails,
versioning, notification pagination ve security metadata testleri bu davranışı
korur. Gateway üzerinden gerçek HTTP smoke testleri ayrıca CI'de çalıştırılır.
