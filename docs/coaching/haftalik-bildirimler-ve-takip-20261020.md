# Haftalık bildirimler, takip merkezi ve ana sayfa bağlantıları

Tarih: 20.10.2026. Durum: geliştirme, yerel doğrulama ve canlı dağıtım tamamlandı. Gerçek oturum ve kayıt koruma sonuçları: [canlı doğrulama raporu](haftalik-bildirimler-canli-20261020.md).

## Tamamlanan akışlar

- Öğrenci haftalık değerlendirmesini kaydettiğinde, o öğrenciyi okuma yetkisi bulunan atanmış öğretmenlere uygulama içi bildirim hazırlanır.
- Öğretmen karar eklediğinde, karar içeriğini veya durumunu değiştirdiğinde öğrenciye uygulama içi bildirim hazırlanır.
- Bildirim öğrencinin ve haftanın kimliğini taşır. Öğrenci karar bildiriminden ilgili hafta ve karar kartına, öğretmen değerlendirme bildiriminden takip merkezindeki ilgili öğrencinin haftasına geçer.
- Öğretmenin takip merkezine **Haftalık değerlendirme bekleyenler** ve **Geciken kararlar** filtreleri ve sayaçları eklendi. Mevcut çalışma, ödev ve teslim listeleri korunur.
- Öğrenci ana sayfasına **Haftan ve kararların** bölümü eklendi: önceki haftanın değerlendirmesi, açık/geciken karar sayıları, son tarihi en yakın beş açık karar ve ilgili sayfalara bağlantılar.
- Takip listesinden açılan karar tamamlanıp listeye dönüldüğünde, mevcut filtre ve sayfa korunarak liste yenilenir. Tamamlanan ve iptal edilen kararlar gecikenler listesinden çıkar; kayıtları ve geçmişleri silinmez.

## Haftanın ve gecikmenin anlamı

Hafta pazartesi–pazar, gün sınırı Türkiye saat dilimidir. “Haftalık değerlendirme bekleyen”, son tamamen bitmiş haftada değerlendirme kaydetmemiş öğrencidir. Bu filtre geçmişteki bütün eksik haftaları biriktirmez. Öğretmene hafta bittikten sonra atanan öğrenciler için geçmiş haftadan eksik oluşturulmaz. Atamanın `StartDate` değeri, yeni haftanın pazartesi 00.00 sınırından önce olmalıdır.

Takip merkezi yalnız aktif, öğretmene atanmış, paylaşım ve kurum yetkileri uygun öğrencileri içerir. Öğretmen kendi oluşturduğu, `Planned` veya `InProgress` durumundaki ve son tarihi bugünden önce olan kararları geciken karar olarak görür. Başka öğretmenin kararı için yazma yetkisi verilmez.

Tarih filtresi değerlendirme eksiklerinde ilgili haftanın pazar gününe, kararlarda son tarihe uygulanır. Arama öğrenci adı ve başlığı kapsar. Sayaçlar tarih ve arama filtresinin bütün sayfalarındaki sonuçları gösterir; kayıt türü seçimi yalnız listeyi daraltır. Sunucu 10.000 kayıt sınırı aşıldığında eksik sonuç göstermek yerine daha kısa dönem ister.

Ana sayfa değerlendirme gereksinimini öğrencinin profilinin önceki hafta bitmeden var olmasına göre belirler. Açık karar sayısı beş kartla sınırlanmaz; kart listesi en yakın tarihli beş kayıtla sınırlıdır. Her kart özgün değerlendirme haftasına bağlanır. Bu haftanın kararları bağlantısı önceki haftalardan açık kalan kararları da gösteren mevcut haftalık ekranı açar.

Bu pakette zamanlanmış hatırlatma, e-posta veya SMS yoktur. Bildirimler değerlendirme ve karar kayıtlarının gerçekleşmesiyle üretilir. Ders saatleri, yıllık dağıtım, planlar ve ödevler kendiliğinden değiştirilmez.

## Servisler ve veri koruması

- `CoachingWeeklyReviewService`: ana sayfa özeti, ilgili kararın sayfasını bulma, değerlendirme/karar olayları.
- Öğrenci için yeni `GET /api/coaching/study-planning/weekly-review/home` uç noktası.
- Mevcut haftalık okuma uç noktalarına isteğe bağlı `decisionId` eklendi. Sunucu kararın yetkili öğrencinin ilgili hafta kapsamına ait olduğunu kontrol edip 20 kayıtlık doğru sayfayı seçer.
- Öğretmen takip uç noktasına `reflection` ve `decision` kayıt türleri, iki sayaç ve `weekStart` alanı eklendi.
- Notification servisine `WeeklyReflectionSavedEvent` ve `WeeklyDecisionChangedEvent` tüketicileri eklendi.
- Öğrenci/öğretmen portallarında ortak, rol kontrollü bildirim hedefi çözümlemesi kullanılır. Yalnız geçerli, boş olmayan GUID ve gerçek bir pazartesi tarihi kabul edilir. Bildirim metninden adres tahmin edilmez.

Yapılandırılmış bildirim referansları:

```text
weekly:<student-guid>:<yyyy-MM-dd>
weekly:<student-guid>:<yyyy-MM-dd>:<decision-guid>
```

Özel değerlendirme metinleri, öğretmenin takip notu ve karar başlığı olay/bildirim gövdesine kopyalanmaz. İçerik ilgili yetkili ekrandan alınır. Bildirimdeki kimlikler erişim yetkisi sağlamaz; her açılışta API yetkiyi yeniden kontrol eder.

Kayıt ve olay mevcut MassTransit EF BusOutbox altyapısında aynı veritabanı işlemi içinde saklanır. İkinci kayıt veya olay yazımı başarısızsa ikisi birlikte geri alınır. Tüketiciler mevcut Inbox/Outbox ve MessageId ile tekrar teslimlerde mükerrer bildirimi engelleyen dağıtıcıyı kullanır. Aynı içerikle yeniden kaydetme olay üretmez; sürüm çakışması 409 olur ve kullanıcının formu korunur. Hesap silme/mahremiyet değerlendirmesi yeni bekleyen olayları öğrenci ve öğretmen kimlikleriyle kapsar.

Şema değişikliği ve yeni migration yoktur. Yeni bilgiler DTO/olay/okuma sorgularıdır; mevcut haftalık tablolar ve bildirim altyapısı kullanılır. Plan, çalışma, kazanım bağı ve hesap silme veya toplu dönüştürme yapılmadı. Yerel geliştirme sırasında canlı veritabanına yazılmadı; sonraki yetkili canlı testte yalnız yeni değerlendirme, karar ve bildirimler eklendi. Ayrıntılar canlı doğrulama raporundadır.

## Doğrulama

- Öğrenci arayüz regresyonu: 111 test başarılı.
- Öğretmen arayüz regresyonu: 112 test başarılı.
- Coaching geniş regresyonu: 61 test başarılı, atlanan test yok.
- Son düzeltmelerden sonra haftalık değerlendirme/takip PostgreSQL testleri: 11 başarılı.
- Bildirim tüketicisi ve yapılandırma testleri: 5 başarılı.
- Öğrenci ve öğretmen üretim derlemeleri başarılı; Coaching ve Notification derlemeleri başarılı.

PostgreSQL testleri ayrı yerel test sunucusu/veritabanlarında çalıştı. Gerçek EF BusOutbox ile hata sonrası geri alma ve aynı kapsamda yeniden deneme, mükerrer kayıt/bildirim engeli, yetkisiz erişim ve paylaşım kısıtı, son tarih/atama sınırı, doğru karar sayfası, sayaçlar, tamamlanan çalışma kaydının değişmemesi ve modelin yeni migration gerektirmemesi doğrulandı. Bildirim tüketici testleri gerçek mesajlaşma sözleşmesini yerel MassTransit test ortamında sınar; canlı RabbitMQ teslimat testi yapılmadı.

Tarayıcıda gerçek bileşenler ve yalnız yerel örnek veriyle şu akış doğrulandı: ana sayfa bağlantısı → doğru haftanın açılması → değerlendirme kaydı → öğretmende bekleyen sayısının düşmesi → geciken kararın açılması → kararın tamamlanması → listeden çıkması → öğrencide güncel özet ve tamamlanan karar. Bu arayüz provası canlı veya gerçek HTTP uçtan uca testi olarak değerlendirilmemelidir; gerçek veritabanı ve mesajlaşma davranışı ayrı entegrasyon testlerinde doğrulandı.

Kanıtlar:

- `artifacts/coaching-weekly-followup-ui-final.log`
- `artifacts/coaching-weekly-followup-regression.log`
- `artifacts/coaching-weekly-followup-final-tests.log`
- `artifacts/coaching-weekly-followup-notification-tests.log`
- `artifacts/coaching-weekly-followup-student-build-final.log`
- `artifacts/coaching-weekly-followup-staff-build-final.log`
- `artifacts/coaching-weekly-followup-home.png`
- `artifacts/coaching-weekly-followup-tracking.png`
- `artifacts/coaching-weekly-followup-decision.png`

Mevcut derleme uyarıları: portallarda başlangıç paketi/stil uyarı eşikleri; öğretmen paketi yaklaşık 992 kB ve 1 MB hata eşiğinin altında. Notification bağımlılık zincirinde `System.Security.Cryptography.Pkcs` 10.0.0 için mevcut NU1902/NU1903 uyarıları var. Bu paket kapsamında bağımsız bağımlılık güncellemesi yapılmadı.

## Canlıya alma sırası

Bu sıra 20.10.2026 tarihinde uygulandı; gerçek sonuçlar canlı doğrulama raporundadır.

1. Güncel ortak sözleşme DLL'leriyle önce Notification servisini dağıt; yeni iki olayın kuyrukları tüketicilerle hazır olsun.
2. Coaching API'yi aynı sözleşmelerle dağıt. Veritabanı migration adımı gerekmez.
3. Öğrenci ve öğretmen portallarını dağıt.
4. Yetkili test hesaplarıyla değerlendirme → öğretmene bildirim; karar oluşturma/durum güncelleme → öğrenciye bildirim; doğru hafta/karara geçiş; listeden çıkma; form koruması akışlarını canlıda doğrula.
5. Mevcut plan, çalışma ve kazanım bağlantısı sayılarını dağıtım öncesi/sonrası karşılaştır. Sürüm geri alımı gerekiyorsa eski uygulama paketlerini kullan; bu paketin şema geri alma ihtiyacı yoktur.
