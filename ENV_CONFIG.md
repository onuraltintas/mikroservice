# Environment Configuration

## Single Source of Truth

Bu projede **tek bir `.env` dosyası** kullanılır: repo kökündeki `.env`

## Neden?

- ✅ **Tek Kaynak:** Tüm güvenlik bilgileri tek yerde
- ✅ **Kolay Yönetim:** Duplikasyon yok, sync sorunu yok  
- ✅ **Git Güvenliği:** Sadece 1 dosya ignore edilmeli
- ✅ **Tutarlılık:** Tüm servisler aynı değerleri kullanır

## Docker Compose Kullanımı

Kök `docker-compose.yml` dosyası `.env` dosyasını otomatik olarak okur. Başlangıç şablonu
`.env.example` dosyasındadır. `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE` ve
`INTERNAL_SERVICE_API_KEY` tüm ilgili servislerde aynı olmalıdır.
Bu anahtar en az 32 UTF-8 byte uzunluğunda, rastgele üretilmiş bir değer olmalıdır.
JWT rotasyonunda yeni değer `JWT_SECRET`, overlap süresince eski değerler
`JWT_PREVIOUS_SECRETS` olarak virgülle ayrılmış şekilde verilir; `JWT_KEY_ID` ve
`JWT_PREVIOUS_KEY_IDS` isteğe bağlı `kid` eşlemesidir. Üretimde placeholder değerler
fail-fast reddedilir.
Compose dağıtımında dış istemciler yalnızca API Gateway'e bağlanır; mikroservis portları
host'a bind edilmez.

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
- `GRAFANA_ADMIN_PASSWORD`
- `PGADMIN_DEFAULT_PASSWORD` (yalnızca legacy `infrastructure/docker/docker-compose.infra.yml` profili kullanılıyorsa)
- vb.

### 3. Docker Compose Başlat

```bash
docker compose up -d
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
docker compose --env-file .env config --quiet
```

### Değişiklikler uygulanmıyor

```bash
# Container'ları yeniden başlat
docker-compose -f docker-compose.infra.yml down
docker-compose -f docker-compose.infra.yml up -d
```
