# Takip merkezi — canlı doğrulama

20 Ekim 2026. API ve iki portal `coaching-tracking-20261020` sürümüne alındı. Veritabanı şeması ve bağımlılıklar değiştirilmedi. Mevcut hesaplar, planlar, çalışmalar ve kazanım bağlantıları korundu.

## Dağıtım ve geçiş provası

- Coaching, Identity ve Notification veritabanları dağıtımdan önce yedeklendi; önceki üç imaj geri dönüş için sabitlendi.
- Canlı API'nin 59 kaynak dosyası portable PDB kaynak özetleriyle karşılaştırıldı; fark yok. Yalnız yeni Takip merkezi kontrolcüsünü ekleyen API DLL/PDB dosyaları yayımlandı. Diğer servis modülleri ve NuGet bağımlılıkları aynı kaldı.
- İki portal canlı kaynaklarının üzerine yalnız 9 ilgili arayüz dosyası uygulandı. Çalışma alanındaki diğer değişiklikler pakete alınmadı.
- İki portal sunucuda izole derlendi. Personel başlangıç paketi yaklaşık 990 kB; mevcut 1 MB hata sınırı artırılmadı.
- Yeni API, canlı Coaching yedeğinin ayrı bir veritabanına geri yüklenmiş kopyasında, mevcut canlı bağımlılıklarla salt okunur işlem içinde dört kayıt türü için çalıştırıldı. Geçici prova veritabanı kaldırıldı.
- Dağıtım öncesi/sonrası korunan tablo içerik özetleri birebir eşleşti. API ve iki portal dışındaki servisler yeniden başlatılmadı.
- Canlı HTML, JavaScript ve CSS dosyaları başarılı durum ve doğru içerik türleriyle sunuldu. Takip merkezinin gecikmeli yüklenen ekranı gerçek öğretmen oturumunda açıldı.

## Gerçek oturumlarla doğrulanan akışlar

| Senaryo | Sonuç |
|---|---|
| İlk açılış ve boş sonuç | Mevcut durumda gecikmiş/değerlendirme bekleyen kayıt olmadığında üç sayaç 0, liste boş ve açıklama doğru. |
| Geçersiz tarih aralığı | Başlangıç bitişten sonra olduğunda uyarı çıktı; eski liste/sayaçlar görünür bırakılmadı. |
| Toplamlar ve sayfalama | Ayrı test kayıtlarında 1 geciken çalışma + 26 geciken ödev = 27 kayıt görüldü. İlk sayfada 25, ikinci sayfada 2 kayıt vardı; toplamlar sayfa değişince aynı kaldı. |
| Ödev ayrıntısından geri dönüş | İkinci sayfadaki 26 numaralı test ödevi doğru öğrenciyle açıldı. Listeye dönünce ikinci sayfa ve filtreler korundu, veri yenilendi. |
| Başlık araması | 26 numaralı ödevin tam başlığıyla aramada yalnız bir kayıt ve 1 geciken ödev gösterildi; sayfa 1'e döndü. |
| Öğrenci ve tür filtresi | Öğrenci adı + geciken çalışma seçimi yalnız ilgili çalışmayı gösterdi; diğer türlerin dönem/arama özetleri korundu. |
| Çalışmadan plana geçiş | Aktif LGS planı ilk plan listesi sayfasında bulunmasa da tam kimliğiyle açıldı. Seçilen çalışma ayrıntısı doğrudan gösterildi; mevcut tamamlanmış kart takvimde aynı kaldı. |
| Öğrenci teslimi | Ayrı 01 numaralı test ödevi gerçek öğrenci hesabından notla teslim edildi. Geciken ödev sayısı 26'dan 25'e, değerlendirme bekleyen sayısı 0'dan 1'e değişti. |
| Tarih filtresi | Yalnız bugünün seçilmesiyle dünün gecikmiş kayıtları elendi, bugünün teslimi kaldı: 0 çalışma / 0 geciken ödev / 1 değerlendirme. |
| Öğretmen puanlama | Bekleyen teslim satırından açılan doğru öğrenciye 92 puan ve geri bildirim kaydedildi. Sunucudan yeniden yüklenen durum Değerlendirildi ve puan 92 oldu. |
| Puanlanan teslimin listeden çıkması | Listeye dönünce aynı tarih, öğrenci araması ve değerlendirme filtresi korundu. Bekleyen sayaç 1'den 0'a düştü; ilgili satır listeden çıktı. |
| Öğrencide değerlendirme | Öğrenci sayfası yenilendi; 92 puan ve yeni geri bildirim görüldü. Sonrasında eski ödev açıldı; mevcut 95/100 ve önceki geri bildirimi aynı kaldı. |

Canlı ürün akışlarında ek bir hata saptanmadı. Bu sonuç sınanan akışları kapsar; tüm olası koşullar için hatasızlık garantisi değildir.

## Test verileri ve mevcut kayıtların korunması

Sayfalama için mevcut test öğretmeni/öğrencisinin **mevcut kurum ataması** altında 26 açıkça işaretlenmiş test ödevi hazırlandı. Gecikmiş çalışmadan geçiş için aktif plana tek bir geçici, kazanım bağlantısı olmayan test çalışması eklendi. Eski çalışmalar ve plan sürümü değiştirilmedi. Test verisini hazırlayan araç yalnız sabit test hesaplarını ve ayrılmış kayıt kimliklerini kabul eder; yeni hesap veya ilişki oluşturmaz.

Test sonunda 26 test ödevi iptal edildi; teslim edilen test ödevinin 92 puanı ve geri bildirimi geçmişte korunuyor. Yalnız bu görevde eklenen, tamamlanmamış ve kazanım bağlantısı olmayan geçici çalışma kaldırıldı. Aktif takip listesinde bu test verileri kalmadı.

Dağıtım sırasında tam tablo özetleri; canlı testler sırasında ise eski satırların tek tek içerik özetleri karşılaştırıldı. Sonuç: **testten önceki tüm korunan satırlar içerikleriyle birlikte aynı**. Yalnız yeni test kayıtları, bunlara ait olay/bildirim ve işlem kayıtları eklendi. Taşıma altyapısının Inbox/Outbox tabloları ve oturum yenileme belirteçleri bu içerik karşılaştırmasına dahil değildir.

Son doğrulama:

| Kayıt | Son durum |
|---|---|
| Aktif plan | `a4cea155-5485-4db6-9f52-1ec4df8080c3`, sürüm **7**, bitiş **1 Haziran 2027** |
| Plan sayısı | **44**, aynı |
| Çalışma sayısı | **62.016**, aynı |
| Tamamlanan çalışma | **1**, aynı |
| Kazanım bağlantısı | **61.979**, aynı |
| Eski ödev puanı | **95**, Graded, aynı |
| Ayrı test teslimi | **92**, Graded; test ödevi iptal edildi, değerlendirme korundu |
| Geçici çalışma | **0** |

Dağıtımdan sonraki son API ve iki portal sunucu günlüklerinde hata/istisna satırı yok. Tarayıcıda uygulamaya ait yeni yükleme veya API hatası görülmedi.

## Testler ve kanıtlar

Paketin yerel kabul sonuçları: **153 arayüz**, **7 gerçek PostgreSQL** testi başarılı. Son boş durum düzenlemesinden sonra ilgili 8 ödev ayrıntısı testi tekrar başarılı; bunlar 153'e yeniden eklenmez. İki üretim derlemesi geçti. CI tanımı güncellendi ve yerelde ayrıştırıldı; GitHub üzerinde bu oturumda ayrı bir CI çalıştırması başlatılmadı.

Canlı kanıtlar: `artifacts/coaching-tracking-live-20261020/`.

- `teacher-page-two.png`: 27 kayıt, iki sayfa ve tüm sayfaları kapsayan sayaçlar.
- `teacher-work-detail.png`: seçilen çalışmanın doğru planda açılması.
- `review-before-grade.png`: değerlendirme bekleyen 1 teslim.
- `grade-saved.png`: sunucudan tekrar yüklenmiş 92 puan ve geri bildirim.
- `review-after-grade.png`: aynı filtrede bekleyen 0 ve satırın listeden çıkması.
- `student-new-grade.png`: öğrencinin gördüğü değerlendirme.
- `final-verification.log`: korunan eski satırlar, son kayıt sayıları, imajlar ve hata günlükleri kontrolü.
- `public-verification.tar.gz`: dağıtım/prova/karşılaştırma kanıtları; parola, bağlantı bilgisi ve veritabanı yedeği içermez.

Sunucudaki yedekler ve eski imajlar geri dönüş için korundu.
