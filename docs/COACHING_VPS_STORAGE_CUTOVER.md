# Koçluk VPS dosya deposu geçiş kontrolü

Bu runbook yalnız Koçluk ekleri içindir. Hızlı Okuma veritabanı ve dosyaları
değiştirilmez. Kullanıcı kararı gereği dosyalar ve yedekler VPS dışına çıkmaz.
Bu tercih, VPS/disk tamamen kaybolduğunda kurtarma garantisi vermez.

## Mevcut durum (24 Eylül 2026)

- Canlı Koçluk, MinIO sağlayıcısını kullanıyor fakat uygulama hesabı MinIO
  root hesabıyla aynı.
- MinIO `/data` yolu Docker volume üzerinde. Şifreli bir host dizini henüz
  doğrulanmadı; `ATTACHMENT_MINIO_DATA_HOST_PATH` canlı `.env` içinde yok.
- MinIO portları host'a yayınlanmıyor.
- GitHub CI koşusunun yeşil sonucu doğrulanmadı; yerel Docker çalışmıyor.

## A. Yalnız uygulama hesabını ayırma

Ön koşullar: bakım penceresi, MinIO nesne envanteri, mevcut Koçluk görüntüsü ve
Compose yapılandırmasının geri dönüş kopyası, mevcut root kimlik bilgilerinin
erişilebilirliği. Sırlar terminal çıktısına veya Git'e yazılmamalı.

1. Yeni, rastgele uygulama hesabını MinIO'da oluştur; yalnız
   `eduplatform-attachments` bucket'ına listeleme/okuma/yazma/silme politikası
   bağla. Root hesabı ve mevcut nesneler değişmeden kalmalı.
2. Yeni hesabın kendi bucket'ına eriştiğini, ilgisiz bucket ve yönetim API'sine
   erişemediğini doğrula. Gerçek kullanıcı dosyası yerine sentetik nesne kullan.
3. `ATTACHMENT_MINIO_ACCESS_KEY` ve `ATTACHMENT_MINIO_SECRET_KEY` değerlerini
   yalnız VPS'deki korumalı dağıtım sırlarında güncelle. Koçluk servisini tek
   başına yeniden oluştur; diğer servisleri yeniden dağıtma.
4. Sentetik dosya için yükle → oku → sil, Koçluk `/health/ready`, hata kayıtları
   ve yeniden başlatma sayısını kontrol et. Eski root hesabını MinIO yönetimi
   için koru; uygulama hesabı olarak yeniden kullanma.

Geri dönüş: yeni hesapla açılış veya nesne işlemi başarısızsa önceki Koçluk
görüntüsü **ve önceki çalışan ortam ayarlarını birlikte** geri yükle. Mevcut
nesneleri silme. Hata nedeni bulunmadan yeni hesabı kaldırma.

## B. Docker volume'dan VPS host dizinine taşıma

Bu, A'dan ayrı bir bakım penceresidir. Hedef dizin, erişim izinleri ve disk
şifrelemesi doğrulanmadan `ATTACHMENT_MINIO_DATA_HOST_PATH` etkinleştirilmez.
Önce VPS içinde geri yüklenebilir bir kopya alınır; aynı disk üzerinde olduğu
için bu kopya felaket kurtarma sayılmaz. Koçluk ve MinIO yazmaları durdurulup
nesne envanteri ve bütünlük doğrulaması yapılır. Hedefe taşıma sonrasında
sentetik ve mevcut nesneler için okuma, yeni yükleme ve silme denenir.
Başarısızlıkta eski Docker volume ve önceki Compose tanımı korunarak geri
dönülür; doğrulama bitmeden volume temizlenmez.

## Yayın kapısı

`node tools/coaching-storage-preflight.mjs` üretim Compose yapılandırmasını
kontrol eder; aynı hostta çalıştırılmalı ve sır içeren tam Compose çıktısı
başka makineye aktarılmamalı. Kontrol, bind mount'un gerçekten şifreli diskte
olduğunu veya yedeğin geri yüklenebildiğini kanıtlamaz. Bu iki nokta ayrıca
işletim sistemi ve geri yükleme provasıyla doğrulanır.
