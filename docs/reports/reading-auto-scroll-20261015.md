# Okuma egzersizlerinde üç satırda otomatik kaydırma · 15.10.2026

## Davranış

Hızlı Okuma, Gruplama, Metin Solma, Geriye Dönüş Azaltma ve İç Ses Azaltma'nın metin üzerinde işaretleme modunda, etkin satırın altında üç tam satır kaldığında metin alanı otomatik kayar. Yeni konumda önceki bir satır bağlam için korunur. Sonraki üç satır eşiğine kadar yeniden kaydırılmaz. Kısa metinler kaydırılmaz; duraklatmada hareket durur; azaltılmış hareket tercihinde animasyon kullanılmaz.

Öğrenci çalışması ve aynı oynatıcıyı kullanan önizleme birlikte düzeltilmiştir. Egzersiz temposu, puanlama, katalog ayarları ve seviye tespiti değişmez. İç Ses Azaltma'nın tek sözcük/grup gösterimleri kapsam dışıdır.

## Neden ve çözüm

Canlıdaki önceki sürümde zamanlayıcı ilerlemesine rağmen `scrollTop=0` kaldığı, etkin kelimenin görünür alanın altına indiği gözlendi. Yerel `detectChanges()` sonrasında beklenen `ngAfterViewChecked` çağrısına bağımlı bayraklar kaydırmayı güvenilir biçimde tetiklemiyordu. Kaydırma artık motorun durum güncellemesinden ve görünüm yenilenmesinden hemen sonra çalışır.

Dört ayrı ortalama/merkezleme yöntemi yerine ortak DOM geometrisi kullanılır. Kısmen görünen satır tam satır sayılmaz. Devam eden yumuşak kaydırmanın hedefi kısa süreyle korunarak her zamanlayıcı güncellemesinin animasyonu tekrar başlatması önlenir. Kesilen animasyon hedefi bir saniye sonra bırakılır. Çok küçük alanlarda tampon görünür satır sayısına göre azalır.

Kısa ve mobil ekranlarda yalnız ilgili okuma kartlarının başlık/alt bilgi boşlukları daraltılır; yazı boyutu korunur. Metin alanı ekran yüksekliğine uyar. İsteğe bağlı İç Ses Azaltma ritmi ve müzik simgesi dar mobil başlıkta ayrı kolonlardadır.

## Doğrulama

- Hata öncesi tarayıcı karşılaştırması: eski gerçek yayın paketinde doğal zamanlayıcıyla kaydırma ve kısa ekran sınırı kontrolleri başarısız oldu (`e2e-red.log`, 2/2).
- Birim regresyonları: eski davranışta 24/25 başarısız; kesilen animasyon ve kısmi satır için ayrıca başarısız testler kaydedildi.
- Öğrenci test paketi: **1027/1027 geçti** (`student-suite-green.log`). Yeni yardımcıda satır kapsamı %100, dal kapsamı %85; bütün projenin kapsamı %80 olarak iddia edilmez.
- Beş okuma biçimi × masaüstü/telefon/kısa ekran ve üç doğal yumuşak kaydırma testi: **18/18 geçti** (`e2e-green.log`). Üç tam satır eşiği, iki satırdan büyük blok hareketi, tekrarlama, duraklat/devam, son satır ve tamamlanma doğrulanır.
- Dar mobil metronom: 320 px genişlikte gerçek çakışma testi önce başarısız oldu (`metronome-red.log`). Düzeltmeden sonra 320/360 px kontrolleri ve üç ekranda İç Ses Azaltma kaydırması geçti: **5 başarılı, 4 ilgili olmayan proje atlaması** (`metronome-green.log`).
- Üretim derlemesi başarılı. Önceden de aşılmış 120 kB bileşen stil bütçesi 135,19 kB; bu uyarı derlemeyi durdurmaz. Bu çalışma geniş stil refaktörü içermez.

Ham test çıktıları ve ekran görüntüleri yerel `artifacts/reading-auto-scroll-20261015/` dizinindedir.

## Yayın

Yayın **2026-10-15T18:30:25Z** tarihinde tamamlandı. Kaynak `e94442006df9da3932f606efa687dad4315f3949`; arşiv SHA-256 `275fd170e81ccf5234b623ad96de7164bf7e6ca9268079bfae3b2f33e6ae4c2b`.

Ön yüz imajı `sha256:4dad6b775af9289e9812136174f760d6abcf282dbf85c232c6bcc8ff4a438b8d`. Önceki tam imaj `sha256:326463acccdfd7c548119fa9c4a74598fe4dc5af21afe71ceeb157de1d6628b5` geri dönüş için korunur. API `sha256:f41b9cf5f0193d8299473c9c8aa101e28fa4a748a0c73da3725c1fbc03f04c54` ve diğer konteyner kimlikleri değişmedi. Veritabanı/migration, kimlik doğrulama ve CAPTCHA ayarı değişmedi.

Yeni kaynak, önceki gerçek canlı kaynakla dosya hashleri üzerinden karşılaştırıldı: yalnız oynatıcı TS/SCSS, yeni kaydırma yardımcısı ve testi farklı (`isolated-source-diff.txt`). Ağdan yalıtılmış aday provası ve yayın manifesti doğrulandı. İşlem kaynakları [yayın dizininde](../../tools/releases/reading-auto-scroll-20261015/README.md).

Önceden açık PWA sekmesi eski uygulama paketini tutuyordu; normal yenileme yeni sürümü yükledi. Ekranı önceden açık kullanıcılar bir kez yenilemelidir.

## Canlı öğrenci doğrulaması

CAPTCHA kapatılmadan mevcut `testogrenci@eduivme.com` oturumu kullanıldı. Canlıda zamanlayıcı hızlandırılmadı, DOM üzerinden kaydırma yapılmadı ve uygulama iç durumu değiştirilmedi.

- **Hızlı Okuma / yetişkin 1:** 757 kelimelik metin normal 200 kelime/dakika temposunda 03:47'de tamamlandı; sonuç kaydedildi. `scrollTop` gözlemleri 0 → 255 → 498 → 1227,5 → 1617,5; son konum maksimum 1618'e ulaştı. Etkin kelime görünür kaldı.
- **Metin Solma / yetişkin 1:** canlı 657 kelimelik metinde 0 → 185 → 876 kaydırma gözlendi; etkin sözcük görünür kaldı. Bu kontrol kaydırma doğrulamasından sonra normal çıkışla sonlandırıldı; anlama soruları tamamlandı olarak raporlanmaz.
- **Regresyon Azaltma / yetişkin 4:** 510 kelimelik metinde 194,5 → 802,5 kaydırma gözlendi; etkin sözcük metin alanında kaldı. Kaydırma kontrolünden sonra normal çıkış yapıldı.
- **İç Ses Azaltma / yetişkin 4 / highlight:** ritim ve müzik göstergesi açık gerçek kayıtlı ayarla 0 → 156 → 732 kaydırma doğrulandı; etkin sözcük görünür kaldı. Kaydırma kontrolünden sonra normal çıkış yapıldı.
- **Gruplama / yetişkin 4:** 610 kelime / 169 grup, kayıtlı dörtlü gruplama ve 290 kelime/dakika temposuyla 02:06'da tamamlandı; sonuç kaydedildi. Kaydırma 0 → 842 → 1609,5; metin sonu maksimum 1610'a ulaşıldı.

Ekran kanıtları: `live-speed-reading.jpg`, `live-speed-reading-completed.jpg`, `live-text-fading.jpg`, `live-regression.jpg`, `live-subvocalization.jpg`, `live-grouping.jpg`, `live-grouping-completed.jpg`. Zaman damgalı DOM geometrileri ve tamamlanma ekranları `live-reading-observations.json` dosyasında.

## Canlı testte ayrılan diğer bulgular

Bu API imajı değiştirilmedi; aşağıdaki katalog/günlük görev sorunları kaydırma düzeltmesi kapsamında çözülmüş sayılmaz:

1. Genel egzersiz kataloğundan başlatılan Hızlı Okuma ve Gruplama'nın sonuçları kaydedildi, fakat günlük ilerleme güncellemesi “Günlük görev belirlenemedi. Günlük egzersiz listesinden tekrar başlatın.” iş kuralıyla reddedildi. Sonuç ekranı bunu açık uyarı ve tekrar deneme düğmesiyle gösterdi.
2. Gruplama yetişkin 1 ve 5 seviyeleri “Seçilen yaş, seviye ve metin uzunluğuna uygun aktif metin bulunamadı.” hatası verdi. Bu nedenle bu iki katalog seçiminin canlı kaydırma testi çalıştırılamadı. Yetişkin 4. seviye uygun metinle açıldı; Gruplama motoru ayrıca üç ekran boyutunda otomatik tarayıcı testiyle doğrulandı.
3. Regresyon Azaltma yetişkin 1, uygun aktif metin olmadığı için açılamadı; canlı kaydırma testi metin sunan 4. seviyede yürütüldü.

Bu üç metin seçimi hatası API'de HTTP 500 olarak kaydedildi (`live-health-03.log`); sıfır sunucu hatası iddia edilmez. Katalog uygunluğu ve eksik metin için anlaşılır hata dönüşü ayrı takip gerektirir. Katalog verisi veya test öğrencisinin yetkileri değiştirilmedi.

## Yayın sonrası izleme

**18:30:25Z → 18:45:38Z (15 dakika 13 saniye)** gözlem tamamlandı. Ön yüz/API çalışıyor, restart 0; başka servislerin konteyner kimlikleri korunuyor. Son public giriş sayfası HTTP 200, 0,738 saniye. API'de 147 istek, ortalama 16,25 ms / maksimum 282,67 ms. Yukarıdaki üç katalog testi kaynaklı 500 dışında ek 5xx gözlenmedi. Günlük görev iş kuralı uyarıları ayrıca kaydedildi; sonuç kayıtları başarılı.

Operasyon ve ön yüz kod incelemeleri tamamlandı. Yeni kaydırma davranışında açık bulgu kalmadı. Bir sonraki ayrı iş için kalanlar: günlük görev bağlamı olmadan tamamlanan kataloğun ilerleme güncellemesi, uygun metin sunmayan seviyelerin görünürlüğü ve bu eksikliklerde 500 yerine anlaşılır hata dönüşü.
