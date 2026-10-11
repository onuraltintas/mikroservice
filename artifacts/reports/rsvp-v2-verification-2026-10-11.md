# RSVP v2 yerel doğrulama — 11 Ekim 2026

## Kapsam ve sonuç

Başlangıç sabitlemesi, yaş/seviye metin bandı, beş geçerli anlama sorusu, özel önizleme süresi ve eski oturum uyumluluğu geliştirildi. Mevcut gösterim süreleri ve yaş/seviye kapsamı korundu; otomatik tempo uyarlaması eklenmedi. Gösterim temposu ölçülmüş okuma hızı olarak kaydedilmiyor.

Paket: **11 mevcut katalog ayarı, 22 tam metin seçkisi, 110 soru**. Metin/sorular projenin mevcut Anlama içerik paketinden seçildi; kaynaklar değişmedi ve metinler kesilmedi. Bu sayı 22 bağımsız yeni hikâye anlamına gelmiyor. Ayrıntılı tablo ve geçiş prosedürü: [paket açıklaması](../../content-packs/rsvp/v2/README.md).

## Doğrulama

| Kontrol | Sonuç |
| --- | --- |
| SpeedReading.Application.UnitTests tamamı | 1199 / 1199 geçti; atlanan 0 |
| Bu toplam içindeki RSVP oturum testleri | 26 / 26 geçti |
| Universal player Angular testleri (Edge Headless) | 578 / 578 geçti |
| Yaş/seviye katalog ve kaynak/soru sözleşmeleri | 5 / 5 geçti |
| Geçici PostgreSQL SQL aktarım/geri-alma testleri | 11 / 11 geçti |
| Gerçek EF PostgreSQL RSVP entegrasyon testleri | 4 / 4 geçti; atlanan 0 |
| Masaüstü/mobil Playwright akışları | 14 / 14 geçti |
| Production Angular derlemesi | Geçti |
| C# ve TS/JS/SQL kod incelemeleri | APPROVE; bulunan iki HIGH sorun giderildi |

PostgreSQL içerik testi 11 hücrenin her birinde doğru yaş/seviye/banttan seçim, beş soru, erken bitirmeyi reddetme, başka kullanıcının oturumuna erişimi reddetme, duraklatma/devam, sunucu anlama puanı, mükerrer tamamlamama ve sonraki metne rotasyonu doğruladı. SQL iki kez uygulanarak idempotentlik ve açık oturum snapshot'larının korunması gerçek EF şemasında doğrulandı.

Tarayıcı API yanıtları kontrollü yerel test fixture'larıdır; gerçek Identity oturumu veya canlı uçtan uca kontrol yapılmış gibi sunulmaz. Ayrı PostgreSQL testleri gerçek servis, entity ve SQL şemasını kullanır.

Tarayıcı kapsamı: V1/V2 kayıtlı oturumda soru olan/olmayan akışlar, V2 yalnız başlangıçta sabitleme, beş soruyla sonuç ekranı, Admin/Teacher gerçek paketle özel önizleme ve hiçbir yazım yapmama, eksik beşinci soruda hata/placeholder reddi, konsol hatası ve yatay taşma kontrolü. Masaüstü ve mobil sonuç ekranları görsel olarak da incelendi. Ekran görüntüleri yerel `artifacts/rsvp-ui` dizinindedir; canlı doğrulama kanıtı değildir.

## Yakalanıp düzeltilen sorunlar

1. SQL şablonunda olmayan `average_rsvp_score` sütunu gerçek `average_comprehension_score` olarak düzeltildi. Eksik metin bandı/geçersiz süre ve içerik çakışmaları atomik olarak reddediliyor.
2. Katalog bandı değiştiğinde açık V1/V2 oturuma metin kimliğiyle dönmek engelleniyordu. Yeni oturum doğrulamasını koruyarak mevcut snapshot'ın devamı düzeltildi; iki başarısız test önce yazılıp yeniden geçirildi.
3. Gerçek metin özel önizlemede yükleniyor ancak RSVP motoruna kelime dizisi verilmediği için çalışamıyordu. RSVP'ye özel `words` aktarımı eklendi; diğer egzersizlere yeni kelime alanı eklenmedi.
4. Önceki tarayıcı testinde cevap sonrası buton oluşmadan `count()` kontrol edilip sonuç adımı atlanıyordu. Görünür/etkin butonu bekleyen kontrolle test yarışı giderildi. Gösterim temposu beklentisi de motorun başlangıç 300 ms'yi içeren hesaplamasıyla eşleştirildi.

## Sınırlar

- Derlemede önceden mevcut SCSS boyut uyarısı sürüyor: 128,99 kB, uyarı sınırı 120 kB. Bu çalışma CSS bütçesini yükseltmedi veya ilgisiz stilleri değiştirmedi.
- Testlerin geçmesi %100 kod kapsaması değildir. Seçili universal-player çalıştırmasında toplam satır kapsamı %68,31; tüm ürünün kapsamı olarak sunulmaz.
- Yaşa bağlı başlangıç parametreleri ürün tercihleridir; bilimsel norm veya eğitim başarısı garantisi değildir.
- **Yerel geliştirme ve kontroller yapıldı. GitHub'a gönderim, canlı SQL aktarımı ve canlıya alma yapılmadı.** Eski kullanıcı sonuçları silinmedi; Identity/Koçluk veritabanlarına dokunulmadı.

## Yeniden çalıştırma

```powershell
dotnet test tests/Unit/SpeedReading.Application.UnitTests/SpeedReading.Application.UnitTests.csproj --no-restore --verbosity minimal
node --test tests/E2E/rsvp-catalog.test.mjs
node --test tests/E2E/rsvp-catalog.postgres.test.mjs
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SpeedReadingRsvp" --verbosity minimal
```

`clients/speed-reading` dizininde Edge Headless ile `npm test -- --watch=false --browsers=ChromeHeadless --code-coverage --include=src/app/features/student/exercises/universal-player/**/*.spec.ts` ve `npm run build` çalıştırıldı. `tests/E2E` dizininde aynı Playwright kurulumu kullanılarak `node node_modules/@playwright/test/cli.js test --config=speed-reading-rsvp.config.mjs` çalıştırılır; farklı frontend Playwright kurulumuyla CLI karıştırılmaz.
