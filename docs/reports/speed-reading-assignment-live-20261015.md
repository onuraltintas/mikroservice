# Hızlı Okuma canlı ödev döngüsü — 15.10.2026

## Sonuç

Gerçek öğrenci, öğretmen ve kurum oturumlarında **öğretmenden ödev atama → öğrencinin tamamlaması → öğretmen ayrıntısı ve kurum raporuna yansıma** doğrulandı. Tamamlanma kaydı başarılıdır. İlk koşuda aşağıdaki üç arayüz/raporlama bulgusu açıktı; o koşuda uygulama kodu, imaj veya migration değiştirilmedi. **Sonraki düzeltme yayını 19:39:43 Türkiye saatinde yapıldı; üç bulgu yeni canlı ödev döngüsüyle kapatıldı.** Güncel sonuçlar, migration ve test kanıtları [düzeltme raporundadır](speed-reading-assignment-fixes-20261015.md). Aşağıdaki kayıt ilk koşunun tarihsel bulgularını korur.

## Test hesapları ve bağlantılar

- Öğrenci: `testogrenci@eduivme.com` (`473bd2d6-e3db-47c4-be56-eeedec0022ed`).
- Öğretmen: `testogretmen@eduivme.com` (`b184504c-a3fa-4f7d-ad40-71fe418eaa10`).
- Kurum yöneticisi: `testkurum@eduivme.com`.
- Ortak test kurumu: `fc4a2a09-87de-46d8-b034-f09f7dae8850`, görünen adı “Koçluk Canlı Yayın Testi · coaching-live-20261013-r1”. Kurum adındaki Koçluk ibaresine rağmen bu işlemler Hızlı Okuma kapsamındadır.

Admin üyelik ekranında yeni üye ekleme kontrolü bulunmadığından yalnız belirtilen test öğrencisi ve test öğretmeni için iki aktif Hızlı Okuma kurum üyeliği kontrollü SQL test verisi işlemiyle oluşturuldu. İşlem öncesinde hesap/e-posta, aktif servis erişimi ve servise özel rol doğrulandı; üyelik tablosu sunucuda yedeklendi. Betik işlem kilidi ve tekrar çalıştırma koruması kullanır. Yedek `/var/lib/eduivme/releases/speed-reading-assignment-live-20261015/memberships-before.backup` dosyasındadır.

Öğretmen–öğrenci bağlantısı gerçek SystemAdmin oturumundaki kurum üye ekranından “Ata / değiştir” ile kuruldu. Kurum ekranındaki öğrenci satırında “Canlı Test teacher” görüldü. [Bağlantı kalıcılık kanıtı](../../artifacts/speed-reading-assignment-live-20261015/link-database-proof.log).

Koçluk bağlantıları, servis rolleri ve abonelikler değiştirilmedi. CAPTCHA açık kaldı; normal kullanıcı girişleri kullanıcının kendi tarayıcısında tamamlandı.

## Canlı doğrulama

| Adım | Gözlenen sonuç |
|---|---|
| Öğretmenin öğrenci listesi | Test öğrencisi ve test kurumu göründü; öğrenci sayısı 1. |
| Öğretmenden yeni ödev | “Canlı Test · Öğretmen Öğrenci Kurum · 15.10.2026” başlıklı Göz Takibi / Hareketi Takip Et ödevi yalnız test öğrencisine atandı. Başarı mesajı ve 0/1 durumu görüldü. |
| Öğrencinin ödevi açması | Öğrenci listesinde ödev bulundu; oyuncu URL'sinde gerçek `assignmentId` ve `practiceMode=false` doğrulandı. |
| Öğrencinin tamamlaması | 75 saniyelik egzersiz normal süreyle tamamlandı; “Sonuç kaydedildi.” ve “Ödevlere Dön” görüldü. Süre/puan taklit edilmedi. |
| Öğrenci geçmişi | Tamamlananlar sekmesine geçildikten sonra 0 bekleyen, 1 tamamlanan ve doğru ödev başlığı görüldü. Otomatik yükleme sorunu aşağıda ayrı kaydedildi. |
| Öğretmen sonucu | Liste yenilendiğinde 1/1 tamamlandı; ayrıntılar yeniden açıldığında “Canlı Test student — Tamamlandı — 15 Eki 2026” göründü. |
| Kurum sonucu | Hızlı Okuma → Kurum raporları → Ödevler: 1 ödev, 1 öğrenci, 1 tamamlandı, 0 devam ediyor, 0 başlamadı, %100 tamamlanma; süre ortalama/medyan/en kısa/en uzun 75 saniye. Öğrenci satırında “Tamamlandı / 75 sn / 15 Eki 2026”. |

Ödev kimliği `ea11655e-9b4b-4bd3-be2f-1ee2c9b8c871`; öğrenci ödevi `26bf6cb6-e7a9-46a6-b377-eeb49d3b8589`; oturum `d6806365-f8f6-4852-945d-1095898c6ead`; sonuç `ddefbe3b-bafa-4794-bba7-cf5894b76195`. Tamamlanma: **15.10.2026 19:17:12 Türkiye**. Salt okunur JOIN sorgusu aynı öğrenci, öğretmen, ödev, oturum ve sonuç ilişkisini; `IsCompleted=true`, süre 75 ve `is_measured=false` değerlerini doğruladı. [Veritabanı kanıtı](../../artifacts/speed-reading-assignment-live-20261015/cycle-database-proof.log).

Öğretmen kendi endpoint'inden oluşturduğu için bu ödevin `institution_id` alanı boş. Kurumun **Ödevler raporu**, aktif kurum öğretmeni ve kapsamındaki öğrenci üzerinden sonucu getiriyor. Kurumun doğrudan oluşturduğu ödevleri yöneten listeyle aynı kapsam olduğu iddia edilmez.

Öğrenci ödev listeleme, oturum başlatma, doğrulama ve tamamlama istekleri **HTTP 200** döndü. [Tarayıcı HTTP kanıtı](../../artifacts/speed-reading-assignment-live-20261015/student-http-proof.json). Bu kayıt yalnız URL ve durum içerir; kimlik doğrulama başlıkları/token saklanmadı.

## İlk koşunun bulguları — sonraki yayında kapatıldı

1. **Teslim tarihi bir gün ileri görünüyor.** Formda 16 Ekim seçildi; kayıt `2026-10-16T23:59:59Z` oldu ve Türkiye saatinde öğretmen/öğrenci ekranında 17 Ekim gösterildi. Staff `submitAssignment()` seçili güne UTC 23:59:59 ekliyor. Beklenen yerel gün sonu ile UTC dönüşümü uzlaştırılmalı. Bu koşuda düzeltilmedi.
2. **Ölçülmeyen egzersiz 0 başarı puanına katılıyor.** Göz takibi sonuç ekranı doğruluk ve WPM ölçmediğini doğru söylüyor; DB `is_measured=false`. Buna rağmen öğrenci ödevi `Score=0` alıyor, öğrencide “%0 Ort. Başarı”, öğretmende 0 ve kurum puan dağılımında 0–9 aralığında 1 sonuç görünüyor. Bu 0 öğrencinin başarısızlığı anlamına gelmez; ölçülmeyen ödevlerin puan ortalamasından ayrılması gerekir. Bu koşuda düzeltilmedi.
3. **Öğrenci ödev listesinin otomatik görünüm güncellemesi güvenilir değil.** Liste API'si 200 ve doğru veri döndürmesine rağmen başlangıçta ve ödevden dönüşte yükleniyor görünümü kaldı. Sekme etkileşimi sonrasında doğru bekleyen/tamamlanan listesi göründü. Ödev ve sonuç verisi kaybolmadı. Bileşenin asenkron görünüm güncellemesi için ayrı regresyon testi gerekir; neden bu koşuda kesinleştirilmedi.

## Kanıtlar ve kapsam

- [Öğrenci sonuç ekranı](../../artifacts/speed-reading-assignment-live-20261015/student-result.jpg)
- [Öğrenci tamamlanan ödev listesi](../../artifacts/speed-reading-assignment-live-20261015/student-assignments.jpg)
- [Öğretmen tamamlanan ödev ve ayrıntısı](../../artifacts/speed-reading-assignment-live-20261015/teacher-result.jpg)
- [Kurum tamamlanma raporu](../../artifacts/speed-reading-assignment-live-20261015/institution-result.jpg)
- [Kurum rapor metni](../../artifacts/speed-reading-assignment-live-20261015/institution-result.txt)

Bu, canlıda tek test öğrencisi ve tek göz takibi ödevi üzerinden yapılan doğrulamadır. Bütün egzersiz türlerinin puanlama akışını, yeni davet kabulünü, gerçek ödeme işlemini veya bütün CI paketlerini kapsamaz. Önceki bakım yayını ve otomatik test sonuçları [bakım raporunda](speed-reading-maintenance-20261015.md) ayrı tutulur.
