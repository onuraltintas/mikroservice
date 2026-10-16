# Hızlı Okuma · Raporların Türkçeleştirilmesi · 16.10.2026

Öğrenci, öğretmen ve kurum raporlarındaki bilinen İngilizce arayüz etiketleri Türkçeleştirildi. Değişiklik gösterim katmanındadır; API kodları, kategori kimlikleri, kurumların özel adları, içerik başlıkları, mevcut sonuçlar ve ilerleme kayıtları korunur.

| Önce | Sonra |
|---|---|
| Above Average / Average / Below Average | Ortalamanın üzerinde / Ortalama / Ortalamanın altında |
| No benchmark | Karşılaştırma verisi yok |
| regression / regression_reduction | Geriye dönüşü azaltma |
| grouping / guided_reading / rsvp / subvocalization | Gruplayarak okuma / Rehberli okuma / Seri okuma / İç ses azaltma |
| high / medium / low / not-measured | Yüksek / Orta / Düşük / Ölçülmedi |
| comprehension / exercise_success | Anlama / Egzersiz başarısı |
| Average WPM / Standard Deviation / Total Readings | Ortalama hız / Standart sapma / Toplam okuma |
| Comprehension / Less / More | Anlama / Daha az / Daha fazla |
| No activity data available / min / 1h 15m | Etkinlik verisi bulunmuyor / dk / 1 sa 15 dk |
| Oct 16 | 16 Eki |

## Kapsam

Öğretmen ve kurumun kullandığı ortak öğrenci ayrıntı raporunda karşılaştırma, güçlü ve gelişim alanları çevrilir. Kurum içerik analizi düzeyleri ile ilerleme listelerindeki gösterge kodları Türkçe görünür.

Öğrencinin kategori grafik/tablo/gelişim alanları, soru türü ve Bloom seviye etiketleri, hız istatistikleri, anlama merkez etiketi ve etkinlik raporunun boş durum/süre/yoğunluk etiketleri çevrildi. Gösterge paneli ve seri raporunda ay adları Türkçe; grafik tarihlerinde de aynı biçim kullanılır. Eski egzersiz çeviri listesindeki parantezli Scanning/Skimming/Tracking/Chunking ifadeleri kaldırıldı ve ortak sözlük kullanıldı.

Tek bir saf gösterim fonksiyonu bilinen kodları çevirir; tanınmayan özel kategori adı aynen kalır. `Object.hasOwn`, `constructor` gibi özel adların JavaScript prototipinden yanlış çözülmesini engeller. Öğrenci `categoryId` ve puan eşleştirmeleri ham API anahtarıyla kalır. Rapor yanıtları veya veritabanı kayıtları yeniden yazılmaz.

## Testler ve inceleme

| Kontrol | Sonuç |
|---|---|
| Öğretmen ve kurum İngilizce etiketlerini yeniden üreten render testleri | Düzeltmeden önce 2 RED, sonra GREEN |
| İçerik düzeyi, öğrenci kategori/soru/Bloom etiketleri, tarih/süre ve grafik/boş durum testleri | İlgili hatalar RED ile doğrulandı, sonra GREEN |
| Hızlı Okuma personel testleri | 63 / 63, 10 dosya |
| Öğrenci servis ve rapor testleri | 29 / 29 |
| Personel / öğrenci üretim derlemeleri | Başarılı |
| TypeScript / UI kod incelemesi | Onaylı |
| Yayın betikleri, Dockerfile ve 154 dosyalık paket incelemesi | Onaylı; tüm payload hashleri eşleşiyor |

Testler gerçek sıfır puanı, `Veri yok` durumunu, kategori kimliğini, özel kategori adını ve her iki kurum ilerleme listesini de sınar. Salt okunur canlı katalog taraması sonunda dört eski kod için ek RED doğrulandı; ortak sözlükte tamamlandı. Katalogdaki **10 teknik kategori kodunun tamamı** beklenen Türkçe etiketlerle sınandı. Son 63/29 koşuları `staff-catalog-green.log` / `student-catalog-green.log`, son derlemeler `staff-catalog-build.log` / `student-catalog-build.log` içinde kayıtlı; katalog kanıtı `category-audit.sql/log`.

Kod kapsamı koşusunda değişen ölçülebilir öğrenci TS satırları **8/8** çalıştı; bu dar sayı tüm uygulamanın kapsamı değildir. Paket tarafından getirilen eski satırlar dahil toplam satır kapsamı **%42,64**. Dış ortak yardımcı ve HTML etiketleri ayrıca davranış/render beklentileriyle sınanır. Kanıt: `changed-line-coverage.json`, `coverage-diff.patch`, `student-green.log`. Son dört ek yalnız bu dış yardımcıdaki sabit alias'lardır; kapsam ölçümüne eklenmiş sayılmaz.

Mevcut derleme bütçesi uyarıları sürüyor: personel başlangıç paketi ve uygulama SCSS'i; öğrenci egzersiz oynatıcı SCSS'i. Bunlar bu metin değişikliğinin dışında kalan mevcut boyut uyarılarıdır; üretim derlemeleri başarılıdır. Bu yayında C# veya veritabanı değişmediği için yeni migration veya PostgreSQL yazma provası yapılmadı.

## Yayın ve canlı doğrulama

Son yayın **11:01:15 TSİ**, revizyon `7dd0f5cf`. İlk Türkçeleştirme **10:47:55 TSİ** yayımlandı; son katalog taramasında bulunan dört eski kod da ikinci paketle tamamlandı. Her iki paket ayrı önizlemelerde doğru `/staff/` ve `/` base-href, HTML ve ana JS HTTP 200 kontrollerinden geçti. Yalnız personel ve öğrenci konteynerleri güncellendi. API ve diğer üretim konteynerlerinin kimlikleri değişmedi; önceki iki arayüz imajına geri dönüş hazırlandı. İlk gözlem son sürüm tarafından geçersiz kılındı; 15 dakikalık son gözlem ikinci yayının zamanından başlatıldı.

**Kurum:** gerçek Kurum yöneticisi oturumunda son `main-RACSK3ZE.js` paketi doğrulandı. Ayrıntı raporunun dört isteği HTTP **200** döndü. **Ortalamanın üzerinde**, **Geriye dönüşü azaltma** ve içerik analizinde **Yüksek / Düşük / Ölçülmedi** görüldü. Gerçek sıfır puanlar `0%`, bağımsız kelime testi `100%`, ölçümsüz hız **Veri yok**, gerçek anlama `%87` ve etkinlik toplamı **21** kaldı. Son paket dizinindeki `institution-report.txt`, `institution-http-status.json`, `institution-turkish.jpg`, `institution-content.txt/jpg` kanıtlardır.

**Öğrenci:** mevcut gerçek oturumda son `main-IDG6HB6Q.js` paketi doğrulandı. Anlama grafiği ekseninde **15 Eki**, rapor özetinde mevcut **21 etkinlik / %87 anlama / Veri yok hız** görüldü. İlk normal yenileme önceki önbellekli paketi tuttu; ikinci normal yenileme yeni paketi yükledi. Görünür ana rapor dışındaki eski ayrı rapor bileşenleri de render testlerinde doğrulandı; bunlar mevcut menüde ayrı sayfa olarak yayımlanmıyor.

**Öğretmen:** kullanıcı normal CAPTCHA girişini tamamladıktan sonra açık sekmede gerçek **Öğretmen** rolü doğrulandı ve Hızlı Okuma açıldı. Son sürüm normal yenilemeyle `main-RACSK3ZE.js` üzerinden tekrar kontrol edildi. Öğrenci ayrıntısında **Ortalamanın üzerinde** ve **Geriye dönüşü azaltma** görüldü. `summary`, `reading-speed`, `comprehension`, `activity` isteklerinin dördü HTTP **200** döndü. Gerçek anlama `%87`, bağımsız kelime testi `100%`, 21 oturum, gerçek sıfır puanlar ve **Veri yok** hız korundu. Kanıtlar son paket dizinindeki `teacher-report.txt`, `teacher-http-status.json`, `teacher-turkish.jpg`. CAPTCHA veya güvenlik ayarları değiştirilmedi. Üç rol de son paketle doğrulandı; bekleyen canlı oturum kontrolü yok.

## Veri koruma ve kaynaklar

Test öğrencisinin üç panelin son paketle canlı kontrollerinden sonraki tam JSON'u ilk yayın öncesi başlangıçla **birebir eşit**: **476 XP, 21 sonuç, 12 günlük kayıt, 12 kelime ilerleme kaydı, Gün 3 ve 1 erken geçiş günü**. Program içeriği, puanlar, oturum/sonuç hashleri ve başarımlar korunuyor. Bu kontrolde yeni egzersiz sonucu oluşturulmadı. Başlangıç `artifacts/report-localization-20261016/student-before.log`; son paket kanıtları `student-after-institution.log`, `student-after-teacher.log`, `preservation-result.json`.

Son uygulama revizyonu `7dd0f5cfa6fc56d5ef80b7d56cdce6ef67518244`; ilk ana düzeltme `985e7597`. Önceki çalışan kaynaklardan yalnız personelde **4**, öğrencide **8** runtime dosyası değişti; iki uygulama aynı **1** yeni ortak etiket dosyasını kullanır. Kaynak hash karşılaştırma listeleri `staff-source-diff.txt` ve `student-source-diff.txt`. Son sürüm bu ortak dosyaya yalnız dört alias ekler; diğer runtime kaynakları değişmez.

| Hedef | Yeni imaj |
|---|---|
| Personel | `sha256:431d619f3021059d1460bfae748e79fa8ce5ee1417a14ee8c818394a4d942b1d` |
| Öğrenci | `sha256:ff2ad1c23ebaaef0997f08ebe0db9993aa6e006d817ca93581dd2a2a71e17e72` |

Testler ve ilk rol kayıtları [ilk kanıt dizininde](../../artifacts/report-localization-20261016); son paket ekranları ve gözlemi [son kanıt dizininde](../../artifacts/report-localization-20261016-r2). Operasyon kaynakları [son yayın dizininde](../../tools/releases/report-localization-20261016-r2). Ortam ve compose dosyaları yalnız korumalı sunucu dizininde tutulur.

## Son gözlem

Son paketin otomatik gözlemi **11:16:38 TSİ** tamamlandı; yayın üzerinden **15 dakika 23 saniye** geçti. Ek son kontrol **11:17:22 TSİ**, yayından **16 dakika 7 saniye** sonra: 35 API isteğinde **0 sunucu 5xx**, uygulamada **0 ERR/FTL**, dış öğrenci/personel sayfaları HTTP **200**. API, personel ve öğrenci yeniden başlama sayıları **0**. İki son imaj ve diğer üretim konteynerlerinin kimlikleri doğrulandı.

İzlem sonrasındaki `student-final.log`, ilk yayın öncesi başlangıç tam JSON'uyla yine birebir eşit. Gözlem kanıtları `monitoring.log`, `monitoring-complete.txt`, `final-health.log`; kontrol dosyalarının yerel/sunucu hash eşitliği `operations-source.sha256`, saklanan ve ortam dosyaları içermeyen statik paket arşivi hash'i `package-audit.log` içinde. Katalogdaki 10 kodun gerçek yardımcı fonksiyonla kontrolü `catalog-translation-check.json` içinde başarılı. Bekleyen yayın veya canlı panel doğrulaması kalmadı.
