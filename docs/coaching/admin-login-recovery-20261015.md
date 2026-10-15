# Admin girişinde eski modül ve bildirim bağlantısı düzeltmesi

15 Ekim 2026 Türkiye saati 16:43'te canlıya alındı (`2026-10-15T13:43:51Z`). Kaynak `b3171cac`; test hazırlığı ekleri `f23442f0`. Yalnız admin paneli yayımlandı.

## Bulgular

Kullanıcının ilk kaydında `Failed to fetch dynamically imported module: /chunk-U6CFYNLY.js` bulunuyordu. Canlıda hem bu dosyanın hem eski giriş dosyası `chunk-FDZP6X4B.js` isteğinin HTTP 200 ile HTML döndürdüğü doğrulandı. Önceki yayın Dockerfile'ı tüm dist klasörünü kaldırdığı için açık/eski sekmelerin hash'li modülleri artık yoktu. Express eksik statik dosyayı Angular SSR'a aktarıyordu. Oturum alındıktan sonraki panel navigasyonu bu nedenle giriş hatası gibi görünüyordu. Kullanıcı yenileyince girişin açıldığını bildirdi.

İkinci kayıtta yalnız bildirim WebSocket denemesinin başarısızlığı vardı. Hata saatinde Gateway/Notification günlüklerinde negotiate HTTP 200, WebSocket HTTP 101 ile yaklaşık 15 saniyelik bağlantı ve ardından HTTP taşımasında başarılı 200 istekleri görüldü. Bu gözlem dış proxy/taşıma uyumsuzluğuna işaret eder; tek başına kesin proxy iç arızasını kanıtlamaz. Anonim WebSocket upgrade denemeleri Notification → Gateway → Caddy → public boyunca 401 aldı; yol auth katmanına ulaşıyor. Her iki sunucuda da sorgu token okuma kodu bulundu; LiteSpeed'de özel WebSocket proxy tanımı zaten vardı. Proxy yapılandırması değiştirilmedi.

`content.js: v2.0.3` satırı kendi başına hata değildir. Çalışan uygulama dosyası olduğuna dair kanıt yoktur; tarayıcı eklentisi olduğu kesinleştirilmedi.

## Değişiklikler

- Yayın imajı mevcut browser dosyalarını silmez. İki önceki doğrulanmış paketten eksik 39 hash'li JS/CSS dosyası eklendi. Aynı dosya adı farklı içerik taşıyorsa paket hazırlığı durduruldu; eski HTML/manifest kopyalanmadı. Eski iki modülün 22 modüllük bağımlılık zinciri aday imaj içinde doğrulandı.
- HTML yanıtları `Cache-Control: no-store`; eksik JS/CSS ve diğer statik uzantılar SSR HTML yerine önbelleğe alınmayan text/plain 404 döndürür.
- Angular router yalnız bilinen dinamik modül yükleme hatalarında aynı origin'deki hedefe tam navigasyon yapar. Sekme storage'ında 60 saniyelik sınır döngüyü önler. Storage erişilemiyorsa veya sunucuda çalışıyorsa otomatik yenileme yapılmaz. Diğer auth/uygulama hataları normal işlenir.
- Notification istemcisi çalışan HTTP taşıma seçeneklerini (SSE/LongPolling) kullanır. Token factory, oturum ayrımı ve otomatik yeniden bağlantı korunur; başarısız WebSocket denemesiyle başlamaz. Eski açık sekme bu değişikliği almak için yenilenmelidir.

## Kanıtlar ve sınırlar

- Oturum, bildirim factory/lifecycle, SSR yapılandırması ve chunk recovery: 21 test geçti.
- Üretim derlemesi geçti; önceden mevcut SCSS bütçe uyarıları var.
- Ağsız aday önizlemesinde eski iki dosya 200/JavaScript, bilinmeyen dosya 404/text, HTML no-store doğrulandı.
- Canlıda eski/yeni 134 JS modülü bağımlılıklarıyla 200/JavaScript döndü; HTML/eksik dosya davranışı geçti.
- Yeni Chrome bağlamında hata veren iki eski modül gerçek canlı asset'lerden dinamik import edildi; ikisi de başarıyla export verdi. Bu testte API çağrıları mock; gerçek kullanıcı girişi yapılmadı.
- Canlıdan sunulan mevcut kullanıcı erişim yönetiminde masaüstü/mobil 20 ve önceki yeni kullanıcı regresyonunda 26 senaryo geçti. SystemAdmin checkbox kaldırma kontrolündeki test zamanlaması alanın DOM'dan ayrılmasını bekleyecek şekilde düzeltildi.

Üretim anahtarıyla iki dakikalık rol/izin içermeyen tanı token'ı üretip SignalR uçlarına gönderecek test otomatik onay denetimince reddedildi; ret sebebi hassas payload kullanımına ayrıca açık izin olmamasıydı. Bu komut yürütülmedi ve başka bir yöntemle token üretilerek tekrar denenmedi. Kimliksiz yol kontrolleri ve mevcut günlükler kullanıldı. **Yayın sonrası gerçek oturumlu SignalR bağlantısı/mesaj teslimi doğrulanmış sayılmaz**; kullanıcıdan normal açık oturumda yenileyip bildirim alanını ve yeni konsol hatalarını kontrol etmesi istendi. Yetkilendirme veya CAPTCHA ayarları değiştirilmedi.

Kanıtlar: `artifacts/admin-login-recovery-live/static-results.json`, `browser-results.json`, `artifacts/admin-edit-access-live/`, `artifacts/admin-user-dual-service-live/`. Betikler `tools/releases/admin-login-recovery-20261015/`.

## Yayın

Yeni imaj: `sha256:401af83b4ae142e11642b10ec7264356178af750d067aecf8a4f4eebec101d6d`.

Gerçek mevcut imaj `sha256:630a6a7493e0daac8950b2a50f5ff95ad7177ff97d8d0952e3d0e128d99b1368` üzerine yalnız yeni admin dist ve eksik eski statikler katmanlandı. Kimlik, bildirim sunucusu, Gateway, veritabanı ve diğer servis konteynerleri değiştirilmedi. Ortam değerleri/imajlar sabitlendi; payload ve aday imaj kimlikleri doğrulandı. Önizleme başarısızsa yayına geçilmez; geçiş hatasında önceki admin imajı ve sağlığı doğrulanarak geri dönülür. Canlı kontrolünde admin running/restart 0; diğer konteyner kimlikleri aynı.

Sonraki yayınlarda hash'li browser varlıklarının korunması sürdürülmelidir. Sıfırdan imaj üretiminde önceki statik varlık arşivi eklenmelidir; route recovery eski sekmelerin yeniden yüklenmesi için ek korumadır. Standart taze imaj Dockerfile'ı kendi başına geçmiş yayınları içermez.
