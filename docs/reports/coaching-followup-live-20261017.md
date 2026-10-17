# Koçluk plan bildirimi ve toplu ödev takibi — canlı dağıtım

17.10.2026 saat **20:59:42 Türkiye** tarihinde `coaching-followup-20261017` canlıya alındı. Kaynak sürüm `1c68ffc1`; uygulama ve test ayrıntıları [geliştirme raporunda](coaching-plan-notifications-assignment-followup-20261017.md).

## Kapsam ve dağıtım

Notification, Coaching ve personel portalı güncellendi. Öğrenci/yönetici uygulamasının mevcut imajı ve ilgisiz servisler korundu. Yeni veritabanı şeması veya veri taşıması yok. Kirli çalışma dizinindeki ilgisiz değişiklikler pakete alınmadı; kaynaklar kaydedilmiş commitlerden ve daha önce doğrulanmış temiz üretim derlemesinden hazırlandı.

Önce Notification tüketicisi açıldı, `coaching-study-plan-updated` kuyruğunun tüketicisi hazır olduğu doğrulandı; ardından Coaching üreticisi ve personel portalı yayımlandı. Bildirim teslimi mevcut outbox/inbox ve alıcı bazlı tekrar önleme mekanizmasını kullanıyor.

| Bileşen | Canlı imaj SHA256 |
| --- | --- |
| Notification | `0e67e21dcd2d2300047061fcbe5dc5fdf24cb54227ba655ae8edb9d2d4a06a64` |
| Coaching | `7a061bb3fe773d192231bcc60a316aa51ce840251c095bb04f2d19f8867573e5` |
| Personel portalı | `f851d2f6d5a491b56bdffc57c8caf598fa42227ff969fcac743d592423dc85d7` |

Paket SHA256: `f221c85033ef0c83f873c8cd55987b6c2089326594608bd6f6bd3091b78273bf`.

## Veri koruma

- Identity, Coaching ve Notification veritabanlarının dağıtım öncesi yedekleri sunucuda alındı. Yedek içerikleri ve özel yapılandırma rapora veya yerel kanıt paketine aktarılmadı.
- Coaching yedeği ayrı veritabanına geri yüklendi; geçiş çalıştırıcısı iki kez çalıştırıldı, parmak izleri değişmedi. Notification yedeği için de iki geçiş/tohumlama provasında tüm mevcut satırlar aynı kaldı. Notification provası mesaj kuyruğuna bağlanmadı.
- Dağıtım öncesi/sonrası ve canlı arayüz testi sonrası karşılaştırmalarda hesaplar, öğretmen–öğrenci ilişkileri, planlar, ödevler, kazanım bağlantıları ve mevcut bildirimler korundu.
- Son toplamlar: **7 plan, 339 çalışma, 6 tamamlanmış çalışma, 115 gerçekleşen dakika, 1 haftalık taslak**. Yetim kazanım bağlantısı **0**.
- Yıllık plan `2f7e2d07-e044-471d-aedc-dfe004df7e2e` aktif kalıyor: 167 çalışma, 7215 planlı dakika, 52 farklı kazanım. Önceki test planı `b5536157-dd00-4f63-b522-9f250c77b33b` arşivde kaldı.

## Canlı öğrenci ve öğretmen doğrulaması

1. Öğretmen aktif yıllık plandaki tamamlanmamış **Çarpanlar ve Katlar** çalışmasının başlığını geçici doğrulama metniyle değiştirdi. Öğrencinin açık bildirim ekranına **Çalışma planın güncellendi** bildirimi, Yenile düğmesine basılmadan ulaştı. Öğrenci planı yeniden açıldığında değişen başlık ve kazanım ayrıntıları görüldü.
2. Öğretmen başlığı **Çarpanlar ve Katlar** olarak geri aldı; ikinci bildirim doğru öğrenciye ve doğru plana ulaştı. Aynı başlığın tekrar kaydedilmesi üçüncü bildirim üretmedi. Veritabanında doğru alıcı ve planla ilişkili iki ayrı bildirim kaydı doğrulandı.
3. Başlık, tarih, saat, süre ve kazanımlar test öncesi değerlerine döndü. Yalnızca hedef çalışmanın ve yıllık planın güncelleme/sürüm denetim alanları ilerledi; yıllık sürüm **12 → 14**. Tamamlanan çalışmalara müdahale edilmedi.
4. Öğretmende **Ödevler → Geciken ve teslim edilmeyenler** açıldı. Mevcut **deneme / Matematik** ödevi için **1 teslim bekleniyor, 1 geciken teslim** görüldü. Son tarih **14.10.2026 23:27 Türkiye**, durum **Atandı** ve gecikme süresi doğru gösterildi. Salt okunur veritabanı sayımı aynı sonucu verdi.
5. **Gecikenler** ve **Teslim edilmeyenlerin tümü** filtreleri, bulunmayan başlıkta boş sonuç, `DENEME` araması ve **Teslimleri aç → Henüz teslim edilmedi** ayrıntısı doğrulandı. Ödev oluşturulmadı, teslim edilmedi veya değiştirilmedi. Canlıda tek bekleyen ödev bulunduğundan gelecek tarihli ödev ve çok sayfalı liste davranışı otomatik testlerle doğrulanmıştır.

## Test ve operasyon kanıtları

Dağıtımdan önceki doğrulamada **593 farklı test** geçti: 82 PostgreSQL Coaching, 11 bildirim/plan API, 174 personel arayüzü, 288 öğrenci/yönetici arayüzü, 38 masaüstü/mobil uçtan uca test. Bu dağıtımda doğrulanmış temiz paket kullanıldı; aynı testlerin yeniden çalıştırıldığı iddia edilmez.

Canlı Notification, Coaching ve personel portalında yeniden başlama sayısı 0; uygulama hata işareti 0. Yeni bildirim kuyruğunda 1 tüketici, 0 bekleyen mesaj görüldü. Gerçek personel uygulamasının `/staff/` tabanı, JavaScript/CSS dosyalarının 200 yanıtı ve doğru MIME türleri ayrıca doğrulandı.

Sağlık gözlemi **21:15:33 Türkiye** saatinde tamamlandı. En az 15 dakikaya yayılan **31/31 örnek** başarılı: üç servis çalışıyor, yeniden başlama yok; gözlenen beş genel erişim adresi her örnekte HTTP 200 döndü. Bu genel adres kontrollerine ek olarak yetkili öğrenci ve öğretmen işlevleri yukarıdaki gerçek tarayıcı akışlarıyla doğrulandı.

Yerel kanıtlar: `artifacts/coaching-followup-live-20261017/live/`; ekran görüntüleri `artifacts/coaching-followup-live-20261017/proof/`. Yalnızca açık izin listesindeki sonuçlar ve parmak izleri toplandı. Sunucudaki özel yedekler korunuyor.

## Geri dönüş

Önceki üç imaj ve geri dönüş compose dosyası `/var/lib/eduivme/releases/coaching-followup-20261017/` altında korunuyor. Manuel geri dönüş gerekirse önce eski Coaching üreticisi, ardından eski Notification ve personel portalı alınır; eski üretici çalışmadan yeni tüketici kaldırılmaz. Veritabanı yedeği otomatik geri yüklenmez; bu sürümde şema değişikliği olmadığından uygulama geri dönüşü mevcut çalışma kayıtlarını geri sarmayı gerektirmez.
