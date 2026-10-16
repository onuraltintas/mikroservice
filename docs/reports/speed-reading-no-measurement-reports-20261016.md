# Hızlı Okuma · Ölçüm olmayan raporlar · 16.10.2026

Öğrenci, öğretmen ve kurum raporlarında ölçülmemiş okuma hızı artık **Veri yok** gösterir. Ölçüm olmadan `Below Average`, hız önerisi, sahte sıfır grafik noktası veya önceki döneme göre yapay düşüş üretilmez. Hız ölçülmemiş olsa da gerçek anlama ölçümleri görünmeye devam eder. Gerçek cevaplardan elde edilen **0%** puan korunur; soru sorulmamış anlama alanı **Veri yok** gösterir.

## Uygulama

Analitik kaynak, geçerli hız bulunmadığında karşılaştırmayı `Veri yok`, önerileri boş, değişim oranını sıfır döndürür. Sayısal API alanları eski istemcilerle uyum için sıfır kalabilir; arayüz ölçüm varlığına göre gösterir. Soru sayısına dayanan ek anlama bayrakları, gerçek sıfır ile hiç ölçülmemiş durumu ayırır. Mevcut DTO yapıcıları değişmedi. Ortak DTO'nun admin üreticisi de bayrağı doldurur.

Kurum içerik analizindeki seviye satırları ve günlük performans serileri de aynı kurala uyar. Öğrenci hız göstergesi, istatistik ve trendleri yalnız gerçek ölçüm varsa oluşturulur. Etkinlik sayıları, bağımsız kelime sonuçları, XP ve program ilerlemesi değiştirilmez.

## Testler

| Kontrol | Sonuç |
|---|---|
| Başlangıç PostgreSQL hata senaryoları | 4 RED, düzeltmeden sonra GREEN |
| Önceki dönemden kaynaklanan sahte düşüş | 4 RED, düzeltmeden sonra GREEN |
| Personel / öğrenci başlangıç arayüz hata senaryoları | 2 / 2 RED, düzeltmeden sonra GREEN |
| Uygulama birim testleri | 1.317 / 1.317 |
| PostgreSQL ve HTTP entegrasyonu | 28 / 28 |
| Hızlı Okuma personel testleri | 59 / 59, 10 dosya |
| Öğrenci rapor testleri | 29 / 29 |
| Personel ve öğrenci üretim derlemeleri | Başarılı |
| Değişen C# satır kapsamı | 28 / 30 ölçülebilir satır, %93,33 |
| Gerçek üretim kopyasında aday DLL provası | 6 senaryo başarılı |
| Şema / veri koruma provası | Migration yok; 86 tablo / 8.609 mevcut satır korundu |

Beş özel senaryo boş geçmiş, ölçülmemiş kayıt, geçersiz sıfır WPM, gerçek hız ve gerçek sıfır anlama ile yalnız anlama ölçümünü sınar. Diğer öğrencinin ölçümü bu öğrencide veri varmış gibi gösterilmez. Önceki dönemde ölçüm varken güncel dönemin boş olması yapay -100% düşüş üretmez. Önceki 21 tür sonuç matrisi de çalıştırıldı; bu, tüm oynatıcıların canlıda yeniden oynandığı anlamına gelmez.

İki ayrı kod incelemesi tamamlandı; bulunan ortak DTO üreticisi ve önceki dönem düşüşü sorunları giderildikten sonra onaylandı. Kapsam yüzdesi tüm uygulamanın toplamı değildir; admin üreticisindeki iki değişen satır PostgreSQL kapsam koşusunda çalışmadı. Mevcut paket/SCSS boyut uyarıları sürüyor; derleme hatası yok.

## Canlı doğrulama

Yayın **10:18:04 TSİ** gerçekleşti. Öğrenci ve öğretmen oturumlarıyla kontrol tamamlandı; kurum CAPTCHA girişi kullanıcı tarafından normal tarayıcıda yapıldı ve açık sekmede **Kurum yöneticisi** rolü ayrıca doğrulandı. CAPTCHA veya güvenlik ayarları değiştirilmedi.

- **Öğrenci:** rapor hız kartı **Veri yok**; ölçülmemiş hız trendi çizilmiyor. Gerçek anlama %87, etkinlik sayısı 21 olarak kaldı. Önceki paketi önbellekte tutan açık sekmede ikinci normal yenileme yeni ana JS'yi yükledi ve sonuç doğrulandı.
- **Öğretmen:** öğrenci ayrıntısındaki hız özeti ve hız gelişimi **Veri yok**; hız önerileri boş. Dört ayrıntı isteği HTTP 200. Gerçek anlama karşılaştırması ve mevcut sonuçlar korunuyor.
- **Kurum:** özet hız **Veri yok**, anlama %87 ve 21 etkinlik. Öğrenci ayrıntısındaki dört istek ve sınıf özeti HTTP 200. Hız yanıtı `performanceLevel: "Veri yok"`, `recommendations: []`, `trend: []`, `improvementRate: 0` döndü. Seviye 1 ve 4 hızları **Veri yok**; seviye 1 anlama %86,7, ölçülmemiş seviye 4 anlama **Veri yok**. Günlük performansta yalnız gerçek anlama serisi kaldı. Bağımsız kelime testi 100%, mevcut gerçek sıfır puanlar 0% olarak görünüyor.

Canlı test hesabında güncel pozitif WPM yoktur. Pozitif hızın ve gerçek sıfır anlama puanının korunması PostgreSQL/arayüz senaryolarıyla doğrulandı; canlıya örnek sonuç eklenmedi.

Ekran ve istek kanıtları [kanıt dizininde](../../artifacts/no-measurement-reports-20261016): `student-no-data.jpg`, `teacher-no-data.jpg`, `institution-no-data.jpg`, `institution-content.jpg`, üç rolün rapor metinleri, öğretmen/kurum HTTP durumları ve hız yanıtları.

## Veri koruma ve yayın izolasyonu

Kaynak revizyonu `4006f41c3676991fa93a4bc6f0ab501642bac02e`. API, personel ve öğrenci kaynakları çalışan tabanlardan ayrı dizinlerde derlendi; sırasıyla yalnız 5 / 3 / 7 runtime dosyası değişti. Öğrenci değişmemiş taban kontrol derlemesinin `main-PACRIYZM.js` üretmesi önceki canlı tabanla eşleşmeyi doğruladı. Yeni öğrenci ana paketi `main-ZSTD3B7J.js`.

Yayın API yazıcısı kapalıyken alınan yedek ve 86 tablonun aday oluşturulmadan önce/sonra tam satır hash karşılaştırmasından geçti. Migration, silme veya backfill yapılmadı. API'ye yalnız Application/Infrastructure DLL ve PDB eklendi; diğer servislerin konteyner kimlikleri korundu. Üç hedef için önceki imajlara geri dönüş hazırlandı. Önizleme kesin HTTP 200 kontrolleriyle geçti.

Test öğrencisinin tam başlangıç/bitiş JSON'u, kurum canlı testinden sonra da **birebir eşit**: **476 XP, 21 sonuç, 12 günlük kayıt, 12 kelime ilerleme kaydı, Gün 3 ve 1 erken geçiş günü**. Program alanları, mevcut puanlar, sonuç hash'leri ve başarımlar korundu. Kanıtlar `preservation-result.json`, `student-before.log`, `student-after-institution.log`.

| Hedef | Yayınlanan imaj |
|---|---|
| API | `sha256:4edf58e412fbb067fdcf21352ef40992d3809d799780be5de92a7ada421631b9` |
| Personel | `sha256:db03c862bc227489fe3dbe9d934dd3c6414235cc7e8219185183da63505f5d04` |
| Öğrenci | `sha256:de45eac77e84f1a7bfab84e030fa92ffbf6155158a67517f944a1b2305c5ecff` |

Operasyon kaynakları [yayın dizininde](../../tools/releases/no-measurement-reports-20261016); yedek ve ortam dosyaları yalnız sunucuda tutulur.

Son gözlem **10:33:43 TSİ**, yayından **15 dakika 39 saniye** sonra: 69 API isteğinde **0 sunucu 5xx**, uygulamada **0 ERR/FTL**; öğrenci/personel dış sayfaları HTTP 200. Üç hedef konteyner çalışıyor, yeniden başlama sayıları 0. Yeni imajlar ve diğer servislerin konteyner kimlikleri doğrulandı. Otomatik gözlem `10:33:30 TSİ` tamamlanma işaretini üretti. İzlem sonrasında alınan `student-final.log` başlangıç tam JSON'uyla yine eşit çıktı. Kanıtlar `monitoring.log`, `monitoring-complete.txt`, `final-health.log`, `student-final.log`; kaynak ve sunucu operasyon dosyalarının hash eşleşmesi `operations-source.sha256` içinde kayıtlı.
