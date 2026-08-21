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

SystemAdmin oturumları için MFA zorunludur. Parola veya Google ilk faktörü yalnız
5 dakikalık Data Protection challenge üretir; access/refresh token TOTP ya da tek
kullanımlık kurtarma kodundan sonra verilir. MFA ile açılan JWT `amr=mfa` ve
`auth_time` taşır. Refresh token `MfaVerifiedAt` değerini yeni token çiftine
aktarır; bu değeri taşımayan eski SystemAdmin refresh tokenı iptal edilir.

Kritik yönetici mutasyonları `MfaRequired` politikasını uygular. TOTP secret'ı düz
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
  `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM_EMAIL` ve gerekiyorsa SMTP kullanıcı adı/
  parolasını gerçek sağlayıcıyla değiştirin. MailCatcher production profilinde
  başlatılmaz.
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

## 6. CI release kapıları

```powershell
dotnet build services/identity-service/Identity.API/Identity.API.csproj -c Release
dotnet build services/coaching-service/Coaching.API/Coaching.API.csproj -c Release
dotnet build services/api-gateway/EduPlatform.Gateway.csproj -c Release
dotnet build services/notification-service/Notification.API/Notification.API.csproj -c Release
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj -c Release
git diff --check
```

Docker tabanlı entegrasyonlar ayrı bir CI job'ında çalıştırılmalı; Docker olmayan
geliştirici makinelerinde yalnızca derleme ve container dışı test sonucu başarılı
sayılmalıdır.
