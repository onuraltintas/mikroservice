# Koçluk teknik temel: tarihsel durum ve güncel depolama kararı

Bu belge 22 Eylül 2026 tarihindeki doğrulama çalışmalarının özetidir; güncel
üretim onayı veya "%100 tamamlandı" beyanı değildir. O tarihte Identity,
Coaching, Notification ve Speed Reading ayrı veritabanlarındaki gizlilik
silme akışı, yasal saklama engeli, tekrarlı olay teslimi, öğrenci veri dışa
aktarma yetkileri ve dosya eki sahiplik denetimleri sentetik verilerle test
edildi. Çoğu prova disposable Docker ortamında yapıldı; gerçek VPS ve dış
güvenlik testi yerine geçmez.

24 Eylül kararıyla Koçluk ekleri için ayrı nesne deposu kaldırılıyor. Geçerli
tasarım, VPS içindeki kalıcı `coaching_attachments` volume'u ve üretimde
ClamAV taramasıdır. Dağıtım sırası ve geri dönüş koşulları
[depolama runbook'unda](COACHING_VPS_STORAGE_CUTOVER.md) kayıtlıdır.

Kapanmamış kapılar: yeni depolama akışının canlı yükle/oku/sil testi,
staging/canary doğrulaması, GitHub Actions yeşil koşu kanıtı, yedek ve geri
yükleme provası, dış güvenlik testi ve operasyonel alarm gözlemi. Kullanıcının
kararı gereği VPS dışı yedek yoktur; aynı fiziksel sunucunun veya diskin tam
kaybında kurtarma garantisi verilemez.
