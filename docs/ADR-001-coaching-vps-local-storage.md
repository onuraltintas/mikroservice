# ADR-001: Koçluk eklerini VPS üzerinde yerel volume'da tutma

**Durum:** 24 Eylül 2026'da güncellendi

## Bağlam ve karar

Kullanıcı, Koçluk dosyalarının VPS dışında saklanmamasını ve ayrı nesne
depolama servisinin kaldırılmasını seçti. Koçluk ekleri, uygulama
container'ındaki `/var/lib/eduplatform/attachments` yoluna bağlı kalıcı
`coaching_attachments` Docker volume'unda tutulur. Üretimde kötü amaçlı
yazılım taraması için ClamAV gerekir. Hızlı Okuma'nın dosyaları ayrı kalır.

## Sonuçlar

- Bu düzen tek VPS ve tek Koçluk yazıcısı için uygundur; paylaşılan volume
  olmadan yatay ölçekleme yapılmaz.
- Volume, container yeniden oluşturulduğunda korunur; Docker volume tek
  başına yedek değildir. VPS dışı yedek kullanıcı kararıyla kapsam dışıdır.
- Erişim izinleri, disk kapasitesi, yedek ve geri yükleme provası işletimsel
  kontrollerdir. VPS/disk kaybında kurtarma garantisi yoktur.
- Canlı geçiş ve geri dönüş adımları
  [depolama runbook'unda](COACHING_VPS_STORAGE_CUTOVER.md) tutulur.
