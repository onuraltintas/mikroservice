# EduPlatform uygulama planı

Bu plan, mevcut repository durumunu ve production'a çıkmadan önce kalan işleri
tek bir yerde tutar. Hedef, ilk aşamada yaklaşık 100.000 kayıtlı kullanıcıyı
taşıyabilen; replica, cache, queue ve database katmanları yatay büyütüldüğünde
daha yüksek kapasiteye geçebilen bir platformdur.

> Durum notu (2026-09-02): Bu belge 2026-08-22 tarihli production öncesi planın
> güncel çalışma kaydıdır. Production deployment artık ayrı bir release süreciyle
> yürütülmektedir; aşağıdaki staging, backup/restore, canary ve kapasite kapıları
> sonraki release'ler için geçerlidir. Güncel migration/cutover kanıtı için
> `docs/SPEED_READING_DATA_MIGRATION.md` ve operasyon adımları için
> `docs/OPERATIONS_RUNBOOK.md` esas alınır.

## Mevcut durum

| Alan | Durum | Doğrulama |
| --- | --- | --- |
| Identity, Coaching, Notification, Gateway | ✅ | .NET 9 Release build, warnings-as-errors |
| PostgreSQL, Redis, RabbitMQ Compose | ✅ | Compose base/scale/production config |
| JWT rotation, trusted proxy, service key | ✅ | security/config tests |
| Tenant/role/active-state scope | ✅ | Docker-backed EF matrix tests |
| Distributed gateway rate limit | ✅ | Redis atomic script + local fallback |
| Common ProblemDetails + traceId | ✅ | API contract tests |
| API v1 compatibility | ✅ | shared Asp.Versioning options + controller metadata |
| Pagination bounds | ✅ | Identity and Notification tests |
| EF inbox/outbox | ✅ | Identity/Coaching/Notification one-shot migration jobs |
| Retry-safe email/SignalR delivery | ✅ | Atomic `EmailDeliveries` queue, encrypted bodies, lease fencing + deterministic event IDs |
| OpenTelemetry/Prometheus/Grafana/Tempo | ✅ | monitoring config validation |
| Angular SSR/auth guards | ✅ | 7 unit test + 22-route prerender |
| Frontend CI security gate | ✅ | locked npm ci, npm audit, test, SSR build |
| Dinamik admin panel yönetimi | ✅ | permission guard, kurum/support/template yönetimi, Coaching SystemAdmin özeti |

## P1 paketleri

### P1.1 API sözleşmeleri — tamamlandı

- Her API cevabı `application/problem+json` kullanır.
- Hatalarda stack trace yerine `type`, `status`, `instance` ve güvenli
  `traceId` bulunur.
- Identity, Coaching ve Notification v1 varsayılanıyla versioning middleware'i
  kullanır; eski route'lar kırılmaz.
- Kullanıcı ve bildirim listeleri bounded pagination uygular; deterministic
  ordering ve toplam kayıt header'ları kullanılır.
- Sözleşme ayrıntısı: `docs/API_CONTRACTS.md`.

### P1.2 Olay güvenilirliği — tamamlandı

- Üç domain servisinde EF Core MassTransit inbox/outbox tabloları vardır.
- Identity, Coaching ve Notification için production Compose `*-migrations`
  one-shot job'ları migration'ı web replica'larından önce uygular.
- Consumer endpoint'lerinde exponential retry ve duplicate delivery için inbox
  middleware'i aktiftir.
- Coaching assignment/exam/session/goal/result ve assignment submit/grade
  producer'ları versionlanmış shared contracts üzerinden EF outbox'a publish
  eder; domain write ile event aynı transaction'dadır.
- SMTP teslimi consumer retry'ından ayrılmış durable `EmailDeliveries` queue ve
  lease-fenced worker ile yürütülür. Kuyruk gövdeleri Data Protection ile
  şifrelenir, gövdeler retention sonunda temizlenirken idempotency tombstone'ları
  korunur ve worker claim index'leriyle tarama maliyeti sınırlanır; SignalR
  bildirim ID'leri event ID'sine bağlanır.
- Kalıcı hatalar `_error` dead-letter kuyruğunda kalır; replay operasyonel bir
  karardır, otomatik sonsuz retry değildir.
- Ayrıntı: `docs/EVENT_RELIABILITY.md`.

### P1.3 Tenant ve rol matrisi — tamamlandı

- Identity scope sorguları aktif user/profile/institution ile sınırlandırılır.
- Kurum pasifleştirilirken o kuruma bağlı aktif refresh oturumları toplu olarak
  iptal edilir; unrelated tenant kullanıcılarının oturumları korunur.
- Coaching write hedefleri teacher profile, institution, active student ve
  assignment ilişkisini doğrular.
- Parent, InstitutionAdmin/Owner, Teacher, Student ve SystemAdmin okuma
  davranışları gerçek PostgreSQL testleriyle kontrol edilir.
- Assignment detayında öğrenciler arası not/feedback sızıntısı engellenir.

### P1.4 Frontend kalite kapısı — tamamlandı

- SSR sırasında browser-only auth, dashboard, register, settings ve notification
  çağrıları çalıştırılmaz.
- Angular testleri, production browser/SSR build'i ve `npm audit` CI'de zorunlu.
- Admin panel bağımlılıkları lock dosyasına sabitlenmiştir; kritik/high audit
  açıkları sıfır olmalıdır.

### P1.5 Admin panel yönetim kapsamı — tamamlandı

- Menü ve lazy route'lar server-issued permission claim'leriyle görünür ve
  erişilebilir hale getirildi; role adına göre hard-code görünürlük kaldırıldı.
- Identity kurum yaşam döngüsü, Notification destek gelen kutusu/yanıtı ve e-posta
  şablonu yönetimi API + panel üzerinden tamamlandı.
- Coaching için global admin salt-okunur bounded operasyon özeti eklendi; öğrenci
  notu/PII içeren genel CRUD özellikle açılmadı.
- Yönetilebilir alanlar ile secret/deploy/database/observability sınırları
  [admin panel matrisinde](docs/ADMIN_PANEL_MANAGEMENT_MATRIX.md) yazılı hale getirildi.
- SystemAdmin rolü ve kullanıcı mutation'ları aktör bazlı korunuyor; kurum işlemleri
  aktif tenant scope'u ile fail-closed uygulanıyor ve koçluk özetleri SQL projection
  ile bounded çalışıyor.

## Sıradaki geliştirme sırası

P1 tamamlandıktan sonra feature geliştirme ve production hazırlığı şu sırada
ilerleyecektir:

1. **Bölünmüş bounded context'ler:** Coaching içindeki Assignment/Exam/Session/
   Goal command sözleşmeleri ve input sınırları `CoachingContractTests` ile
   sabitlendi; ayrıntılı veri sahibi/HTTP/event matrisi
   [bounded context sözleşmesinde](docs/BOUNDED_CONTEXT_CONTRACTS.md) tutulur.
   Yeni Blog, Content ve Analytics servisleri ancak veri sahibi ve olay sözleşmesi
   netleşince eklenecek.
2. **Idempotency ve audit — tamamlandı:** Dış write endpoint'lerinde client
   idempotency key, audit actor/tenant/event metadata ve replay davranışı
   uygulanmıştır. Gateway genel
   response replay'i yapmaz; kimlik/tenant kontrolünü atlamamak için idempotency
   veri sahibi servisin transaction/unique constraint sınırında kalır. Support
   submit bu modelin örneğidir: support row, e-mail delivery ve Identity-forward
   delivery aynı transaction'da yazılır; worker'lar bounded retry kullanır ve
   Identity event MessageId'si support/admin çifti için deterministiktir.
   Identity kurum oluşturma ve Coaching assignment, exam, session, goal ve
   exam-result komutları için durable command-level idempotency (scope +
   canonical payload hash + resource ID + unique constraint) uygulanmıştır.
   Event consumer teslim idempotency'si P1 kapsamında tamamlandı.
3. **E2E kritik akışlar:** Gateway health, anonim auth/support bootstrap ve
   protected admin yüzeylerinin 401 sözleşmesi Docker üzerinde doğrulandı.
   Yetkili login → refresh → admin yüzeyleri ile Angular login/dashboard akışı
   Playwright testine bağlandı; staging SystemAdmin kimliği olmadığından lokal
   koşuda bilinçli olarak skip edilir. Disposable profile'da registration →
   MailCatcher verification → email confirmation akışı da bağlandı. Ayrı
   `coaching-disposable` Playwright profili teacher login → assignment
   idempotent replay → student tenant-scope read → gerçek SignalR notification
   teslimini kapsar; dört fixture secret'ı ve `E2E_DISPOSABLE_ENV=true` olmadan
   çalışmaz. 22.08.2026 tarihinde Docker üzerinde coaching profili 2/2 geçti;
   gateway profilinde 11 test geçti, 2 yetkili SystemAdmin testi staging secret'ı
   olmadığı için skip edildi. CI/staging'de aynı disposable fixture ile yeşil kanıt ve yetkili
   admin/UI koşusu alınmadan production verisiyle çalıştırılmaz.
4. **Kapasite doğrulama — lokal referans tamamlandı:** disposable tenant ile
   smoke → baseline → 64/128/256 worker ve 60 saniyelik soak koşuları çalıştırıldı.
   `/api/users/me` profil GET'indeki gereksiz login yazımı kaldırıldı; servis
   başına PostgreSQL havuzu `POSTGRES_MAX_POOL_SIZE=30` ile sınırlandı. Son
   256-worker koşusu 19.044/19.044 HTTP 200, p95 265 ms, p99 297 ms verdi;
   PostgreSQL `too many clients`, Identity DB bağlantı hatası, RabbitMQ backlog
   ve Redis reject/eviction sinyalleri sıfırdı. Ayrıntılı tablo
   [performans runbook'unda](docs/PHASE6_PERFORMANCE_OBSERVABILITY.md) bulunur.
   Bu tek-host/tek-replica kanıtı production kapasitesi değildir; staging'de
   30–60 dakikalık soak, replica artışı, gerçek trafik karışımı ve managed
   PostgreSQL/PgBouncer doğrulaması sonraki operasyon aşamasında zorunludur.
5. **Staging operasyonu:** immutable image SHA, migration forward/rollback,
   backup/restore tatbikatı, secret rotation, readiness/canary ve incident
   runbook'larını [staging operasyon runbook'u](docs/OPERATIONS_RUNBOOK.md)
   üzerinden prova et. Yerel Docker operasyon provası 22.08.2026 tarihinde
   tamamlandı: üç PostgreSQL custom-format dump'ı checksum'larıyla alındı,
   Identity dump'ı ayrı disposable database'e restore edilip 20 tablo ve 14
   kullanıcıyla doğrulandı, ardından yalnızca bu disposable database kaldırıldı;
   RabbitMQ definitions ve commit/image manifest'i repo dışı artifact dizinine
   yazıldı. Staging registry/digest push, gerçek readiness/canary, replica soak,
   Data Protection/SMTP sertifikaları ve environment secret'ları hâlâ gerçek
   staging erişimi gerektirir.
6. **Production go/no-go:** SLO'lar, restore RPO/RTO, alarm sahipliği, domain
   ve TLS/ingress kararı yazılı onaylanmadan deployment yapılmaz.

## Mimari kurallar

- Servisler birbirinin veritabanına bağlanmaz; paylaşım yalnız HTTP contract veya
  versionlanmış event ile yapılır.
- Controller'lar authorization, validation ve ProblemDetails sözleşmesini
  bypass edecek broad catch blokları eklemez.
- Uzun süren veya tekrarlanabilir işlerde request thread'ini tutmak yerine
  outbox + consumer + bounded retry kullanılır.
- Liste endpoint'leri sınırsız sonuç döndürmez; tenant filtresi count ve data
  sorgusunda aynı olmalıdır.
- Secret, token, request body ve PII loglanmaz; correlation/trace ID güvenli
  formatta taşınır.
- Ücretli kütüphane yoktur; kullanılan paketler OSS lisans/vulnerability
  taramasından geçer.

## Doğrulama komutları

```powershell
# Backend
$env:TEMP = (Join-Path (Get-Location) '.build-temp')
$env:TMP = $env:TEMP
dotnet restore tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj `
  --configuration Release --no-restore

# Frontend
npm ci --prefix clients/admin-panel --legacy-peer-deps
npm audit --prefix clients/admin-panel --audit-level=high
npm test --prefix clients/admin-panel -- --watch=false --progress=false
npm run build --prefix clients/admin-panel

# Compose/monitoring
docker compose --env-file .env.example config --quiet
docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.scale.yml config --quiet
```

## Sonraki production release koşulları

CI'nin yeşil olması tek başına production onayı değildir. Her yeni release için
`docs/CI_CD_AND_PRODUCTION_MONITORING.md`, `docs/PHASE5_PRODUCTION_HARDENING.md`
ve `docs/PHASE6_PERFORMANCE_OBSERVABILITY.md` runbook'ları staging üzerinde
uygulanıp kanıtlanmalıdır.

Son güncelleme: 2026-09-02
