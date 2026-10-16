# Hızlı Okuma · Raporların Türkçeleştirilmesi · 16.10.2026

Öğrenci, öğretmen ve kurum raporlarındaki bilinen İngilizce arayüz etiketleri Türkçeleştirildi. Değişiklik gösterim katmanındadır; API kodları, kategori kimlikleri, kurumların özel adları, içerik başlıkları, mevcut sonuçlar ve ilerleme kayıtları korunur.

| Önce | Sonra |
|---|---|
| Above Average / Average / Below Average | Ortalamanın üzerinde / Ortalama / Ortalamanın altında |
| No benchmark | Karşılaştırma verisi yok |
| regression / regression_reduction | Geriye dönüşü azaltma |
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

Testler gerçek sıfır puanı, `Veri yok` durumunu, kategori kimliğini, özel kategori adını ve her iki kurum ilerleme listesini de sınar. Kod kapsamı koşusunda değişen ölçülebilir öğrenci TS satırları **8/8** çalıştı; bu dar sayı tüm uygulamanın kapsamı değildir. Paket tarafından getirilen eski satırlar dahil toplam satır kapsamı **%42,64**. Dış ortak yardımcı ve HTML etiketleri ayrıca davranış/render beklentileriyle sınanır. Kanıt: `changed-line-coverage.json`, `coverage-diff.patch`, `student-green.log` ve son ek etiket kontrolüyle `student-final.log`.

Mevcut derleme bütçesi uyarıları sürüyor: personel başlangıç paketi ve uygulama SCSS'i; öğrenci egzersiz oynatıcı SCSS'i. Bunlar bu metin değişikliğinin dışında kalan mevcut boyut uyarılarıdır; üretim derlemeleri başarılıdır. Bu yayında C# veya veritabanı değişmediği için yeni migration veya PostgreSQL yazma provası yapılmadı.

## Yayın ve canlı doğrulama

Yayın **10:47:55 TSİ**. Yeni arayüzler önce ayrı önizlemelerde doğru `/staff/` ve `/` base-href, HTML ve ana JS HTTP 200 kontrollerinden geçti. Yalnız personel ve öğrenci konteynerleri güncellendi. API ve diğer üretim konteynerlerinin kimlikleri değişmedi; iki eski arayüz imajına geri dönüş hazırlandı.

**Kurum:** gerçek Kurum yöneticisi oturumunda rapor açıldı. **Ortalamanın üzerinde**, **Geriye dönüşü azaltma** ve içerik analizinde **Yüksek / Düşük / Ölçülmedi** görüldü. Gerçek sıfır puanlar `0%`, bağımsız kelime testi `100%`, ölçümsüz hız **Veri yok**, gerçek anlama `%87` ve etkinlik toplamı **21** kaldı.

**Öğrenci:** mevcut gerçek oturumda yeni `main-FC7IOEVX.js` paketi doğrulandı. Anlama grafiği ekseninde **15 Eki**, rapor özetinde mevcut **21 etkinlik / %87 anlama / Veri yok hız** görüldü. İlk normal yenileme önceki önbellekli paketi tuttu; ikinci normal yenileme yeni paketi yükledi. Görünür ana rapor dışındaki eski ayrı rapor bileşenleri de render testlerinde doğrulandı; bunlar mevcut menüde ayrı sayfa olarak yayımlanmıyor.

**Öğretmen:** kullanıcı normal CAPTCHA girişini tamamladıktan sonra açık sekmede gerçek **Öğretmen** rolü doğrulandı ve Hızlı Okuma açıldı. Önceden açık sekme eski paketi tuttuğundan normal yenileme yapıldı; yeni `main-NUPQAKRZ.js` yüklendi. Öğrenci ayrıntısında **Ortalamanın üzerinde** ve **Geriye dönüşü azaltma** görüldü. `summary`, `reading-speed`, `comprehension`, `activity` isteklerinin dördü HTTP **200** döndü. Gerçek anlama `%87`, bağımsız kelime testi `100%`, 21 oturum, gerçek sıfır puanlar ve **Veri yok** hız korundu. Kanıtlar `teacher-report.txt`, `teacher-http-status.json`, `teacher-turkish.jpg`. CAPTCHA veya güvenlik ayarları değiştirilmedi; üç panelin canlı kontrolünde bekleyen adım kalmadı.

## Veri koruma ve kaynaklar

Test öğrencisinin üç panelin canlı kontrollerinden sonraki tam JSON'u başlangıçla **birebir eşit**: **476 XP, 21 sonuç, 12 günlük kayıt, 12 kelime ilerleme kaydı, Gün 3 ve 1 erken geçiş günü**. Program içeriği, puanlar, oturum/sonuç hashleri ve başarımlar korunuyor. Bu kontrolde yeni egzersiz sonucu oluşturulmadı. Kanıtlar `student-before.log`, `student-after-institution.log`, `student-after-teacher.log`, `preservation-result.json`.

Uygulama revizyonu `985e7597d658098a2329f65cf91846fb551e22f3`. Önceki çalışan kaynaklardan yalnız personelde **4**, öğrencide **8** runtime dosyası değişti; iki uygulama aynı **1** yeni ortak etiket dosyasını kullanır. Kaynak hash karşılaştırma listeleri `staff-source-diff.txt` ve `student-source-diff.txt`. Test kapsamını tamamlayan sonraki commit runtime paketini değiştirmez.

| Hedef | Yeni imaj |
|---|---|
| Personel | `sha256:8d39fbc3e76243fc59a2eac4cb2d70e0da8584aad62260343270be3df9099aae` |
| Öğrenci | `sha256:db267210db72ff6c5682875da773001606401942742597ef4eb7e43f82e455ef` |

Ekranlar ve test kayıtları [kanıt dizininde](../../artifacts/report-localization-20261016). Operasyon kaynakları [yayın dizininde](../../tools/releases/report-localization-20261016). Ortam ve compose dosyaları yalnız korumalı sunucu dizininde tutulur.
