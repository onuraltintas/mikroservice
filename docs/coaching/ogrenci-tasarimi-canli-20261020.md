# Öğrenci paneli tasarımı ve canlı doğrulama

20 Ekim 2026. Öğrenci arayüzü canlıya alındı. Son öğrenci paketi: `coaching-student-design-20261020-r7`; ortak bileşen uyarlamalarını içeren personel paketi: `coaching-student-design-20261020`.

## Tasarım

MEBİ'nin açık ve koyu görünümleri tarayıcıda incelendi. Ekranları kopyalamadan, mevcut Eduİvme öğrenci çalışma alanına uygun sakin yeşil bir tasarım sistemi uygulandı. MEBİ'nin tema tercihi inceleme sonunda başlangıçtaki açık duruma döndürüldü.

- Ana sayfa, profil, kurulum, planlar, takvimler, sihirbaz, önizleme, gelişim, ödevler, seanslar, bildirimler ve hesap bölümleri ortak yüzey, kenarlık, metin, boşluk ve form kurallarını kullanır.
- Üst çubuktan açık/koyu tema değişir. Mevcut tema servisi ve kalıcı tercih kullanılır; sayfa yenilendiğinde seçim korunur.
- Başlık hiyerarşisi, metin satır aralıkları, okunabilir ikincil metinler, 16 px form alanları ve belirgin klavye odağı düzenlendi. Sıkışık takvim hücreleri dışında kontroller en az 44 px yüksekliğe getirildi.
- Mobilde çekmece menü ve altta hızlı erişim; geniş ekranda gruplu yan menü korunur. Çekmece odak tuzağı kullanır; Escape kapatır ve odağı menü düğmesine verir.
- Plan listesi kendi alanında kayar; telefonda bütün geçmiş planlar takvimin önünü kapatmaz. Tablo ve takvimlerin kendi yatay kaydırma alanları korunur. Sihirbaz tablet genişliğinde tek sütuna geçer.
- Önizlemede yardımcı metin opaklığı kaldırıldı ve tablo metinleri büyütüldü. Ders kartlarının açık/koyu renkleri ayrı tanımlandı. Tamamlanan çalışma işaretleri ve kazanım ayrıntıları korunur.
- Ödev ayrıntısındaki eski mor başlık ortak yüzeye dönüştürüldü; teslim durumları Türkçeleştirildi. Ödev filtresinin mevcut sayfa içindeki kapsamı açıkça belirtilir; seçimi `aria-pressed` ile duyurulur.

Temel dosyalar: `coaching-student-tokens.scss`, `coaching-student-surfaces.scss`, öğrenci layout bileşenleri ve `coaching-feedback.ts`.

## Bildirimler ve form koruması

Başarı/hata mesajları mevcut merkezi `ToasterService` üzerinden gösterilir. Kalıcı alan hata metinleri de formun yanında kalır. Öğrenci sayfalarındaki ve ortak profilindeki `window.confirm` çağrıları merkezi uygulama onay penceresine geçirildi. Onay penceresinin etiketi, ilk odağı ve odağı geri verme davranışı düzenlendi.

Uygulama içindeki sayfa/adım değişimlerinde kaydedilmemiş değişiklik koruması sürer. Tarayıcıya ait uyarı üretmemek için `beforeunload` dinleyicileri kaldırıldı. **Tarayıcı sekmesini kapatmak veya sayfayı yenilemek, kaydedilmemiş alanlar için uygulama içi onay açmaz; kaydedilmemiş veriye cihazlar arası ya da yenileme sonrası kalıcılık eklenmedi.** Sunucuda kaydedilmiş taslakların devam etme akışı korunur.

Ortak bileşenlerin asenkron onay sözleşmesi için gerekli öğretmen/kurum çağıranları güncellendi. Sihirbaz personel ekranında ihtiyaç olduğunda yüklenir; ilk paket boyutu hata sınırı artırılmadan derlenir. Öğretmene özel ekranların tasarımını yeniden yapma bu paketin kapsamında değildir.

## Testler

`node tools/test-coaching-student-design.mjs`:

| Paket | Dosya | Başarılı test |
| --- | ---: | ---: |
| Öğrenci ve ortak öğrenci bileşenleri | 46 | 310 |
| Personel ve ortak çağıranlar | 30 | 264 |
| Toplam çalıştırılan test | 76 | 574 |

Ortak senaryolar iki uygulamada da çalışabildiği için bu toplam benzersiz senaryo sayısı değildir. Son ödev etiketi değişikliği ayrıca iki dosyada 11 testle doğrulandı.

- Öğrenci ve personel üretim derlemeleri başarılı. Son öğrenci kaynakları ayrıca canlı sunucuda izole üretim derlemesinden geçti.
- `node tools/check-coaching-student-design.mjs`: açık/koyu ana metin, ikincil metin, durumlar, birincil düğme yazısı ve beş ders renk çiftinde en az 4.5:1 kontrast. Bu kontrol tam WCAG uygunluk sertifikası değildir.
- Öğrenci sayfaları ve ortak profil için native `alert/confirm/prompt` ve `beforeunload` taraması başarılı.
- Kapsamlı dosyalarda `git diff --check` başarılı.
- Var olan uyarı bütçeleri bazı bileşenlerde devam eder; derleme hata bütçeleri artırılmadı.

## Canlı kontroller

Gerçek test öğrencisinin oturumunda, kayıtları değiştirmeden:

- Tema geçişi ve yenileme sonrası tercihin korunması.
- Profilde kaydedilmemiş okul alanı ile uygulama içi onay: Vazgeç/İptal davranışı; iptal sonrası yazılan değerin korunması. Kaydetmeden ayrılınca sunucudaki eski okul bilgisi tekrar doğrulandı.
- Geçersiz rapor tarih aralığında merkezi hata toasterı ve alan yanındaki hata. Tarihler ardından eski değerlerine döndürüldü.
- Aktif LGS planı, gün/hafta/ay görünümleri, dolu saat odağı, tamamlanmış çalışmanın 55 dk kaydı ve kazanım bağlantıları.
- Koyu takvimde gerçek hesaplanmış renkler: Bugün düğmesi `rgb(25,42,39)` / `rgb(237,245,241)`; matematik kartı `rgb(71,58,41)` / `rgb(243,217,181)`. Önceki açık kart/koyu tema çakışması düzeltildi.
- Haftalık kayıtlı taslak, sağ tık uygulama menüsü, ders seçimi ve 30/60 dk formu. Deneme eklemesi kaydedilmeden iptal edildi.
- Sihirbazdaki mevcut ders/konular, kayıtlı haftanın yüklenmesi ve 1 Haziran 2027 bitişli önizleme. Yeni plan oluşturma/yayımlama yapılmadı.
- Hedefler, gelişim raporu, değerlendirilmiş ödevin 95/100 puanı ve mevcut geri bildirimi, seans/ücret/bildirim sayfalarının yüklenmesi.
- 320, 390, 768 ve 1440 px ekran genişlikleri; incelenen sayfalarda belge düzeyinde yatay taşma bulunmadı. Mobil menü Escape/odak dönüşü canlıda doğrulandı.
- Son sürümde önizlemenin yardımcı metni: `rgb(189,207,198)`, opaklık `1`, 13 px. Tablo metni de 13 px. Tablet sihirbazı 768 px genişlikte tek sütun; mobil plan listesi 352 px içinde kayıyor. Son tarayıcı konsol kontrolünde hata yok.

## Dağıtım ve veri koruma

Bu yayın **arayüz yayınıdır**. Backend imajları, API sözleşmeleri ve veritabanı şeması değiştirilmedi; migration uygulanmadı. Önceki işte geliştirilen karar–çalışma–ödev bağlantıları ve onun migration'ı bu yayınla canlıya taşınmadı.

Dağıtım, doğrulanmış canlı kaynak kopyası üzerinde sunucuda hazırlandı. Güvenlik incelemesinin reddettiği özel kaynakları yerel bilgisayara indirme adımı kullanılmadı. Paket özeti ve kaynak kapsamı doğrulandı; yeni karar API'sine istemeden bağımlılık eklenmediği kontrol edildi.

İlk yayından önceki Coaching, Identity ve Notification satırlarının içerik özetleri, son yayından sonra tekrar karşılaştırıldı: **mevcut bütün karşılaştırılan satırlar aynen korunuyor**. Teknik Inbox/Outbox/RefreshTokens tabloları bu içerik karşılaştırmasının dışında. Bu tasarım testi yeni profil, plan, ödev teslimi veya çalışma kaydı oluşturmadı.

Paketleme sırasında eski imajların katman sınırı görüldü. Çalışan sürümün dosya sistemi yeni bir imaj aşamasına kopyalandı; Node/işletim sistemi dosyaları ile User, WorkingDir, Entrypoint, Cmd, Env, ExposedPorts ve StopSignal eşitliği sunucuda doğrulandı. Katman sayısı azaltıldı. Önceki imajlarla geri dönüş dosyası sunucuda tutuluyor.

İki portalın CSS/JS dosyaları 200 ve doğru MIME ile sunuluyor. Son öğrenci konteyneri çalışır durumda, yeniden başlatma sayısı 0. Başlangıç yoklamasında tek seferlik bağlantı reddi konteyner hazır olunca düzeldi.

## Kanıt dosyaları

- `artifacts/coaching-student-design-20261020/design-regression-final-3.log`
- `artifacts/coaching-student-design-20261020/assignment-design-final.log`
- `artifacts/coaching-student-design-20261020/build-5.log`
- `artifacts/coaching-student-design-20261020-r7/promotion.log`
- `artifacts/coaching-student-design-20261020-r7/final-health.log`
- `artifacts/coaching-student-design-20261020/live-home-light-final.png`
- `artifacts/coaching-student-design-20261020/live-home-dark-final.png`
- `artifacts/coaching-student-design-20261020/live-home-mobile-dark-final.png`
- `artifacts/coaching-student-design-20261020/live-wizard-tablet-final.png`
- `artifacts/coaching-student-design-20261020/live-preview-tablet-dark-final.png`

Son paket SHA-256: `9ed21f88b5bf7a99577cbc9a2da82c6cc8cf6c684644440c1c39e844d8bfc57c`.

