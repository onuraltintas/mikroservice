# Gateway E2E doğrulama

EduPlatform'ın Gateway arkasındaki kritik dış davranışı Playwright ile
`tests/E2E` altında doğrulanır. Testler ücretli bir servis kullanmaz; Playwright
Apache-2.0 lisanslı bir geliştirme bağımlılığıdır.

## Kapsam

- Gateway health/readiness edge'i
- Login ve destek gönderiminin anonim bootstrap davranışı
- Destek yanıtı ve yönetim yüzeylerinin anonim erişime kapalı olması
- Admin yönetim API'lerinin Gateway üzerinden 401 ile korunması
- Yetkili admin ile login → refresh-token → yönetim yüzeylerini okuma
- Admin panel login ekranının tarayıcıda açılması
- Yetkili admin ile panel login → dashboard geçişi (staging kimlik bilgisi gerekir)
- Disposable ortamda support submit → aynı idempotency anahtarıyla retry

Testler varsayılan olarak yalnızca Docker'da çalışan Gateway'e bağlanır ve
`http://127.0.0.1:5000` kullanır. E2E testleri business/support verisi yazmaz;
yetkili akış login ve refresh-token metadata kayıtları oluşturabilir. Kayıt/e-posta, tenant write ve
SignalR senaryoları disposable staging tenant ve MailCatcher/SMTP erişimi olan
ayrı bir profile bağlanmalıdır; ortak production verisiyle çalıştırılmamalıdır.

## Lokal çalıştırma

Önkoşul: `docker compose --env-file .env up -d` ile Gateway ve bağımlı servisler
çalışıyor olmalı.

```powershell
npm ci --prefix tests/E2E
npm run install:browsers --prefix tests/E2E
npm run test:critical --prefix tests/E2E
```

Admin panel login ekranını da çalıştırmak için Angular dev server'ını Playwright
başlatır:

```powershell
$env:E2E_START_UI = 'true'
npm test --prefix tests/E2E -- --project=admin-panel-ui
```

Yetkili admin akışını çalıştırmak için disposable/staging SystemAdmin bilgilerini
process ortamından verin. Parola komut satırına veya repoya yazılmamalıdır:

```powershell
$env:E2E_REQUIRED = 'true'
$env:E2E_ADMIN_EMAIL = 'admin@example.test'
$env:E2E_ADMIN_PASSWORD = '<secret-from-secret-store>'
$env:E2E_RUN_UI = 'true'
$env:E2E_START_UI = 'true'
npm test --prefix tests/E2E
```

SystemAdmin MFA zorunlu olduğundan E2E hesabının authenticator secret'ını kaynak
kodda veya CI değişkenlerinde düz metin tutmayın. Disposable admin'i her koşuda
`/api/auth/mfa/setup` akışıyla kaydedin ve koşu sonunda temizleyin. Kurtarma
kodları yalnız ilk kurulum cevabında görünür; loglara veya ekran görüntüsü
artifact'lerine yazılmamalıdır.

Support write sözleşmesi yalnız ayrı database/SMTP kullanan disposable bir ortamda
çalıştırılır. Test kalıcı bir support kaydı oluşturduğu için bu iki anahtar
birlikte zorunludur:

```powershell
$env:E2E_DISPOSABLE_ENV = 'true'
$env:E2E_RUN_SUPPORT_WRITE = 'true'
npm run test:critical --prefix tests/E2E
```

`E2E_REQUIRED=true` kimlik bilgisi yoksa test keşfi sırasında fail eder; yanlışlıkla
korumalı akışın sessizce skip edilmesini engeller. Kimlik bilgisi verilmezse
yalnızca anonim Gateway sözleşmesi ve public login ekranı çalışır; bu lokal smoke
çalıştırmalarında bilinçli davranıştır.

Staging veya farklı bir Gateway için API adresini ayrıca verebilirsiniz. UI testi
aynı-origin `/api` proxy'sini kullandığından `E2E_UI_BASE_URL` UI'nın Gateway
route'larının da yayınlandığı origin olmalıdır:

```powershell
$env:E2E_API_BASE_URL = 'https://staging-api.example.com'
$env:E2E_UI_BASE_URL = 'https://staging.example.com'
```

## CI/CD

`.github/workflows/e2e.yml` yalnız manuel `workflow_dispatch` ve `staging`
environment ile çalışır. `E2E_API_BASE_URL`, `E2E_UI_BASE_URL`,
`E2E_ADMIN_EMAIL` ve `E2E_ADMIN_PASSWORD` GitHub Environment secret olarak
tanımlanmadan job başlatılmamalıdır. Raporlama HTML/JUnit ile sınırlıdır; auth
akışlarında token, parola, network trace veya ekran görüntüsü CI artefact'ı olarak
toplanmaz. Ortak staging verisine yazan yeni senaryolar eklenirse
her test kendi disposable tenant'ını üretmeli ve teardown yapmalıdır.
Support write akışı varsayılan olarak kapalıdır; manuel workflow'da yalnız
`run_support_write=true` ve staging environment variable
`E2E_DISPOSABLE_ENV=true` ise açılır.

## Rapor ve hata ayıklama

- HTML: `tests/E2E/playwright-report/`
- JUnit: `tests/E2E/playwright-results.xml`
- CI artefact'ı: HTML/JUnit raporu; auth içeren testlerde trace/video/screenshot
  kapalıdır.

Test sözleşmesi Gateway route'larını ve HTTP durum kodlarını kontrol eder; servis
iç implementasyonuna değil kullanıcıya görünen davranışa dayanır. Yeni route veya
izin değişikliğinde önce bu sözleşme güncellenmeli, sonra Docker Gateway üzerinde
yeşil sonuç alınmalıdır.
