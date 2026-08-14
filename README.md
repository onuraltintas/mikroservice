# 🚀 Eğitim Platformu - Mikroservis Projesi

## 📋 Proje Durumu

✅ **Proje başarıyla ayağa kaldırıldı!**

### Kurulu Bileşenler:
- ✅ .NET 9.0.308
- ✅ Docker 28.5.1
- ✅ Node.js v20.20.0
- ✅ PostgreSQL 16
- ✅ RabbitMQ 3.12
- ✅ Redis 7.2
- ✅ Angular 21

---

## 🏗️ Proje Yapısı

```
mikroservice/
├── services/              # Mikroservisler
│   ├── identity-service/  # Kullanıcı yönetimi
│   ├── coaching-service/  # Koçluk servisi
│   ├── notification-service/ # Bildirim servisi
│   └── api-gateway/       # YARP API Gateway
├── clients/
│   └── admin-panel/       # Angular Admin Panel
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
| Identity / Coaching / Notification | Gateway üzerinden erişilir | İç ağ |

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

---

## 🔐 Güvenlik Notları

⚠️ **ÖNEMLİ:**
- `.env` dosyası GIT'e push edilmemeli
- Production ortamında güçlü şifreler kullanın
- Secret Manager kullanın (Azure Key Vault, AWS Secrets Manager)

---

## 📝 Notlar

- Bu proje **Development** ortamı için yapılandırılmıştır
- Production deployment için Kubernetes konfigürasyonları `infrastructure/kubernetes/` klasöründe
- Tüm migration'lar uygulanmıştır
- Swagger UI her servis için `/swagger` endpoint'inde mevcuttur

---

**Son Güncelleme:** 2026-01-24
**Versiyon:** 1.0.0
