# Haftalık taslaktan kazanım planı — 17 Ekim 2026

Geliştirme ve yerel doğrulama tamamlandı. **Bu özellik henüz canlıya alınmadı.**
Mevcut canlı haftalık takvim yayını bu görevde değiştirilmedi.

## Öğrenci akışı

1. **Haftalık programım** ekranında ders kartlarını hazırla ve **Taslağı kaydet** seç.
2. Kayıtlı, boş olmayan haftada **Çalışma planı oluştur** düğmesi açılır. Kaydedilmemiş
   kartlar varken bu düğme kapalıdır.
3. `/coaching-portal/weekly-plan-preview` ekranında başlangıç ve hedef bitiş tarihini seç.
   İlk açılışta başlangıç öğrencinin kayıtlı saat dilimindeki bugündür. Bitiş, bu
   başlangıçtan itibaren yaklaşan **1 Haziran** olur; 1 Haziran başlangıç da aynı güne
   dahildir. 2 Haziran ve sonrasında sonraki yılın 1 Haziran'ı önerilir.
4. Öğrenci bitişi değiştirebilir. Bitiş hâlâ varsayılan ise başlangıcı değiştirmek yeni
   1 Haziran'ı hesaplar; özel seçilmiş bitiş öğrenci yeniden değiştirene kadar korunur.
   Tek plan aralığı en fazla 730 gün olabilir. Katalogda tanımlı sınav tarihleri bilgi
   olarak gösterilir; öğrencinin seçimini otomatik değiştirmez.
5. **Önizleme oluştur** ile tarih/saatli kazanım dağılımı ve ders bitiş tablosu hazırlanır.
   Önizleme veritabanına yazmaz. Sayfayı kapatmak veya yenilemek önizlemeyi kaydetmez.
6. Kapasite yetmiyorsa öğrenci üç yoldan birini seçer: önerilen bitişe uzatıp yeniden
   önizleme; erken biten derslerin boşalan saatlerini kullanarak yeniden önizleme;
   haftalık takvime dönüp ders saati ekleme ve kaydetme. Tarih ve haftalık kartlar
   öğrencinin seçimi olmadan değişmez. Farklı bir bitiş elle de girilebilir.
7. Tarihler, ders bitişleri ve süreler kontrol edilip onay kutusu işaretlenince
   **Çalışma planı taslağını oluştur** kullanılabilir. Eksik kazanım/süre veya kalan
   kapasite açığı varken kısmi plan oluşturulmaz.
8. Oluşan kayıt mevcut plan listesinde **ayrı taslak** olarak açılır. Eski aktif plan
   korunur. Öğrenci bu ekranda ayrıca onay verip yayımlarsa önceki aktif plan arşivlenir;
   tamamlanmış çalışmalar ve gerçek süre/soru kayıtları geçmişte kalır.

## Önizlemede görünen bilgiler

- Ders başına haftalık dakika, kazanım sayısı, gerekli ve yerleşen süre, tahmini bitiş,
  hedef tarihte kalan dakika. Mobil görünümde ders adı ve bitiş tarihi önce gelir.
- Her çalışma parçasının tarihi, başlangıç/bitiş saati, dersi, konusu, kazanım kodu ve
  metni, ayrılan dakika. Ders filtresi ve 25 satırlık sayfalama vardır.
- Planlanan toplam dakika, hedefe kadar boş kapasite, yerleşmeyen süre.
- Korunacak tamamlanmamış sabit çalışmaların tarih, saat ve süreleri.
- Her sığmayan ders için yaklaşık ek dakika/hafta önerisi ve mümkünse bütün dersleri
  bitiren ortak yeni hedef tarih. Ek saat önerisi 30 dakikaya yuvarlanmış yaklaşık
  kapasite hesabıdır; kesin sonuç yeniden önizlemede görülür.
- Onaysız eşleştirme, eksik kazanım, yayımlanmamış süre veya uygun olmayan eski sabit
  çalışma tarihi gibi kayıt oluşturmayı engelleyen gerçek veri eksikleri.

## Dağıtım kuralları

Haftalık taslaktaki dersler, öğrencinin eğitim bağlamına uygun aktif katalogla yeniden
kontrol edilir. Aktif ünite/konu/alt konuların yaprakları kullanılır. Akademik yıl,
sınıf, ders adı ve sınav kapsamına uygun onaylı eşleştirmeler ile yayımlanmış en yeni
kazanım süreleri okunur. Aynı kazanım birden fazla konuya bağlansa da bir kez planlanır.
Bu özellik eksik kazanım veya süre üretmez; yönetici kataloğunun tamamlanması gerekir.
Eski konu tahmini dakikası, eksik kazanım süresinin yerine sessizce kullanılmaz.

Sıra ünite, konu ve kazanım sırasıdır. Kullanılan dakika, katalogdaki öğrenme + pratik +
kontrol toplamıdır. Kazanım tek karta sığmazsa kalan dakika aynı dersin sonraki saatine
taşınır. Bir kartın içine birden fazla kazanım yerleşebilir; dakika kaybettiren 30/60
dakika yuvarlaması yapılmaz. Örneğin 60 dakikalık derste 20 + 40 dakika iki kazanım
yerleşir. Hedef bitiş günü hesaba dahildir.

Varsayılan olarak ders kendi kartlarında devam eder ve bittikten sonraki kartları boş
kalır. Öğrenci aktarımı seçerse, biten dersin boş kapasitesi kalan süresi en fazla olan
derse verilir; diğer ders devam ederken saati elinden alınmaz. Aktarım önizlemede
işaretlenir ve haftalık şablonu değiştirmez. Bu yaklaşım kesintisiz haftalık tekrar
varsayar; tatiller veya özel ara günleri için ayrıca takvim istisnası eklenmedi.

Önizleme yalnız seçilen bitişe kadar olan görevleri döndürür. Ders bitiş tahminleri için
başlangıçtan itibaren en fazla 730 gün ileri bakılır. Bu aralıkta bitiş bulunamazsa
tarih uydurulmaz; ders saatinin artırılması gerekir.

Mevcut aktif plandaki tamamlanmış veya sabit görevler, ilgili günün kapasitesinden
düşülür. Saati olan görev için aynı aralık, saati olmayan görev için günün ilk uygun
kapasitesi ayrılır. Tamamlanmamış sabit görevler yeni taslağa plan olarak taşınır.
Bu görevde aynı kazanımın ayrılmış dakikası varsa o pay yeni dağıtımda tekrar eklenmez.
Tamamlanmış görevler ve gerçek sonuçlar yeni planın gerçekleşmiş çalışması olarak
kopyalanmaz. Geçmişte kalmış sabit görev önce mevcut planda yeniden tarihlenmelidir.

## Servis ve veri modeli

Yeni Student rolüyle sınırlı uçlar:

| Uç | İşlem |
|---|---|
| `GET /api/coaching/study-planning/weekly-outcome-plans/options` | Kayıtlı haftanın sürümü, tarih önerileri, saat dilimi, sınav tarihleri |
| `POST .../preview` | Başlangıç/bitiş, beklenen haftalık sürüm ve boş saat aktarımı seçimiyle salt okunur hesap |
| `POST .../drafts` | Başlık, aynı önizleme isteği ve sunucunun döndürdüğü parmak iziyle ayrı taslak kaydı |

İstemci öğrenci kimliği veya hesaplanmış görev listesi göndermez. Öğrenci UserId'si
oturumdan alınır; sunucu dağılımı yeniden hesaplar. Haftalık kaynak, kazanım/süre,
eğitim bağlamı veya korunmuş aktif çalışmalar değişirse eski parmak iziyle kayıt 409
alır. POST istekleri mevcut planlama yazma hız sınırı ve 64 KiB gövde sınırını kullanır.
Kayıt sırasında öğrenci bazında planlama ve katalog yönetimi işlem kilitleri alınır.

Geçiş: `20261017061920_WeeklyOutcomePlans`.

| Tablo / alan | Saklanan bilgi |
|---|---|
| `study_plan_revisions.WeeklySource` | Nullable JSONB: haftalık/eğitim sürümü, başlangıç/bitiş, aktarım seçimi, parmak izi, kaynak aktif plan/sürümü, saat dilimi |
| `study_plan_tasks.PlannedStartMinute` | Nullable gün içi başlangıç dakikası |
| `study_plan_tasks.WeeklyLessonId` | Nullable gerçek planlanan ders kimliği |
| `study_plan_tasks.LessonNameSnapshot` | Nullable ders adı anlık görüntüsü |

Dört alan da nullable eklenir; eski satırlar güncellenmez veya silinmez. Saat/ders
alanları birlikte dolu veya birlikte boş olmalıdır; çalışma gece yarısını aşamaz.
Mevcut görev-kazanım bağlantıları süre sürümü ve metin anlık görüntüsünü korur. Yeni
alanlar plan yanıtında ve öğrenci veri dışa aktarımında bulunur. Ayrı yeni sahiplik
tablosu, Identity alanı veya hesap taşıması yoktur.

Üretilen taslak sıradan manuel editörde açılmaz; böylece saat ve kazanım bağlantıları
değiştirme işleminde düşmez. Yeniden dağıtmak için taslak arşivlenip tekrar önizlenir.
Görevler plan ekranında 50'şer gösterilir; tek üretimde korunanlar dahil en fazla
10.000 çalışma parçası kabul edilir. Yayımlarken haftalık/eğitim sürümü ve kaynak aktif
plan tekrar doğrulanır. Saatli görev başka güne taşınırken saat çakışması engellenir.

## Doğrulama

| Kontrol | Sonuç | Günlük |
|---|---|---|
| Müfredat + haftalık takvim + yeni dağıtım | 70 başarılı, 0 atlanan; gerçek yerel PostgreSQL dahil | `artifacts/weekly-outcome-backend-final.log` |
| Mevcut koçluk regresyonu | 694 başarılı, 0 atlanan | `artifacts/weekly-outcome-coaching-regression-final.log` |
| Öğrenci arayüzü | 196 başarılı | `artifacts/weekly-outcome-ui-coverage.log` |
| Masaüstü + mobil tarayıcı | 16 başarılı | `artifacts/weekly-outcome-e2e-final.log` |
| Üretim derlemesi | Başarılı; mevcut stil/template uyarıları günlükte | `artifacts/weekly-outcome-build-final.log` |
| EF model/geçiş | Bekleyen fark yok | `artifacts/weekly-outcome-model-final.log` |

Yeni motor/controller satır kapsamı %100, dağıtım servisi %99; yeni arayüz/HTTP servis
satır kapsamı %100, ifade kapsamı %96,09 ve dal kapsamı %90,32 ölçüldü. Eski iki geçiş
testinin veri hazırlığı, eski şemaya güncel EF sütunlarıyla yazmak yerine açık eski
sütunlarla yapılacak şekilde düzeltildi; koruma kontrolleri kaldırılmadı.

Tarayıcı senaryoları yerel üretim derlemesinde taklit API yanıtlarıyla çalışır; canlı
doğrulama değildir. Ekranlardaki LGS sınav tarihi ve örnek Türkçe kazanımı test verisidir.
Gerçek PostgreSQL testlerinde önizlemenin yazmaması, onayla ayrı taslak, sabit kazanım
payının iki kez sayılmaması, kaynak değişikliğinde yayın reddi, eski tamamlanmış
kayıtların aynen kalması, saat çakışması, JSONB/saat dilimi ve kısmi saat verisinin
veritabanınca reddi doğrulandı. Mevcut geçiş geri/ileri koruma testleri de geçti.

Ekran kanıtları: `artifacts/weekly-outcome-preview-desktop.png`,
`weekly-outcome-preview-mobile.png`, `weekly-outcome-decisions-desktop.png` ve
`weekly-outcome-decisions-mobile.png`.

17.10.2026 tarihinde canlı yayın ve doğrulama tamamlandı; konu hiyerarşisi ve dolu saat
odağı da düzeltildi. Ayrıntılar: [canlı doğrulama raporu](weekly-outcome-live-20261017.md).

Canlı yayın yöntemi: önce yedek ve üretim kopyası geçiş provası, sonra Coaching
API ile öğrenci arayüzü birlikte güncellenmelidir. Eski kayıt parmak izleri karşılaştırılırken
yalnız yeni dört nullable alan dışarıda bırakılarak önceki sütunların aynılığı ölçülmelidir.
Otomatik Down veya eski veriyi geri yükleme uygulanmamalıdır.
