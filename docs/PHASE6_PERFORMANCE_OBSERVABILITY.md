# Faz 6 — Performans, Yük Testi ve Gözlemlenebilirlik

Bu runbook, 100.000 kayıtlı kullanıcı hedefi için kapasite ölçümünü ve üretimde
hangi sinyallerin izleneceğini tanımlar. 100.000 kayıtlı kullanıcı, 100.000 eş
zamanlı istek anlamına gelmez; gerçek trafik ölçülene kadar aşağıdaki varsayımlar
başlangıç noktasıdır:

- Zirve saatte kayıtlı kullanıcıların %1–5'i aktif kabul edilir (1.000–5.000
  aktif kullanıcı).
- Her aktif kullanıcı için istek sıklığı ve okuma/yazma oranı ölçümle
  doğrulanır; tek bir sabit RPS değeri kapasite taahhüdü değildir.
- Login/PBKDF2 ve `429` rate-limit cevapları, normal iş yükü performansından
  ayrı bir abuse testi olarak raporlanır.

## 1. Ücretsiz yük testi aracı

`tools/load-test.ps1`, PowerShell 7 ile gelen `Invoke-WebRequest` ve paralel
çalışan worker'ları kullanır. Harici veya ücretli bir kütüphane gerektirmez.
Her worker en fazla 10.000 latency örneği tutar; uzun koşularda p50/p95/p99
sonuçları bounded-reservoir örneklemine dayalı yaklaşık değerlerdir. JSON çıktısı
ayrıca gerçek koşu süresini ve ölçülen `RequestsPerSecond` değerini verir; uzun
timeout'lar nedeniyle bu değer yapılandırılan duration'dan farklı olabilir.

Gateway health smoke testi:

```powershell
& .\tools\load-test.ps1 `
  -BaseUrl http://localhost:5000 `
  -Path /health -Concurrency 32 -DurationSeconds 60 -Json -FailOnError
```

Kimlik doğrulanmış bir okuma endpoint'i için token'ı komut satırında sabit
metin olarak saklamayın; oturumdan veya güvenli bir secret store'dan alın:

```powershell
$token = $env:LOAD_TEST_TOKEN
& .\tools\load-test.ps1 `
  -BaseUrl http://localhost:5000 `
  -Path /api/users/me `
  -Concurrency 32 `
  -DurationSeconds 300 `
  -Header "Authorization=Bearer $token" `
  -Json -FailOnError
```

Yazma senaryolarında test tenant'ı ve sentetik kimlikler kullanın:

```powershell
$teacherId = $env:LOAD_TEST_TEACHER_ID # seeded test-tenant TeacherProfile
$studentId = $env:LOAD_TEST_STUDENT_ID # active student assigned to that teacher
if ([string]::IsNullOrWhiteSpace($teacherId) -or [string]::IsNullOrWhiteSpace($studentId)) {
  throw "LOAD_TEST_TEACHER_ID and LOAD_TEST_STUDENT_ID must be set"
}
$dueDate = [DateTime]::UtcNow.AddDays(7).ToString("o")
$body = @{
  TeacherId = $teacherId
  Title = "load-test"
  Description = "synthetic"
  DueDate = $dueDate
  StudentIds = @($studentId)
} | ConvertTo-Json
& .\tools\load-test.ps1 `
  -BaseUrl http://localhost:5000 `
  -Path /api/assignments `
  -Method POST -Body $body -ContentType application/json `
  -Header "Authorization=Bearer $token" `
  -Concurrency 16 -DurationSeconds 120 -Json
```

Bu örnek, Gateway'in gerçek `/api/assignments` route'unu kullanır. ID'ler
önceden oluşturulmuş aynı test tenant'ına ait değilse authorization katmanı
isteği bilinçli olarak `403` ile reddeder; bu durumda kapasite ölçümü değil,
yetki testi yapılmış olur. POST koşusu yalnızca silinebilir bir test tenant'ı
veya disposable veritabanında, düşük concurrency ile ayrı bir koşu olarak
çalıştırılmalıdır; her worker başarılı olduğunda yeni bir kayıt üretir. Koşu
sonunda sentetik kayıtları temizleyin ve gerçek kullanıcı/PII kullanmayın.

Login endpoint'ini normal kapasite testi yerine ayrı bir senaryo olarak çalıştırın.
PBKDF2 maliyeti, rate-limit ve başarısız giriş backoff'u iş trafiğini temsil etmez.
Gerçek kullanıcı token'larını veya kişisel verileri yük testine taşımayın.

## 2. Kademeli kapasite planı

Her basamakta aynı sentetik veri setini ve aynı endpoint karışımını kullanın.
Bir sonraki basamağa yalnızca hata oranı, latency ve altyapı göstergeleri
kararlıysa geçin:

| Basamak | Eşzamanlı worker | Süre | Amaç |
| --- | ---: | ---: | --- |
| Smoke | 1–10 | 1 dk | Route, auth ve health doğrulaması |
| Baseline | 16–32 | 5 dk | İlk p50/p95/p99 ve kaynak tabanı |
| Step-up 1 | 64 | 10 dk | Gateway ve uygulama thread-pool davranışı |
| Step-up 2 | 128 | 10 dk | DB pool, Redis ve RabbitMQ doygunluğu |
| Step-up 3 | 256 | 10 dk | Yatay çoğaltma öncesi tek replica sınırı |
| Soak | Sınırın %60–70'i | 30–60 dk | Bellek sızıntısı, kuyruk büyümesi ve GC |

Başlangıç kabul eşikleri (ölçüm sonrası ürün SLA'ları ile kesinleştirilmelidir):

- Gateway health: p95 < 100 ms, p99 < 250 ms.
- Kimlik doğrulanmış salt-okuma: p95 < 300 ms, p99 < 750 ms.
- Yazma/komut endpoint'i: p95 < 500 ms, p99 < 1.000 ms.
- `5xx` oranı < %0,1; `401` ve beklenen `429` ayrı raporlanır.
- DB connection pool, CPU ve bellek sürekli %80'in üzerinde kalmamalıdır.
- RabbitMQ consumer lag ve Redis latency koşu boyunca artış trendine girmemelidir.

Bu eşikler karşılanmıyorsa replica sayısını artırmadan önce yavaş sorgu,
N+1 erişim, gereksiz payload, connection pool ve cache hit oranını inceleyin.

## 3. Ölçülecek sinyaller

Her koşu için timestamp, commit SHA, image tag, endpoint, concurrency ve test
veri setini kaydedin. Aşağıdaki sinyaller aynı zaman aralığında toplanmalıdır:

| Katman | Minimum sinyaller |
| --- | --- |
| Gateway | RPS, p50/p95/p99, 2xx/4xx/5xx/429, upstream timeout ve retry |
| .NET servisleri | CPU, working set, GC pause, thread-pool queue, request duration |
| PostgreSQL | active/idle connections, pool wait, slow query, lock, transaction rollback |
| Redis | command latency, memory, evictions, connected clients, limiter errors |
| RabbitMQ | queue depth, publish/ack rate, consumer lag, redelivery ve dead-letter |
| Host/container | CPU throttling, memory limit, restart/OOM ve network error |

`429` cevapları hem güvenlik sinyali hem de kapasite sinyali olarak ayrı
gösterilmelidir; bunları p95 hesabına karıştırmak gerçek uygulama latency'sini
maskeler.

## 4. Mevcut gözlemlenebilirlik durumu

- Ortak Serilog yapılandırması ve request logging middleware'i mevcut.
- Seq sink'i mevcut; `infrastructure/docker/docker-compose.infra.yml` Seq'i
  sağlar. Ana `docker-compose.yml` içinde Seq servisi yoktur; Seq kullanımı
  için `SEQ_URL` ve ilgili compose profili açıkça sağlanmalıdır. Console logları
  bu bağımlılıktan bağımsız tutulmalıdır.
- Identity ve Coaching'de DB tabanlı `/health`, `/health/ready` ve `/health/live`
  endpoint'leri bulunur. Gateway health endpoint'i şu anda yalnızca process
  canlılığını gösterir; upstream bağımlılıklarını temsil etmez.
- Gateway ve tüm uygulama servisleri ortak `CorrelationIdMiddleware` kullanır.
  Güvenli karakter kümesine uyan `X-Correlation-ID` korunur; eksik veya şüpheli
  değerler yerine yeni bir GUID üretilir. Değer response header'ına,
  `HttpContext.TraceIdentifier`'a ve Serilog `CorrelationId` property'sine yazılır.
- OpenTelemetry trace/metric export'u, Prometheus/Grafana dashboard'ları ve
  merkezi alert kuralları henüz ortak bir sözleşme olarak eklenmemiştir.

## 5. Önerilen ücretsiz üretim standardı

Yeni gözlemlenebilirlik kodu servis servis kopyalanmamalı; `shared` altında
tek bir extension ile etkinleştirilmelidir:

1. OpenTelemetry SDK ile ASP.NET Core, `HttpClient`, EF Core ve RabbitMQ
   Activity'lerini toplayın; OTLP exporter ile seçilen collector'a gönderin.
2. Collector'dan Prometheus/Grafana veya OpenTelemetry uyumlu ücretsiz bir
   backend'e yönlendirin. Geliştirme ortamında collector yerine Aspire
   Dashboard kullanılabilir.
3. Mevcut korelasyon middleware'ini W3C trace context ile tamamlayın veya
   collector propagator'ı ile aynı ID'yi eşleyin. `X-Correlation-ID` yalnızca
   güvenli karakterler ve sınırlı uzunlukla kabul edilir; trace, user ID veya
   e-posta gibi PII alanlarını log attribute'u yapmayın. Secret, token ve request
   body loglamayın.
4. `/health/live` yalnız process canlılığını; `/health/ready` DB, Redis,
   RabbitMQ ve gerekli upstream bağımlılıklarını ifade etsin. Gateway readiness
   kontrolü downstream timeout ile sınırlı olmalıdır.
5. İlk alert seti: 5xx oranı, p95/p99, readiness failure, Redis fallback,
   RabbitMQ backlog/dead-letter, DB pool saturation ve 401/429 anomalisidir.

Bu standardın kabul kriteri; her servis için aynı trace ID'nin gateway →
downstream → DB/message loglarında bulunması ve bir isteğin tek dashboard'dan
uçtan uca izlenebilmesidir.

## 6. Docker/Testcontainers doğrulaması

Gerçek tenant matrisi testleri Docker daemon gerektirir. Daemon çalıştığında:

```powershell
docker version
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj `
  -c Release --filter FullyQualifiedName~CoachingStudentReadRepositoryTests
```

Docker olmayan makinelerde build ve container dışı testler çalıştırılabilir;
Testcontainers testlerinin çalışmadığı açıkça raporlanmalıdır. Gerçek `.env`
oluşturmadan ve production secret kullanmadan lokal smoke testi yapın.
