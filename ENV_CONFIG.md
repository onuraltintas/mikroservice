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
- `speedreading_owned_db` - Speed Reading Service
- `blog_db`, `content_db`, `exam_db`, `analytics_db` - Gelecek servisler

## Tanıtım sitesi ve ölçüm (O11)

- `GA4_MEASUREMENT_ID`: Google Analytics 4 ölçüm kimliği (`G-` ile başlar). Boşsa analitik yüklenmez ve çerez bandı görünmez.
  Doluysa ziyaretçi çerez bandında onay verdikten sonra GA4 yüklenir; yalnız tanıtım sitesi, kayıt ve giriş sayfaları ölçülür.
- `SITE_API_ORIGIN` (compose içinde `http://api-gateway:8080`): web sunucusunun `sitemap.xml` ve blog/sayfa bağlantı önizlemeleri için
  okuduğu iç API adresi.
- `robots.txt` ve `sitemap.xml` istek yapılan alan adına göre üretilir (izinli alan adları listesinden).

## Koçluk bağlantı adresi (D3)

- Koçluk e-postalarındaki bağlantılar (davet, bülten) `PUBLIC_APP_BASE_URL` adresine gider; compose bunu `Coaching__PublicBaseUrl` olarak koçluk servisine verir (boşsa `https://eduivme.com`).
- Production ve Staging'de adres `https` olmalıdır; değilse koçluk servisi başlamaz. Yayından önce sunucudaki `.env` değerini kontrol edin.
- Eski `CoachingNewsletter__PublicBaseUrl` ayarı yalnız `Coaching__PublicBaseUrl` yoksa okunur.

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

## Bildirimler: e-posta, SMS ve WhatsApp (O8, O9)

- Önemli koçluk bildirimleri kullanıcının tercihine göre e-postayla da gönderilir. Bunun için ek ayar gerekmez; mevcut SMTP ayarları kullanılır.
  Bildirim servisi alıcı adlarını ve adreslerini Identity'nin iç ucundan (`api/internal/notification/recipients`) okur.
  Bu çağrı `INTERNAL_SERVICE_API_KEY` ile yapılır.
- `TEXT_MESSAGING_ENABLED` (varsayılan `false`): SMS / WhatsApp gönderimi.
  - Kapalıyken kullanıcıların seçtiği SMS / WhatsApp iletileri gönderilmez.
  - Yalnız maskeli numarayla "gönderilmedi" kaydı tutulur; metin tutulmaz.
- `TEXT_MESSAGING_PROVIDER`: sağlayıcı adı (ör. `Netgsm`). Henüz bağlı bir sağlayıcı yok.
  - Sağlayıcı seçildiğinde `ITextMessageProvider` uyarlaması eklenir.
  - Gönderim ancak ondan sonra açılmalıdır.
- Ticari ileti izinleri (kampanya/duyuru; SMS, e-posta, arama) kullanıcı ayarlarında saklanır ve "İYS'ye aktarılmayı bekliyor" durumunda kalır.
  - İleti Yönetim Sistemi bağlantısı açılınca aktarılır.
  - Bilgilendirme amaçlı iletiler (seans hatırlatması, abonelik bitişi, veli özeti) için İYS izni gerekmez.

