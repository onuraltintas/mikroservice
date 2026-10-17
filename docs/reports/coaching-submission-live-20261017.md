# Öğrenci ödev teslimi — canlı doğrulama ve düzeltmeler

17.10.2026 tarihinde test öğrencisi ve atanmış öğretmen hesabıyla canlı arayüzden doğrulandı. Düzeltme kaydı `2bc1cf94`; Coaching ve personel portalı **21:27:08 Türkiye** saatinde `coaching-submission-20261017` sürümüne alındı. Notification ve diğer servisler değiştirilmedi.

## Canlı akış

- Test öğrencisinin `c260e5c4-facd-42cd-af3a-69aa0b9cc67f` kimlikli **deneme / Matematik** ödevi not eklenerek **Ödevi teslim et** düğmesiyle teslim edildi. Başarı mesajı görüldü; tekrar teslim ve fotoğraf ekleme düğmeleri teslim sonrasında devre dışı kaldı.
- Veritabanında durum `Submitted`, `submitted_at` dolu ve öğrenci notu kayıtlı. Ödev teslim edilmiş bırakıldı; değerlendirme/puan eklenmedi ve teslim geri alınmadı.
- Öğretmen bildirim merkezinde **Ödev teslim edildi** göründü. Veritabanında test başlangıcından sonra **tek** teslim bildirimi, doğru öğretmen ve doğru ödev bağlantısıyla doğrulandı.
- Öğretmenin toplu takip ekranında **Yenile** sonrası kayıt çıktı; **Gecikenler** ve **Teslim edilmeyenlerin tümü** filtreleri boş sonuç verdi. Bekleyen/geciken sayaçları **1 → 0** oldu. Bu ekran mevcut yenileme/açılış sorgusuyla güncelleniyor; otomatik anlık liste yenilemesi eklendiği iddia edilmez.
- Öğretmen ödev listesinde **1 / 1 teslim**, ayrıntıda öğrenci adı, teslim zamanı ve **Teslim edildi** durumu görüldü.

## Bulunan ve düzeltilen eksikler

1. **Öğrenci notu öğretmen ayrıntısında görünmüyordu.** Not veritabanında tutuluyordu ancak ayrıntı DTO'sunda ve öğretmen görünümünde yoktu. Var olan öğrenci erişim filtresinden geçen satırlara `StudentNote` eklendi; öğretmen ekranına salt okunur **Öğrencinin notu** alanı kondu. Angular metin bağlama kullanılıyor. Yetkilendirme kapsamı genişletilmedi.
2. **Ayrıntı teslim sayacı boştu.** Listeye ait toplam alanları ayrıntı yanıtında bulunmadığından ` / ` görünüyordu. Ayrıntıda görülebilen öğrencilerin `Submitted`/`Graded` durumlarından teslim sayısı hesaplanıyor; toplam görünür öğrenci sayısı kullanılıyor. Canlıda **1 / 1** doğrulandı.

## Testler

- Arayüzde notun gösterilmemesi önce başarısız testle üretildi; düzeltmeden sonra **6/6** öğretmen teslim ayrıntısı testi geçti. Teslim/puan/ek indirme davranışları ve `Submitted`, `Graded`, `Assigned`, `InProgress` sayımı kapsandı.
- API'de kaydedilmiş notun DTO'dan eksik gelmesi başarısız testle üretildi; düzeltmeden sonra ödev filtreleri ve bildirim tüketicisiyle birlikte **7/7** test geçti.
- Temiz kaynaklardan personel üretim derlemesi ve Coaching publish başarılı. Önceden mevcut paket/boyut uyarıları bu çalışmanın kapsamı dışında kaldı.
- Dağıtım sonrası gerçek öğretmen arayüzünde öğrenci notu ve **1 / 1** sayacı görüldü. İki takip filtresi tekrar boş sonuç verdi.
- Coaching, personel portalı ve Notification çalışıyor; yeniden başlama 0, gözlenen uygulama hata işareti 0. Personel JavaScript/CSS dosyaları 200 ve doğru MIME türünde.

## Veri koruma ve dağıtım

Teslimden önce Coaching ve Notification yedekleri alındı. Hedef öğrenci–ödev kaydındaki durum/not/teslim zamanı ve ödevin sürüm/güncelleme denetimi dışında bütün Coaching iş verileri satır parmak iziyle aynı kaldı. Planlar, tamamlanan çalışmalar ve kazanım bağlantıları değiştirilmedi. Teslimden sonraki düzeltme dağıtımında **tüm** Coaching iş satırları ayrıca tam karşılaştırıldı ve değişmedi. İlgisiz konteynerler aynı kimliklerle korundu; Identity'ye veri yazılmadı. Yeni şema geçişi yok.

İlk paket denemesinde manifestin kendisini de içermesi ve sonraki derleme denemesinde imaj kimliğinin Docker `FROM` ifadesinde kullanılması, servisler değiştirilmeden hazırlığı durdurdu. Manifest ve yerel imaj etiketi düzeltildi; başarılı dağıtımda gerçek canlı imajların üzerine yalnız temiz derlenmiş hedef dosyaları alındı.

- Paket SHA256: `63602338133abe5583b2418b885d2a40a48e922f3fda91ae905ce5aa03380299`.
- Coaching imajı: `961f4a68be3e0bc2c9d4689fdc0786aecd171a9367232e22032c076da1249022`.
- Personel imajı: `f26a2ce9c6109db40a52658dc42fc3255ee20af5ad41e566259b29e9824a7543`.
- Yedek ve geri dönüş compose dosyaları sunucuda `/var/lib/eduivme/releases/coaching-submission-20261017/` altında; teslim öncesi yedekler önceki sürümün `submission-verification/` klasöründe. Teslim kaydını geri saran bir veritabanı geri yüklemesi yapılmadı.
- Ekran kanıtları: `artifacts/coaching-followup-live-20261017/proof/teacher-submission-fixed.jpg`, `teacher-submitted-removed.jpg`, `student-submitted.jpg`.
