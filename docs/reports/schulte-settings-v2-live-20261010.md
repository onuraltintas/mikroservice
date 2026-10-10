# Schulte ayarları v2 — doğrulama ve yayın

## Kapsam

Onaylanan boyut dağılımı ve kesin üst süre sınırları
`content-packs/schulte-settings/v2/README.md` dosyasında.
Yalnız 20 mevcut Schulte eğitim egzersizi. Katalog kimlikleri, kullanıcılar,
eski sonuç/oturumlar, seviye tespit ve diğer egzersizler değişmez.

## Kanıtlar

- 3 Node veri sözleşmesi testi geçti.
- 12 gerçek geçici PostgreSQL senaryosu geçti: doğru geçiş, tam alan/ek seçenek/snapshot
  korunumu, tekrar çalıştırma, eksik kayıt, kaynak sapması, yanlış yaş, çakışan alias,
  mükerrer kayıt, eksik grid ve dört büyük/küçük harf çakışma senaryosu.
- 15 ChromeHeadless Schulte ayar/motor testi geçti.
- 3 .NET sunucu oturumu testi geçti: iki 7×7 zaman aşımı senaryosu ve
  6×6 sunucu yerleşimi/tıklama doğrulaması. Bunlar mevcut birim testleridir.
- SQL incelemesinde NULL/case alias açıkları bulundu, gerçek PG testlerinde RED üretildi,
  düzeltildi ve aynı koşu GREEN oldu. İnceleme sonrası engelleyici SQL bulgusu kalmadı.
- Son test koşusunun geçici PostgreSQL konteynırı ve anonim test volume'ü kaldırıldı.
  İlk iki koşuda `-v` kullanılmadığı için yerelde artık anonim volume kalmış olabilir;
  kimlikleri kaydedilmediğinden genel Docker temizliği yapılmadı. VPS'e test konteynırı açılmaz.
- Canlı ön kontrolde 20 kayıt ve servis Healthy; kaynak ayarlar beklenen v1 değerleridir.
- Yayın betiği ve 20 kimlikli envanter dışlama listesi ayrıca incelendi; engelleyici bulgu yok.
- Tam kullanıcı/mobil tarayıcı testi kullanıcıya bırakıldı; geçtiği iddia edilmez.
  Süreler akademik yaş normu değildir, yeni süre/boyut dağılımı için ürün testi gerekir.

## Yayın

Yayın henüz yapılmadı. Aynı Speed Reading API konteynırı yedekli veri geçişinde
kısa süre durdurulacak; diğer servis/veritabanları ve tüm imajlar korunacak.
Sonuç ve 15 dakikalık izleme kanıtı yayın sonrası buraya eklenecek.
