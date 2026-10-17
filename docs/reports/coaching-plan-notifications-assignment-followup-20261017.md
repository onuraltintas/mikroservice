# Plan değişikliği bildirimi ve toplu ödev takibi

Tarih: 17.10.2026. Uygulama kayıtları: `037c8ed4`, `baa5f54c`. Bu çalışma geliştirme ve test kapsamındadır; **canlıya dağıtım yapılmadı**. Canlıdaki yıllık plan ve hesaplar değiştirilmedi.

## Öğrenciye plan değişikliği bildirimi

Öğretmen öğrencinin yayımlanmış planında dersin gününü/saatini, çalışma tarihini veya çalışma başlığını değiştirdiğinde `StudyPlanUpdatedEvent` üretilir. Öğrenci bildirim merkezinde **Çalışma planın güncellendi** başlığıyla ilgili ders ve eski/yeni gün-saat ya da başlık gösterilir.

- Taslak düzenlemeleri bildirim göndermez.
- Öğrencinin kendi yaptığı değişiklik kendisine bildirim göndermez.
- Değişmeyen konuma bırakılan kart ve yalnız boşluk farkı içeren aynı başlık bildirim üretmez.
- Aynı dersin birlikte taşınan kazanım parçaları için tek bildirim üretilir.
- Yetkisiz, eski sürümlü veya başarısız işlemde bildirim kaydı oluşmaz.
- Tamamlanan çalışmaların değiştirilmesini engelleyen kurallar devam eder.
- Görev güncellemesi ve outbox olayı aynı veritabanı işleminde kaydedilir. Gerçek outbox ile zorlanmış hata testi, hem kaydın hem olayın geri alındığını; sonraki başarılı isteğin eski başarısız olayı taşımadığını doğrular.
- Yeni Notification tüketicisi ayrı `coaching-study-plan-updated` kuyruğunda mevcut retry ve EF inbox/outbox mekanizmasını kullanır. Kaynak mesaj kimliği korunur; mevcut alıcı bazlı bildirim tekrar önleme mekanizması kullanılır.
- Yeni olay mevcut öğrenci veri yaşam döngüsünün değerlendirme/temizleme sorgusuna dahil edildi. Bu görevde bir kullanıcı verisi silinmedi.
- Öğrenci bildirim ekranının yeni olay türünü ve ders ayrıntılarını gösterebildiği ayrıca test edildi. Açık planın takvimini yenileme davranışı mevcut haliyle devam eder.

## Öğretmen toplu takip ekranı

Yol: **Ödevler → Geciken ve teslim edilmeyenler**.

- **Gecikenler:** son teslim tarihi sunucunun UTC saatinden kesin olarak geçmiş, teslim edilmemiş kayıtlar.
- **Teslim edilmeyenlerin tümü:** süresi dolmuş veya henüz dolmamış tüm bekleyen kayıtlar.
- Ödev `Active`, öğrenci durumu `Assigned` veya `InProgress`, `SubmittedAt` boş olmalıdır. Teslim edilen, notlandırılan, iptal edilen veya tamamlanmış ödevler dahil edilmez.
- Son tarihe tam eşit an henüz gecikme sayılmaz; tarih geçtiğinde gecikmeye girer.
- Her öğrenci–ödev ilişkisi bir satırdır. Öğrenci, ödev, ders, Türkiye saatinde son teslim tarihi, durum ve gecikme süresi görünür.
- Beklenen teslim ve geciken teslim toplamları erişilebilir tüm aktif ödevler için gösterilir. Başlık araması yalnız listeyi filtreler; özetlerin kapsamı ekranda açıklanır.
- Türkçe büyük/küçük harfli başlık araması C karşılaştırma düzenindeki PostgreSQL üzerinde de doğrulandı. `%` ve `_` gibi karakterler metin olarak aranır.
- Son tarihe göre sıralama, 25 kayıtlık sayfalama, boş sonuç, hata ve tekrar deneme durumları var.
- **Teslimleri aç** ile mevcut ödev detayına gidilir. Detaydan geri dönüş toplu takip ekranına gelir; geri açılan takip varsayılan geciken filtresiyle yüklenir.
- Filtreleme ve toplamlar veritabanında, sayfalamadan önce hesaplanır. Yalnız görüntülenen sayfadaki kayıtları süzme yöntemi kullanılmaz.
- Hata oluşursa önceki sorgunun satırları ve özetleri temizlenir; geç gelen eski istek yanıtları yeni sonucu değiştiremez.

API: `GET /api/coaching/teachers/me/assignment-follow-up?mode=overdue&pageNumber=1&pageSize=25`.

Parametreler: `mode=overdue|pending`, `pageNumber=1..1000`, `pageSize=1..100`, isteğe bağlı `search` (en çok 150 karakter).

Öğretmen kimliği oturumdan alınır. Yalnız öğretmenin kendi aktif ödevleri, mevcut kurum kapsamı ve mevcut öğrenci okuma yetkisi kullanılır. Aktif Koçluk erişimi/profil/atama ve öğrencinin paylaşım tercihi korunur. İlgisiz öğretmenin kayıtları veya erişilemeyen öğrenci adları sayfalara ve toplamlara girmez.

## Veri koruma

Yeni tablo, kolon, veri taşıma veya migration yok. Takip API'si yalnız okur. Ödev durumlarını, teslimlerini, notlarını ve dosyalarını değiştirmez. Mevcut tamamlanan plan görevleri, kazanım bağlantıları ve gerçek çalışma süreleri için birebir koruma testleri geçti. Testler yeni, geçici PostgreSQL veritabanlarında çalıştırıldı; canlıya yazılmadı. Test için açılan yerel PostgreSQL örneği başlangıçtaki kapalı durumuna geri getirildi.

## Doğrulama

| Kontrol | Sonuç |
|---|---:|
| Koçluk PostgreSQL entegrasyon | 82 başarılı, 0 atlanan |
| Bildirim tüketicileri ve plan API | 11 başarılı |
| Öğretmen arayüzü | 174 başarılı |
| Öğrenci/yönetici Koçluk arayüzü | 288 başarılı |
| Masaüstü/mobil takvim, profil ve yeni takip akışları | 38 başarılı |
| Toplam farklı test | **593 başarılı** |

Üretim öğretmen derlemesi temiz önceki kaynağa yalnız bu görevin altı arayüz dosyası uygulanarak alındı. İlgisiz çalışma dizini değişiklikleri pakete dahil edilmedi. Yeni API'nin ölçülen satır kapsamı **%100 (36/36)**; yeni bildirim tüketicisinin satır kapsamı **%100**. Mevcut plan servisinin tüm dosya kapsamı yaklaşık %70'tir; yeni dosya kapsamı ile tüm eski servisin kapsamı birbirine karıştırılmamıştır.

Tarayıcı regresyonunda mevcut mobil kart testinin yeniden çizim sırasında eski DOM öğesine eriştiği görüldü. Test, diyalog kapandıktan ve kart hedef günde oluştuktan sonra boyutlandırmayı başlatacak şekilde düzeltildi. Takvimin uygulama kodu değiştirilmedi. Son çalıştırmada 38 senaryonun tamamı geçti.

Derleme mevcut başlangıç paketi ve kök personel stil bütçesi uyarılarıyla başarılı oldu; derleme hatası yok.

Test kanıtları:

- `artifacts/coaching-followup-api-verified.log`
- `artifacts/coaching-followup-notification-full.log`
- `artifacts/coaching-followup-staff-full.log`
- `artifacts/coaching-followup-student-full.log`
- `artifacts/coaching-followup-browser-final.log`
- `artifacts/coaching-followup-coverage-verified/target-summary.json`
- `artifacts/coaching-followup-check-20261017/source-provenance.json`
- Test verili ekran: `artifacts/coaching-followup-table-desktop.png`, `artifacts/coaching-followup-table-mobile.png`

## Dağıtımda gerekli olanlar

Bu rapor bir canlı dağıtım kaydı değildir. Daha sonraki dağıtımda **Notification önce** güncellenmeli ve yeni kuyruk bağının hazır olduğu doğrulanmalıdır. Ardından yeni sözleşme dosyasıyla Coaching, son olarak öğretmen arayüzü güncellenmelidir. Böylece üretici yeni olay türünü göndermeden tüketici hazır olur. Notification ve Coaching paketlerine güncel `EduPlatform.Shared.Contracts` assembly'si dahil edilmelidir. Yalnız öğretmen arayüzünü güncellemek yeni API ve bildirim akışını çalıştırmaz. Veritabanı geçişi gerekmiyor.
