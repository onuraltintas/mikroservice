# 🚀 Eğitim Platformu - Mikroservis Projesi

## 📋 Proje Durumu

✅ **Proje başarıyla ayağa kaldırıldı!**

### Kurulu Bileşenler:
- ✅ .NET 9.0.308
- ✅ Docker 28.5.1
- ✅ Node.js v20.20.0
- ✅ PostgreSQL 16
- ✅ RabbitMQ 4.3.4
- ✅ Redis 7.2
- ✅ Angular 21

---

## 🏗️ Proje Yapısı

```
mikroservice/
├── services/              # Mikroservisler
│   ├── identity-service/  # Kullanıcı yönetimi
│   ├── coaching-service/  # Koçluk servisi
│   ├── speed-reading-service/ # Bağımsız hızlı okuma bounded context'i
│   ├── notification-service/ # Bildirim servisi
│   └── api-gateway/       # YARP API Gateway
├── clients/
│   ├── speed-reading/     # Bağımsız hızlı okuma web istemcisi
│   └── admin-panel/       # Angular yönetim paneli
├── shared/                # Ortak kütüphaneler
├── infrastructure/        # Docker & K8s
└── .env                   # Environment değişkenleri
```

---

## 🚀 Hızlı Başlangıç

### 1️⃣ Sadece Altyapıyı Başlat (PostgreSQL, RabbitMQ, Redis, MailCatcher)

```bash
./start-infra.sh
```

veya

```bash
docker compose up -d
```

### 2️⃣ Tüm Servisleri Başlat (Altyapı + .NET Servisleri + Angular)

```bash
./start-all.sh
```

### 3️⃣ Manuel Başlatma

#### Docker Altyapısı:
```bash
docker compose up -d postgres redis rabbitmq mailcatcher
```

#### Identity Service:
```bash
cd services/identity-service/Identity.API
dotnet run
```

#### Coaching Service:
```bash
cd services/coaching-service/Coaching.API
dotnet run
```

#### Speed Reading Service:
```bash
cd services/speed-reading-service/SpeedReading.API
dotnet run
```

#### Notification Service:
```bash
cd services/notification-service/Notification.API
dotnet run
```

#### API Gateway:
```bash
cd services/api-gateway
dotnet run
```

#### Angular Frontend:
```bash
cd clients/admin-panel
npm run start
```

---

## 📍 Servis URL'leri

### Backend Servisleri:
| Servis | URL | Port |
|--------|-----|------|
| API Gateway | http://localhost:5000 | 5000 |
| Identity / Coaching / Speed Reading / Notification | Gateway üzerinden erişilir | İç ağ |

Docker Compose kullanımında mikroservis portları host'a açılmaz; dış istemciler yalnızca API Gateway'e bağlanır. Servisleri ayrı ayrı `dotnet run` ile başlatırken ilgili `launchSettings.json` portları kullanılabilir.

### Frontend:
| Uygulama | URL | Port |
|----------|-----|------|
| Angular Admin Panel | http://localhost:4200 | 4200 |

### Altyapı Servisleri:
| Servis | URL | Kullanıcı Adı | Şifre |
|--------|-----|---------------|-------|
| RabbitMQ Management | http://localhost:15672 | `${RABBITMQ_DEFAULT_USER}` | `${RABBITMQ_DEFAULT_PASS}` |
| MailCatcher Web UI | http://localhost:1080 | - | - |
| PostgreSQL | localhost:5433 | `${POSTGRES_USER}` | `${POSTGRES_PASSWORD}` |
| Redis | localhost:6379 | - | `${REDIS_PASSWORD}` |

---

## 🗄️ Veritabanları

Aşağıdaki veritabanları otomatik oluşturuldu:

- `identity_db` - Identity Service
- `coaching_db` - Coaching Service
- `notification_db` - Notification Service

Hızlı okuma verileri owned modda `speedreading_owned_db` veritabanında tutulur.
`SPEED_READING_CONNECTION_STRING` yalnızca geçiş/backfill veya legacy fallback
modu için kullanılır; ayrıntı için `docs/SPEED_READING_SERVICE.md` belgesine
bakın.

### Veritabanına Bağlanma:

```bash
# PostgreSQL CLI
docker exec -it postgres psql -U eduplatform -d identity_db

# Tüm veritabanlarını listele
docker exec postgres psql -U eduplatform -d postgres -c '\l'
```

---

## 🔧 Geliştirme Komutları

### .NET Servisleri

```bash
# Tüm projeyi restore et
dotnet restore

# Belirli bir servisi build et
dotnet build services/identity-service/Identity.sln

# Migration oluştur
dotnet ef migrations add MigrationName --project services/identity-service/Identity.Infrastructure --startup-project services/identity-service/Identity.API

# Migration uygula
dotnet ef database update --project services/identity-service/Identity.Infrastructure --startup-project services/identity-service/Identity.API
```

### Angular

```bash
cd clients/admin-panel

# Development server başlat
npm run start

# Production build
npm run build

# Test çalıştır
npm run test

# Security audit (high/critical açık bırakma)
npm audit --audit-level=high
```

### Docker

```bash
# Tüm container'ları başlat
docker compose up -d

# Tüm container'ları durdur
docker compose down

# Container loglarını görüntüle
docker compose logs -f [service-name]

# Container'ları yeniden başlat
docker compose restart

# Tüm container'ları ve volume'ları sil
docker compose down -v
```

### CI/CD ve gözlemlenebilirlik

Ücretsiz OSS monitoring stack'ini (OpenTelemetry, Prometheus, Grafana,
Alertmanager, Tempo) geliştirmede ayrı overlay ile açabilirsiniz:

```bash
docker compose --env-file .env \
  -f docker-compose.yml \
  -f docker-compose.observability.yml up -d --build
```

Admin panelinin hangi platform alanlarını yönettiği, hangi alanların bilinçli
olarak CI/CD veya secret manager sınırında kaldığı için
[Admin Panel Yönetim Matrisi](docs/ADMIN_PANEL_MANAGEMENT_MATRIX.md) belgesinde
tanımlıdır.

Grafana `http://localhost:3000`, Prometheus `http://localhost:9090` ve
Alertmanager `http://localhost:9093` adreslerinde yalnızca localhost'a açık
olur. Ayrıntılı CI kapıları, alarm eşikleri ve production kullanım şekli için
[CI/CD ve Production Monitoring](docs/CI_CD_AND_PRODUCTION_MONITORING.md)
runbook'una bakın.

Gateway ve Angular admin panelinin kritik kullanıcı akışlarını çalıştırmak için
[E2E doğrulama kılavuzunu](docs/E2E_TESTING.md) kullanın. Lokal smoke, Docker
Gateway'e bağlanır; yetkili admin ve UI akışları yalnız disposable/staging
kimlik bilgileriyle etkinleştirilir.

HTTP hata formatı, v1 versioning ve pagination kuralları
[API Contracts](docs/API_CONTRACTS.md); RabbitMQ/EF inbox-outbox, retry ve
dead-letter davranışı [Event Reliability](docs/EVENT_RELIABILITY.md)
belgesinde tanımlıdır. Güncel geliştirme sırası ve production go/no-go koşulları
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) dosyasındadır.

---

## 🐛 Sorun Giderme

### Docker container'ları başlamıyor

```bash
# Container'ları durdur ve yeniden başlat
docker compose down
docker compose up -d

# Logları kontrol et
docker compose logs
```

### .NET servisi başlamıyor

```bash
# Restore ve build yap
dotnet restore
dotnet build

# appsettings.json dosyasını kontrol et
cat services/identity-service/Identity.API/appsettings.json
```

### Angular başlamıyor

```bash
# node_modules'u sil ve yeniden yükle
cd clients/admin-panel
rm -rf node_modules package-lock.json
npm install
```

### Veritabanı bağlantı hatası

```bash
# PostgreSQL'in çalıştığını kontrol et
docker compose ps postgres

# Connection string'i kontrol et (.env dosyası)
cat .env | grep POSTGRES
```

---

## 📚 Dokümantasyon

- [Proje Yapısı](PROJECT_STRUCTURE.md)
- [Mimari Rapor](ARCHITECTURE_REPORT.md)
- [Environment Konfigürasyonu](ENV_CONFIG.md)
- [Test Stratejisi](TESTING_STRATEGY.md)
- [Uygulama Planı](IMPLEMENTATION_PLAN.md)
- [Faz 5 Üretim Sertleştirme Runbook'u](docs/PHASE5_PRODUCTION_HARDENING.md)
- [Faz 6 Performans ve Gözlemlenebilirlik Runbook'u](docs/PHASE6_PERFORMANCE_OBSERVABILITY.md)
- [CI/CD ve Production Monitoring](docs/CI_CD_AND_PRODUCTION_MONITORING.md)

---

## 🔐 Güvenlik Notları

⚠️ **ÖNEMLİ:**
- `.env` dosyası GIT'e push edilmemeli
- Production ortamında güçlü şifreler kullanın
- Secret Manager kullanın (Azure Key Vault, AWS Secrets Manager)

---

## 📝 Notlar

- Repository development, staging ve production compose varyantlarını içerir; aktif production
  dağıtımı için release/image ve ortam secret'ları kullanılmalıdır.
- Deployment yardımcıları `infrastructure/caddy/`, `infrastructure/litespeed/`
  ve compose override dosyalarında bulunur.
- Tüm migration'lar uygulanmıştır
- Swagger UI her servis için `/swagger` endpoint'inde mevcuttur

---

**Son Güncelleme:** 2026-09-02
**Sürüm:** CI/CD release tag'i ile belirlenir
