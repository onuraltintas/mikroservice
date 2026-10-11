# Bekleyen Hızlı Okuma paketleri — 11 Ekim 2026

Kullanıcı onayı: RSVP, N-back, Kelime Hazinesi, Serbest Okuma ve Hata Analizi birlikte yayımlanır. Yalnız Hızlı Okuma API/frontend yeniden oluşturulur. Identity, Koçluk, Notification, admin ve diğer veritabanları kapsam dışıdır.

1. Birim, PostgreSQL ve masaüstü/mobil tarayıcı testlerini çalıştırın. `prepare.mjs` beş paketi tek transaction, dar kapsamlı koruma envanteri ve tam idempotence envanteri olarak üretir.
2. API publish ve frontend production build çıktılarını, burada bulunan araçları, `nginx.conf` ve 40 haneli `source-revision.txt` dosyasını paketleyin. Paket içindeki tüm dosyaları `payload.sha256` ile sabitleyin; gizli ortam ayarlarını dahil etmeyin.
3. Root erişimiyle `/var/lib/eduivme/releases/pending-speed-reading-20261011` altında `deploy.sh validate`, `build`, `rehearse` çalıştırın. Önceki image kimlikleri ve statik dosyalar korunur. Canlı yedek geçici veritabanına geri yüklenir; tüm paketler iki kez uygulanır. Her iki uygulama arasında tam envanter aynı olmalıdır. Geçici veritabanı çıkışta kaldırılır.
4. `promote` yalnız Hızlı Okuma API'sini durdurur; yeni, doğrulanmış yedek ve içerik dışı kayıtların envanterini alır. SQL tek transaction'dır. Korunan kayıtlar değişirse veya SQL sonrası doğrulama başarısız olursa API durdurulmuş kalır; otomatik riskli geri dönüş yapılmaz.
5. API/frontend sağlık, HTTPS statik dosya bütünlüğü ve oturumlu önizleme kontrollerini yapın; `monitor.sh` ile 15 dakika izleyin. Yeni frontend eski hash'li dosyaları da içerir; önceden açık tarayıcılar için lazy chunk'lar korunur.

## Geri dönüş

Yeni image/ayar/sağlık hatası geri dönüş tetikleyicisidir. Önceki image ID ve tag'leri release dizininde kayıtlıdır; yedeğin geri yüklenmesi provada denenmiştir. SQL öncesindeki hata eski API'yi yeniden başlatır. SQL sonrasında API'yi durdurup veri durumunu inceleyin. Yalnız uygulama image'ını geri almak yeni içerik ayarlarıyla uyumlu olmayabilir. Tam yedek geri yüklemesi ancak yeni kullanıcı yazılarının kaybolmayacağı doğrulandıktan sonra yapılmalıdır; yeni yazılar varsa kapsamlı geri dönüş SQL'i hazırlayın. Otomatik tam DB restore veya kullanıcı geçmişi silme yoktur.

Beklenen kapsam: 88 katalog kaydı (80 güncelleme, 8 ekleme), 162 yeni metin ve 110 yeni soru. Eğitim programları, seviye tespiti, eski metin/sorular ve kullanıcı geçmişi korunur. Paketler beklenen eski/yeni yapılandırma dışında sapma varsa geçişi iptal eder.
