# EduPlatform Mikroservis Projesi Yapısı

Bu belge repository'nin güncel kaynak ağacını gösterir. Son güncelleme:
2026-09-02. Servis sözleşmelerinin ayrıntıları için
[`docs/BOUNDED_CONTEXT_CONTRACTS.md`](docs/BOUNDED_CONTEXT_CONTRACTS.md) ve
[`docs/API_CONTRACTS.md`](docs/API_CONTRACTS.md) esas alınır.

## Kaynak ağacı

```text
microservice/
├── services/
│   ├── identity-service/          # Kullanıcı, kimlik doğrulama, rol ve MFA
│   ├── coaching-service/          # Koçluk, kurum, görev ve seanslar
│   ├── speed-reading-service/     # Hızlı okuma bounded context'i
│   ├── notification-service/      # Bildirim, e-posta ve destek mesajları
│   └── api-gateway/               # YARP gateway ve dış API sınırı
├── clients/
│   ├── speed-reading/             # Hızlı okuma web istemcisi
│   └── admin-panel/               # Angular yönetim paneli
├── shared/
│   ├── EduPlatform.Shared.Kernel/          # Ortak domain yapıtaşları
│   ├── EduPlatform.Shared.Contracts/       # Servisler arası sözleşmeler
│   ├── EduPlatform.Shared.Infrastructure/ # Ortak API/observability altyapısı
│   └── EduPlatform.Shared.Security/        # JWT, permission ve güvenlik yardımcıları
├── tests/
│   ├── Unit/                      # Speed Reading birim testleri
│   ├── Integration/               # Docker-backed servis/gateway testleri
│   ├── Shared.IntegrationTests/   # Ortak integration fixture'ları
│   └── E2E/                       # Playwright API/UI senaryoları
├── infrastructure/
│   ├── caddy/                     # Gateway ve public edge konfigürasyonları
│   ├── docker/                    # Altyapı compose yardımcıları
│   ├── litespeed/                 # LiteSpeed deployment yardımcıları
│   └── scripts/                   # Operasyon scriptleri
├── monitoring/                    # Prometheus, Grafana, Tempo ve alert kuralları
├── docker-compose.yml             # Lokal geliştirme compose'u
├── docker-compose.staging.yml     # Staging override'ı
├── docker-compose.production.yml  # Production override'ı
├── docker-compose.scale.yml       # Replica/ölçek override'ı
├── docker-compose.observability.yml # Gözlemlenebilirlik override'ı
├── docker-compose.*.litespeed.yml # LiteSpeed edge override'ları
└── EduPlatform.sln                # Tüm .NET projelerinin root solution'ı
```

## .NET proje düzeni

Her backend bounded context'i mümkün olduğunca aşağıdaki katmanlara ayrılır:

```text
<service>/
├── <Service>.Domain/          # Entity ve domain kuralları
├── <Service>.Application/     # Command/query, DTO ve use-case'ler
├── <Service>.Infrastructure/  # EF Core, dış servis ve persistence
└── <Service>.API/             # HTTP endpoint, auth ve composition root
```

`speed-reading-service`, bağımsız owned persistence modeline sahiptir. Legacy
adapter ve backfill kodu yalnız migration, rollback veya açık fallback akışları
için tutulur; normal production runtime'ı legacy veritabanına bağlanmaz.

## Frontend ve test giriş noktaları

- Hızlı okuma istemcisi: `clients/speed-reading`
- Yönetim paneli: `clients/admin-panel`
- Playwright testleri: `tests/E2E`
- Lokal altyapı: `./start-infra.sh`
- Tüm lokal servisler: `./start-all.sh`

Build ve deployment doğrulama komutları için
[`README.md`](README.md), release kapıları için
[`docs/OPERATIONS_RUNBOOK.md`](docs/OPERATIONS_RUNBOOK.md) kullanılmalıdır.
