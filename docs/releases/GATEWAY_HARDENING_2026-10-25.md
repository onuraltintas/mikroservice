# API Gateway sertleştirme — 25.10.2026

Kapsam: `services/api-gateway`, ortak `TrustedProxyConfiguration`, Identity (istemci IP'si,
bakım anahtarları, liveness) ve Notification (liveness). Coaching ve Speed Reading kaynakları
ile mevcut 68 route / 4 cluster (korunan ürün sözleşmesi) değiştirilmedi.

## Davranış değişiklikleri

| Alan | Önce | Sonra |
| --- | --- | --- |
| Hata gövdeleri | 429/503 düz JSON | `application/problem+json`; `error`, `message`, `service` alanları korunur |
| Rate limit | Yalnız IP, `/api/auth/**` tümü 30/dk, sabit pencere; Redis düşünce bazı kurallar kapalı | Config'den (`Gateway:RateLimiting`) sliding window; refresh 300/dk, login 60/dk, diğer auth 120/dk (IP); girişli kullanıcı başı 600/dk; her kuralda yerel yedek; `RateLimit-*` başlıkları; metrik + alarm |
| İstemci IP başlığı | Yalnız kurallı yollarda temizleniyordu | Her proxied istekte silinip gateway tarafından yazılır |
| Identity istemci IP'si | Gateway IP'si kaydediliyordu | İç ağdan gelen `X-Forwarded-For` (yalnız For) |
| Gövde limiti | Kestrel 30 MB (katalog importu 48 MB izinli ama reddediliyordu) | Varsayılan 30 MB; katalog importu 50 MB; auth/destek/bülten 64 KB → 413 |
| Zaman aşımı | YARP 100 sn boşta kalma | 100 sn istek zaman aşımı (504 problem); export/import/rapor route'ları 300 sn; SignalR muaf |
| Upstream | Health check / load balancing adımları yoktu | Pasif + aktif (`/health/live`) health check, `HealthyOrPanic`, bağlantı ömrü 120 sn |
| Bakım modu | Her istekte 2 Redis okuması; 24 saat sonra anahtar düşüyordu | 5 sn önbellek, Redis hatasında son bilinen durum; Identity anahtarları süresiz yazar |
| CORS | `localhost:4200` her ortamda | Yalnız `Gateway:Cors:AllowedOrigins` (Development'ta 4200) |
| Container | root | UID 1654 (`USER $APP_UID`) |
| `/api/gateway/services` | Her girişli kullanıcı | SystemAdmin veya `Permissions.Operations.View` |
| Correlation | Servisler farklı id üretebiliyordu | Gateway'in `X-Correlation-ID` değeri servise iletilir |

## Yayın adımları

1. Önce Identity ve Notification, sonra gateway imajı yayınlanır (ara durumda davranış eskisiyle aynı).
2. `api-gateway-volume-permissions` tek seferlik servisi mevcut anahtar volume'ünü UID 1654'e verir;
   compose bunu gateway'den önce otomatik çalıştırır.
3. Production `.env`: `SERVICE_TRUSTED_PROXY_NETWORKS` = production subnet (örn. `172.31.0.0/16`).
   Boşsa `172.16.0.0/12` kullanılır.
4. CI: `.github/workflows/ci.yml` uzak araçla yazılamayan korumalı dosya olduğu için değişiklik
   `GATEWAY_HARDENING_2026-10-25-ci.patch` olarak bırakıldı:
   `git apply docs/releases/GATEWAY_HARDENING_2026-10-25-ci.patch`
   (Redis servisli `gateway-tests` işi ve gateway alarm testi).
5. Yük testi: tek kullanıcıyla 128 worker kullanıcı başı limite takılır. Staging'de
   `Gateway__RateLimiting__AuthenticatedUser__PermitLimit` yükseltin veya izole ortamda
   `Gateway__RateLimiting__Enabled=false` kullanın.

Geri dönüş: önceki gateway/Identity/Notification imaj etiketleri. Config'ler geriye uyumludur.

## Doğrulama (bulut ortamı, kullanıcının derlenmiş paket DLL'leriyle)

- Yeni `EduPlatform.Gateway.UnitTests`: 140/140 (Redis'li ve Redis'siz modlar).
- Faz bazlı RED → GREEN: her faz commit'i ayrı derlendi ve test edildi.
- Identity entegrasyon paketi: değişiklik öncesi/sonrası başarısız test kümeleri karşılaştırıldı;
  fark yalnız önceden de kararsız olan MassTransit consumer testleri (aynı kod tekrarlarda 1–3 hata).
  Docker gerektiren testler bu ortamda koşamadı.
- Speed Reading gateway route testleri 19/19; E2E route sözleşmeleri (korunan baseline dahil) geçti.
- 6 servis API'si derlendi; değişen dosyalarda derleme uyarısı yok.
- Gerçek Kestrel: 40 MB import 200, 31 MB 413, 70 KB auth (chunked dahil) 413, yavaş servis 504,
  export 300 sn politikası, sahte IP başlıkları değiştiriliyor.
- `docker compose config`: 9 overlay kombinasyonu; promtool: kurallar ve alarm testleri.

## Bilinen / sonraya kalan

- Önceden var olan hata: `CoachingContractTests.CreateExamContract_RejectsInvalidScoreAndPastDate`
  (ExamDate doğrulaması) değişiklikten önce de başarısız.
- Paket sürümleri (net10 üzerinde 9.x Microsoft.Extensions/Serilog/JwtBearer) tüm ortak projelerle
  birlikte ayrı bir işte hizalanmalı.
- Coaching ve Speed Reading korunan ürün olduğu için forwarded-header ve non-root değişikliği
  onlara uygulanmadı; bu servisler gateway'in `X-EduPlatform-Client-IP` başlığını kullanmaya devam eder.
- Diğer servis imajları hâlâ root ile çalışıyor (Course Management hariç).
- Public GET'ler için output cache ve root+catch-all route sadeleştirmesi bilinçli olarak yapılmadı
  (route tablosu korunan sözleşme).
