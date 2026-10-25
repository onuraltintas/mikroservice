# Haftalık bildirimler ve takip merkezi — canlı doğrulama

20 Ekim 2026. **`coaching-weekly-followup-20261020` canlıya alındı.** Notification, Coaching API, öğrenci portalı ve öğretmen portalı güncellendi. Veritabanı şeması değiştirilmedi; migration veya veri temizliği yapılmadı.

## Dağıtım ve yedek provası

- Canlı DLL/PDB kaynak özetleriyle 551 mevcut kaynak dosyası kontrol edildi. Yedi ilgili dosya değişikliği ve bir yeni haftalık bildirim tüketicisi dışında beklenmeyen değişiklik, yeni veya kaldırılan kaynak bulunmadı.
- Notification servisi canlı sürümünün doğrulanan kaynaklarından hazırlandı; çalışma alanındaki ilgisiz geliştirmeler pakete alınmadı. Canlı imajların bağımlılıkları korundu; ilgili uygulama ve ortak sözleşme DLL/PDB dosyaları değiştirildi.
- Portallar canlı kaynak kopyasına uygulanan 22 ilgili dosyayla, sunucuda ağ erişimi olmayan ayrı derleme konteynerinde üretildi. İki üretim derlemesi başarılı. Öğretmen başlangıç paketi 992,18 kB ile mevcut 1 MB hata sınırının altında; mevcut bütçe/stil uyarıları sürüyor.
- Coaching, Identity ve Notification yedekleri, şema özetleri ve önceki satırların içerik özetleri alındı. Eski dört imaj geri dönüş için sabitlendi. Hata halinde bu imajları geri getiren betik hazırdı; geri dönüş gerekmedi.
- Coaching yedeği ayrı bir prova veritabanına geri yüklendi. Yeni imajla şema/model tutarlılığı, değerlendirme kaydı ve doğru öğretmen alıcısı, aynı içerikle tekrar kaydetme, karar olayları, ana sayfa sayaçları, doğru karara geçiş, üç aşamalı geçmiş, eski sürüm isteğinin reddi ve tamamlanan kararın açık sayısından çıkması doğrulandı. Prova şeması değişmedi; prova veritabanı kaldırıldı.
- Önce Notification güncellendi; iki yeni RabbitMQ kuyruğunun tüketicileri hazır olunca Coaching API ve portallar güncellendi. İlgisiz konteynerlerin kimlikleri aynı kaldı.
- Dağıtım sonrası dört servis hazır, yeniden başlatma sayıları sıfır. Public öğrenci ve iki personel adresindeki HTML/JavaScript/CSS erişimleri, durum kodları ve MIME türleri doğru.

Dağıtım doğrulama zamanı 17:15:50; ilk son kontrol 17:21:03 (Türkiye). Hazır olma kontrolünün ilk açılış denemesinde Notification, mesajlaşma başlatılana kadar geçici 503 verdi; hazır olduktan sonraki izleme aralığında uygulama hata satırı bulunmadı.

## Gerçek öğrenci ve öğretmen oturumları

Mevcut test öğrencisi ve atanmış test öğretmeni kullanıldı. Önceki 12–18 Ekim değerlendirmesi ve tamamlanmış karar değiştirilmedi. Yeni test kayıtları için boş olan **5–11 Ekim 2026** değerlendirme haftası seçildi. Gecikme kontrolü için yeni kararın son tarihi **17 Ekim** olarak belirlendi.

| Senaryo | Canlı sonuç |
|---|---|
| Ana sayfa değerlendirme bağlantısı | `week=2026-10-12` ile Çalışma raporu sekmesi ve doğru haftanın mevcut değerlendirmesi açıldı. |
| Öğrenci değerlendirmesi kaydı | 5–11 Ekim için yeni test değerlendirmesi, gelecek hafta odağı ve 4/5 puanı kaydedildi. Önceki hafta kaydı korunuyor. |
| Öğretmene bildirim | `WeeklyReflectionSaved` bildirimi öğretmen oturumunda göründü. İçerik genel bilgilendirme; öğrencinin serbest metni bildirime kopyalanmadı. |
| Bildirimden öğretmende açılış | “Haftalık değerlendirmeyi aç” doğru öğrencinin 5–11 Ekim değerlendirmesini takip merkezinde açtı. Öğrenci metni ve 4/5 puanı API üzerinden yüklendi. |
| Aynı içerikle yeniden kayıt | Öğrenci değerlendirmeyi ikinci kez kaydetti. Sürüm 0 kaldı; veritabanında yalnız bir değerlendirme bildirimi var. |
| Öğretmen yeni karar oluşturma | `CANLI BİLDİRİM TESTİ 20261020 · Paragraf süre takibi` başlıklı, açıklamalı ve 17 Ekim son tarihli karar kaydedildi. |
| Öğrenciye karar bildirimi | Yeni haftalık karar bildirimi geldi; bağlantı `week=2026-10-05&decision=78c885b5-d9ab-41a0-9aed-8a42952c09da` ile doğru kartı açtı. “Bağlantıdan açılan karar” işareti göründü. |
| Ana sayfada açık/geciken karar | Bir açık ve bir geciken karar, başlığı, son tarihi ve ilgili karara geçiş bağlantısıyla gösterildi. |
| Öğretmen geciken karar filtresi | Arama metni ve Geciken kararlar türüyle bir kayıt ve sayaç 1 gösterildi. Satırdan doğru hafta/karar açıldı. |
| Tarih aralığı | Başlangıç 18 Ekim yapılınca 17 Ekim son tarihli karar dışarıda kaldı; liste ve geciken sayacı 0 oldu. Başlangıç 20 Eylül'e dönünce kayıt ve sayaç 1 oldu. |
| Değerlendirme bekleyen filtresi | Son tamamlanan 12–18 Ekim haftasının değerlendirmesi zaten mevcut olduğu için 0 kayıt gösterildi. Tür filtresi yalnız listeyi daraltırken tarih/arama özetindeki geciken karar sayacı 1 kaldı. Bu canlı hesapta eksik değerlendirme üretmek için eski kayıt silinmedi veya değiştirilmedi. Pozitif eksik değerlendirme ve atama sınırı ayrı PostgreSQL regresyon testlerinde kapsanıyor. |
| Karar durumu | Öğretmen Planlandı → Devam ediyor → Tamamlandı geçişlerini kaydetti. Öğrenci haftayı yenileyince Devam ediyor durumunu gördü. Her iki değişiklik için öğrenciye bildirim ulaştı. |
| Tamamlanma sonrası takip | Listeye dönülünce tarih, arama ve kayıt türü korundu. Karar listeden çıktı; geciken sayaç 1'den 0'a indi. |
| Tamamlanma bildiriminden açılış | Öğrencide son güncelleme bildirimi doğru kartı açtı. Tamamlandı rozeti, tamamlanma zamanı ve üç aşamalı geçmiş göründü. |
| Ana sayfanın son hali | Yeniden açılınca 0 açık / 0 geciken karar gösterildi; tamamlanan karar açık kart listesinden çıktı. |
| Bu haftanın tüm kararları | `week=2026-10-19` ile 19–25 Ekim haftası, Çalışma raporu sekmesi ve karar bölümü açıldı. |

Bu sürümde öğretmen/öğrenci işlemlerinin otomatik ekran yenilemesi eklenmedi; yeni veriler ilgili sayfanın açılması veya Yenile işlemiyle doğrulandı. Uygulama içi bildirimler gerçek canlı mesajlaşma ve Notification veritabanı üzerinden ulaştı. Test edilen akışlarda uygulama hatası bulunmadı; ek üretim kodu düzeltmesi gerekmedi.

## Kayıtların korunması ve bildirim sayıları

Dağıtım öncesi satır özetleri, canlı testler sonrasıyla karşılaştırıldı. **Önceden mevcut tüm korunan Coaching, Identity ve Notification satırları içerikleriyle aynı kaldı.** Taşıma altyapısının Inbox/Outbox kayıtları ve oturum yenileme belirteçleri karşılaştırma dışıdır. Yeni test değerlendirmesi, karar ve bildirimler eklendi; önceki bildirimler de değiştirilmedi.

| Kontrol | Sonuç |
|---|---|
| Planlar | 44; değişmedi |
| Çalışmalar / tamamlanan | 62.016 / 1; değişmedi |
| Kazanım bağlantıları | 61.979; değişmedi |
| Aktif LGS planı | `a4cea155-5485-4db6-9f52-1ec4df8080c3`, sürüm 7, bitiş 1 Haziran 2027; değişmedi |
| Önceki ödev değerlendirmesi | 95/100, Graded; değişmedi |
| Yeni değerlendirme | 5 Ekim haftası, sürüm 0, puan 4 |
| Yeni karar | Sürüm 2, Completed; üç geçmiş girdisi |
| Öğretmene değerlendirme bildirimi | 1 |
| Öğrenciye yeni karar bildirimi | 1 |
| Öğrenciye karar güncelleme bildirimi | 2; iki gerçek durum değişikliğine karşılık |
| Bekleyen haftalık mesajlar | Her iki kuyrukta hazır/bekleyen mesaj 0, tüketici 1 |

Bildirimlerde test değerlendirmesi veya takip notunun serbest metninin bulunmadığı ayrıca sorgulandı. Yeni haftalık bildirimlerin sayıları beklenenle aynı; tekrar kaydetme mükerrer bildirim üretmedi. Üç veritabanının şemaları dağıtım öncesiyle aynı. Dört servisin hazır olduktan sonraki son günlüklerinde hata satırı 0.

## Test ve kanıtlar

Yerel kabul: 111 öğrenci, 112 öğretmen arayüz testi; 61 Coaching regresyonu ve 5 bildirim testi başarılı. Son küçük düzeltmelerden sonra 11 haftalık PostgreSQL testi tekrar geçti. Buna canlı yedekten prova ve yukarıdaki gerçek oturum testleri eklendi.

Kanıt dizini: `artifacts/coaching-weekly-followup-live-20261020/`.

- `source-check-final.log`: yedi izin verilen değişiklik, bir yeni tüketici; beklenmeyen fark 0.
- `source-overlay.diff`: canlı portal kaynakları üzerine uygulanan ilgili farklar.
- `promotion.log`: yedek provası, servis sırası, kayıt ve şema koruma kontrolü.
- `final-verification.log`: kayıt/sürüm/geçmiş, bildirim sayıları, imaj ve günlük doğrulaması.
- `student-home-open.png`, `student-home-completed.png`: ana sayfadaki 1 → 0 açık/geciken karar.
- `teacher-overdue-filter.png`, `teacher-completed-filter.png`: öğretmen filtresi, satır ve 1 → 0 geciken sayacı.
- `student-decision-notification.png`, `student-completed.png`: canlı bildirim bağlantısı ve tamamlanmış karar.
- `public-verification.tar.gz`: yedek prova sonucu, public varlık erişimleri ve koruma karşılaştırması. Parola, bağlantı dizesi veya veritabanı yedeği içermez.

Önceki imajlar ve üç veritabanının yedekleri sunucuda korundu. Canlı test için eklenen yeni değerlendirme ve tamamlanmış karar, geçmişleriyle birlikte bırakıldı.
