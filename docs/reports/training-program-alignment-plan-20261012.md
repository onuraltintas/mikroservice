# Hızlı Okuma program–egzersiz uyum incelemesi ve V3 planı

Tarih: 12 Ekim 2026. Durum: salt okunur inceleme / uygulama öncesi öneri.

## Kapsam ve doğrulama

Canlı `speedreading_owned_db` üzerinde READ ONLY transaction ile dört yaş grubu, aktif normal programlar, aktif egzersiz seviyeleri, sayısal ayarlar, metin/soru ve sahne sayıları, toplulaştırılmış atama bilgileri karşılaştırıldı. Kullanıcı kimlikleri ve kişisel bilgiler alınmadı. Uygulama kodu da incelendi. Canlı veri veya uygulama değiştirilmedi; bu doküman dışında geliştirme yapılmadı.

- Çocuk: 9–12; genç: 13–16; genç yetişkin: 17–21; yetişkin: 22+.
- Her yaşta beş seviye; toplam 20 normal program.
- Her program 28 gün ve 182 görev: ilk iki hafta günde 6, son iki hafta 7.
- Toplam 560 gün ve 3.640 görev.
- Mevcut çizelgedeki tüm tip/yaş/seviye istekleri için aktif katalog kaydı var. Bu kontrol doğru amaçlı kaydın seçildiğini veya her oturumun çalıştığını tek başına kanıtlamaz.
- Canlıda bir aktif/frozen Genç Seviye 1 ataması var; null schedule'lı atama görülmedi.
- Seviye tespiti programları kapsam dışı; korunacak.
- Paket README'lerindeki eski “canlıya uygulanmadı” notları yayın durumuna kanıt sayılmadı; canlı kayıtlar esas alındı.

## Öncelikli teknik bulgu: ölçüm içeriği eğitime karışıyor

Normal çizelge oluşturucu tip/yaş/seviyeye göre adayları bulup GUID sırasıyla seçiyor. Assessment-only ayrımı yok.

Canlıdaki tek aktif Genç Seviye 1 atamasında 37 görev seviye tespiti kimliklerine bağlı:

- 20 Takistoskop;
- 16 Görsel Genişleme;
- 1 Anlama.

Bunlar gerçek assessment attempt sayılmıyor; assessment modu oturumun assessment attempt bağlamından gelir. Sorun, kısa ölçüm ayarlarının normal eğitimde kullanılmasıdır. Örneğin kısa Takistoskop kaydı 6 uyaran içeriyor. Normal Anlama'nın “öğrenme / değerlendirme” seçimi de programda açıkça belirtilmediği için kimlik sırasına kalıyor.

Kalıcı plan:

1. Egzersize açık `IsAssessmentOnly` kapsamı ekle; normal kayıtların varsayılanı false olsun.
2. Bilinen 12 ölçüm kaydını yaş/tip/assessment template referanslarıyla doğrulayarak işaretle. Başlık veya created_by çalışma zamanı filtresi olmasın.
3. Normal program seçimi ve normal öğrenci kataloğu ölçüm kayıtlarını dışlasın. Assessment formu ve normal oturum başlatma bağlamı aynı ayrımı doğrulasın; admin bunları etiketli görebilsin.
4. Anlama slotunda `comprehensionMode` açıkça learning/evaluation seçilsin. `readingPurpose=evaluation` assessment-only anlamına gelmez; onu filtre olarak kullanma.
5. Önce yeni atama seçimini test et. Mevcut kirlenmiş atamayı toptan yeniden üretme: ayrıca onaylanmış, beklenen eski JSON'u doğrulayan hedefli onarımda yalnız başlanmamış/tamamlanmamış slotları düzelt. Tamamlanan görevler, günlük loglar, sonuçlar ve açık oturumlar korunmalı. Doğrudan başlatma guard'ı mevcut atamayı bloke edecek sırada yayımlanmamalı.

## Yaş ve seviye bazında mevcut kapsama

Hücrelerde “programdaki tür / o seviyede katalogda kullanılabilir tür” sayısı var. Bir türün katalogda olması her programa zorunlu eklenmesi anlamına gelmez; aşağıdaki dağılım öneridir.

| Yaş | S1 | S2 | S3 | S4 | S5 |
|---|---:|---:|---:|---:|---:|
| Çocuk | 13/15 | 14/16 | 14/17 | 15/20 | 15/20 |
| Genç | 14/16 | 14/16 | 18/19 | 18/20 | 19/21 |
| Genç yetişkin | 14/16 | 15/17 | 18/19 | 18/20 | 19/21 |
| Yetişkin | 14/16 | 15/17 | 18/19 | 18/20 | 19/21 |

### Eksikler

- Görselleştirme: dört yaşta S1–5 mevcut; hiçbir programda yok. Canlıda 120 sahne ve 360 soru bulunuyor; her hücrede 6 sahne/18 soru.
- Metin Solma: dört yaşta S1–5 mevcut. Çocuk programlarının hiçbirinde yok; diğer yaşlarda yalnız S3–5 programlarında birer görev var. Yeni alt seviyeler çizelgeye taşınmamış.
- Serbest Okuma: artık dört yaşta S1–5 mevcut. Çocuk S4 programındaki 12 görev hâlâ S3 istiyor. Çocuk S5 doğru S5 kullanıyor. Diğer üç yaşın S4–5 programlarında Serbest Okuma hiç yok; eski 12 slot Anlama'ya çevrilmiş, Anlama sayısı 26'dan 38'e çıkmış.
- Çocukta Göz Gezdirme S3–5, RSVP S4–5 ve Hata Analizi S4–5 mevcut fakat hiçbir çocuk programında yok.
- Genç / genç yetişkin / yetişkinde RSVP, Metin Solma ve Göz Gezdirme uygun üst seviye programlarında yalnız birer defa var. Kapsam var, tekrar sıklığı düşük.
- Regresyon programlara yansımış: çocuk S2–5, diğer yaşlar S1–5 programlarında ikişer görev.
- İç Ses Azaltma programlara yansımış: çocuk S4–5, genç S3–5, genç yetişkin/yetişkin S2–5 programlarında ikişer görev.
- Gruplama S5 düzeltmesi yansımış; dört yaşın S5 programları S5 kaydını istiyor.
- Schulte, Takistoskop, Görsel Genişleme, Sabitleme, Sakkad ve Göz Takibi her programda mevcut. Esas eksik yokluk değil, doğru normal kaydın ve güncel sürenin seçilmesi.
- “Hedefi Yakala” yalnız önizleme/prototip akışı; üretim katalog/atama/sonuç sözleşmesi yok. Bu V3 çizelgesine çalışır günlük görevmiş gibi eklenmeyecek. Focus/N-back mevcut görev olarak kalacak.

## İçerik ve süre bulguları

Hızlı Okuma için ortak veya egzersize özel, yaş/seviye/kelime aralığına uygun metin sayısı her hücrede en az iki. Egzersize özel kayıt sayısının sıfır olması tek başına içerik yokluğu değildir; ortak havuz kullanılabiliyor.

Normal Anlama'nın 40 öğrenme/değerlendirme kaydında ikişer bağlı metin; Serbest Okuma'da hücre başına beş; Gruplama, Tarama, Göz Gezdirme, Regresyon ve İç Ses Azaltma'da üç; RSVP'de iki bağlı metin var. Bunlar kayıt sayılarıdır; bağımsız hikâye çeşitliliği veya pedagojik kalite garantisi değildir.

Metin Solma ortak havuzu kullanır; her yaş/seviye için en az 11 metinde dört soru kaydı bulunuyor. Oturum kabul testinde soru tipi, cevap anahtarı ve seçenek geçerliliği ayrıca sınanmalı. Çocuk S1 metinleri 321–452 kelime: 60 kelime/dk ayarında yalnız gösterim yaklaşık 5,35–7,53 dakika + başlangıç beklemesi; dört sorunun cevap süresi hariç. Bu yüzden “her yaşta alt seviyeye ekle” işlemi süre hesabı yapılmadan tamamlanmış sayılmaz.

Günlük API Focus dışında her egzersize sabit beş dakika tahmini veriyor. Mevcut toplamlar çocuk/gençte 24–35, diğer yaşlarda 26–35 dakika gösteriyor; bunlar gerçek ölçüm değil. Yaş ayarında önerilen günlük süreler hâlâ 15/25/30/30 dakika. Yeni tur/metin/soru sayılarıyla bu alanlar tutarlı hale getirilmeli.

Plan:
- Zamanlı egzersizlerde gerçek süreyi, tempolu okumada metin uzunluğu/tempo aralığını, serbest okumada önerilen süreyi kullan.
- Cevap bekleyen turlarda gösterim süresiyle kullanıcı cevap süresini ayır; cevap süresi için gerekçesiz kesin dakika üretme.
- Soru/cevap, N-back alıştırması ve dinlenmeyi ayrıca belirt.
- Özellikle çocuk Metin Solma alt seviyeleri için mevcut tempo korunarak kısa, dört sorulu ve egzersize bağlı bir havuz hazırla; doğru uzunluk bandını ayarlara işle. Metni kesip mevcut soruları geçersiz hale getirme.
- Pilot günlük bütçeyi 560 günün gerçek ayarlarından hesapla; ilk/son hafta dağılımını koruyan yer değiştirmelerle aşırı uzun günleri azalt. Yalnız bu auditten kesin yaş normu/süre hedefi ilan etme.

## V3 çizelge önerisi

28 gün / 182 görev / günde 6–7 görev korunacak. Yeni türler ek görev yığarak değil, aynı bölümdeki tekrar eden okuma görevlerinin bir bölümünün yerine gelecek.

| Bölüm | Görsel takip / algılama / dikkat | Okuma / dil / anlama |
|---|---:|---:|
| 1. hafta | 4 | 2 |
| 2. hafta | 4 | 2 |
| 3. hafta | 3 | 4 |
| 4. hafta | 2 | 5 |

S5 sınav gününde mevcut farklı dağılım açık istisna; sınav + uzun anlama yığılmasın. Bu sınıflandırma tıbbi “göz kası geliştirme” veya ölçülmüş kazanım iddiası değildir.

### Değiştirilecek dağılım

- Her yaş / S1–5: Görselleştirme iki oturum (ikinci haftanın sonu ve dördüncü hafta; örneğin gün 14 ve 25, bir Anlama slotu yerine). Kalan günlük okuma/dil görevi ve görsel görev sayısı korunur.
- Metin Solma: S1–2 programlarında bir, S3–5 programlarında iki oturum; üçüncü/dördüncü hafta. Çocuk alt seviyelerindeki kısa içerik paketi geçmeden ekleme yapılmaz.
- Serbest Okuma: her yaş / seviyede haftada en az bir. Çocuk S4 eşleştirmesi S4'e çıkarılır. Diğer üç yaşın S4–5 programlarında önce dört tekrarlı Anlama slotu Serbest Okuma'ya dönüştürülür; diğer tekrarlar süre/çeşitlilik hesabında değerlendirilir.
- Çocuk S3–5: Göz Gezdirme iki oturum; S4–5: RSVP iki, Hata Analizi iki oturum. Çocukta Sınav Simülasyonu eklenmez; uygun normal kayıt yok.
- Diğer yaşlarda S3–5: RSVP/Göz Gezdirme sıklığı birden ikiye çıkarılır; Metin Solma da ikiye çıkarılır.
- Hata Analizi: dört yaşta yalnız S4–5, ikişer oturum.
- Sınav Simülasyonu: genç / genç yetişkin / yetişkin S5'te bir; mevcut sekiz sorulu ayar korunur.
- Regresyon ve İç Ses Azaltma: mevcut uygun seviye aralıkları ve ikişer oturum korunur. Mevcut gün 21/26 ve 23/28 çakışmaları bozulmaz.
- Gruplama, Hızlı Okuma, Anlama, Kelime Hazinesi ve temel görsel/dikkat türleri programda düzenli kalır. Bütün türleri aynı güne doldurma.
- Aynı egzersiz günde iki defa kullanılabilir; iki ayrı slot ve iki ayrı tamamlanma gerekir. Soru içeren uzun okuma tekrarları otomatik çoğaltılmasın.
- Program seviyesi açık sabit 1–5 eşleştirmesi olarak korunur. Canlıda WeeksPerDifficultyIncrease=0; haftalar ilerledikçe tüm program kendiliğinden S5 olmuyor. Egzersiz içi mevcut otomatik uyarlama (örn. Takistoskop) ayrı mekanizma; korunacak.
- Normal Anlama: çoğunluk öğrenme modu; hafta sonunda bir değerlendirme görevi. Mode açık seçilir, GUID sırasına bırakılmaz. Bunlar giriş/çıkış seviye tespiti yerine geçmez.

Tam 28 günlük sıralama, bu dağılımı üretirken çakışma ve süre denetimiyle oluşturulacak; örnek günler kesinleşmiş canlı çizelge değildir.

## Uygulama sırası ve kabul kapıları

1. **Seçim güvenliği:** assessment-only ayrımı ve normal learning/evaluation seçimi. Test: ölçüm GUID'i önce gelse de normal kayıt seçilmeli; doğrudan normal başlatma ölçüm içeriğine kaçmamalı; seviye tespiti aynı form/snapshot ile çalışmalı.
2. **Güncel tek kaynak:** canlı V2 + Gruplama/Regresyon/İç Ses yamalarını içeren 20 V3 planını üret. Eski v2 sil/değiştir scriptlerini yeniden çalıştırma. Test: aynı üretim deterministik; 20 program, 560 gün, 3.640 slot; sıra ve 6/7 görev sayısı doğru.
3. **İçerik ve süre:** gereken kısa Metin Solma içerikleri, bağlı dört soru; türlere göre tahmin. Test: her yaş/seviyede doğru metin/soru seçimi; boş havuzda açık hata, başka yaş veya örnek metne sessiz düşüş yok; cevapsız çalışmada sahte WPM/başarı üretilmez.
4. **Yaş grubu çizelgeleri:** çocuk → genç → genç yetişkin → yetişkin; her grup sonrası tam yaş/seviye ve 28 gün testi. Güncel ayarlara erişim, görsel-okuma dengesi, uzun görev yığılması ve bağımsız tekrarlar doğrulanır.
5. **Geçiş güvenliği:** program kimliklerini koruyan, sadece normal template'leri güncelleyen V3 transaction. Beklenen önce/sonra kontrolü, idempotency ve rollback. Mevcut atamanın düzeltmesi ayrı hedefli/onaylı işlem. Test: mevcut log/sonuç/açık oturum/assessment geçmişi korunur; eski ve yeni atama ayrımı doğru.
6. **Uçtan uca:** gerçek PostgreSQL'de 20 program için çizelge/oturum/sonuç; masaüstü/mobil öğrenci, admin/öğretmen önizlemesi; günlük sıranın tamamlanması ve bitiş ölçümü. Her tekrar ayrı tamamlanmalı; tek session iki görevi bitirmemeli.
7. **Kontrollü yayın:** ayrıca yayın onayı; yalnız Hızlı Okuma DB yedeği ve geri yükleme provası; uyumlu backend/frontend/pack sırası. Sonrasında yeni atamada doğru normal katalog seçimi, eski atama davranışı, içerik ve sağlık doğrulanır. Identity/Koçluk/Notification DB'leri kapsam dışı.

## İlerleme korunumu ve teknik kaynaklar

ScheduleJson yalnız hafta/gün/order/egzersiz kimliğini sabitler; configuration veya metin kimliğini değil. Yeni katalog ayarları mevcut kimliğe bağlı yeni oturuma yansıyabilir; template değişikliği mevcut frozen gün sırasına otomatik yansımaz. Bir atamayı silip yeniden üretmek önceki görevleri ve tekrar bağlantılarını bozabilir.

- `OwnedSpeedReadingProgramSchedule.cs:9, 72, 77`: sabit ID çizelgesi ve aday seçimi.
- `StudentProgramProgress.cs:162`: ScheduleJson write-once.
- `OwnedSpeedReadingDailyProgress.cs:708, 753, 784`: legacy çizelge oluşturma, güncel katalog çözümleme, süre tahmini.
- `SpeedReadingDailyProgressContracts.cs:232`: Focus dışındaki sabit süre tahmini.
- `OwnedSpeedReadingExerciseSessions.cs:353–424, 1388`: metin/yaş/seviye/soru filtreleri ve Anlama mode.
- `Exercise.cs:16`: mevcut katalogda assessment-only kapsamı eksik.
- `content-packs/assessment/v1/apply.sql`: 12 kısa ölçüm kaydı ve dört ölçüm template'i.
- `content-packs/*-programs/v2/apply.sql`: eski fiziksel değiştirme scriptleri; V3 güncellemesinde yeniden kullanılmamalı.

Bu turda geliştirme testleri veya tarayıcı kabul testleri çalıştırılmadı; yapılan doğrulama canlı salt okunur SQL envanteri ve kaynak kod incelemesidir. Yeni yayın veya yüzde yüz tamamlık iddiası yoktur.

