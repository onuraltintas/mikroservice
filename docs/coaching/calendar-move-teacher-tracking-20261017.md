# Yıllık takvimde taşıma ve öğretmen çalışma takibi

Tarih: 17 Ekim 2026. Geliştirme ve yerel doğrulama tamamlandı; Coaching, öğrenci ve öğretmen
arayüzleri canlıya dağıtıldı. Canlı kontrol durumu ve kanıtlar:
[canlı dağıtım raporu](calendar-move-live-20261017.md).

## Öğrenci akışı

- Öğrenci aktif yıllık planını gün, hafta veya ay görünümünde açar.
- Gün/hafta görünümünde tamamlanmamış ders kartını başka günün boş saatine sürükler.
  Başlangıç 30 dakikalık adımlarla seçilir; dersin ve kazanım parçalarının süreleri korunur.
- Ay görünümünde başka güne sürükleme başlangıç saatini korur.
- **Gün ve saat değiştir** formu, sürüklemeye alternatif olarak ders, tarih ve saat seçtirir.
- Karttaki bitişik kazanım parçaları birlikte ve tek işlemde taşınır. Görev, konu, ders,
  kazanım, süre sürümü ve metin snapshot kimlikleri değiştirilmez.
- Çakışma, gün sonunun aşılması veya o günün toplamının 24 saati geçmesi engellenir.
- Tamamlanmış çalışma içeren kartın tamamı sürüklemeye kapalıdır. Tamamlanmamış parçanın
  tarihi mevcut çalışma ayrıntısından ayrıca değiştirilebilir. Tamamlanan kayıt oynatılmaz.
- Öğrenci kartı açıp gerçek süre ve isteğe bağlı soru sayısıyla **Tamamla** işlemini yapar.
  Takvimde tamamlanan parça sayısı, tümü tamamlandığında tamamlanmış kart görünümü gösterilir.
  Gerçek süre/soru sayısı ile tamamlanma zamanı özgün çalışma kaydında kalır.

Taşıma önizlemesi kayıt değildir. Sunucu başarılı yanıt vermeden kayıtlı kart konumu
değiştirilmez. Reddedilen bırakma, iptal veya bağlantı/sürüm hatası mevcut planı korur.

## Öğretmen akışı

Öğretmen panelindeki **Çalışma takibi** sekmesi yalnız atanmış, erişim yetkisi bulunan
öğrencilerin planlarını kullanır. Öğrenci arama, tarih aralığı ve öğrenci sayfalaması vardır.

**Öğrenci özeti** görünümünde aktif/taslak plan durumu, tamamlanan, bekleyen, geciken çalışma
sayıları ve gerçek dakika toplamı birlikte gösterilir. **Göster** seçimiyle geciken,
bekleyen veya tamamlanan çalışmaların kendisi de ortak listede incelenir: öğrenci, başlık,
tarih/saat, durum, planlanan ve gerçekleşen süre. Çalışma listesi 50, öğrenci listesi 25
kayıtla sayfalanır. Çalışma sayfalaması o öğrenci sayfasına aittir; kapsam ekranda yazılıdır.

- Dönem, çalışmanın **planlanan tarihine** göre filtrelenir.
- Bekleyen ve geciken kayıtlar aktif plandan gelir. Gecikenler bekleyenlerin alt kümesidir.
- Arşivde tamamlanmış çalışmalar dönem tamamlanma toplamına ve ortak tamamlananlar listesine
  dahildir. Arşivdeki yapılmamış işler yeniden bekleyen iş olarak sayılmaz.
- Saatli çalışma, planın saat dilimindeki bitiş saatine gelince gecikmiş sayılır. Saatsiz
  çalışma ertesi gün gecikir. Eski manuel planlarda Europe/Istanbul kullanılır.
- **İlgili planı aç**, kaydın ait olduğu özgün aktif/arşiv planını açar.
- Öğretmen **Listeyi yenile** veya **Planı yenile** ile öğrencinin en güncel tamamlama
  kaydını okur. Aynı çalışma tablosu kullanılır; öğretmene ikinci bir çalışma kopyalanmaz.

Öğretmen aktif plandaki tamamlanmamış çalışmaların gün/saatini ve başlığını düzeltebilir.
Kazanımlı bir çalışmanın süresi ve kazanım bağlantıları bu düzeltmede değiştirilmez.
Tamamlanmış kayıtlar ve arşiv planları düzenlemeye kapalıdır. Öğretmen öğrenci adına
tamamlama yapamaz.

Planı olmayan öğrencide **Plan oluştur → Yeni taslak** akışı kullanılabilir. Başlık,
çalışma tarihleri, isteğe bağlı başlangıç saatleri, süre, kaynak, planlanan soru ve sabitlik
alanları doldurulur. Taslakta çalışma eklenip çıkarılabilir. **Taslağı kaydet** ve ardından
ayrı onayla **Planı yayımla** işlemi yapılır. Öğrenci aynı plan kimliğini kendi ekranında
görür. Varsa önceki aktif plan, çalışma geçmişiyle arşivde kalır.

## Veri ve servis değişiklikleri

Mevcut UserId sahipliği, hesaplar, öğretmen–öğrenci ilişkileri ve tamamlama API'si korunur.
Identity servisinde üretim değişikliği yapılmadı.

Yeni uç noktalar:

- Öğrenci: `PUT /api/coaching/study-planning/plans/{id}/calendar-move`.
- Öğretmen: `PUT /api/coaching/staff/students/{studentId}/study/plans/{id}/calendar-move`.
- Öğretmen başlık düzeltmesi: aynı kökte `tasks/{taskId}/title`.
- Toplu takip: `GET /api/coaching/teachers/me/study-overview`.

Taşıma gövdesi `expectedVersion`, `taskIds`, `plannedDate`, `startMinute` içerir. Sunucu
sahipliği, ilişki yetkisini, plan sürümünü, arşiv/tamamlama durumunu, parça bütünlüğünü ve
çakışmaları doğrular. Aynı öğrencinin yazma işlemleri mevcut PostgreSQL transaction/advisory
lock düzeniyle seri çalışır. Tek parça bile başarısızsa grubun tamamı eski yerinde kalır.
Başka öğrencinin görev kimliği veya eski sürümle gönderilen istek kabul edilmez.

Rapor, görev ile planın saat dilimini aynı sorgudan alır. Öğrenci rapor sorguları arasında
yeni plan yayımlasa da yeni planın kayıtları kendi saat dilimiyle değerlendirilir; eski
metadata sözlüğünde bulunmadığı için rapor hataya düşmez. Bu durum gerçek PostgreSQL'de
sorgu arası yayın işlemi enjekte edilerek test edildi.

### Veritabanı geçişi

`20261017140000_ManualTimedStudyTasks`, yalnız `ck_study_task_weekly_time` CHECK kuralını
genişletir. Manuel çalışmalarda haftalık/katalog ders kimliği uydurmadan saat saklanabilmesini
sağlar. Yeni tablo/sütun, satır silme veya mevcut kaydı güncelleme işlemi yoktur.

Eski kuralın kabul ettiği kayıtlar yeni kurala da uygundur. Haftalık ders kimliği ile ders adı
eşleşmesi ve günün içinde kalan pozitif süre kontrolü korunur. Geçiş eski tamamlanmış veriler
üzerinde uygulanıp tekrar çalıştırıldı; satır içerikleri aynı kaldı. Geri dönüşte uygulama
imajına dönüş tercih edilmelidir: yeni manuel saatli kayıtlar varken eski CHECK kuralına
zorla dönülmemeli veya bu kayıtlar silinmemelidir.

## Doğrulama

- **285** öğrenci koçluk arayüz/servis testi: `artifacts/calendar-move-ui-final.log`.
- **170** öğretmen koçluk arayüz testi: `artifacts/calendar-move-staff-reviewed.log`.
- **77** API, servis, müfredat ve gerçek yerel PostgreSQL testi: `artifacts/calendar-move-api-ready.log`.
- **32** masaüstü/mobil tarayıcı senaryosu: `artifacts/calendar-move-e2e-ready.log`.
- Öğrenci ve öğretmen üretim derlemeleri başarılı:
  `artifacts/calendar-move-build-final.log`, `artifacts/calendar-move-staff-build-verified.log`.

Kapsam: takvim bileşeninde %98,44 ifade / %91,74 dal; takvim yardımcılarında %100 ifade/dal;
öğretmen toplu takip bileşeninde %100 ifade / %93,62 dal kapsamı doğrulandı. Taşıma servisinin
yeni işleminde %100 satır / %94,64 dal kapsamı, öğretmen düzeltmesinde %100 satır/dal elde edildi.
Mevcut uygulamanın tamamı için %100 kapsam iddiası yoktur.

Tarayıcı testleri yerel üretim derlemeleri ve taklit API ile çalışır; gerçek veritabanı,
sahiplik/yetki, sürüm, atomiklik, migration ve eşzamanlı rapor kontrolleri PostgreSQL
testlerinde ayrıca yapılır. Sonraki canlı testler, dağıtım raporunda ayrıca belirtilmiştir.

Ara testler, sürükleme mesajının takvimi iterek 30 dakika kaymaya yol açtığını yakaladı;
mesaj alanı sabitlenerek giderildi. Son testler gerçek fare hareketiyle saat ve ay görünümünde
gün taşımayı, çakışmayı reddetmeyi, formdan taşımayı, parça sürelerini korumayı ve tamamlama
sonrası kartın kilitlenmesini doğrular. Öğretmenin sıfırdan saatli plan oluşturup başlığını
düzeltmesi, öğrencinin tamamlaması ve öğretmenin ortak listede görmesi de kapsamdadır.

Yerel ekran kanıtları: `artifacts/calendar-move-desktop.png`, `artifacts/calendar-move-mobile.png`,
`artifacts/calendar-teacher-desktop.png`, `artifacts/calendar-teacher-mobile.png`.
Bu ekranlardaki kayıtlar test verisidir. Üretim derlemelerindeki mevcut stil bütçesi uyarıları
hata seviyesinde değildir; derlemeler başarılıdır.

## Canlıya alma kapsamı

Temiz kaynak paketleriyle **Coaching, admin-panel ve staff-portal** güncellendi. Veritabanları
yedeklendi, CHECK geçişi canlı kopyada prova edildi ve özgün iş satırları karşılaştırıldı.
Öğrenci taşıma ve tamamlanma işaretleri, öğretmenin toplu/geciken takibi, takvim düzeltmesi ve
öğrenciye yeni taslak oluşturması canlıda doğrulandı. Son 15 dakikalık sağlık izlemesi başarılı.
Sonraki açık kullanıcı talebiyle yeni test taslağı öğretmenden yayımlandı; öğrencinin 12 dakika /
0 soruyla tamamlaması ve öğretmenin toplu listesinde görünmesi de canlıda doğrulandı.
Önceki yıllık plan bütün çalışma geçmişiyle arşivde korunuyor. Ayrıntılı kapsam ve
kanıtlar [canlı dağıtım raporunda](calendar-move-live-20261017.md) tutulur.
