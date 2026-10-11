# N-back — konum tekrar sınırları

11 Ekim 2026. Kullanıcının onayladığı tekrar önerisi yerelde uygulandı. **GitHub'a gönderilmedi, canlıya alınmadı.**

## Davranış

- Sunucunun ürettiği konum dizilerinde ve özel ayarlı önizlemede 1-back aynı konumu en fazla iki kez art arda gösterir. Doğru eşleşmeler korunur; üçlü tekrar olmaz.
- 2–5-back konum dizilerinde ardışık aynı konum olmaz. N adım öncekiyle eşleşmeler korunur.
- Aynı konumun iki gösterimi mevcut boş geçişle ayrılır: normal sürelerde 150 ms; çok hızlı özel ayarlarda en fazla uyaran süresinin dörtte biri. Geçiş sırasında yanıt kabul edilmez; duraklatma/resume davranışı korunur.
- Konum+kelime modunun konum kanalında da bu kurallar uygulanır. Kelime üretimi değiştirilmedi.
- Önceden tanımlı hedef indeksleri için gelecek eşleşme kısıtları üretimden önce hesaplanır. Geçerli hedefler rastgele reddedilmez; 1-back'te üçlü tekrar gerektiren çelişkili hedefler reddedilir.
- Sabit `PositionSequence` dizileri, mevcut seviye tespit snapshot'ları, geçmiş sonuçlar ve puanlama değiştirilmedi. Yeni sınırlar otomatik üretilen konum dizilerine uygulanır.
- Yaş/seviye N-back değerleri, uyaran sayıları ve süreleri değiştirilmedi. Canlı katalogdaki 20 kaydın hepsi hâlâ 1-back'tir; bu çalışma yaş/seviye planının uygulanması değildir. Veritabanı migration'ı gerekmiyor.

## Doğrulama

- RED: yeni arayüz testleri 6 başarısız / 58 başarılı; sunucu hedefi 6 başarısız / 12 başarılı. Kontrol noktası `dc86ee2f`.
- GREEN: aynı hedefler 64/64 arayüz ve 18/18 sunucu testi geçti. Kontrol noktası `25da968e`.
- Kod incelemesi geçerli ileri hedeflerin rastgele reddedilebildiğini buldu. N=3–5 tekrarlı üretim testleri 3/3 RED (`7711d429`); gelecekteki hedef kısıtlarıyla düzeltildi, 21/21 odaklı test geçti (`49ccd371`). N=1–2 için de aynı hedef koruma kontrolü eklendi.
- Son tüm Hızlı Okuma arayüz birim testleri **868/868**; tüm Hızlı Okuma Application birim testleri **1135/1135**; atlanan yok.
- Gerçek, geçici Docker PostgreSQL veritabanında N-back hedefli entegrasyon testleri **8/8** geçti. Canlı veritabanına dokunulmadı.
- Yerel Playwright masaüstü/mobil N-back akışları **18/18** geçti. Kayıtlı öğrenci akışı, üç önizleme modu, duraklatma, 1–5-back tekrar sınırları, boş geçiş sırasında yanıt reddi ve önizlemede kayıt yapılmaması kontrol edildi. Identity/API fixture'ları kullanıldı; canlı kullanıcı testi değildir.
- İlk tarayıcı koşusu 16/18 geçti; iki hata düğmenin erişilebilir adındaki ikon metnini hesaba katmayan test seçicisindeydi. Seçici düzeltildi, davranış beklentileri gevşetilmedi.
- Odaklı arayüz kapsamı: Focus motoru satır %86,84 / ifade %83,96; özel önizleme ayarları satır %83,5 / ifade %80,67. Bunlar ilgili dosyaların kapsamıdır, tüm proje kapsamında %80 veya %100 iddiası değildir.
- Üretim derlemesi geçti. Universal Player SCSS'nin mevcut 128,99 kB / 120 kB bütçe uyarısı devam ediyor; bu değişiklik SCSS'yi değiştirmedi.
- C# uzmanı, TypeScript uzmanı ve genel kod incelemesi tamamlandı; bildirilen engelleyici bulgu giderildi. TypeScript uygulama/test kontrolleri ve E2E sözdizimi geçti. ESLint kurulu değil; mevcut dosyalardaki eski whitespace nedeniyle genel format doğrulaması başarılı sayılmıyor.

## Yeniden çalıştırma

- `clients/speed-reading`: `npm test -- --watch=false --browsers=ChromeHeadless`
- `clients/speed-reading`: `npm run build -- --configuration production`
- Repo kökü: `dotnet test tests/Unit/SpeedReading.Application.UnitTests/SpeedReading.Application.UnitTests.csproj --no-restore`
- Repo kökü, Docker açık: `dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SpeedReadingFocusPostgresTests`
- `tests/E2E`: `npx playwright test --config=speed-reading-focus.config.mjs`

Tarayıcı başarısızlık çıktıları `artifacts/focus-ui/results`, kapsam raporu `clients/speed-reading/coverage/speed-reading-frontend/universal-player` altında yereldedir.
