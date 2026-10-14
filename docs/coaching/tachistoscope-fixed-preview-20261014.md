# Takistoskop önizlemesi: sabit basamak ve 75 ms

14 Ekim 2026, `2026-10-14T17:40:44Z` tarihinde canlıya alındı. Kaynak commit'i `c505ec91e72d87d4cec44b1c92d4e52bb064bb27`; yayın etiketi `tachistoscope-75ms-20261014`. Ön yüz imajı: `sha256:bc486ca42a55e5c50c6699403576455acce0ba1363a87f8903fd6069a5407483`.

Normal ve özel sayısal takistoskop önizlemelerinde başlangıç basamak sayısı oturum boyunca sabit kalır. Başarıya göre gösterim süresi uyarlanmaya devam eder; alt sınır 75 ms'dir. Özel önizleme formu 75 ms kabul eder, 74 ms'yi reddeder. Yaş grubunun başlangıç basamağı seçim sınırları korunur.

Motor, oyuncunun zaten verdiği `previewOnly` işaretini kullanır. Sabitleme yalnız sayısal, sunucunun yönetmediği, değerlendirme ve RSVP olmayan önizlemelerde uygulanır. Kayıtlı yapılandırma mutasyona uğramaz; özel ayarlar öğrenci rolleri ve kalıcı çalışmalar için uygulanmaz. Öğrenci motorunun mevcut uyarlama kuralları değiştirilmedi. Backend veya migration yayını yapılmadı.

Doğrulama:

- Önce başarısız olan testlerle basamak artışı yeniden üretildi; düzeltmeden sonra 97 seçili birim testi geçti. Alt/üst süre sınırlarında sabit basamak, reset, 75 ms gösterimi, eski harf/basamak uyarlaması, sunucu oturumunun 400 ms sınırını koruması ve değerlendirme akışları kapsandı.
- Üretim derlemesi başarılı. Önceden mevcut egzersiz oyuncusu SCSS bütçe uyarısı dışında yeni uyarı yok.
- Yerel üretim arayüzünde 8 masaüstü/mobil senaryo geçti: 500 ms özel önizleme, 75 ms özel önizleme, özel ayar verilmeden normal önizleme ve geri bildirim yerleşimi. 3/4 basamak sabitliği, 74 ms reddi, 74. ms'de görünür/75. ms'de gizli uyaran, tamamlama ve sonuç kaydedilmemesi doğrulandı.
- Canlıdan sunulan arayüzde aynı 8 senaryo da geçti. API yanıtları fixture ile karşılanır ve service worker'lar engellenir; gerçek hesapla giriş veya canlı sonuç yazımı yapılmaz.
- Canlıdaki 20 kayıtlı takistoskop yapılandırması yayın öncesi ve sonrası birebir aynı; SHA256 `25c06c66a9812e208cd70ac9ed2a24237fa2d537fd55bef8770b72da0b6d76f1`. Bu karşılaştırma salt okunur veritabanı sorgusuyla yapıldı.
- İki bağımsız kod/yayın incelemesinde engelleyici bulgu yok. Yalnız `speed-reading-frontend` değişti; diğer konteyner kimlikleri ve Speed API imajı `sha256:8403a4cd5a0df3bd02ddc5257df811dba3c60d618b724f5bb2dbf9fb8d1af7a6` korundu. Ön yüz healthy, restart sayısı 0. Önceki ön yüz imajına doğrulanmış geri dönüş hazırlandı.

Kanıtlar: `artifacts/tachistoscope-fixed-preview-red.log`, `tachistoscope-fixed-preview-unit.log`, `tachistoscope-fixed-preview-build.log`, `tachistoscope-fixed-preview-browser.log`, `tachistoscope-fixed-preview-live-browser.log`, `tachistoscope-fixed-preview-deployment.log`, `tachistoscope-fixed-preview-live-health.log`, `tachistoscope-live-catalog.json`, `tachistoscope-live-catalog-after.json`. Yayın betikleri: `tools/releases/tachistoscope-75ms-20261014/`; tarayıcı testleri: `tests/E2E/specs/speed-reading-tachistoscope-ui.spec.mjs`.
