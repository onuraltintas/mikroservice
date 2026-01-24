# Environment Configuration

## Single Source of Truth

Bu projede **tek bir `.env` dosyası** kullanılır: `/mikroservice/.env`

## Neden?

- ✅ **Tek Kaynak:** Tüm güvenlik bilgileri tek yerde
- ✅ **Kolay Yönetim:** Duplikasyon yok, sync sorunu yok  
- ✅ **Git Güvenliği:** Sadece 1 dosya ignore edilmeli
- ✅ **Tutarlılık:** Tüm servisler aynı değerleri kullanır

## Docker Compose Kullanımı

`infrastructure/docker/.env` dosyası **symbolic link** ile root `.env`'e işaret eder:

```bash
infrastructure/docker/.env -> ../../.env
```

Docker Compose otomatik olarak bu dosyayı okur.

## Yapılandırma

### 1. .env Dosyasını Oluştur

Root dizinde `.env.example`'dan kopyala:

```bash
cp .env.example .env
```

### 2. Güvenlik Bilgilerini Güncelle

`.env` dosyasındaki şifreleri production için mutlaka değiştir:

- `POSTGRES_PASSWORD`
- `REDIS_PASSWORD`
- `RABBITMQ_DEFAULT_PASS`
- `KEYCLOAK_ADMIN_PASSWORD`
- vb.

### 3. Docker Compose Başlat

```bash
cd infrastructure/docker
docker-compose -f docker-compose.infra.yml up -d
```

## Database Listesi

Aşağıdaki veritabanları otomatik oluşturulur:

- `identity_db` - Identity Service
- `notification_db` - Notification Service  
- `coaching_db` - Coaching Service
- `speedreading_db` - Speed Reading Service
- `blog_db`, `content_db`, `exam_db`, `analytics_db` - Gelecek servisler

## Güvenlik Notları

⚠️ **ÖNEMLİ:**
- `.env` dosyası **GİT'E PUSH EDİLMEMELİ**
- `.gitignore` içinde `.env` olduğundan emin ol
- Production'da ortam değişkenlerini secret manager ile yönet (Azure Key Vault, AWS Secrets Manager vb.)
- Development için de güçlü şifreler kullan

## Sorun Giderme

### Docker Compose .env okumuyor

```bash
# Symlink'i kontrol et
ls -la infrastructure/docker/.env

# Yeniden oluştur
cd infrastructure/docker
rm .env
ln -sf ../../.env .env
```

### Değişiklikler uygulanmıyor

```bash
# Container'ları yeniden başlat
docker-compose -f docker-compose.infra.yml down
docker-compose -f docker-compose.infra.yml up -d
```
