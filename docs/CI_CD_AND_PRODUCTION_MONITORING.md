# CI/CD ve Production Monitoring

Bu paket iki farklı problemi çözer:

- **CI/CD**, yeni kodun kullanıcıya ulaşmadan önce derlenmesini, test edilmesini
  ve güvenli bir container olarak üretilebilmesini sağlar.
- **Production monitoring**, deploy edilmiş sistemin gecikme, hata, kuyruk,
  cache ve readiness durumunu sürekli ölçer; sorun kullanıcı etkisine
  dönüşmeden alarm üretir.

Bunlar birbirinin alternatifi değildir. CI/CD değişiklik öncesi kalite kapısıdır;
monitoring değişiklik sonrası çalışma kontrolüdür.

## CI pipeline

`.github/workflows/ci.yml` şu olaylarda çalışır:

- Pull request
- `main` veya `master` branch'ine push
- Manuel `workflow_dispatch`

Kapılar sırasıyla:

1. Tracked dosyalarda private key ve yaygın cloud key formatı taraması.
2. JSON ve Docker Compose base/scale/production/observability yapı doğrulaması.
3. Dört .NET 9 servisi için restore ve `Release --warnaserror` build.
4. Docker-backed integration test suite ve coverage artifact'i.
5. Ayrı Gateway process'i ile gerçek `/health` smoke testi.
6. Tüm uygulama projelerinde NuGet vulnerability taraması.
7. Monitoring Compose, Prometheus/Alertmanager/OTel/Tempo/Blackbox config doğrulaması.
8. Dört Docker image'ının push edilmeden reproducible build edilmesi.

Bu workflow dış bir registry'ye veya production cluster'a otomatik deploy etmez.
Registry, imaj adlandırma, Kubernetes/VM hedefi ve rollback politikası
belirlenmeden source-derived image'ı dışarı göndermek güvenli değildir. Hedef
ortam netleştiğinde aynı son gate'in arkasına manuel onaylı publish/deploy job'ı
eklenmelidir.

## Monitoring bileşenleri

`docker-compose.observability.yml` ile eklenen ücretsiz OSS bileşenleri:

```text
ASP.NET services ──OTLP──> OpenTelemetry Collector ──> Prometheus ──> Grafana
                                     │                      │
                                     └──────────> Tempo      └──> Alertmanager

RabbitMQ / PostgreSQL / Redis exporters ────────────────> Prometheus
Gateway + service readiness ──HTTP probe──> Blackbox exporter ─> Prometheus
```

- Shared `OpenTelemetryExtensions` ASP.NET Core, HttpClient ve .NET Runtime
  metrik/trace instrumentation'ını tek yerden kaydeder.
- Geliştirmede exporter endpoint'i boşsa uygulamalar collector'a bağlanmayı
  denemez. Monitoring overlay'i eklendiğinde endpoint otomatik olarak
  `http://otel-collector:4317` olur.
- Prometheus ve Grafana yalnız `127.0.0.1` üzerinde yayınlanır. Dışarıdan
  erişim gerekiyorsa VPN/SSH tunnel veya kimlik doğrulamalı ayrı ingress
  kullanılmalıdır; portlar internete doğrudan açılmamalıdır.
- RabbitMQ'nun native Prometheus endpoint'i ve PostgreSQL/Redis exporter'ları
  iç Compose ağında kalır.
- cAdvisor veya Docker socket mount'u kullanılmaz; host güvenlik sınırı
  monitoring uğruna zayıflatılmaz. Host CPU/memory metrikleri production'da
  orkestratörün güvenli node metrics çözümünden alınmalıdır.

Bu Compose overlay'i tek host üzerinde çalışan bir başlangıç/topoloji referansıdır;
Prometheus, Tempo, Grafana ve Alertmanager verileri named volume'larda tutulur ve
tek başına HA sağlamaz. Production'da host kaybına karşı düzenli volume yedeği,
Tempo için S3-uyumlu object storage, Prometheus için remote-write/uzun süreli
storage ve en az iki replica'lı izleme katmanı planlanmalıdır. OTLP ve Tempo
trafiği bu örnekte güvenilir tek Docker ağı içindeki TLS'siz bağlantıdır; servisler
hostlar arası taşınacaksa mTLS/TLS ve network policy uygulanmalıdır.

## Başlatma

Geliştirme ortamında monitoring'i ayrı overlay olarak başlatın:

```powershell
docker compose --env-file .env `
  -f docker-compose.yml `
  -f docker-compose.observability.yml up -d --build
```

Ardından:

- Grafana: `http://localhost:3000`
- Prometheus: `http://localhost:9090`
- Alertmanager: `http://localhost:9093`

Grafana parolası `GRAFANA_ADMIN_PASSWORD` ile verilmelidir. `.env.example`
değeri yalnız örnektir; gerçek deployment'ta rastgele secret kullanılmalıdır.

Production'da Data Protection ve production port hardening overlay'iyle birlikte
çalıştırın:

```powershell
docker compose --env-file .env `
  -f docker-compose.yml `
  -f docker-compose.production.yml `
  -f docker-compose.observability.yml up -d
```

Bu komut için gerçek PFX/PKCS#12, SMTP ve Grafana secret'ları deployment secret
manager'dan sağlanmalıdır. PostgreSQL/Redis exporter kimlik bilgileri de aynı
secret manager veya Docker secret mekanizmasıyla environment yerine dosyadan
sağlanmalıdır. RabbitMQ eski volume migration'ı ayrıca
[production hardening runbook'ındaki](PHASE5_PRODUCTION_HARDENING.md) cutover
adımlarına göre yapılmalıdır.

## İlk alarm sözleşmesi

Başlangıç eşikleri ölçüm tabanı oluştuktan sonra ürün SLO'larıyla
kesinleştirilmelidir:

| Alarm | Başlangıç eşiği | Aksiyon |
| --- | --- | --- |
| Readiness başarısız | 2 dakika | Trafiği durdur, bağımlılıkları kontrol et |
| HTTP 5xx | 5 dakika boyunca > %1 | Son deploy'u ve exception loglarını incele |
| HTTP p95 | 10 dakika boyunca > 500 ms | DB/Redis/Rabbit doygunluğu ve trace incele |
| RabbitMQ ready backlog | 10 dakika boyunca > 1.000 | Consumer lag ve dead-letter kontrolü |
| Redis memory | 10 dakika boyunca > %80 | Key TTL/eviction ve kapasite kontrolü |
| Telemetry target down | 2 dakika | Collector/exporter/servis health kontrolü |

Alertmanager varsayılan olarak alarmı UI'da gösterir. E-posta, webhook veya
Slack/Teams gibi bir kanal kullanılacaksa credential'lar config dosyasına
yazılmadan secret manager üzerinden receiver eklenmelidir.

## Deploy checklist

Her release'te:

- CI build, test, vulnerability ve Compose gate'leri yeşil olmalı.
- Migration planı ve rollback image SHA'sı hazır olmalı.
- Önce staging'de smoke ve readiness testleri çalıştırılmalı.
- Production canary/ilk replica sonrası 15 dakika 5xx ve p95 izlenmeli.
- Rabbit backlog, Redis memory ve DB connection pool normal olmalı.
- Eşik aşılırsa yeni rollout durdurulmalı veya önceki immutable image SHA'sına
  dönülmelidir.
