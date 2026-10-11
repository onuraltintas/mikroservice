# Anlama egzersizi: yerel geliştirme ve doğrulama

Tarih: 11 Ekim 2026. Dal: `codex/platform-hardening`.

> Bu rapor ilk motor geliştirme kontrolünün tarihsel kaydıdır. Motor 11 Ekim'deki `6f315ff3` yayınıyla canlıya alınmıştır; aşağıdaki “gönderilmedi/canlı değişmedi” ifadeleri ilk kontrol anını anlatır. Yeni içerik paketinin sonraki sonuçları [Anlama içerik paketi raporunda](comprehension-content-20261011.md) izlenir.

## Sonuç ve kapsam

**Motor, soru akışı ve kayıt ayrımı yerelde geliştirildi. Tüm içerik/veritabanı geçişi tamamlanmadı. GitHub'a gönderilmedi; canlı servis, ayar veya içerik değiştirilmedi.** Canlı envanter yalnız okunarak incelendi. Kullanıcı veya eğitim geçmişi silinmedi.

İki normal Anlama amacı:

- **Anlama çalışması (`learning`)**: sorular sırasında metne dönme; yanıttan sonra doğruluk ve açıklama. Hız ne ara sunucu yanıtında ne sonuçta bağımsız ölçüm olarak verilir.
- **Anlama değerlendirmesi (`evaluation`)**: sorular sırasında metin görünmez; doğruluk ve açıklama tamamlamadan sonra açılır. Tekrar gönderilen yanıt da erken cevap anahtarı açmaz.

Her ikisi sorulu akışı korumak için `readingPurpose=evaluation` kullanır. `comprehensionMode` ayrıca feedback ve kayıt davranışını ayırır. Çalışma modunu `readingPurpose=practice` yapmamak gerekir.

Normal Anlama varsayılan olarak süre kısıtlamasızdır; hızlandırıcı/pacer ve oturum içi uyarlama yoktur. Özel önizlemede bilerek seçilen okuma süreleri korunur. Seviye tespiti ve sınav simülasyonu mevcut ayrı kurallarında kalır; tespit cevap anahtarı yayımlanmaz.

## Tamamlanan teknik işler

1. Sunucu ve önizlemede gerçek metin zorunluluğu; Anlama için örnek/yedek metne sessiz geçiş kaldırıldı. Serbest Okuma'nın eski yedek metni bu görevin kapsamı dışında bırakıldı.
2. Yaş/seviye ve yapılandırılmış kelime aralığına uygun seçim. Yeterli geçerli A–D sorusu yoksa hedef sayı sessizce azaltılmaz. Otomatik seçim ve sabit metin seçimi test edildi.
3. Soru türleri 1 (açık bilgi), 2 (çıkarım), 3 (değerlendirme) korunur. Tür/Bloom/seçenek/anahtar alanlarının geçerliliği kontrol edilir; bu, soruların pedagojik kalitesinin otomatik doğrulaması değildir.
4. Yeni modlu normal oturumlarda okuma bitmeden yanıt kabul edilmez. Mod bilgisi oturum anlık görüntüsünde tutulur. Önceden başlamış eski oturumlar için geriye uyumluluk korunur.
5. Anlama puanı yalnız doğru cevap yüzdesidir; yüksek hız yanlış cevap puanını yükseltmez. Geçerli ilk okuma hızı ayrı metriktir. Önizleme de sunucunun 3 saniye ve 20–1500 WPM teknik aralık kontrolünü uygular; aralık dışı hız gösterilmez.
6. Çalışma sonucu egzersiz geçmişinde saklanır, fakat bağımsız `ReadingSession` ölçümü ve otomatik anlama profiline katılmaz. Değerlendirme uygun ölçüm kaydı oluşturur. Tamamlamanın tekrar edilmesi mükerrer sonuç üretmez.
7. Tamamlanan normal oturumun yalnız kendi soru/cevap/açıklamaları gösterilir. Kayıtlı sonuç beklenirken geçici istemci sonucu gösterilmez; hata halinde yeniden deneme vardır.
8. Sonuçta ilk okuma ve soruları yanıtlama süreleri ayrıdır. Çalışma/değerlendirme başarı etiketi hızdan bağımsızdır.
9. Mevcut 40 normal Anlama egzersizi için kimlikleri koruyan pilot ayar taslağı oluşturuldu; dört seviye tespit egzersizi değiştirilmedi.

## Son doğrulamalar

| Kontrol | Sonuç |
|---|---|
| SpeedReading.Application.UnitTests, tüm proje | 1037 geçti, 0 başarısız, 0 atlanan |
| Angular/Chrome, tüm arayüz testleri | 728 geçti |
| Gerçek geçici PostgreSQL Anlama entegrasyonu | 4 geçti |
| Playwright masaüstü ve mobil | 14 geçti |
| Üretim Angular derlemesi | Geçti; mevcut SCSS boyut uyarısı sürüyor |
| Anlama motoru kapsaması | Satırlar %87,4; ifadeler %87,5; dallar %86,55; fonksiyonlar %80 |
| Tüm arayüz kodunun kapsaması | Satırlar %62,24; tüm proje için %80/%100 iddiası yok |

Kapsama kaynağı bu koşuda üretilen `clients/speed-reading/coverage/speed-reading-frontend/app/features/student/exercises/universal-player/engines/reading-comprehension.engine.ts.html` dosyasıdır. Klasördeki daha eski raporlar kullanılmadı. .NET kapsam yüzdesi ayrıca ölçülmedi.

Tarayıcı senaryoları: admin/öğretmen önizlemesi, öğrenci için özel önizleme eylemlerinin gizlenmesi, süre/duraklatma, geçersiz özel ayar, metne dönüş, süre kısıtlamasız soru, değerlendirmede erken feedback gizliliği, tamamlanan açıklamalar, geçersiz hız ve mobil taşma kontrolü. Identity/API tarayıcı yanıtları test fixture'ıdır; gerçek canlı Identity oturumuyla uçtan uca test değildir. PostgreSQL kayıt testleri ayrıca gerçek geçici veritabanıyla çalışmıştır; canlı veritabanında yazma testi yapılmadı.

SCSS uyarısı: mevcut `exercise-player.component.scss` 128,98 kB; uyarı sınırı 120 kB. Bu görev SCSS'yi değiştirmedi.

## Odaklı kod incelemesi

İnceleme; oturum sahipliği, erken/tekrar yanıtında anahtar gizleme, seviye tespiti uyumluluğu, soru kategorileri, yapılandırma sınırları, hız-puan ayrımı ve kayıt tekrarları üzerinde yapıldı. İncelemede bulunan bölünmüş ters kelime aralığı, yalnız ilk soru türünü kabul etme ve çalışma ara yanıtındaki hız sorunları testle giderildi. Görsel kontrolün yakaladığı çok kısa önizleme hızı ve yanlış süre etiketi de düzeltildi. Bu, tüm platform için kapsamlı güvenlik denetimi değildir.

## Tamamlanmamış içerik ve geçiş

Yeni pilot aralıklarında 20 yaş/seviye havuzunun **10'u boş**:

- Çocuk: 1, 2, 5.
- Genç: 2, 4.
- Genç yetişkin: 1, 4, 5.
- Yetişkin: 4, 5.

10 Ekim salt okunur envanteri `content-packs/comprehension/v1/live-inventory-20261010.json` dosyasındadır. Sayısal uygunluk, soru kalitesi/tek doğru cevap/metin içi kanıt/kullanım yetkisi kontrolünün yerine geçmez.

Sırayla kalan işler:

1. Boş havuzlara yaş/seviye aralığına uygun farklı özgün metinler ve hedef sayıda açıklamalı sorular hazırlama; her mod için en az iki uygun metin.
2. Mevcut uygun havuzlarda da soru ve açıklama kalitesini gözden geçirme; üst seviyelerde kanıt, karşılaştırma ve değerlendirme ilerlemesini içerikte doğrulama. Sayısal pilot bantlarını akademik norm olarak sunmama.
3. Metin/soru manifesti, eski ayar eşleşme korumaları, içerik yeterlilik denetimi, atomik/idempotent uygulama ve geri dönüş hazırlama.
4. Paketin tüm 40 egzersizini gerçek geçici PostgreSQL'de uygulama/yeniden uygulama ve uyuşmazlıkta tam geri alma testleriyle doğrulama.
5. Ayrı canlı yayın onayı, yedek ve gerçek kullanıcı oturumuyla son kontrol.

`settings.json` tek başına canlıya aktarılmamalıdır. Henüz çalıştırılabilir Anlama `apply.sql` veya tamamlanmış metin/soru manifesti yoktur.

## Tekrar çalıştırma

```powershell
dotnet test tests/Unit/SpeedReading.Application.UnitTests --no-restore --verbosity quiet
dotnet test tests/Integration/Identity.API.IntegrationTests --no-restore --filter FullyQualifiedName~SpeedReadingComprehensionPostgresTests --verbosity quiet
```

Arayüz dizininde:

```powershell
$env:CHROME_BIN='C:\Program Files\Google\Chrome\Application\chrome.exe'
npx ng test --watch=false --browsers=ChromeHeadless --reporters=dots --code-coverage
npx ng build --configuration production
```

`tests/E2E` dizininde:

```powershell
npx playwright test --config speed-reading-comprehension.config.mjs
```
