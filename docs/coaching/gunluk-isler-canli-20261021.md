# Günlük işler ve takip bağlamı: canlı doğrulama

21 Ekim 2026, Europe/Istanbul. Paket canlıya alındı; öğrenci ve atanmış öğretmenin gerçek oturumlarında akışlar doğrulandı. Uygulamada yeni bir hata bulunmadı. Yerel test sonuçları canlı test olarak sunulmadı.

## Dağıtım ve geri dönüş

Coaching API, admin-panel ve staff-portal imaj etiketi: `coaching-daily-agenda-20261021`.
Sunucu dizini: `/var/lib/eduivme/releases/coaching-daily-agenda-20261021`.
Geri dönüş dosyası aynı dizindeki `rollback.compose.json`; önceki API r2 ve arayüz r4 imajlarının gerçek kimlikleri saklandı. Diğer servisler değiştirilmedi.

API paketi canlı kaynak tabanının ayrılmış kopyasından derlendi; PDB karşılaştırmasında 473 mevcut kaynak belgesi eşleşti, beklenmeyen fark 0. Yalnız `StudentDailyAgendaController.cs` eklendi ve API DLL/PDB çifti değiştirildi. Diğer Coaching ve ortak sözleşme derlemeleri korundu. Arayüzde 12 dosya sınırlı olarak güncel canlı kaynak üzerine uygulandı; önceki takvim menüsü, ders renkleri ve tamamlanan çalışma koruması korundu.

Coaching, Identity ve Notification yedekleri sunucuda alındı. **Migration veya şema değişikliği yok.** Üç şema dağıtım öncesi ve sonrası eşleşti. Ayrı veritabanına geri yüklenen üretim yedeğinde Bugün 5, Yaklaşan 26, Geciken 3 kaydın tamamı; sayaç, sayfalama ve öğrenci kapsamı bağımsız beklentilerle doğrulandı. Prova öncesi/sonrası bütün iş satırı özetleri eşit kaldı; geçici veritabanı kaldırıldı.

## Canlı öğrenci akışı

İlk sayaçlar **Bugün 5 / Yaklaşan 26 / Geciken 3** olarak üretim provasıyla eşleşti. Yaklaşan listesi 20+6 kayıtla iki sayfa gösterdi. Tarihler, saat dilimi, ilgili plan/çalışma kimlikleri ve doğru kayda geçiş kontrol edildi.

| Yeni test işlemi | Canlı sonuç |
| --- | --- |
| Geçmiş güne yeni çalışma ekleme | Geciken 3→4; doğru çalışma ayrıntısı açıldı |
| Bu çalışmayı 25 dakika olarak tamamlama | Geciken 4→3; yeni çalışma listeden çıktı; öğretmenin başlığa göre filtreli geciken çalışma sayacı 1→0 |
| Yarın için yeni çalışma ekleme/tamamlama | Yaklaşan 26→27→26; bağlantı doğru güne ve çalışmaya gitti |
| Bugün son tarihli yeni ödev atama/teslim | Bugün 5→6→5; teslim edilen ödev günlük işlerden çıktı |
| Geçmiş son tarihli yeni karar oluşturma/kapatma | Geciken 3→4→3; bağlantı doğru haftayı ve kararı açtı; tamamlanan karar listeden çıktı |

Son sayaçlar yeniden **5 / 26 / 3**. Eski bekleyen işler tamamlanmadı veya taşınmadı. Öğrencinin günlük listeleri yalnız aktif planın bekleyen çalışmalarını ve ilgili açık ödev/kararları gösterdi.

## Canlı öğretmen akışı

- Başlangıç/bitiş tarihleri, arama ve kayıt türü; ayrıntıya girip dönüşte ve başka öğretmen sekmesine gidip dönüşte korundu. Tarih aralığı testte 01–31 Ekim, arama `CANLI GÜNLÜK 21.10` idi.
- Henüz uygulanmamış arama metni de sekme dönüşünde korundu. Sonuç ve sayaçların son uygulanmış filtreye ait olduğunu belirten açıklama göründü.
- Yeni geciken çalışma tamamlanınca ayrıntıdan dönüş listeyi sunucudan yeniledi ve filtreli geciken çalışma sayacı 1→0 düştü.
- Yeni teslim değerlendirme bekleyen sayacını 0→1 yükseltti. Doğru öğrencinin teslimi takip merkezinden açıldı, 95/100 puan ve geri bildirim kaydedildi. Dönüşte tarih/arama/değerlendirme filtresi korundu; teslim listeden çıktı, sayaç 1→0 oldu.
- Yeni geciken karar kapatıldı; filtreli geciken karar sayacı 1→0 oldu. Son test filtresinde bütün sayaçlar 0 ve boş liste açıklaması görüldü.
- Ekran çizimi/istek devam ederken alınan ara çıktılar son sonuç sayılmadı; sunucudan yüklenmiş nihai görünüm ayrıca doğrulandı.

Öğretmen test filtreleri kanıt kaydından sonra varsayılan son 30 gün, boş arama ve tüm kayıtlar durumuna döndürüldü. Filtre bağlamı çalışma alanı belleğinde korunur; tam sayfa yenilemesinden sonra kalıcı saklama bu paketin kapsamı değildir.

## Test ve görünüm

- Yerel sunucu/PostgreSQL/HTTP regresyonu: **32 başarılı, 0 başarısız, 0 atlanan**. Yeni uç, yetki, tarih sınırları, sayfalama, kayıt koruma ve ilgili mevcut haftalık/takip akışları dahil.
- Son arayüz çalıştırması: öğrenci **141**, öğretmen **128** başarılı. Daha eski rapordaki 129 öğrenci sayısı son çalıştırmanın sayısı değildir.
- Ayrılmış API Release derlemesi ve iki arayüzün üretim derlemeleri başarılı. Mevcut ilgisiz stil bütçesi uyarıları sürüyor; bütçeler gevşetilmedi.
- İki rolde açık/koyu tema, normal masaüstü ve 390×844 mobil kontrol edildi. Mobil `scrollWidth=innerWidth=390`; sayfa taşması yok. Öğrenci listeleri ve öğretmen filtreleri kullanılabilir kaldı. Merkezi toaster kullanılıyor; tarayıcı dialog eklenmedi.
- Canlı JS/CSS istekleri 200 ve doğru MIME türlerinde. Üç servis çalışıyor, yeniden başlama sayıları 0. API günlüklerinin doğrulama aralığında ERR/FTL veya işlenmemiş istisna yok. Gözlemin başlangıç/bitiş zamanları `final-records.log` dosyasında yer alır.
- Yeni günlük işler ucuna oturumsuz genel erişim isteği canlıda 401 ile reddedildi.

Son gözlem: dağıtım başlangıcından itibaren **15 dakika 14 saniye**, 21 Ekim 2026 **12:52:04–13:07:18 Türkiye saati**. API hata kaydı 0, üç servisin yeniden başlama sayısı 0. Bu aralık sonunda eski iş satırı karşılaştırması yeniden geçti.

## Kayıt koruma

Dağıtım öncesindeki bütün Coaching, Identity ve Notification iş satırları içerik özetleriyle karşılaştırıldı; eksik/değişmiş eski iş satırı yok. Yalnız testte kullanılan aktif planın sürüm/güncelleme zamanı ile iki test kullanıcısının doğal giriş metadata alanları ayrı tutuldu. Eski tamamlanan çalışmalar ve karar/ödev/kazanım ilişkileri karşılaştırmaya dahil edildi.

| Veri | Önce → sonra |
| --- | --- |
| Plan | 44 → 44 |
| Çalışma | 62.018 → 62.020; yalnız iki yeni test çalışması |
| Tamamlanan çalışma | 3 → 5; yalnız iki yeni test tamamlaması |
| Kazanım bağlantısı | 61.979 → 61.979 |
| Aktif plan sürümü | 21 → 25; iki ekleme ve iki tamamlama |

Yeni test kayıtları korunarak bırakıldı; silme yapılmadı ve bekleyen test işi kalmadı:

- Geciken çalışma: `b5bf7b0e-919a-4359-81ee-aea1ba21cbb1`, tamamlandı, 25 dakika.
- Yaklaşan çalışma: `10d67a3a-cc73-4be2-abae-eaffce931aa5`, tamamlandı, 25 dakika.
- Ödev: `578b633f-64a8-4aad-88d7-e2221691f257`, teslim edildi ve değerlendirildi, 95/100.
- Karar: `d937fae3-c763-4a34-ab51-d2e0b052b6d4`, tamamlandı.
- Korunan aktif plan: `a4cea155-5485-4db6-9f52-1ec4df8080c3`.

## Kanıtlar

- `artifacts/coaching-daily-agenda-live-20261021/final-records.log`
- `artifacts/coaching-daily-agenda-live-20261021/promotion.log`
- `artifacts/coaching-daily-agenda-live-20261021/drill.log`
- `artifacts/coaching-daily-agenda-live-20261021/source-check.log`
- `artifacts/coaching-daily-agenda-live-20261021/live-ui-proof.json`
- `artifacts/coaching-daily-agenda-live-20261021/server/`
- `artifacts/daily-agenda-release-backend.log`
- `artifacts/daily-agenda-release-ui.log`
- `artifacts/coaching-daily-agenda-live-20261021/student-light-desktop.png`
- `artifacts/coaching-daily-agenda-live-20261021/student-dark-mobile.png`
- `artifacts/coaching-daily-agenda-live-20261021/teacher-light-desktop.png`
- `artifacts/coaching-daily-agenda-live-20261021/teacher-dark-mobile.png`
