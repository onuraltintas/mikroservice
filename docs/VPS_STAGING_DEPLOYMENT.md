# VPS Staging Deployment

Bu doküman tek VPS üzerinde, production verisinden izole bir **staging** ortamı
kurmak içindir. Staging ortamı kabul testleri içindir; 100.000+ kullanıcı için
production topolojisi değildir.

Bu kurulumda kullanılan staging adresi: `https://staging.onuraltintas.net`.

## Topoloji

```text
Internet :80/:443
        |
      Caddy (ACME/TLS, compression)
       /                     \
  Angular SSR              API Gateway
 (admin-panel)                 |
                 Identity / Coaching / Notification
                         |
       PostgreSQL + RabbitMQ + Redis (private network)
```

`docker-compose.staging.yml` yalnızca Caddy'nin 80/443 portlarını yayınlar.
PostgreSQL, Redis, RabbitMQ, MailCatcher ve Gateway host portlarına bağlanmaz;
servisler Docker ağı içinde kalır. Prometheus, Grafana ve Alertmanager da
observability overlay'i ile loopback'e bağlanır ve VPS dışından erişilemez.

Mevcut VPS üzerinde LiteSpeed zaten 80/443 kullanıyorsa Caddy'yi public porta
almayın. `docker-compose.staging.litespeed.yml` override'ı Caddy'yi yalnızca
`127.0.0.1:5100` üzerinde HTTP router olarak çalıştırır; LiteSpeed yalnızca
`staging.onuraltintas.net` vhost'u için bu porta proxy yapar ve TLS'i kendi
üzerinde sonlandırır. Böylece diğer sitelerin listener/vhost'ları değişmez.
LiteSpeed'in proxy katmanı `X-Forwarded-For` zincirini iç hopta taşır; Caddy
sadece staging Docker gateway adresini trusted proxy kabul eder ve backend'e
TLS'in public kenarda sonlandığını `X-Forwarded-Proto: https` ile bildirir.

## VPS ön koşulları

- Ubuntu 24.04 LTS (veya güncel, desteklenen Linux), Docker Engine ve Compose v2.
- Staging için en az 4 vCPU / 8 GB RAM; build sırasında swap önerilir.
- DNS'te `STAGING_DOMAIN` için VPS public IP'sine A/AAAA kaydı.
- Güvenlik duvarında yalnızca TCP 22 (yönetim), 80 (ACME HTTP-01) ve 443 açık.
  5432, 6379, 5672, 15672, 9000, 9001, 9090, 3000 ve 9093 dışarı açılmaz.
- GitHub deploy anahtarı veya read-only registry erişimi. Production secret'ları
  staging makinesine kopyalanmaz.

## İlk kurulum

Repo'yu VPS'te sabit bir dizine klonlayın ve immutable bir commit'e geçin:

```bash
git clone https://github.com/onuraltintas/mikroservice.git /opt/eduplatform
cd /opt/eduplatform
git checkout <commit-sha>
cp .env.example .env.staging
chmod 600 .env.staging
```

`.env.staging` içinde en az şu değerleri değiştirin:

```dotenv
ENVIRONMENT=Staging
POSTGRES_HOST=postgres
POSTGRES_PORT=5432
POSTGRES_PASSWORD=<unique-postgres-password>
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_DEFAULT_PASS=<unique-rabbitmq-password>
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=<unique-redis-password>
JWT_SECRET=<at-least-32-random-characters>
INTERNAL_SERVICE_API_KEY=<random-service-key>
PUBLIC_APP_BASE_URL=https://staging.onuraltintas.net
SMTP_HOST=mailcatcher
SMTP_PORT=1025
ATTACHMENT_STORAGE_PROVIDER=Minio
ATTACHMENT_SCANNER_PROVIDER=ClamAv
STAGING_DOMAIN=staging.onuraltintas.net
TLS_ACME_EMAIL=onuraltintas@gmail.com
FORWARDED_HEADERS_FORWARD_LIMIT=1
FORWARDED_HEADERS_KNOWN_NETWORKS=172.30.0.0/16
```

`GOOGLE_CLIENT_ID`, `POSTGRES_*`, MinIO kullanıcı/şifreleri ve observability
admin şifresi de staging'e özel olmalıdır. `TEST_ADMIN_PASSWORD` ve
`TEST_DEFAULT_PASSWORD` boş bırakılmalıdır; gerektiğinde yalnızca disposable
E2E ortamında geçici olarak set edilir. Her altyapı bileşeni için farklı parola
kullanın. Staging overlay Docker ağına `172.30.0.0/16` sabit subnet'i verir;
bu nedenle VPS host ağınızla çakışmadığını kontrol edin. `.env.staging` Git'e
commit edilmez.

## Compose doğrulama ve başlatma

Önce birleşik Compose modelini ve opsiyonel güvenlik/storage profillerini
doğrulayın:

```bash
docker compose --env-file .env.staging \
  -f docker-compose.yml -f docker-compose.staging.yml \
  -f docker-compose.observability.yml \
  --profile security-scan --profile object-storage config --quiet
```

İlk build ve çalıştırma:

```bash
docker compose --env-file .env.staging \
  -f docker-compose.yml -f docker-compose.staging.yml \
  -f docker-compose.observability.yml \
  --profile security-scan --profile object-storage up -d --build
```

LiteSpeed'in 80/443 kullandığı mevcut sunucularda bunun yerine şu override'ı
kullanın:

```bash
docker compose --env-file .env.staging \
  -f docker-compose.yml -f docker-compose.staging.yml \
  -f docker-compose.staging.litespeed.yml \
  -f docker-compose.observability.yml \
  --profile security-scan --profile object-storage up -d --build
```

Bu modda `staging.onuraltintas.net` için ayrı bir LiteSpeed vhost oluşturulmalı,
backend'i `127.0.0.1:5100` adresine proxy etmeli ve yalnız bu vhost için
Let's Encrypt sertifikası alınmalıdır. Mevcut vhost dosyalarını kopyalayıp
üzerine yazmayın. Vhost'u kurduktan sonra `/usr/local/lsws/bin/lshttpd -t`
ile yapılandırmayı kontrol edin; mevcut sunucudaki eski, ilgisiz vhost
uyarılarını staging vhost hatalarından ayırın ve yalnızca başarılı kontrolün
ardından `/usr/local/lsws/bin/lswsctrl reload` ile graceful reload yapın.

Migration container'ları (`identity-migrations`, `coaching-migrations`,
`notification-migrations`) tamamlanmadan web servisleri başlamaz. Kontrol:

```bash
docker compose --env-file .env.staging \
  -f docker-compose.yml -f docker-compose.staging.yml \
  -f docker-compose.observability.yml \
  --profile security-scan --profile object-storage ps

docker compose --env-file .env.staging \
  -f docker-compose.yml -f docker-compose.staging.yml \
  --profile security-scan --profile object-storage logs \
  identity-migrations coaching-migrations notification-migrations staging-edge
```

## Staging smoke testi

DNS ve Caddy sertifikası hazır olduktan sonra aşağıdaki kontrollerin tamamı
başarılı olmalıdır:

```bash
curl --fail --silent https://staging.onuraltintas.net/health/live
curl --fail --silent https://staging.onuraltintas.net/health/ready
```

Tarayıcıda Angular SSR giriş sayfasını, Google girişini (etkinse), token
yenileme akışını ve logout'u test edin. Coaching için en az şu senaryoları
uygulayın: öğretmen ödev/sınav oluşturma ve düzeltme, öğrencinin kitap ödevi
metin + fotoğraf yüklemesi, koç/öğrenci ilerleme görünümü, veli salt-okunur
görünümü ve bildirim teslimi. Yüklenen dosyanın MinIO'da saklandığını ve
ClamAV taramasından geçmeyen dosyanın reddedildiğini doğrulayın.

## Gözlemleme ve geri alma

Observability servisleri yalnızca loopback'te dinlediği için güvenli SSH tüneli
kullanın; portları public yapmayın:

```bash
ssh -N \
  -L 127.0.0.1:9090:127.0.0.1:9090 \
  -L 127.0.0.1:3000:127.0.0.1:3000 \
  -L 127.0.0.1:9093:127.0.0.1:9093 user@<vps-ip>
```

Deploy öncesi PostgreSQL ve MinIO yedeklerinin varlığını doğrulayın. Yeni
commit'te readiness, migration, error-rate veya kritik alert bozulursa son
sağlıklı SHA'ya dönün, migration geri alma prosedürünü ayrıca değerlendirin ve
olayı `docs/OPERATIONS_RUNBOOK.md` formatında kaydedin:

```bash
git checkout <last-known-good-sha>
docker compose --env-file .env.staging \
  -f docker-compose.yml -f docker-compose.staging.yml \
  -f docker-compose.observability.yml \
  --profile security-scan --profile object-storage up -d --build
```

Rollback, veri şemasını otomatik olarak geriye almaz. Geriye dönük uyumlu
migration ve restore planı olmadan production veritabanında `down` veya manuel
`DROP` çalıştırmayın.

## Production'a geçiş kapısı

Staging smoke ve E2E testleri geçmeden production deploy yapılmaz. Production'da
`docker-compose.production.yml` kullanılmalı; gerçek SMTP, PFX ile Data
Protection, MinIO/ClamAV, harici registry'den immutable image digest'leri,
backup/restore provası, canary ve en az 15 dakikalık monitoring gözlemi
tamamlanmalıdır. 100.000+ kullanıcı hedefinde tek VPS yalnızca geçici staging
olur; production için gateway/servis replica'ları, yönetilen veya HA PostgreSQL,
RabbitMQ/Redis topolojisi, harici object storage ve merkezi log/metric/trace
altyapısı gerekir.
