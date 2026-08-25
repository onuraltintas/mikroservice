# Faz 5 — Üretim Sertleştirme ve Doğrulama Runbook'u

Bu doküman, tenant/rol kontrollerinin gerçek EF sorguları ile doğrulanmasını,
Redis kesintisindeki davranışı ve JWT/secret rotasyonunu tanımlar.

## 1. EF tenant/rol matrisi

`tests/Integration/Identity.API.IntegrationTests/CoachingStudentReadRepositoryTests.cs`
şu durumları gerçek PostgreSQL üzerinde doğrular:

- Veli yalnızca aktif çocuklarının verisini görür.
- Kurum yöneticisi yalnızca kendi aktif kurumundaki aktif öğrencileri görür.
- Öğretmen yalnızca aynı kurumda atanmış öğrencileri görür.
- Pasif kullanıcı, aktif profili ve ataması olsa bile reddedilir.
- Farklı kurum, pasif kullanıcı ve pasif profil verileri sonuçtan çıkarılır.

Çalıştırma:

```powershell
docker version
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj `
  -c Release --filter FullyQualifiedName~CoachingStudentReadRepositoryTests
```

Testcontainers PostgreSQL kullandığı için CI runner'da Docker daemon zorunludur.
Docker yoksa derleme yapılabilir; entegrasyon testleri geçerli bir sonuç üretmez.

## 2. Redis rate-limit kabul kriterleri

Gateway'deki dağıtık limiter, Redis üzerinde atomik `INCR + PEXPIRE` Lua script'i
kullanır. Her route için aşağıdaki smoke senaryoları release öncesi çalıştırılmalıdır:

| Senaryo | Beklenen davranış |
| --- | --- |
| Redis sağlıklı, aynı IP'den 31 auth isteği | İlk 30 istek route'a ulaşır, 31. istek `429` olur |
| Redis sağlıklı, aynı IP'den 11 support submit isteği | İlk 10 istek route'a ulaşır, 11. istek `429` olur |
| Redis kesilirken yeni auth isteği | Middleware hata loglar; process düşmez, yerel ASP.NET limiter devreye girer |
| Gateway replica sayısı artırılır | Redis anahtarı ortak olduğu için kota replica başına çoğalmaz |
| Güvenilir reverse proxy arkasında çalışma | Proxy zinciri ve gerçek istemci IP'si açıkça yapılandırılmadan `RemoteIpAddress` limiter anahtarı olarak kullanılmaz |

Gateway, `X-Forwarded-For` ve `X-Forwarded-Proto` başlıklarını varsayılan olarak
işlemez. Reverse proxy kullanılıyorsa yalnızca proxy tarafından kontrol edilen
adresleri `.env`/deployment secret içinde açıkça tanımlayın:

```text
FORWARDED_HEADERS_FORWARD_LIMIT=1
FORWARDED_HEADERS_KNOWN_PROXIES=10.0.0.10,10.0.0.11
FORWARDED_HEADERS_KNOWN_NETWORKS=10.42.0.0/16
```

Bu listeler boş bırakıldığında istemcinin gönderdiği forwarded header'lar yok
sayılır ve rate-limit anahtarı doğrudan TCP peer adresinden üretilir. Trusted
ingress, istemcinin gönderdiği `X-Forwarded-For` ve `X-Forwarded-Proto` değerlerini
üstüne yazmalı veya strip etmelidir; yalnızca proxy adresini listelemek, kötü
yapılandırılmış bir proxy'nin istemci zincirini taşıması halinde spoofing'i
engellemez. Listeye internet istemcilerinin, `/0` ağlarının veya kontrol edilmeyen
ağların eklenmesi güvenlik açığıdır.

Redis tamamen kullanılamazsa yerel limiter yalnızca process kapsamındadır; bu,
erişilebilirlik için fail-open/fallback davranışıdır ve kalıcı kötüye kullanım
koruması değildir. Üretimde Redis geri geldiğinde dağıtık limiter otomatik olarak
yeniden devreye girer.

Compose dosyasında uygulama servisleri için sabit `container_name` kullanılmaz.
Gateway replica smoke testi, host portunu her replica'ya bind etmemek için override
dosyasıyla çalıştırılmalıdır:

```powershell
docker compose --env-file .env -f docker-compose.yml -f docker-compose.scale.yml `
  up -d --scale api-gateway=2
```

Bu profilde Gateway yalnızca Compose iç ağında dinler; edge/load-balancer dışarıda
olmalıdır. Kalıcı yatay ölçekleme ve gerçek istemci IP'si için production
ortamında Kubernetes/başka bir orkestratör ile Redis'in ortak kullanıldığı bir
dağıtım tercih edilmelidir.

## 3. JWT iptal (revocation) kararı

SystemAdmin için MFA varsayılan olarak kapalıdır ve kullanıcı bazında opt-in'dir.
Kullanıcı profil ayarlarından mevcut parolasını yeniden doğrulayarak MFA kurulumunu
başlatabilir. MFA etkinleştirildiğinde parola veya Google ilk faktörü yalnızca
5 dakikalık Data Protection challenge üretir; access/refresh token TOTP ya da tek
kullanımlık kurtarma kodundan sonra verilir. MFA ile açılan JWT `amr=mfa` ve
`auth_time` taşır. Refresh token `MfaVerifiedAt` değerini yeni token çiftine
aktarır; bu değeri taşımayan ve MFA sonradan etkinleştirilen eski SystemAdmin
refresh tokenı iptal edilir.

Kritik yönetici mutasyonları `MfaRequired` politikasını uygular; MFA kapalı bir
SystemAdmin giriş yapabilir ancak bu hassas mutasyonlar için önce MFA kurulumu
gerekir. TOTP secret'ı düz
metin saklanmaz, recovery kodları SHA-256 hash olarak ve tek kullanımlı tutulur.
Beş hatalı MFA denemesi kullanıcıyı beş dakika kilitler; aynı TOTP zaman adımı
ikinci kez kabul edilmez.

Mevcut erişim token'ları HMAC-SHA256 ile imzalanır ve `jti` içerir. Refresh token'ları
veritabanında tutulup döndürülür/revoke edilir. Erişim token'ı için tüm servislerde
her istekte veritabanı sorgusu yapılmadığından, kullanıcı pasifleştirme sonrası
token'ın kalan kısa ömrü boyunca kullanılabilmesi beklenen mevcut davranıştır.

Üretim geçişi:

1. `JWT_EXPIRY_MINUTES` değerini 15 dakika veya daha düşükte sabitleyin; dinamik
   `Auth.TokenLifetime` ayarı ile response'taki `ExpiresInMinutes` değerini aynı
   sözleşmede tutun.
2. Logout, password reset, rol değişikliği ve kullanıcı pasifleştirmesinde refresh
   token ailesini revoke edin.
3. Anında iptal zorunluysa kullanıcıya monotonic `TokenVersion` (veya
   `SecurityStamp`) ekleyin. Token'a `ver` claim'i yazın; değişiklikte değeri artırın.
4. Gateway/servisler için `auth:user:{userId}:ver` Redis kaydı ekleyin. JWT
   doğrulamasından sonra claim ile Redis değeri eşleşmiyorsa `401` verin.
5. Redis doğrulaması yapılamıyorsa yalnızca kimlik/rol değişikliği gibi ayrıcalıklı
   endpoint'lerde fail-closed politika uygulayın; normal public health endpoint'leri
   bu kontrolün dışında bırakın.
6. Tekil logout veya şüpheli token için `auth:revoked:jti:{jti}` anahtarı kullanın;
   TTL token'ın `exp` süresini geçmemelidir.

Bu adımların tamamı uygulanmadan `jti` claim'ini varmış gibi yorumlayıp kısmi bir
revocation kontrolü eklenmemelidir; aksi durumda servisler farklı güvenlik
politikaları uygular.

## 4. JWT ve internal service key rotasyonu

- JWT anahtar halkası `JWT_SECRET` (aktif) ve isteğe bağlı
  `JWT_PREVIOUS_SECRETS` (virgülle ayrılmış, validation-only) değerlerinden
  oluşur. `JWT_KEY_ID` ve `JWT_PREVIOUS_KEY_IDS` verilirse token header'ına
  `kid` yazılır ve bilinmeyen key id'ler fail-closed reddedilir. Aktif key id
  kullanılırken overlap'teki her eski secret için karşılık gelen bir
  `JWT_PREVIOUS_KEY_IDS` değeri verilmelidir; böylece eski `kid` taşıyan token'lar
  yanlışlıkla geçersizleşmez.
- Rotasyon sırası: yeni secret'ı tüm doğrulayıcılara `JWT_PREVIOUS_SECRETS` veya
  aktif değer olarak dağıtın → doğrulayıcıların hazır olduğunu gözlemleyin →
  Identity'de yeni secret'ı `JWT_SECRET` ve yeni `JWT_KEY_ID` yapın → en uzun
  access-token ömrü (ve güvenli bir saat payı) geçince eski secret'ı halkadan
  kaldırın. Eski secret yalnızca overlap süresinde tutulmalıdır.
- `JWT_SECRET` ve `INTERNAL_SERVICE_API_KEY` en az 32 rastgele byte olmalı; bilinen
  placeholder değerleri production ortamında reddedilmelidir.
- `ASPNETCORE_ENVIRONMENT=Production` olmalıdır; Compose bunu `.env` içindeki
  `ENVIRONMENT` değerinden alır. Örnek dosyadaki `replace-with-*` değerleri
  production başlangıcında bilinçli olarak fail-fast olur.
- DB, RabbitMQ, Redis ve SMTP parolaları da aynı bakım penceresinde döndürülmeli;
  servisler yeniden başlatılmadan eski değerler iptal edilmemelidir.
- Gerçek credential içeren `.env`, `.env.backup` ve log dosyaları repoya
  alınmamalıdır. Yalnızca açıkça placeholder içeren örnek dosyalar istisnadır.
  Geçmiş commit'lerde gerçek credential bulunduğu doğrulanırsa değerleri önce
  rotate edin; ardından kontrollü yedek alıp tüm branch/tag/ref geçmişinde
  history purge uygulayın ve remote'u `--force-with-lease` ile güncelleyin.

## 5. Data Protection ve RabbitMQ geçişi

- Servislerin Data Protection key ring'leri Compose named volume'larında
  kalıcıdır. Bu, container yeniden oluşturulduğunda cookie/SignalR/antiforgery
  anahtarlarının kaybolmasını önler; tek başına at-rest şifreleme değildir.
  Production'da `DataProtection:KeysPath` zorunludur ve her replica'nın aynı
  persistent/shared key ring'i görmesi gerekir. Aynı PFX sertifikasını mount
  etmek tek başına key ring paylaşımı sağlamaz; Kubernetes'te ortak PVC veya
  eşdeğer bir paylaşımlı repository kullanılmalıdır. Uygulama key path yoksa
  production startup'ında fail-fast kapanır.
- Production'da her servise aynı deployment'a ait, private key içeren bir
  X.509/PKCS#12 sertifikası secret olarak mount edin ve
  `DATAPROTECTION_CERTIFICATE_PATH` ile
  `DATAPROTECTION_CERTIFICATE_PASSWORD` değerlerini verin. Uygulama Production
  ortamında sertifika yoksa fail-fast kapanır; warning'i bastırmak için sahte
  veya repoya alınmış sertifika kullanılmamalıdır.
- Hazır Compose overlay'i bu mount'u yapar ve dört uygulama servisini zorunlu
  olarak `ASPNETCORE_ENVIRONMENT=Production` ile başlatır. PostgreSQL, Redis,
  RabbitMQ ve MailCatcher portlarını host'a açmaz; dış trafik yalnız Gateway
  üzerinden gelmelidir. Secret manager'dan host path ve parolayı vererek
  çalıştırın:

  ```powershell
  docker compose --env-file .env -f docker-compose.yml -f docker-compose.production.yml up -d
  ```

  `DATAPROTECTION_CERTIFICATE_HOST_PATH` yalnız deployment makinesinde bulunan
  PFX/PKCS#12 dosyasını göstermelidir; `secrets/` altındaki dosyalar repoya
  alınmaz.
- Production overlay'i gerçek SMTP ayarlarını da bekler. `.env.example` içindeki
  `SMTP_HOST=mailcatcher` yalnız geliştirme içindir; production `.env` dosyasında
  `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM_EMAIL`, `SMTP_USERNAME` ve
  `SMTP_PASSWORD` değerlerini gerçek sağlayıcıyla değiştirin. MailCatcher
  production profilinde başlatılmaz.
- Production overlay'i attachment fotoğrafları için MinIO'yu ve malware taraması
  için ClamAV'ı otomatik olarak etkinleştirir; bunlar host portlarına açılmaz.
  Coaching servisi her iki container'ın healthcheck'i geçmeden başlamaz.
- İlk SystemAdmin hesabı için `BOOTSTRAP_ADMIN_EMAIL` ve
  `BOOTSTRAP_ADMIN_PASSWORD` yalnızca ilk identity-service başlatmasında set
  edilir. Başarılı seed log'u görüldükten sonra `BOOTSTRAP_ADMIN_PASSWORD`
  deployment secret store'dan kaldırılır ve identity-service yeniden oluşturulur
  (yalnızca restart eski environment değerini container'dan kaldırmaz); parola
  veritabanında yalnızca hash olarak tutulur. Mevcut bir kullanıcı bu e-posta ile
  kayıtlıysa seeder rolü otomatik olarak yükseltmez ve açık hata loglar.

  ```powershell
  docker compose -f docker-compose.yml -f docker-compose.production.yml up -d --force-recreate identity-service
  ```

- Hostinger Email için production `.env` değerleri sağlayıcının verdiği gerçek
  mailbox ile doldurulmalıdır. Eduİvme alan adı için tipik ayarlar:
  `SMTP_HOST=smtp.hostinger.com`, `SMTP_PORT=465` (SSL; `587` STARTTLS alternatifi),
  `SMTP_FROM_EMAIL=<mailbox>@eduivme.com`, `SMTP_USERNAME=<mailbox>@eduivme.com`,
  `SMTP_PASSWORD=<mailbox-parolası>`, `SMTP_FROM_NAME=Eduİvme`. Kullanılan
  mailbox adının `eduivme` yazımını taşıdığı doğrulanmalıdır.
- Doğrulama ve parola akışlarının tarayıcı bağlantıları Notification servisinde
  `PublicApp__BaseUrl` / `PUBLIC_APP_BASE_URL` üzerinden üretilir. Bu değer
  public HTTPS frontend origin'i olmalı; credentials, query veya fragment
  içermemeli ve production'da `localhost` olmamalıdır.
- RabbitMQ tek node için sabit `rabbit@rabbitmq` node adı kullanır. Mevcut
  `rabbitmq_data` volume'u eski container-id tabanlı node adıyla oluşturulduysa
  bu geçiş queue/message metadata'sını otomatik taşımaz. Production geçişinden
  önce `rabbitmqctl export_definitions`/yedek alın, yeni node'u ayrı bir
  cutover ortamında doğrulayın ve gerekiyorsa kontrollü import/mesaj replay
  uygulayın. Volume silerek migration yapılmamalıdır.
- RabbitMQ 4.x'te legacy management metrics collector kapalı, Prometheus
  plugin'i açıktır. Uzun süreli grafik ve alerting için Prometheus/Grafana
  collector'ı RabbitMQ'nun iç ağdaki `15692` endpoint'inden scrape etmelidir.

## 5.1. Eduİvme root domain ve mevcut LiteSpeed

Mevcut VPS'te LiteSpeed 80/443 portlarını kullandığı için production edge
yalnızca loopback'te çalışır. Bu overlay, Eduİvme için `127.0.0.1:5200` portunu
kullanır; diğer sitelerin vhost/listener ayarlarına dokunulmaz:

Production Docker ağı `172.31.0.0/16`, Eduİvme edge'i `172.31.0.20` ve bağımsız
Hızlı Okuma edge'i `172.31.0.21` sabit adreslerini kullanır. LiteSpeed'in Docker
köprü adresi `172.31.0.1/32` olarak güvenilir; aynı ağdaki diğer container'lar
forwarded header üretemez. Bu değerler Gateway'in forwarded-header güveni ve
rate-limit istemci IP'si için birlikte ayarlanmıştır. VPS'te bu subnet başka bir ağla çakışıyorsa
`PRODUCTION_NETWORK_SUBNET`, `PRODUCTION_NETWORK_NAME`,
`FORWARDED_HEADERS_KNOWN_PROXIES` ve Caddyfile içindeki `trusted_proxies`
adresi aynı değişiklikle güncellenmelidir.

```bash
test -z "$(git status --porcelain)" || { echo 'Working tree must be clean'; exit 1; }
export RELEASE_TAG="$(git rev-parse HEAD)"
export PRODUCTION_IMAGE_REPOSITORY="eduivme"
RELEASE_DIR="/var/lib/eduivme/releases/${RELEASE_TAG}"
mkdir -p "$RELEASE_DIR"

# Keep the exact release inputs and rendered manifest for rollback/audit.
printf 'RELEASE_TAG=%s\nPRODUCTION_IMAGE_REPOSITORY=%s\n' \
  "$RELEASE_TAG" "$PRODUCTION_IMAGE_REPOSITORY" > "$RELEASE_DIR/release.env"
mkdir -p "$RELEASE_DIR/source"
git archive --format=tar HEAD | tar -xf - -C "$RELEASE_DIR/source"
docker compose --env-file .env \
  -f docker-compose.yml \
  -f docker-compose.production.yml \
  -f docker-compose.production.litespeed.yml config --no-interpolate > "$RELEASE_DIR/compose.config.yml"
docker run --rm \
  -v "$PWD/infrastructure/caddy/Caddyfile.production.litespeed:/etc/caddy/Caddyfile:ro" \
  caddy:2.9.1-alpine caddy validate --config /etc/caddy/Caddyfile
docker compose --env-file .env \
  -f docker-compose.yml \
  -f docker-compose.production.yml \
  -f docker-compose.production.litespeed.yml \
  build --pull
docker compose --env-file .env \
  -f docker-compose.yml \
  -f docker-compose.production.yml \
  -f docker-compose.production.litespeed.yml \
  up -d --no-build
docker compose --env-file .env \
  -f docker-compose.yml \
  -f docker-compose.production.yml \
  -f docker-compose.production.litespeed.yml \
  images > "$RELEASE_DIR/images.txt"
```

`RELEASE_TAG` değişmez bir Git commit etiketi olmalıdır; dağıtılmış bir etiket
tekrar kullanılmamalıdır. Her uygulama/migration imajı bu etiketle saklandığı
için önceki release imajları VPS'te tutulduğu sürece geri alma yeniden build
gerektirmez. Önceki release'in `release.env` dosyasını yükleyip aynı Compose
komutunu `up -d --no-build` ile çalıştırın:

```bash
set -a
. /var/lib/eduivme/releases/<onceki-commit>/release.env
set +a
docker compose --env-file .env \
  -f /var/lib/eduivme/releases/<onceki-commit>/source/docker-compose.yml \
  -f /var/lib/eduivme/releases/<onceki-commit>/source/docker-compose.production.yml \
  -f /var/lib/eduivme/releases/<onceki-commit>/source/docker-compose.production.litespeed.yml \
  up -d --no-build
```

Deploy öncesinde yalnızca Eduİvme vhost ve edge dosyalarının yedeğini alın;
diğer sitelerin vhost/listener dosyalarına dokunmayın. Geri alma sırasında
önceki vhost yedeğini geri yükleyip `lshttpd -t` ve `lswsctrl reload` çalıştırın.

LiteSpeed'e yalnızca `infrastructure/litespeed/eduivme.com.vhost.conf` içindeki
yeni vhost eklenmelidir. Önce DNS'in `eduivme.com`, `www.eduivme.com` ve
kullanılacak `.com.tr` isimlerini VPS'e çözdüğünü doğrulayın. Sonra ACME
challenge docroot'u ve sertifikayı oluşturun:

```bash
mkdir -p /home/eduivme.com/public_html/.well-known/acme-challenge
certbot certonly --webroot \
  -w /home/eduivme.com/public_html \
  -d eduivme.com -d www.eduivme.com \
  -d eduivme.com.tr -d www.eduivme.com.tr \
  --email <sertifika-iletisim-adresi> --agree-tos --no-eff-email
```

Sertifika yenileme hook'unu bir kez kurup önce dry-run ile doğrulayın. Hook
yalnızca `eduivme.com` lineage'ı yenilendiğinde LiteSpeed'i reload eder:

```bash
install -m 0750 infrastructure/litespeed/eduivme-certbot-deploy-hook.sh \
  /etc/letsencrypt/renewal-hooks/deploy/eduivme-litespeed-reload
certbot renew --dry-run
```

`.com.tr` DNS'i henüz hazır değilse önce yalnızca `.com` isimleriyle sertifika
alın; `.com.tr` kayıtları yayıldıktan sonra aynı sertifikayı SAN isimleriyle
yenileyin. Sertifika alındıktan sonra LiteSpeed yapılandırmasını doğrulayın ve
graceful reload yapın:

```bash
/usr/local/lsws/bin/lshttpd -t
/usr/local/lsws/bin/lswsctrl reload
```

Ardından aşağıdaki smoke testleri çalıştırın:

```bash
curl --fail --silent https://eduivme.com/health/live
curl --fail --silent https://eduivme.com/health/ready
```

### Bağımsız Hızlı Okuma domaini

`masterhizliokuma.com` ayrı uygulama olarak yayınlanacaksa aynı release ile
`docker-compose.production.litespeed.yml` overlay'i de kullanılmalıdır. Bu
overlay, yalnızca `speed-reading-edge` için host loopback `127.0.0.1:5201`
portunu açar ve sabit Docker IP'si `172.31.0.21` kullanır. Gateway'in güvenilen
proxy listesinde hem Eduİvme edge'i (`172.31.0.20`) hem de bu edge bulunmalıdır.
LiteSpeed vhost'u bu sabit porta yönlenir; portu env ile değiştirmeyin.

Deploy öncesi yalnızca hızlı okuma vhost'unun ve edge release bilgilerinin
yedeğini alın:

```bash
install -d -m 0750 /root/backups/speedreading-deploy/<commit>
cp /usr/local/lsws/conf/vhosts/masterhizliokuma.com/vhost.conf \
  /root/backups/speedreading-deploy/<commit>/masterhizliokuma.com.vhost.conf
cp /opt/eduivme/.env /root/backups/speedreading-deploy/<commit>/eduivme.env
```

`infrastructure/litespeed/masterhizliokuma.com.vhost.conf` dosyasını yalnızca
bu vhost'a kurun. Sertifika dosyaları deploy öncesi mevcut olmalı; yoksa önce
challenge docroot'unu oluşturup HTTP-01 ile alın:

```bash
mkdir -p /home/masterhizliokuma.com/public_html/.well-known/acme-challenge
certbot certonly --webroot \
  -w /home/masterhizliokuma.com/public_html \
  -d masterhizliokuma.com -d www.masterhizliokuma.com \
  --email <sertifika-iletisim-adresi> --agree-tos --no-eff-email
```

Vhost'taki `/.well-known/acme-challenge` context'i HTTPS redirect'inden
muaf tutulmuştur; böylece yenileme sırasında challenge dosyası Caddy'ye
proxylenmez. Kurulumdan sonra yapılandırmayı ve domain akışını doğrulayın:

```bash
/usr/local/lsws/bin/lshttpd -t
/usr/local/lsws/bin/lswsctrl reload
curl --fail --silent https://masterhizliokuma.com/health/live
curl --fail --silent https://masterhizliokuma.com/health/ready
curl --fail --silent https://masterhizliokuma.com/api/speed-reading/exercise-types?pageNumber=1\&pageSize=1
```

Geri alma gerekiyorsa yeni SpeedReading edge/service'i durdurun, yalnızca
yedeklenen hızlı okuma vhost'unu geri yükleyin, ardından `lshttpd -t` ve
`lswsctrl reload` çalıştırın. Eduİvme vhost'una ve diğer sitelere dokunmayın.

## 6. CI release kapıları

```powershell
dotnet build services/identity-service/Identity.API/Identity.API.csproj -c Release
dotnet build services/coaching-service/Coaching.API/Coaching.API.csproj -c Release
dotnet build services/api-gateway/EduPlatform.Gateway.csproj -c Release
dotnet build services/notification-service/Notification.API/Notification.API.csproj -c Release
dotnet build services/speed-reading-service/SpeedReading.API/SpeedReading.API.csproj -c Release
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj -c Release
git diff --check
```

Docker tabanlı entegrasyonlar ayrı bir CI job'ında çalıştırılmalı; Docker olmayan
geliştirici makinelerinde yalnızca derleme ve container dışı test sonucu başarılı
sayılmalıdır.

Development/E2E smoke için Compose'a `TEST_ADMIN_PASSWORD` ve
`TEST_DEFAULT_PASSWORD` değerleri yalnızca geçici olarak verilebilir. Bu değerler
Production'da boş bırakılmalı; demo kullanıcı seed'i gerçek deployment sırrı veya
kalıcı yönetici hesabı olarak kullanılmamalıdır.
