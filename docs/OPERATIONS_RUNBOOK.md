# Staging operasyon runbook'u

Bu runbook production'a geçişten önce disposable/staging ortamında prova
edilmesi gereken release, migration, backup/restore, secret rotation ve rollback
adımlarını toplar. Komutlar gerçek production verisine karşı çalıştırılmamalıdır;
restore adımları özellikle ayrı bir PostgreSQL database/volume üzerinde denenir.

## 1. Release ve immutable image

1. CI'nin `Release --warnaserror`, test, npm audit, EF drift, vulnerability ve
   Compose/monitoring kapıları yeşil olmalıdır.
2. İmajlar branch adıyla değil commit SHA ile etiketlenir. Registry push ve deploy
   ayrı, onaylı bir job olmalıdır.

The deployment job must provide `CONTAINER_REGISTRY` and an external,
write-controlled `STAGING_ARTIFACT_ROOT`. The registry path must not come from
untrusted pull-request input.

`SupportForwardDelivery` ekleyen release, Notification yazıcısını değiştirdiği
için özel bir **no-overlap** geçişidir: Gateway'de `POST /api/support/submit`
bakım moduna alınır, uçuşta olan istekler en fazla request timeout süresi kadar
beklenir, eski Notification replica'ları tamamen drain edilir ve migration
uygulanır. Ardından eski Identity replica'ları tamamen drain edilir, yeni
Identity image'ı tüm replica'larda healthy olana kadar beklenir; yalnız bundan
sonra yeni Notification image'ı ve forward worker başlatılır. Son health
doğrulamasından sonra bakım modu kaldırılır. Eski Identity replica'sı
deterministik olmayan event kimliği üretebildiği için bu sıralama zorunludur.
Migration öncesi SupportRequest kayıtları otomatik yeniden bildirilmez;
gerekiyorsa onaylı, denetlenebilir bir manuel replay yapılır.

```powershell
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Native {
  param(
    [Parameter(Mandatory)][string]$Description,
    [Parameter(Mandatory)][scriptblock]$Command
  )

  $output = & $Command
  if ($LASTEXITCODE -ne 0) {
    throw "$Description failed with native exit code $LASTEXITCODE."
  }

  return $output
}

$sha = (@(Invoke-Native -Description 'Read the Git revision' -Command {
  git rev-parse --verify HEAD
}) | Select-Object -First 1).Trim()
$registry = $env:CONTAINER_REGISTRY
$artifactRoot = $env:STAGING_ARTIFACT_ROOT
if ([string]::IsNullOrWhiteSpace($registry) -or [string]::IsNullOrWhiteSpace($artifactRoot)) {
  throw 'CONTAINER_REGISTRY and STAGING_ARTIFACT_ROOT must be provided by the deployment environment.'
}

$composeProject = 'eduplatform-release'
$composeArgs = @('--project-name', $composeProject, '--env-file', '.env.example')
Invoke-Native -Description 'Build release images' -Command {
  docker compose @composeArgs build --pull
} | Out-Host
$services = @('identity-service', 'coaching-service', 'notification-service', 'api-gateway')
$manifestPath = Join-Path $artifactRoot "eduplatform-$sha-images.json"
$manifest = foreach ($service in $services) {
  $imageId = (@(Invoke-Native -Description "Locate the $service image" -Command {
    docker image ls `
      --filter "label=com.docker.compose.project=$composeProject" `
      --filter "label=com.docker.compose.service=$service" `
      --format '{{.ID}}'
  }) | Select-Object -First 1).Trim()
  if ([string]::IsNullOrWhiteSpace($imageId)) { throw "No image was produced for $service." }

  $reference = "$registry/eduplatform/$service`:$sha"
  Invoke-Native -Description "Tag $service" -Command { docker image tag $imageId $reference } | Out-Host
  Invoke-Native -Description "Push $service" -Command { docker push $reference } | Out-Host
  $digest = (@(Invoke-Native -Description "Inspect the $service registry digest" -Command {
    docker buildx imagetools inspect $reference --format '{{json .Manifest.Digest}}'
  }) | Select-Object -First 1).Trim()
  if ([string]::IsNullOrWhiteSpace($digest)) { throw "No registry digest was returned for $reference." }

  [pscustomobject]@{ Service = $service; Commit = $sha; Reference = $reference; Digest = $digest }
}
$manifest | ConvertTo-Json | Set-Content -Encoding UTF8 -Path $manifestPath
Invoke-Native -Description 'Validate the release Compose configuration' -Command {
  docker compose @composeArgs config --quiet
} | Out-Host
```

The deployment overlay must consume the recorded `reference@digest` values and
must not rebuild or deploy `latest`. The staging manifest stores the commit,
registry digest, migration result and deployment time. A rollback selects the
previous manifest digest, never a mutable tag.

## 2. Migration ve rollback

Production overlay'inde migration web replica'larından önce tek seferlik
`*-migrations` job'larında çalışır. Migration job başarıyla bitmeden servisler
başlamaz. Şema değişiklikleri expand/contract (önce geriye uyumlu kolon/index,
sonra kod, en son cleanup) şeklinde hazırlanır.

- Migration öncesi PostgreSQL custom-format backup alınır ve checksum yazılır.
- Migration job logunda hedef database, migration assembly ve sonuç saklanır;
  parola veya connection string loglanmaz.
- Başarısız migration'da web rollout durdurulur. Otomatik `down` migration
  çalıştırılmaz; önceki image yalnızca şema ile geriye uyumluysa geri alınır.
- Destructive bir şema değişikliğinde disposable restore üzerinde rollback
  prova edilmeden production onayı verilmez.

## 3. PostgreSQL backup/restore provası

Backup dosyaları repo dışında, erişim kontrollü bir dizinde tutulur. Binary
custom-format stream için WSL/bash veya Linux CI runner kullanılmalıdır; bu,
Windows PowerShell'in olası encoding dönüşümlerini devreden çıkarır.

```bash
set -euo pipefail
: "${STAGING_BACKUP_ROOT:?Set STAGING_BACKUP_ROOT to an external, access-controlled directory}"
: "${POSTGRES_DB_IDENTITY:?Set database names from the deployment config store}"
: "${POSTGRES_DB_COACHING:?Set database names from the deployment config store}"
: "${POSTGRES_DB_NOTIFICATION:?Set database names from the deployment config store}"

backup_root="$STAGING_BACKUP_ROOT/$(date -u +%Y-%m-%dT%H%M%SZ)"
mkdir -p "$backup_root"
postgres_user="$(docker compose exec -T postgres printenv POSTGRES_USER | tr -d '\r\n')"
: "${postgres_user:?The PostgreSQL container did not expose POSTGRES_USER}"

for database in "$POSTGRES_DB_IDENTITY" "$POSTGRES_DB_COACHING" "$POSTGRES_DB_NOTIFICATION"; do
  path="$backup_root/$database.dump"
  docker compose exec -T postgres pg_dump \
    --format=custom --no-owner --no-acl \
    --username "$postgres_user" --dbname "$database" > "$path"
  test -s "$path"
done

sha256sum "$backup_root"/*.dump | tee "$backup_root/SHA256SUMS"
```

Restore provası yeni, boş bir database/volume'a yapılır; canlı database'e
`--clean`/`DROP DATABASE` uygulanmaz:

```bash
set -euo pipefail
: "${STAGING_BACKUP_ROOT:?Set STAGING_BACKUP_ROOT to the external backup root}"
postgres_user="$(docker compose exec -T postgres printenv POSTGRES_USER | tr -d '\r\n')"
restore_database='identity_restore_drill'
docker compose exec -T postgres createdb --username "$postgres_user" "$restore_database"
docker compose exec -T postgres pg_restore \
  --exit-on-error --no-owner --no-acl \
  --username "$postgres_user" --dbname "$restore_database" \
  < "$STAGING_BACKUP_ROOT/2026-08-19T000000Z/identity_db.dump"
docker compose exec -T postgres psql \
  --username "$postgres_user" --dbname "$restore_database" \
  --set ON_ERROR_STOP=1 \
  --command '\dt identity.*'
```

Every backup and restore command is fail-closed: non-zero exit status or an
empty dump stops the drill before a checksum or health result is recorded.
Prova çıktısı:
restore süresi, satır/tablo doğrulaması, uygulama health/readiness sonucu ve
RPO/RTO ölçümü. Başarısız restore production go/no-go'yu durdurur.

Redis rate-limit/cache state'i kaybolabilir; Redis backup'ı iş verisi değildir.
RabbitMQ definitions ve kritik queue durumları ayrıca export edilir:

```bash
set -euo pipefail
: "${STAGING_BACKUP_ROOT:?Set STAGING_BACKUP_ROOT to the external backup root}"
rabbit_backup_root="$STAGING_BACKUP_ROOT/$(date -u +%Y-%m-%dT%H%M%SZ)"
mkdir -p "$rabbit_backup_root"
docker compose exec -T rabbitmq rabbitmqctl export_definitions /tmp/definitions.json
docker compose cp rabbitmq:/tmp/definitions.json "$rabbit_backup_root/rabbitmq-definitions.json"
test -s "$rabbit_backup_root/rabbitmq-definitions.json"
```

Mesaj replay'i otomatik yapılmaz; dead-letter ve durable queue replay'i olay
özeline göre onaylanır.

### 22.08.2026 yerel Docker prova kanıtı

Disposable Docker ortamında üç PostgreSQL custom-format backup'ı alındı ve
checksum manifest'i üretildi. Identity backup'ı ayrı bir
`identity_restore_drill_<timestamp>` veritabanına restore edildi; tablo ve
kullanıcı sayısı doğrulandıktan sonra yalnızca bu disposable veritabanı
kaldırıldı. RabbitMQ definitions export'u ve commit/image manifest'i de repo
dışındaki, erişim kontrollü artifact köküne yazıldı. Bu kanıtlar repository'ye
eklenmedi ve production backup'ı yerine geçmez; staging'de aynı adımlar gerçek
artifact store, RPO/RTO ölçümü ve erişim onayıyla tekrarlanmalıdır.

## 4. Secret ve key rotation

- Yeni `JWT_SECRET` ve `JWT_KEY_ID` önce doğrulayıcıların `JWT_PREVIOUS_SECRETS`
  / `JWT_PREVIOUS_KEY_IDS` overlap listesine eklenir.
- Gateway ve servislerin yeni halkayı okuyabildiği staging login/refresh ve
  eski-token kabul testleriyle doğrulanır.
- Identity yeni aktif anahtarla deploy edilir; en uzun access-token ömrü + saat
  payı geçince eski secret halkadan çıkarılır.
- `INTERNAL_SERVICE_API_KEY`, PostgreSQL, Redis, RabbitMQ, SMTP ve Data Protection
  certificate secret'ları aynı anda değil, bağımlılık sırasıyla döndürülür.
- `PUBLIC_APP_BASE_URL` staging/production'da doğrulanmış public HTTPS frontend
  origin'ine ayarlanır; kayıt ve parola e-postelerindeki bağlantılar bu değeri
  kullanır.
- Eski değerler revoke edilmeden önce iki replica'da health/readiness ve
  service-to-service çağrı smoke testi alınır. Değişkenler loglanmaz.

Gerçek credential geçmişte commit edilmişse önce tüm değerler rotate edilir;
history purge (git filter/rewrite) kontrollü yedek ve force-with-lease push ile
uygulanır. İşlem tamamlandıktan sonra eski commit SHA'ları ve yerel klonlar
geçersiz kabul edilir.

### Repository credential olayı müdahalesi

Bu prosedür, bir secret veya credential'ın Git geçmişine girdiği doğrulandığında
uygulanır. `git revert` yeterli değildir; hassas blob eski commit'te kalır.

1. Önce etkilenen sağlayıcıdaki credential'ı revoke/rotate edin. PostgreSQL,
   RabbitMQ, Redis, SMTP, Keycloak admin/database/client ve test yönetici
   hesapları ayrı ayrı doğrulanır; secret değeri ticket, log veya commit'e
   yazılmaz.
2. Yeni değerle health/readiness, login/refresh ve servisler arası çağrı smoke
   testini doğrulayın. JWT için mevcut overlap sırası korunur.
3. Her yolu doğrulayarak `git-filter-repo --sensitive-data-removal` ile hassas
   dosyayı ya da metni tüm branch/tag/ref geçmişinden çıkarın. Uzak dalları
   yalnız güncel uç SHA'lara karşı `--force-with-lease` ile güncelleyin.
4. Eski history'den türemiş tüm yerel klonlar yeniden klonlanır; eski dalı merge
   etmek sızıntıyı geri getirebileceğinden kullanılmaz. Korunmamış yerel iş
   varsa önce patch alınır.
5. Açık repository, fork veya pull request geçmişi varsa GitHub Support'a
   repository adı, etkilenen pull request sayısı ve `git-filter-repo` tarafından
   bildirilen ilk değişen commit bilgisiyle purge talebi açın. Force-push tek
   başına GitHub cache'lerini ve diğer klonları garantiyle silmez. Ayrıntılar:
   [GitHub hassas veri temizleme prosedürü](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository).

2026-08-21'de geçmişteki `infrastructure/docker/.env.backup` dosyası ve
credential örnekleri `main` ile `codex/platform-hardening` dallarının tüm
erişilebilir geçmişinden çıkarıldı. Uzak branch'ler beklenen SHA'lara karşı
`--force-with-lease` ile güncellendi; doğrulamada `.env.backup` yolu ve bilinen
credential literal'leri bulunmadı. Yerel PostgreSQL, RabbitMQ, Redis, Grafana,
JWT ve internal-service-key değerleri döndürüldü; container health/readiness ve
Gateway smoke testi başarılıdır. Eski Keycloak/harici SMTP kurulumu hâlâ
kullanılıyorsa sağlayıcı erişimi olan kişi ilgili credential'ı ayrıca revoke veya
rotate etmelidir. GitHub Actions secret'larının listelenmesi/revokasyonu için
geçerli GitHub yetkisi gerekir; force-push bu secret'ları değiştirmez.

CI, `.env`/`.env.backup` benzeri izlenen dosyaları ve yaygın private-key
biçimlerini reddeder. Bu koruma secret manager, kısa ömürlü credential ve kod
incelemesinin yerine geçmez.

## 5. Canary, readiness ve rollback

1. Migration job tamamlanır; yeni image tek staging replica ile başlatılır.
2. `/health/live`, `/health/ready`, Gateway health, login/refresh, support
   validation ve admin 401 E2E smoke çalıştırılır.
3. 15 dakika boyunca 5xx, p95/p99, DB pool, Redis fallback, Rabbit backlog ve
   email delivery sinyalleri izlenir.
4. Sonra replica sayısı artırılır; her replica aynı image digest ve Data
   Protection key ring'i kullanır.
5. Eşik aşılırsa rollout durur ve önceki immutable image digest'ine dönülür.
   Şema geriye uyumlu değilse rollback yerine forward fix uygulanır.

## 6. Go/no-go kaydı

Release kaydında şu alanlar doldurulmadan production deployment yapılmaz:

| Alan | Kanıt |
| --- | --- |
| Commit/image digest | CI run URL ve image manifest |
| Migration | job logu, hedef migration, backup checksum |
| Restore | disposable restore süresi ve doğrulama çıktısı |
| RPO/RTO | ölçülen değer ve onaylanan hedef |
| Secret rotation | rotation zamanı, overlap ve revoke kanıtı |
| Canary | 15 dakika metric snapshot'ı |
| Alarm sahipliği | on-call kişi/kanal ve escalation süresi |
| Rollback | önceki digest ve şema uyumluluk kararı |

Bu kayıtların biri eksikse sistem staging'de kalır.
