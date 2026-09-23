# Koçluk VPS dosya deposu geçiş kontrolü

Koçluk ekleri yalnız VPS üzerindeki `coaching_attachments` Docker volume'unda
saklanır. Hızlı Okuma dosyaları ve veritabanı bu geçişin dışındadır. VPS veya
disk tamamen kaybolursa yalnız VPS içinde tutulan yedekler kurtarma sağlamaz.

## Canlıya alma sırası

1. Koçluk veritabanındaki ek kayıtlarını ve eski depodaki gerçek dosyaları
   say; sayı sıfır değilse her dosya için kontrollü taşıma planı uygula.
2. Mevcut Koçluk görüntüsünü ve Compose dosyasını geri dönüş için sakla.
3. `docker compose config --quiet` ve
   `node tools/coaching-storage-preflight.mjs` ile yerel sağlayıcı ve kalıcı
   volume bağını doğrula. Canlı Compose komutlarında proje adını açıkça
   `-p eduivme-production` olarak belirt; varsayılan proje adı ikinci bir
   konteyner oluşturabilir. Sır içeren Compose çıktısını dışarı aktarma.
4. Yeni Koçluk görüntüsünü yalnız bu servise dağıt; `Provider=Local`,
   `RootPath=/var/lib/eduplatform/attachments` ve `Scanner=ClamAv` kullan.
5. Sentetik bir dosyada yükle → oku → sil akışını, sahiplik kontrolünü,
   `/health/ready` durumunu ve servis loglarını doğrula.
6. Ancak bu kontrollerden sonra eski depolama servisini durdur. Eski volume'u
   veri envanteri ve geri dönüş penceresi bitmeden silme.

Başarısızlıkta eski görüntü ve önceki Compose ayarları birlikte geri yüklenir.
Yerel volume'da yazılmış yeni dosyalar varsa geri dönüş öncesi envanterlenir;
sessizce kaybedilmez.
