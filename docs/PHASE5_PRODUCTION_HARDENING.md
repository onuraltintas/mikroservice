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
  rotate edin; history purge işlemi için ayrıca onay ve yedekleme gerekir.

## 5. CI release kapıları

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
