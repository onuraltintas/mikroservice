# Koçluk: yıllık planı geri etkinleştirme ve öğretmen öğrenci profili

Tarih: 17 Ekim 2026. Üretim kaynak kaydı: `7609d374`. Dağıtım: `coaching-restore-profile-20261017`.

## Canlı sonuç

- `b5536157-dd00-4f63-b522-9f250c77b33b` test öğretmen planı arşivlendi.
- `2f7e2d07-e044-471d-aedc-dfe004df7e2e` önceki yıllık plan aynı kayıt üzerinden yeniden etkinleştirildi; sürümü 11 → 12 oldu.
- Yıllık planda 167 çalışma, 7.215 planlanan dakika ve 52 farklı kazanım var. Önceden tamamlanan 17 dakikalık çalışma korundu.
- Arşivlenen test planının 12 dakika / 0 soru tamamlanma kaydı korundu.
- Sistem toplamları işlem öncesi ve sonrası aynı: 7 plan, 339 çalışma, 6 tamamlanan çalışma, 115 gerçek dakika.
- Kazanım bağlantısı kaybı ve sahipsiz kazanım bağlantısı yok.
- Hesaplar, roller, ürün erişimleri ve diğer Koçluk iş verileri karşılaştırma ile korundu. Profil denemesinde okul adı geçici değiştirildi, öğrenci ekranında görüldü, sonra asıl değere geri alındı. Profilin yalnızca sürüm ve güncelleme denetim alanları değişti.

## Geri etkinleştirme nasıl çalışır?

Arşiv plan ekranında onay kutusu ve **Planı yeniden aktif et** düğmesi bulunur. İşlem mevcut aktif planı ve arşiv planı aynı veritabanı işlemi içinde değiştirir. İstemci hem geri açılacak planın sürümünü hem mevcut aktif planın kimliğini/sürümünü gönderir. Bu sırada başka değişiklik olmuşsa 409 döner. Öğrenci bazında kilit ve tek aktif plan kısıtı korunur.

Çalışmalar kopyalanmaz; yeniden dağıtılmaz. Görev kimliği, kazanım kimliği, süre sürümü, tamamlanma zamanı, gerçek dakika/soru ve yıllık üretim kaynağı aynen kalır. Geri açma eski haftalık taslakla yeniden üretim yapmadığından sonraki taslak değişiklikleri mevcut yıllık planı bozmaz.

Uç noktalar:

- Öğrenci: `POST /api/coaching/study-planning/plans/{id}/restore`.
- Öğretmen: `POST /api/coaching/staff/students/{studentId}/study/plans/{id}/restore`.
- İstek: `expectedVersion`, `expectedActiveRevisionId`, `expectedActiveRevisionVersion`.

Öğrenci yalnız kendi planını, öğretmen yalnız aktif olarak atanmış ve yetki kapsamındaki öğrencinin planını yönetebilir.

## Öğretmenin ayrıntılı öğrenci profili

Yol: **Öğrenciler ve raporlar → Raporu incele → Öğrenci profili → Profili düzenle**.

| Alan | Görüntüleme | Öğretmen düzenleme |
|---|---|---|
| Eğitim durumu, sınıf, mezuniyet yılı | Var | Eğitim kurulumu üzerinden yönetilir |
| Okul adı | Var | Var |
| Şehir, ilçe | Var | Var |
| Okul türü, şube | Var | Var |
| Doğum tarihi | Var | Var |
| Öğrenci notu / hakkımda | Var | Var |

Ad, e-posta, şifre ortak hesap alanlarıdır. Öğretmen profil düzenleme bu alanlara dokunmaz. Öğrencinin paylaşım tercihi ve avatarı korunur. Not alanı öğrencinin de görebildiği Koçluk profil alanıdır; öğretmene özel gizli not değildir.

- Profil **Coaching** içinde kalır; Identity hesabına taşınmaz.
- GET/PUT: `/api/coaching/teachers/me/students/{studentId}/profile`.
- Yalnız atanmış öğrenci ve aktif öğretmen/öğrenci yetkisi; kurum kapsamı mevcut erişim servisiyle denetlenir.
- PUT yalnız okul/kişisel profil alanlarını kabul eder, `expectedVersion` zorunludur.
- Eski sürümle yazma 409; yetkisiz veya ataması kapanmış öğretmen 403.
- Güncelleyen öğretmen denetim alanına kaydedilir.
- Başarısız kaydetmede form verileri korunur. Kaydedilmemiş değişiklikle ayrılmada uyarı vardır.
- Canlı deneme: okul adı öğretmen tarafından kaydedildi → öğrenci Koçluk profilinde aynı değer görüldü → asıl okul adı geri kaydedildi → veritabanı karşılaştırması geçti.

## Bildirim ve takip sorularının yanıtı

| Olay / ihtiyaç | Mevcut durum | Doğrulama |
|---|---|---|
| Öğretmen plan yayımladı | Öğrenciye **Çalışma planın hazır** bildirimi gönderilir | Canlı öğrenci bildirim merkezinde önceki öğretmen yayını görüldü |
| Arşiv yıllık planı tekrar etkinleştirme | Aynı yayımlama olayı ile öğrenci bildirimi gönderilir | Canlıda 19:52 tarihli yeni bildirim görüldü |
| Öğretmen plan başlığı/saatini değiştirdi | Ayrı değişiklik bildirimi **yok**; öğrenci planı yenilediğinde güncel kaydı görür | Servis mutasyonlarında olay yayını yok; önceki öğretmen/öğrenci uçtan uca testi güncel başlığı doğrular |
| Öğretmen ödev verdi | Atanan öğrencilere **Yeni ödev** bildirimi var | Canlı öğrencide mevcut `deneme` ödevi bildirimi görüldü |
| Öğretmen ödevi düzenledi | Atanan öğrencilere **Ödev güncellendi** bildirimi var | Olay tüketicisi/alıcı entegrasyon testi geçti |
| Öğrenci ödevi teslim etti | Ödevde öğretmen kimliği varsa öğretmene **Ödev teslim edildi** bildirimi var | Submit handler olayı ve tüketicinin öğretmen alıcısı entegrasyon testinde doğrulandı; bu görevde mevcut teslim edilmemiş ödev değiştirilmedi |
| Öğretmen ödevi değerlendirdi | Öğrenciye puan/geri bildirim bildirimi var | Tüketici/alıcı testi |
| Ödevi teslim etmeyen öğrenci | Ödev detayında adı ve **Henüz teslim edilmedi** durumu görünür | Canlıda 14 Ekim son tarihli ödev ve teslim etmeyen test öğrenci görüldü |
| Geciken ödevleri topluca filtreleme | Ayrı geciken filtresi / tüm ödevleri birleştiren gecikme listesi **yok** | Canlı liste yalnız Tüm / Aktif / Tamamlandı / İptal filtrelerine sahip |
| Çalışma planındaki geciken görevler | Öğretmenin **Çalışma takibi** ekranında toplu takip vardır | Önceki sürümün testleri ve bu sürümün regresyon testleri korundu |

Bildirim merkezi canlı bağlantı kullansa da açık çalışma planı kendiliğinden yeniden yüklenmez. Başka kişinin yaptığı değişikliği görmek için planı yenilemek / tekrar açmak gerekir. Çalışma planı görevi tamamlamak ile ödev teslim etmek iki ayrı iş akışıdır; tamamlanan plan görevi öğretmenin toplu takibine yansır, ödev teslimi ayrıca bildirim olayı üretir.

İkincil bulgular: ödev detayındaki toplam teslim sayacı canlıda `/` olarak boş görünüyor; öğrenci satırı ve liste sayacı `0 / 1` görünür. Mevcut ödev bildirim metnindeki son tarih saati ile tarayıcıdaki yerel saat arasında üç saat fark gözlendi; tüketici UTC tarihini saat dilimi dönüşümü yapmadan metne basıyor. Bu görevde Notification servisi değiştirilmedi.

## Sonraki geliştirme önerisi

1. Plan değişiklikleri için `StudyPlanUpdated` olayı ve öğrenciye hangi dersin hangi tarihe taşındığını açıklayan bildirim. Taslak düzenleme bildirimleri yerine öğrenciye yayımlanan değişiklikler bildirilmelidir; sürükleme sırasında her ara hareket için bildirim üretilmemelidir.
2. Ödevlerde son tarihe göre **Geciken / Teslim etmeyen** toplu görünümü; öğrenci, ödev ve gecikme süresi filtreleri.
3. Ödev bildirim saatlerini `Europe/Istanbul` olarak tutarlı gösterme, detay teslim sayacını düzeltme ve bildirimden ilgili plan/ödeve doğrudan gitme.

## Testler ve dağıtım

- PostgreSQL üzerinde Koçluk entegrasyon: **79 başarılı**.
- Bildirim tüketicileri ve plan API entegrasyon: **9 başarılı**.
- Öğrenci/yönetici arayüzü: **287 başarılı**.
- Öğretmen arayüzü: **172 başarılı**.
- Temiz üretim derlemesinde mevcut masaüstü/mobil takvim regresyonu: **32 başarılı**.
- Yeni plan geri etkinleştirme ve öğretmen profil düzenleme tarayıcı senaryoları: **4 başarılı**.
- Toplam: **583 test başarılı**. Canlı doğrulamalar bu sayıya dahil değildir.
- Yeni şema geçişi yok. Canlı tabanlardan yalnız 4 Koçluk assembly dosyası ve hedef arayüz paketleri güncellendi; ilgisiz çalışma dizini değişiklikleri dağıtılmadı.
- Identity/Coaching yedekleri sunucunun özel sürüm dizininde alındı. Yedekten izole veritabanı provası ve geçişin tekrar çalıştırılması iş verilerini değiştirmedi.
- Üç uygulama imajı için önceki imajlara geri dönüş hazır. Otomatik veritabanı geri yükleme yapılmaz.
- Koçluk, öğrenci/yönetici ve öğretmen servisleri çalışıyor; yeniden başlama sayıları 0. Öğretmen `/staff/` JS/CSS dosyaları 200 ve doğru MIME türleriyle açılıyor.
- 15 dakikayı aşan üretim sağlık izlemi **31/31 başarılı** olarak 17:06:54 UTC (20:06:54 Türkiye) tarihinde tamamlandı. Üç hedef serviste yeniden başlama ve hata işareti görülmedi; kontrol edilen tüm sayfalar HTTP 200 döndü. Kanıt: `live/monitor-verified.txt`.

## Kanıtlar

- Kaynak/kapsam: `artifacts/coaching-restore-profile-live-20261017/source-provenance.json`.
- Veri koruma: `artifacts/coaching-restore-profile-live-20261017/preservation-profile.log`.
- Öğretmen kaydı: `artifacts/coaching-restore-profile-live-20261017/proof/teacher-profile-saved.jpg`.
- Öğrencide yansıma: `artifacts/coaching-restore-profile-live-20261017/proof/student-profile-updated.jpg`.
- Yıllık plan + korunan tamamlanma: `artifacts/coaching-restore-profile-live-20261017/proof/annual-restored-completion.jpg`.
- Canlı yayın / prova / izleme kanıtları: aynı artifact klasörünün `live/` altı. Özel yedekler ve bağlantı ayarları bu klasöre indirilmez.
## İşletim notu

Sürüm dizini: `/var/lib/eduivme/releases/coaching-restore-profile-20261017`.

Canlı son durumun doğrulanması: `bash deploy.sh validate`, ardından bu görev için `verify.sh verify` işlemi. Veri koruma temel dosyaları özel `live-verification/` altındadır; yeni bir baseline mevcut kontrolün üzerine yazılmaz.

Gerekirse önceki uygulama imajlarına dönüş, aynı dizindeki `rollback.compose.json` ile yalnız `coaching-service admin-panel staff-portal` servislerine `up -d --no-deps --pull never` uygulanarak yapılır. Compose proje adı `eduivme-production`, proje dizini `/opt/eduivme`, mevcut env dosyası `/opt/eduivme/.env` olarak korunur. Şema bu sürümde değişmedi. Uygulamayı geri almak plan/çalışma kayıtlarını veya profil değerlerini otomatik olarak geri almaz; yıllık planın aktif, test planının arşivde kalması beklenir. Yedek veritabanını canlıya otomatik geri yüklemek yeni çalışma kayıtlarını kaybettirebileceğinden uygulanmaz.

Geri dönüş tetikleyicileri: kritik profil/plan isteğinin sürekli 5xx vermesi, öğrencinin mevcut planlarına erişememesi, hedef servislerin yeniden başlama döngüsüne girmesi veya veri koruma karşılaştırmasının beklenmeyen satır değişikliği göstermesi.
