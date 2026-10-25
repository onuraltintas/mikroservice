# Master Hızlı Okuma — öğrenci ve tanıtım uygulaması

Angular 21 ve Angular Material 21 tabanlı bağımsız istemci. Öğrenci çalışma alanı, egzersiz motorları, seviye tespiti, içerik sayfaları ve giriş/kayıt burada bulunur. Öğretmen ve kurum çalışma alanları `../admin-panel/projects/staff-portal`, merkezi yönetim `../admin-panel` içindedir.

Hızlı Okuma verisi frontend'e bağlanmaz; istemci Gateway üzerinden `/api/speed-reading` API'sini kullanır. Kimlik ve servis erişimi ortak Identity altyapısındadır; Koçluk ve Hızlı Okuma rolleri/abonelikleri ayrı değerlendirilir.

## Gereksinimler ve çalıştırma

- Node.js 22.12 veya üzeri bir 22.x sürümü (CI: 22.x), npm ve kilitli `package-lock.json`.
- Backend'i yerel çalıştırmak için .NET 10; PostgreSQL entegrasyon testleri için çalışan Docker motoru.
- Öğrenci birim testleri için Chrome/Chromium; tarayıcı E2E testleri için `tests/E2E` bağımlılıkları ve ilgili tarayıcı.

Bu klasörden:

```powershell
npm ci --ignore-scripts
npm start
```

Uygulama varsayılan olarak `http://localhost:4200` üzerinde açılır. `proxy.conf.json`, `/api` isteklerini `http://localhost:5000` Gateway adresine iletir. Üretimde API aynı origin arkasındaki edge üzerinden sunulur. Gizli anahtar veya veritabanı bağlantısı frontend environment dosyalarına konmaz.

## Kaynak yapısı

```text
src/app/
  core/                  # Kimlik, yetki, API servisleri, modeller, guard ve interceptor'lar
  features/auth/         # Öğrenci, öğretmen, kurum kayıt ve giriş ekranları
  features/public/       # Tanıtım, CMS, blog, fiyatlandırma ve ödeme ekranları
  features/student/      # Profil kurulumu, assessment, program, ödev, rapor ve egzersizler
    exercises/universal-player/
      engines/           # Egzersiz motorları
  features/coaching/     # Merkezi koçluk paneline uyumluluk yönlendirmesi
  shared/                # Kullanılan ortak istemci bileşenleri
public/                  # Build'e kopyalanan görsel, favicon ve statik dosyalar
../shared/               # Admin/staff ile ortak stiller ve tipler
```

Öğrenci route'ları `features/student/student.routes.ts`, public route'lar `features/public/public.routes.ts`, uygulama girişleri `app.routes.ts` içindedir. Eski `/teacher`, `/admin` ve koçluk adresleri güncel merkezi panellere yönlendirilir; bu yönlendirmeler uyumluluk için korunur.

## Akış ve veri sınırları

- Profil kurulumu ve ilk seviye tespiti, öğrencinin programa hazırlanmasını sağlar. İlk assessment abonelik gerektirmez; normal çalışma alanlarında abonelik ve servis erişimi kontrol edilir.
- Sunucuya bağlı egzersizlerde oturum/yanıt ve sonuç doğrulaması backend'dedir. Önizleme sonuçları öğrenci ilerlemesi olarak kaydedilmez.
- Normal görsel genişlemede her doğru cevapta uzaklık bir birim artar; yanlışta aynı kalır. Seviye tespitinin sürümlenmiş protokolü ayrıdır.
- Takistoskop özel önizlemesinde basamak sayısı sabittir ve minimum gösterim süresi 75 ms'dir. Öğrenciye atanmış egzersizlerin ayarları ayrı kalır.
- Saccade/takip gibi göz hareketlerini ölçmeyen egzersizlerde gösterim temposu, süre veya geçiş sayısı gerçek okuma hızı olarak sunulmaz.

## Test komutları

Bu klasörden öğrenci birim testleri ve şablon/backend sözleşmeleri:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless --progress=false
npm test -- --watch=false --browsers=ChromeHeadless --progress=false --code-coverage
npm run test:contracts
```

Chrome otomatik bulunamazsa `CHROME_BIN` ortam değişkenine kurulu tarayıcının yolunu verin. `test:contracts`, `template-contracts` ve backend altındaki `.test.mjs` dosyalarını birlikte çalıştırır; API veya gerçek veritabanı başlatmaz. CI öğrenci testleri, sözleşme testleri ve production build'i çalıştırır.

Depo kökünden backend testleri:

```powershell
dotnet test tests/Unit/SpeedReading.Application.UnitTests/SpeedReading.Application.UnitTests.csproj
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --filter "FullyQualifiedName~SpeedReading"
```

İkinci komut ortak entegrasyon projesindeki Hızlı Okuma testlerini seçer. PostgreSQL testleri Testcontainers ile geçici veritabanı açar ve temizler; canlı bağlantı bilgileri kullanılmaz. Unit projesi API/Infrastructure testleri de içerir; PostgreSQL kalıcılığının yerine geçmez.

Tarayıcı testleri Cypress yerine Playwright kullanır. `tests/E2E` bağımlılıklarını kurduktan sonra depo kökünden örnekler:

```powershell
npm ci --prefix tests/E2E
node tests/E2E/node_modules/@playwright/test/cli.js test --config tests/E2E/speed-reading-visual-expansion.config.mjs
node tests/E2E/node_modules/@playwright/test/cli.js test --config tests/E2E/speed-reading-tachistoscope.config.mjs
```

Bu özel config'ler varsayılan yerel uygulamayı başlatır; ilgili spec'lerde API yanıtları fixture ile taklit edilir. Ortam değişkenleriyle tanımlanan base URL'leri kontrol edin. `npm run test:speed-reading --prefix tests/E2E` ise yalnız Gateway assessment API projesini çalıştırır; bütün egzersiz UI testlerini çalıştırmaz. Gerçek kayıt/ödeme/yazma akışları yalnız silinebilir test ortamında doğrulanır.

## Build ve yayın

```powershell
npm run build -- --configuration development
npm run build -- --configuration production
```

Çıktı `dist/speed-reading-frontend/browser` altındadır. Tanımlı yapılandırmalar development/production'dır; `staging` adlı Angular build yapılandırması yoktur. Üretim build'i environment değişimi, minification, hash'li asset ve service worker kullanır. Başlangıç bütçesi 3 MB uyarı / 5 MB hata; component CSS bütçesi 120 kB uyarı / 200 kB hatadır.

Frontend Dockerfile'ın build context'i `clients/` klasörüdür; `../shared` de image build'ine girer. `build-release.txt`, Dockerfile'da service worker sürüm işaretinde kullanılır. Yayın, API migration ve rollback işlemleri [servis belgesi](../../docs/SPEED_READING_SERVICE.md) ve [operasyon runbook'u](../../docs/OPERATIONS_RUNBOOK.md) üzerinden yürütülür.

## İlgili belgeler

- [Servis mimarisi, veri sınırı ve API](../../docs/SPEED_READING_SERVICE.md)
- [Araştırma/ölçüm protokolü](../../docs/SPEED_READING_RESEARCH_PROTOCOL.md)
- [Özel önizleme](../../docs/SPEED_READING_CUSTOM_PREVIEW.md)
- [Öğretmen eğitimi](../../docs/SPEED_READING_STAFF_TRAINING.md)
- [API sözleşmeleri](../../docs/API_CONTRACTS.md)
- [Playwright ve Gateway doğrulaması](../../docs/E2E_TESTING.md)
- [Sitemap sahipliği](sitemap-README.md)
- [Üçüncü taraf bildirimleri](../../THIRD_PARTY_NOTICES.md)

Tarihli audit/release raporları ilgili sürümün kanıtıdır; güncel çalışma rehberinin yerine kullanılmaz. Cache, build ve eski release klasörleri kaynak modül değildir; rollback/kanıt gereksinimi kontrol edilmeden topluca silinmez.
