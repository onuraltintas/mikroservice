# Hedefi Yakala — katalog ve canlı yayın

11 Ekim 2026. Uygulama kaynağı: `e330da93e0487f755e000ea1a255e619aa4e3728`, GitHub dalı: `codex/platform-hardening`. Canlı geçiş: **18:27:06 Türkiye saati**.

## Kullanım ve kapsam

Admin hesabıyla [Hızlı Okuma egzersiz kataloğu](https://masterhizliokuma.com/student/exercises) → Odaklanma → **Hedefi Yakala → Önizlemeyi başlat**. Doğrudan adres: [Hedefi Yakala](https://masterhizliokuma.com/student/attention-preview).

- Mevcut N-back/Odaklanma kartı ve motoru korundu. Admin oturumundaki katalog 21'den 22 karta çıktı; Odaklanma filtresinde Metin Solma, Odaklanma ve Hedefi Yakala birlikte görünüyor.
- Yeni kart yalnız Admin, SystemAdmin veya Teacher için eklenir. Teacher+Student karma rolü de önizlemeye erişebilir; yalnız Student, Editor veya InstitutionAdmin erişemez.
- Bu kart ön uçtaki **önizleme prototipidir**; veritabanı katalog kaydı, öğrenci programı ataması, sunucuya sonuç kaydı veya merkezi admin CRUD ekranı değildir. Pilot ayarlar yeni egzersizin kendi ekranından seçilir.
- Üç süre sınırı olmayan alıştırmanın ardından Sakin/Dengeli/Yoğun seçenekleri kullanılır. Parametreler ve ölçüm sınırları [prototip raporunda](attention-prototype-20261011.md) açıklanır.
- Yalnız `speed-reading-frontend` yayımlandı. API, admin paneli, Identity, Koçluk, Notification ve diğer çalışan konteynırların kimlikleri değişmedi. Veritabanı, mevcut N-back ayarları, programlar ve kullanıcı geçmişine yazılmadı.

## Doğrulama

- Katalog kartının eksikliği önce 5 başarısız/3 başarılı testle doğrulandı (`b680061b` RED); kart eklendikten sonra aynı hedef 8/8 geçti (`98922d49` GREEN).
- Arayüzün tüm birim testleri **838/838** geçti. Son kart açıklaması düzeltmesinden sonra model/bileşen/katalog odaklı testler **25/25** geçti.
- Masaüstü ve Pixel 5 boyutunda tarayıcı testleri **20/20** geçti: alıştırma, doğru/yanlış/kaçırılan sayaçları, zaman aşımı, üç pilot ayar, duraklatma, klavye, mobil sınırlar, rol reddi, karma roller, katalogdan açılış ve eski Focus kartının korunması. Aynı 20 senaryo yerel üretim yapılandırması/optimizasyonlarıyla da **20/20** geçti. Bunlarda Identity yerel fixture'dır; canlı test gibi sunulmaz.
- Hedefli kapsam raporunda yeni modelin satır/fonksiyon kapsamı %100, dal kapsamı %95,45; yeni bileşenin dört kapsam ölçütü de %100. Bu tüm uygulama kapsamı değildir.
- TypeScript ve yayın araçları incelemesinde engelleyici bulgu bulunmadı. Üretim derlemesi, paket SHA256 doğrulaması, Compose doğrulaması ve ağsız nginx yapılandırma testi geçti.
- Üretim derlemesinde önceden mevcut Universal Player SCSS bütçe uyarısı (128,99 kB / 120 kB) sürüyor; bu yayınla ilgili yeni hata değildir.
- GitHub uzak dalı uygulama commit'iyle eşleşti. CI push tetikleyicisi yalnız main/master için ayarlı olduğundan bu dalda otomatik CI çalışmadı; yukarıdaki testler yerelde çalıştırıldı.
- Gerçek, açık admin oturumuyla canlı katalog kartı/filtre, üç alıştırma, süreli 12 turluk deneme, doğru yanıt ve zaman aşımı, sonuç ekranı ve yeniden başlatma doğrulandı. Canlı sonuçta 1 doğru/11 kaçırılan, %8 ve saniye cinsinden yanıt süresi gösterildi; “kaydedilmedi” uyarısı yer aldı. Canlı akışta yanlış yanıt ayrıca doğrulanmadı; yerel tarayıcı testinde kapsandı.
- Canlı `index.html`, `ngsw.json` ve `main-XCO4DKWE.js` SHA256 değerleri paketle eşleşti. Oturumsuz öğrenci program API'si beklenen 401 döndürdü.
- Yayın sonrası 15 dakikalık sağlık izlemesi **18:43:23 Türkiye saatinde tamamlandı**: 30 ölçümün tamamında frontend sağlıklı, yeniden başlatma sayısı 0 ve yönlendirmeler izlenerek üç ürün adresinin HTTPS yanıtı 200. Yayın sonrası frontend kayıtlarında kritik/hata kaydı bulunmadı.

## Yayın ve geri dönüş

Yayın dizini: `/var/lib/eduivme/releases/attention-20261011-catalogue`. Yeni imaj: `eduivme/speed-reading-frontend:attention-20261011-catalogue`; revision etiketi yukarıdaki kaynak commit'idir. Önceki imaj `eduivme/speed-reading-frontend:exercises-20261011-6f315ff3` korunur. Eski içerik özetli dosyalar yeni imajda da tutuldu; açık sekmelerin eski lazy chunk istekleri karşılanabilir.

Rollback tetikleyicileri: frontend unhealthy/restart, yeni uygulama dosyalarının doğrulanamaması veya izinli admin katalog/başlatma akışının başarısız olması. Root SSH ile yayın dizininde `bash deploy.sh rollback` yalnız eski frontend imajına döner; veri tabanı geri yüklemesi veya diğer servislerin yeniden başlatılması yapmaz.

Windows'tan Git arşivi hazırlanırken kabuk betiği satır sonları CRLF olabilir. Yayın kopyasında `sed -i 's/\r$//' deploy.sh` ile normalize edildi; ardından `bash -n deploy.sh` tekrar geçti. Paket özetleri normalizasyondan önce doğrulandı.

İlk yenilemede service worker önceki arayüzü gösterebilir. Yeni manifest arka planda hazırlandıktan sonraki yenilemede yeni kart görüldü. Kart gelmiyorsa Ctrl+F5 ile yenileyin; uygulama hesabı/verileri silinmemelidir.
