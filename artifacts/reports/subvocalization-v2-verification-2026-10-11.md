# İç Ses Azaltma v2 — yerel doğrulama

11 Ekim 2026. Kapsam: sayı sayma, yaş/seviye ayarları, beş sorulu içerik seçimi, program yerleşimi ve güvenli veri paketi. GitHub'a gönderim ve canlı veritabanı uygulaması yapılmadı.

## Tamamlananlar

- Çocuk 4–5, genç 3–5, genç yetişkin/yetişkin 2–5; toplam 13 ayar. Mevcut 6 ayar güncellenir, 7 yeni ayar eklenir.
- Okurken hafif sesli 1–2–3–4 sayma; bağımsız 60 BPM görsel ritim. Duraklatma/durdurma/soru aşamasında ritim kontrol edilir. Mikrofon yok; sayma veya iç ses azalması doğrulanmaz.
- Seviye 2–4 iki kelime, seviye 5 üç kelime. Tam metin görünür; cümle ve satır sınırları korunur. Otomatik tempo uyarlaması yok.
- Gerçek yaş/seviye/kelime bandına uygun metin gerekir. Eksik içerikte yedek metin kullanılmaz. Hatalı WordCount metadata'sı gerçek içerik kontrolünü aşamaz.
- Her ayara üç uygun metin; toplam 39 metin kaydı ve 195 soru. Beş geçerli soru sunucuda seçilir/değerlendirilir; önizleme değerlendirilmez/kaydedilmez.
- 13 programda gün 23 ve 28 yerleşimi; 182 görev ve günlük 6–7 görev korunur. Regresyon gün 21/26 görevleri değişmez.
- SQL atomik ve tekrar uygulanabilir. Katalog/program/soru düzenlemesi çakışmaları ve eski aktif programın eksik ScheduleJson'ı işlemi iptal eder. Kayıtlı takvimler/oturumlar/sonuçlar yeniden yazılmaz, silinmez.

## Son doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Hızlı Okuma Application birim testleri (tam proje) | 1.109 / 1.109 geçti |
| Hızlı Okuma frontend birim testleri (tam proje) | 806 / 806 geçti |
| İç Ses + önceki Regresyon katalog PostgreSQL testleri | 12 / 12 geçti; Docker PostgreSQL 16 |
| İç Ses masaüstü/mobil Playwright akışları | 12 / 12 geçti |
| İçerik/parametre/program paketi Node testleri | 6 / 6 geçti |
| Frontend üretim derlemesi | Geçti; önceden bulunan SCSS bütçe uyarısı sürüyor (128,99 kB / 120 kB) |
| Backend Release derlemesi | Geçti; 0 uyarı / 0 hata (`--disable-build-servers -m:1`) |
| Paket üreticisini tekrar çalıştırma | Dosya hash'leri değişmedi |
| git diff --check | Geçti |

Hedefli 20 frontend testinin kapsama çalışmasında İç Ses motoru: %86,12 satır, %83,05 ifade, %79,62 dal, %70,27 fonksiyon. Bu, tüm uygulamanın kapsaması değildir ve %100 kapsama iddiası yoktur.

Önce testler ilgili eksiklikleri gösterdi (RED), sonra uygulama düzeltildi (GREEN). İlave incelemede bulunan durdurma sonrası yeniden ritim başlatma ve WordCount drift hataları da aynı süreçle kapatıldı. C#/SQL ve TS/JS son incelemelerinde kalan kod blokeri bildirilmedi.

## Sınırlar ve yayın önkoşulları

Tarayıcı testleri yerel gerçek frontend ve kontrollü API yanıtlarıyla çalıştı; gerçek canlı Identity oturumu/üretim uçtan uca testi değildir. PostgreSQL testleri gerçek servis persistence kodunu ve paketi yalnız disposable Testcontainers DB'de çalıştırdı.

İçerik havuzu üç özgün hikâyenin tam paragraf seçkileridir; seviyeler ve başka egzersizler arasında hikâye/soru tekrarı olabilir. Bağımsız transfer ölçümü değildir. Çocuk 160/180 ve diğer tempo değerleri pilot başlangıç ayarlarıdır, yaş normu değildir. Sayı sayma anlamayı zorlaştırabilir; kalıcı iç ses azalması veya öğrenme artışı kanıtlanmış sayılmaz.

Canlıya geçiş ayrı onayla: önce API/frontend, yedek, yaş program v2 + Regresyon v2, başlangıç istekleri durdurulmuş ve tamamlanmış bakım penceresi, sonra yalnız Hızlı Okuma DB'de apply.sql. Tablo kilidi normal şablon SELECT'ini bloklamaz. Eksik eski takvim varsa eski şablondan kaydetmeden geçiş sürdürülmez. Ayrıntılar content-packs/subvocalization/v2/README.md içindedir.
