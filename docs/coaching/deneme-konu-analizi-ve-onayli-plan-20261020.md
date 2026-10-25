# Deneme konu analizi ve onaylı plan güncelleme

Tarih: 20 Ekim 2026. Sürüm canlıya alındı; öğrenci ve öğretmen hesaplarında uçtan uca doğrulandı. Canlı testte bulunan gereksiz analiz bırakma uyarısı düzeltildi ve iki portalın düzeltme sürümü de yayımlandı.

Sonraki canlı regresyonda TYT, üç AYT alanı ve İngilizce YDT için iki rolde 10 senaryo da tamamlandı. Sınav eki taşıyan katalog ders adlarının eşleştirilmesi düzeltildi; API `coaching-practice-adaptation-20261020-r3` sürümüne yükseltildi. Ayrıntılar: [YKS canlı regresyon raporu](yks-konu-onerisi-canli-regresyon-20261020.md).

## Kullanım

Öğrenci **Gelişim / Hedefe ilerleme ve plan önerileri** alanından kullanır. Öğretmen, kendisine atanmış öğrenciyi seçip çalışma planı bölümündeki aynı bileşeni kullanır. Kurumun salt okunur kullanıcıları analizleri görebilir; yazma işlemleri öğretmen yetkisi gerektirir.

1. Kapsamı belirlenmiş bir deneme sonucunu kaydet.
2. **Denemelerin konu dağılımını incele ve düzenle** alanında ilgili denemeyi aç.
3. Ders/test ve etkin alt konu seç; doğru, yanlış, boş sayılarını ve isteğe bağlı hata nedenini gir.
4. Konu analizini kaydet. Öneriler yeniden hesaplanır.
5. Son üç karşılaştırılabilir denemeden çıkan tekrar önerisini incele. Her konu için 0, 30 veya 60 dakika seçilebilir. 0, öneriyi bu uygulamaya almamak anlamına gelir.
6. Tekrarların yerleştirileceği son tarihi belirle ve **Takvim etkisini önizle** düğmesine bas.
7. Gün/hafta/ay takvimindeki **Öneri** kartlarını, mevcut çalışmaları ve derslerin son çalışma tarihlerine etkisini incele.
8. Onay kutusunu işaretleyip **Onayla ve planı güncelle** düğmesine bas. Önizleme tek başına veri yazmaz.

Analiz ekranı başlangıç paketini büyütmemek için görünür olduğunda ayrı bir parça olarak yüklenir. Kaydedilmemiş konu analizi veya devam eden yazma işlemi varken rapor yenileme engellenir. Öğretmenin plan ekranı güncellenirken kaydedilmemiş taslak ve çalışma tamamlama girişleri atılmaz.

## Konu analizi kuralları

- LGS, TYT, AYT Sayısal, AYT Eşit Ağırlık, AYT Sözel ve İngilizce YDT desteklenir.
- Konular sunucudan, mevcut etkin ders/ünite/alt konu kataloğundan gelir. Sınav kapsamı ve resmî ders adlarının kesin karşılıkları denetlenir; benzer isimlere göre tahminî eşleştirme yapılmaz. Matematik testinde geometri, ilgili AYT testlerinde tarih/coğrafya alt testleri desteklenir. Tanınmayan özel ders adları yanlış bir teste bağlanmaz.
- Aynı test ve konu bir denemede bir kez girilir. Her satır en az bir soru içerir.
- Konu doğru, yanlış ve boş toplamları dersin ilgili toplamını ayrı ayrı aşamaz.
- Dağıtılmayan sorular **konusu belirtilmemiş** olarak kalır. Sistem bunlara konu veya kazanım uydurmaz.
- Doğru soruların da sınıflandırılması gerekir. Yalnız yanlışları sınıflandırmak konu oranlarını yanıltabilir; bu sınırlama ekranda açıklanır.
- Hata nedenleri: konu bilgisi, işlem/dikkat, yorumlama, süre yetmemesi veya belirtilmemiş.
- Konu/ders adları sunucuda doğrulanıp analizle birlikte saklanır. İstemciden gönderilen adlar güvenilir kabul edilmez.
- Öğrenci kendi beyanının analizini düzenler; öğretmen, atanmış öğrencinin analizlerini düzenleyebilir. Öğrenci öğretmenin deneme kaydını düzenleyemez.
- Cevap dağılımı/kapsam değişirse eski konu analizi kaldırılır. Yalnız puanın değişmesi, aynı cevap dağılımına bağlı konu analizini silmez.
- Eski analiz formu, değişmiş deneme veya analiz üzerine yazamaz; güncel kayıtla tekrar açılması gerekir.

## Son üç denemeden öneri üretme

Son 90 gün değerlendirilir. Gelecekteki denemeler hesaba katılmaz. Her sınav kapsamı ve kayıt kaynağı ayrı tutulur; TYT/AYT veya öğrenci beyanı/öğretmen kaydı karıştırılmaz.

Aynı denemeyi birden fazla kez saymamak için isteğe bağlı yayıncı/deneme kimliği kullanılabilir. Kimlik girilmemişse aynı gün, aynı kapsam ve aynı ders cevap dağılımındaki kayıtlar ihtiyatlı biçimde tek kanıt sayılır. Çift kayıtta öğretmen kaydı önceliklidir. İki farklı denemenin aynı gün tamamen aynı dağılıma sahip olması halinde bu yaklaşım sayıyı azaltabilir; farklı deneme kimlikleri girilerek ayrıştırılabilir.

- Bir veya iki farklı denemede yalnız gözlem gösterilir.
- Üç veya daha fazla denemede en güncel üçü kullanılır.
- Konu en az iki denemede görülmeli, toplam en az beş sınıflandırılmış soruya dayanmalıdır.
- Bu soruların yanlış/boş oranı en az %40 olmalı ve en güncel denemede de aynı eşik devam etmelidir. Son denemede düzelen veya artık ölçülmeyen konu için tekrar önerisi üretilmez.
- Baskın neden konu bilgisi ve yanlış/boş oranı en az %50 ise 60 dakika; diğer durumlarda 30 dakika başlangıç önerilir.
- Süreler kullanıcı tarafından 30/60 dakika olarak değiştirilebilir. Altmış dakika, farklı iki güne 30'ar dakika yerleştirilir.

**Üç deneme, beş soru ve yüzde eşikleri ürünün başlangıç kurallarıdır; bilimsel bir ustalık teşhisi veya öğrenme süresi ölçümü olarak sunulmaz.** Denemelerin yayınevlerine göre zorluğu eşitlenmez. Öğrenci ve öğretmen öneriyi inceleyip reddedebilir. Taban puanından net, yerleşme olasılığı veya konu bilgisi çıkarılmaz.

## Takvim ve onay

Aktif planın saat dilimi esas alınır. Aynı saat dilimindeki kayıtlı müsaitlik pencereleri kullanılır; bu kayıt yoksa haftalık taslak saatleri kullanılabilir. Birleşen saat pencereleri birleştirilir.

- Varsayılan tekrar ufku 14 gündür; kullanıcı en fazla 90 gün ileriyi seçebilir.
- Öneriler yarından başlar, 30 dakikalık boş saatlere yerleşir.
- Mevcut saatli çalışmaların tümü dolu kabul edilir. Saati belirtilmemiş çalışma olan güne otomatik ekleme yapılmaz.
- Tamamlanmış ve sabitlenmiş işler, mevcut plan görevleri ve kazanım bağlantıları değişmez. Yeni konu tekrarı, belirli bir kazanımın öğrenilmediği varsayımıyla etiketlenmez.
- Aktif planın LGS/YKS sınav ailesiyle çelişen deneme kapsamı plana eklenemez.
- Önizleme mevcut çalışmaları ve öneri kartlarını aynı takvimde gösterir. Ders bazında önceki/önerilen son çalışma tarihi ile eklenecek dakika ayrı gösterilir.
- Bir bölüm sığmazsa kalan dakikalar gösterilir ve **kısmi uygulama engellenir**. Kullanıcı konu/süre seçimini, çalışma saatlerini veya tarih ufkunu değiştirip yeniden önizler.
- Yıllık planın mevcut bitişi aşılacaksa açık tarih uzatma seçimi gerekir. Onaylanan uzatma aktif planın bitiş bilgisine kaydedilir. Bitiş tarihi bulunmayan manuel planda seçilen tarih tekrar ufkudur.
- Yeni oluşturulan öğretmen planının uyarlamasını öğrenci tek başına onaylayamaz; atanmış öğretmen onaylar. Eski planlar için geçmişte kaydedilmemiş öğretmen sahipliği tahmin edilmez.
- Öğretmenin uyguladığı güncelleme mevcut plan değişikliği bildirim olayıyla öğrenciye iletilir.

Bu sürüm yakın dönem boş saatlere **ek tekrar** yerleştirir. Öğrencinin müsaitlik sınırını kendiliğinden artırmaz; mevcut ders saatlerini veya yılın tamamındaki çalışmaları sessizce yeniden sıralamaz. Katalogdaki ilk öğrenme sürelerini değiştirmez.

## Kayıt güvenliği ve veritabanı

`20261020100033_PracticeTopicAdaptation` geçişi iki nullable alan ekler:

| Tablo | Alan | İşlev |
|---|---|---|
| `coaching.exam_results` | `topic_analysis` (`jsonb`) | Konu dağılımı, nedenler ve isteğe bağlı deneme kimliği |
| `coaching.study_plan_revisions` | `CreatedByTeacherId` (`uuid`) | Yeni öğretmen planları için onay yetkisinin belirlenmesi |

Hesap, mevcut hedef/plan, tamamlanan çalışma veya kazanım bağlantısı silinmez. Eski analizler boş başlar. Canlıda önceki aşamadan bekleyen `PracticeExamBreakdown`, `PracticeGoalsAndLegacyExamCleanup` ve yeni `PracticeTopicAdaptation` geçişleri uygulandı. Temizlik geçişinin kapsamındaki eski LGS/YKS sonuçlarının canlıdaki sayısı sıfırdı. Üç geçiş önce yedekten oluşturulmuş ayrı veritabanında prova edildi; canlı geçişte mevcut kayıtların özetleri birebir eşleşti.

Önizlemenin parmak izi planı, denemeleri, analizleri, ilgili takvim girdilerini, müsaitliği, tarih/saat dilimini ve katalog durumunu kapsar. Onay sırasında güncel veriler yeniden hesaplanır. Eski önizleme 409 ile reddedilir. Planın mevcut öğrenci kilidi kullanılır; deneme ve saat girdileri uygulama boyunca paylaşım kilitleriyle korunur. Tekrar eden onay isteği aynı çalışmaları tekrar oluşturmaz.

Aynı üç deneme grubuna ait aynı konu tekrarı mevcut aktif plana yeniden önerilmez. Uygulama kaydı mevcut idempotency altyapısında tutulur; öğrencinin kişisel veri silme akışı bu yeni onay kayıtlarını da temizler.

## API

Öğrenci kökü: `/api/coaching/study-planning`.

Öğretmen kökü: `/api/coaching/staff/students/{studentId}/study`.

- `GET .../practice-analysis`: denemeler, konu analizleri ve öneriler.
- `PUT .../practice-analysis/{examId}`: güncel parmak iziyle konu analizini kaydet.
- `POST .../practice-adjustment/preview`: seçilen konu/sürelerin takvim etkisi.
- `POST .../practice-adjustment/apply`: aynı önizleme isteği, parmak izi ve açık onayla uygula.
- `GET /api/coaching/study-planning/practice-topics?examCode=LGS&sectionCode=MAT`: doğrulanmış konu seçenekleri.

## Doğrulama

Geniş birleşik koşuda 202 backend testi geçti. Altı sınav kapsamında dinamik konu seçimi, öneri, önizleme ve onaylı ekleme gerçek izole PostgreSQL üzerinde doğrulandı. Önizlemenin yazmaması, tamamlanan/sabitlenen işlerin korunması, kazanım bağlantıları, eski analiz/önizleme çatışması, yetersiz kapasitede kısmi eklemenin engellenmesi, tarih uzatma, öğretmen onayı ve tekrar isteğinin tek işlem sayılması sınandı.

Son kod incelemesinden sonraki **14 hedefli backend testi**, öğrenci/ortak arayüzde **62**, öğretmen arayüzünde **34 test** geçti; başarısız/atlanan test yok. İki panelin üretim derlemesi başarılıdır ve EF modelinde bekleyen değişiklik yoktur. Canlı düzeltmeden sonra ilgili üç arayüz dosyasında **23 test** geçti; iki portal Linux ortamında yeniden üretim olarak derlendi. Önceki ve canlı doğrulama sonuçları `artifacts/practice-progress-20261020/adaptation-verification.json` ve `artifacts/coaching-practice-adaptation-live-20261020/live-verification.json` dosyalarındadır.

## Canlı uçtan uca sonuçlar

- Öğrenci hesabından üç LGS denemesi, öğretmen hesabından ayrı üç LGS denemesi kaydedildi. Her birinin konu analizi arayüzden girilip kaydedildi. Öğrenci ve öğretmen kayıtları ayrı kanıt grupları olarak görünüyor; öğrenci öğretmen sonucunda yalnız görüntüleme yetkisine sahip.
- Matematik / Çarpanlar ve Katlar ile Fen Bilimleri / Mevsimlerin Oluşumu için üçer denemede 3 doğru, 9 yanlış, 3 boş üzerinden 60'ar dakikalık başlangıç önerileri üretildi.
- Mevcut haftalık taslağın saatleri doluydu. 14 ve 90 günlük önizlemede kısmi ekleme engellendi; onay düğmesi gösterilmedi. Hiçbir eski çalışma taşınmadı.
- Başarılı akışı sınamak için test öğrencisinin daha önce boş olan müsaitliğine Pazartesi 17.00–18.00 eklendi. Bu ayar otomatik yapılmadı; çalışma saatleri ekranından açıkça kaydedildi. Mevcut planın saatlerini değiştirmedi.
- Öğrenci, Matematik tekrarını onayladı: 26 Ekim ve 2 Kasım 2026, 17.00–17.30. Öğretmen, Fen tekrarını onayladı: aynı günler 17.30–18.00. İki rol de gün/hafta/ay takvim önizlemesini kullandı. Önceki/önerilen ders bitişleri ve ek süreler görüntülendi.
- Aynı aktif yıllık planın sürümü **3 → 4 → 5**, çalışma sayısı **560 → 562 → 564** oldu; bitişi **1 Haziran 2027** olarak kaldı. Yeni tekrarlar öğrenci takvimi yeniden açıldığında da görünüyor; öğretmenin eklediği çalışmanın kaynak açıklaması okunabiliyor.
- Öğretmen güncellemesinin bildirimi öğrenci ekranında görüldü: iki tekrar, toplam 60 dakika eklendiği ve önceki çalışmaların korunduğu belirtiliyor.
- Her iki öneri grubu uygulama sonrasında **“Bu deneme grubuna ait tekrar planda mevcut”** durumuna geçti.

### Canlıda bulunan ve düzeltilen hata

Öğrencinin ilerleme sayfasındaki hedef ilerleme çubukları, `disabled` bağında sayfadan ayrılma kontrolünü çağırıyordu. Kirli konu analizi olduğunda her ekran güncellemesinde bırakma onayı açılıyordu. Görüntüleme için yan etkisiz `canUpdateGoals` / `canRefresh` kontrolleri kullanıldı; gerçek ayrılma kontrolü kullanıcı eylemlerinde kaldı. Regresyon testi, render sırasında onay açılmadığını ve kirli analiz sırasında hedef çubuklarının kilitlendiğini doğruluyor. Düzeltme sonrasında canlıda altı analiz kaydı sorunsuz tamamlandı.

### Veri koruma ve dağıtım kanıtı

- Canlıdan alınan yedekler ayrı doğrulama veritabanlarına geri yüklendi; karşılaştırmalarda canlıya geri yükleme yapılmadı. Geçici kopyalar test sonunda kaldırıldı.
- **8 kullanıcı**, mevcut rol/ürün erişimleri ve hesap alanları birebir aynı. Normal oturum yenilemelerinde değişen `RefreshTokens` bu hesap karşılaştırmasından ayrı tutuldu.
- **43 plan**, **18 akademik hedef**, eski **61.991 çalışma**, bunların **1 tamamlanmış kaydı** ve **61.979 kazanım bağlantısı** korundu. Plan karşılaştırmasında yalnız beklenen sürüm ve güncelleme zamanı dışarıda tutuldu; plan kimlikleri, durumları ve yıllık kaynak bilgileri aynı.
- Test verisi olarak altı deneme/analiz, bir müsaitlik kaydı ve dört tekrar çalışması eklendi. Eski kayıtlar silinmedi.
- API imajı: `coaching-practice-adaptation-20261020`. Öğrenci ve personel portalı imajları: `coaching-practice-adaptation-20261020-r2`. İlgisiz servisler yeniden başlatılmadı.
- İki tarayıcının hata konsolu boş. Dağıtım sonrası alınan API/portal günlüklerinde hata/istisna işareti yok. Bu, sınanan akışların sonucudur; bütün olası kullanım koşulları için hatasızlık garantisi değildir.
- Kanıt klasörü: `artifacts/coaching-practice-adaptation-live-20261020/`. Takvim önizlemesi `teacher-preview.png`, başarılı kayıt `teacher-applied.png`, öğrenci bildirimi `student-notification.png` dosyalarında saklandı. `public-verification/` sır veya veritabanı yedeği içermez.
