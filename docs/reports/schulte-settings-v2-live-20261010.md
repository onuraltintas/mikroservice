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

- Yayın: 10 Ekim 2026, 21:50:10 Türkiye saati; veri paketi commit'i `de956856`.
- GitHub dalı: `codex/platform-hardening`; paket yayın öncesi gönderildi.
- Paket SHA256: `3ec43887969f43ee92086b38af69c911bb3b5d88e4a4ed18c930145236f22cd6`.
- VPS sürüm klasörü: `/var/lib/eduivme/releases/schulte-settings-v2-20261010-de956856`.
- Yedek: `speedreading-before.dump`, 1.576.110 bayt, izin 600; `pg_restore --list` doğrulandı.
  SHA256: `48996f876ecc5ea3dd957172dc362485eb64b58fcd0493eaad78e3528a6f797f`.
  Tam restore tatbikatı yapılmadı.
- Atomik SQL geçişi 20 satırı güncelledi; 85 kalıcı tablo kapsam dışı kayıt özetleri birebir aynı.
- Aynı Speed Reading API konteynırı/imajı yeniden başlatıldı. Diğer servis ve tüm imajlar aynı kaldı.
- Readiness Healthy, ana sayfa HTTP 200, oturumsuz korumalı program API'si HTTP 401.
- Canlıda 20 ayarın yeni değerleri salt okunur sorguyla doğrulandı.
- 15 dakikalık yayın sonrası izleme tamamlandı: 30/30 Healthy, yeniden başlatma sayısı 0
  ve değişmedi; bu aralıkta ciddi log işareti yok. `MONITOR_OK`.
  İzleme kaydı VPS sürüm klasöründeki `monitor.txt` dosyasında.
- Kullanıcı testi için sayfa yenilenip yeni oturum açılmalı. Açık oturumlar eski ayarlarını korur;
  özel önizleme boyut seçimi kayıtlı boyutu geçersiz kılabilir. Eski ve yeni farklı boyutların
  ham tamamlanma süreleri doğrudan karşılaştırılmamalıdır.
