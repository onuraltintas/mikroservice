# Hızlı Okuma ödev düzeltmeleri — 15.10.2026

## Sonuç ve değişiklikler

Önceki [canlı ödev döngüsü raporundaki](speed-reading-assignment-live-20261015.md) üç hata düzeltildi ve **15.10.2026 19:39:43 Türkiye** saatinde canlıya alındı. Kaynak checkpoint'i `febe5b6c19d8885f1f53750c829ff78622561b04`; başarısız regresyon checkpoint'leri `6c7983c5` ve `4cdf435`.

1. **Teslim tarihi:** öğretmen ve kurumun ortak ödev oluşturma bileşeni seçilen günün Türkiye saatindeki sonunu UTC'ye dönüştürüyor. 16 Ekim → `2026-10-16T20:59:59Z` → ekranlarda 16 Ekim. Öğretmen ve kurum için ayrı test var; yıl sonu sınırı da kapsanıyor. Eski kayıtların amaçlanan tarihi çıkarılamayacağından geçmiş tarihler topluca değiştirilmedi.
2. **Ölçülmeyen sonuç:** sunucu, ölçümü olmayan egzersizi tamamlanmış olarak saklıyor; öğrenci ödevinin puanı ve performans metriği `NULL` kalıyor. Böylece öğrenci, öğretmen ve kurum ortalamalarına yapay sıfır eklenmiyor. Gerçek ölçülmüş sıfır korunuyor. Sonuç tablosunun mevcut `score=0, is_measured=false` biçimi değiştirilmedi; ödev projeksiyonu bu sonucu puansız taşıyor.
3. **Ödev listesi:** asenkron yükleme başlangıcı, yanıt, hata ve arama gecikmesi sonrasında görünüm güncelleniyor. Gerçek şablonla yazılan regresyonlar sonradan `detectChanges()` veya sekme tıklaması olmadan bekleyen listeyi, tamamlananlar görünümünü ve hata durumunu doğruluyor.

Geçmiş yapay sıfırlar için `20261015163000_ClearUnmeasuredAssignmentMetrics` veri migration'ı uygulandı. Yalnız tamamlanmış, aynı öğrenciye ait sonuçla eşleşen ve `is_measured=false` olan ödev metriklerini temizler; sonuç kimliği, tarih, tamamlanma, üyelik ve ölçülmüş puanlar korunur. İkinci çalıştırma etkisizdir. `Down` geçmiş puanı güvenilir biçimde geri üretemediği için veri uydurmaz; gerektiğinde işlem öncesi yedek kullanılabilir.

## Otomatik doğrulama

| Kontrol | Sonuç | Kanıt |
|---|---:|---|
| Backend birim testleri | 1.294 / 1.294 | `artifacts/speed-reading-assignment-fixes-20261015/unit-green.trx` |
| Hızlı Okuma entegrasyon paketi, gerçek PostgreSQL dahil | 190 / 190 | `postgres-suite.trx` |
| İzole canlı kaynak tabanında öğrenci frontend | 994 / 994 | `student-suite.log` |
| Staff çalışma alanı testleri | 291 / 291, 47 dosya | `staff-workspace-suite.log` |
| Öğrenci odaklı liste/puan regresyonları | 5 / 5 | `student-green.log` |
| Öğretmen/kurum tarih regresyonları | 2 / 2 | `staff-green.log` |
| Sunucuda gerçek üretim yedeğinin ayrı DB kopyası ve aday DLL'lerle prova | 67 / 67 | `clone-results.json` |
| Backend, öğrenci ve staff üretim derlemeleri | Başarılı | `backend-release-build.log`, `student-build.log`, `staff-build.log` |

Sonuç dosyaları aynı [kanıt klasöründedir](../../artifacts/speed-reading-assignment-fixes-20261015/). Odaklı testler geniş koşulara dahildir; toplam olarak tekrar toplanmamalıdır. Paket ve TypeScript kontrolleri ile C#/TypeScript değişiklikleri bağımsız incelemelerden geçti.

Çalışma alanındaki yayımlanmamış Course Management değişiklikleri izole staff test tabanında rol tanımı/fixture uyumsuzluğu oluşturuyor (`staff-suite.log`, `staff-speed-suite.log`). Bu denemeler başarılı gösterilmiyor. Canlıya bunları katmadan gerçek önceki staff yayın kaynağına yalnız ödev tarih düzeltmesi eklendi; bu paketin üretim derlemesi geçti. Çalışma alanının kendi staff paketinde 291 test geçti. Depo genelindeki bütün CI işleri veya %80 genel kapsam iddia edilmez.

## Yayın, prova ve geri dönüş

Sunucu yayın dizini `/var/lib/eduivme/releases/speed-reading-assignment-fixes-20261015`.

| Bileşen | Canlı imaj SHA-256 |
|---|---|
| Hızlı Okuma API | `76236a56a1b02e3655ec2f3611fdd1acc72112488b7ad2430e4c396e12cdd733` |
| Öğrenci frontend | `b2d2091aed906c6710fd997ec6db53a4a88b1dd8226a07c6b426a18e570e0f57` |
| Personel portalı | `6215a3e3d25876fc6da1f191798d656f55ca0afd846fcef6c2350343e8c47477` |

Üç imaj etiketi `speed-reading-assignment-fixes-20261015`. Backend önceki canlı `d1b74c30`, öğrenci `d7f743ab` tabanından izole üretildi. Staff önceki gerçek yayın kaynak paketiyle üretildi. Backend'in Domain ve Infrastructure DLL/PDB dosyaları birlikte taşındı: nullable metrik metot imzası bu iki derlemenin uyumunu gerektiriyor. Başka servisler ve yayımlanmamış çalışma alanı değişiklikleri taşınmadı. Önceki hash'li JavaScript varlıkları korundu.

İşlem öncesi tam PostgreSQL yedekleri alındı. Aday paket önce yalnız `speed_maintenance_drill_20261015` adlı yedek kopyasında migration, geçmiş veri koruması, yeni puansız tamamlanma, tekrar tamamlama, kelime kalıcılığı ve öğretmen sorgularıyla doğrulandı; kopya sonunda kaldırıldı. Canlı eski API yazıcısı kısa süre durduruldu, yeni yedek alındı, migration-only çalıştırıldı, ardından API ve iki frontend yayımlandı. Böylece eski yazıcının veri onarımı sırasında yeni yapay sıfır üretmesi engellendi.

Geri dönüş yapılandırması önceki tam imaj kimlikleri ve ortamdan donduruldu. Başarısız yayın durumunda eski imajlar ve sağlıkları kontrol edilerek geri dönülür; doğru `NULL` onarımı korunabilir. Yeni gerçek kullanıcı yazılarını kaybettireceği için otomatik tam DB geri yükleme yapılmaz. CAPTCHA açık ve eşik **0,5** kaldı.

## Canlı doğrulama

Yeni test ödevi kurum oturumundan yalnız `testogrenci@eduivme.com` için `testogretmen@eduivme.com` öğretmenine bağlanarak oluşturuldu: **Canlı Düzeltme Testi · 15.10.2026**, ödev `b8421edc-c31a-4538-80d4-fa69ee892caf`, öğrenci ödevi `ebd2f213-05ed-4a3d-b4b8-a3e88f369800`.

- Kurum formunda 16 Ekim seçildi; kurum tablosu **16 Eki 2026**, öğrenci kartı **16 Ekim** gösterdi. Salt okunur DB kontrolü `2026-10-16 20:59:59+00` kaydını doğruladı.
- Öğrenci yeni sürüm sekmesinde sayfayı açınca bekleyen ödev, sekmeye tıklanmadan yüklendi. Önceki ölçülmeyen tamamlanma için **Henüz puan yok** görüldü.
- Önceki canlı ödevin tamamlanması, sonuç kimliği ve 75 saniye süresi korundu; yalnız yapay ödev puanı/performans metriği temizlendi. Kurum raporunda tamamlanma %100, ortalama puan **Veri yok**, öğrenci puanı **—** oldu.

Yeni ödev **19:45:34 Türkiye** saatinde normal süreyle tamamlandı. Ekran 01:15 ve “Sonuç kaydedildi.” gösterdi; sunucunun kaydettiği geçen süre 76 saniyedir. Saat veya sonuç taklit edilmedi. Öğrenci “Ödevlere Dön” ile döndüğünde başka etkileşim olmadan **0 bekleyen / 2 tamamlanan** ve **Henüz puan yok** göründü. Öğretmenin mevcut gerçek oturumunda liste filtrelemesi ve ayrıntı isteği yenilendi: **1/1 tamamlandı**, teslim 16 Ekim, öğrenci puanı **—**. Kurum raporu **2 ödev / 1 öğrenci / %100 tamamlanma**, puan **Veri yok**, öğrenci süre 76 saniye gösterdi. Raporun “Tamamlandı 1” sayacı öğrenci sayısıdır; iki ödevin tek öğrenci tarafından tamamlandığı DB ve iki ayrı ödev satırıyla doğrulandı.

Salt okunur DB kontrolü eski ödevin sonucunu ve tamamlanma zamanını koruduğunu; yeni ödevin `Score=NULL`, `KeyPerformanceMetric=NULL`, `is_measured=false`, `IsCompleted=true` olduğunu gösterdi. Yeni sonuç `3cf1af06-72ff-462a-a55d-315b827c9a0e`. Sistemde bu onarımın koşuluna uyup metrikleri dolu kalan ödev sayısı **0**. [Tamamlanma ve DB kanıtı](../../artifacts/speed-reading-assignment-fixes-20261015/production-observation-2.log).

Ekran kanıtları: [yeni teslim tarihi](../../artifacts/speed-reading-assignment-fixes-20261015/student-due-date.jpg), [öğrenci sonuç](../../artifacts/speed-reading-assignment-fixes-20261015/student-result.jpg), [otomatik tamamlanan liste](../../artifacts/speed-reading-assignment-fixes-20261015/student-final.jpg), [öğretmen ayrıntısı](../../artifacts/speed-reading-assignment-fixes-20261015/teacher-final.jpg), [kurum raporu](../../artifacts/speed-reading-assignment-fixes-20261015/institution-final.jpg). Kimlik doğrulama token'ı, parola veya cookie kanıt dosyalarına kaydedilmedi.

Yayın izlemi **19:39:43–19:55:09 Türkiye**, toplam **15 dakika 26 saniye** boyunca tamamlandı. Ara sağlık örnekleri 19:45:04, 19:46:31, 19:49:20, 19:51:37 ve 19:52:46'da; son örnek 19:55:09'da kaydedildi. Son kontrolde üç yayın bileşeni çalışıyor, imaj kimlikleri doğru ve yeniden başlama sayıları 0; iki dış giriş URL'si 200. Yayın sonrası API günlüğündeki **49 HTTP isteğinde 0 sunucu 5xx**, ortalama **95,45 ms**, en yüksek **475,83 ms**. Onarım koşuluna uyup metrikleri dolu kalan ödev sayısı hâlâ 0. [Son sağlık ve veri kanıtı](../../artifacts/speed-reading-assignment-fixes-20261015/production-observation-final.log). Bu düşük trafikli gözlem yük testi veya kalıcı performans garantisi değildir.

## Diğer açık konular

- **Admin üzerinden mevcut hesabı Hızlı Okuma kurumuna doğrudan üye ekleme kontrolü eksik.** Sonraki kullanıcı isteğiyle canlı SystemAdmin ekranı ve kaynak yeniden kontrol edildi: listeleme, etkinleştirme/pasifleştirme ve öğretmen atama var; kurumda henüz bulunmayan hesabı arayıp ekleyen form yok. Admin roster servisinde de yeni üyeyi seçmeye/eklemeye ait metot yok. Backend `PUT /api/speed-reading/institutions/{institutionId}/members/{userId}` ise yeni aktif üyelik oluşturmayı zaten destekliyor: aktif Hızlı Okuma kurumu, işlem yetkisi ve hesabın aktif Hızlı Okuma erişimiyle eşleşen öğrenci/öğretmen rolü kontrol ediliyor. Kaydın kapsamı kurum + kullanıcı + roldür; mevcut başka kurum üyeliklerini, ürün rollerini veya abonelikleri değiştirmez. Mevcut üyelik/controller testleri **13/13**, ürün rolü/erişim ve kurum yöneticisi kapsam testleri **2/2** yeniden geçti (`--no-build`, önceki derlenmiş test paketleri). Bu kontrol sırasında canlı üyelik değişikliği veya ekleme formu yayını yapılmadı; arayüz eksikliği açık kalıyor. Önceki test bağlantısında kontrollü test verisi işlemi kullanılmıştı. Kurumun normal davet akışı mevcut.
- Statik taramada kalan **77 kullanılmayan kod adayı**, kullanım dışı oldukları kesinleşmediği için tutuluyor. Bunlar doğrulanmış 77 uygulama hatası değildir.
- Eski açık PWA sekmesi önceki service worker sürümünde kalabiliyor. Yeni sekmede yeni `main-3MZVISNU.js` ve düzeltmeler doğrulandı. Eski görünüm sürerse uygulama sekmelerini kapatıp yeniden açmak gerekir. Service worker veya güvenlik ayarları zorla devre dışı bırakılmadı.
- Önceden var olan derleme uyarıları: staff SCSS bütçesi yaklaşık 377 bayt aşımı; ortak Identity/Notification test bağımlılıklarında `System.Security.Cryptography.Pkcs 10.0.0` NU1902/NU1903 denetim uyarıları. Bunlar bu dar yayınla giderilmiş sayılmıyor; tam güvenlik taraması yapılmış olduğu iddia edilmez.

Canlı test bir test öğrencisi ve göz takibi ödeviyle sınırlıdır; tüm egzersiz türleri, gerçek ödeme ve yeni davet kabulü için toplu uçtan uca başarı iddiası yoktur.
