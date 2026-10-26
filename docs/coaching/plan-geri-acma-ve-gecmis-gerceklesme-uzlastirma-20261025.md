# Planı geri açma ve geçmiş gerçekleşmeyi uzlaştırma — 25 Ekim 2026

## Eski planın yeniden etkinleştirilmesi

Yayımlanmış bir plan geri açılırken her çalışma, aynı köke ait güncel çalışma zinciriyle uzlaştırılır. Aktif devamın kalan süre ve soru kapsamı korunur. Zincirde bağımsız bir tamamlanma kaydı varsa eski görev de tamamlanmış görünür; tamamlanmanın kaynak görevi ve zamanı saklanır. Açık kararın çalışma bağlantısı geri açılan plandaki aynı çalışmaya devredilir.

Eski planın görev kimlikleri, planlanan süreleri, soru hedefleri, yerleşimleri ve kazanım dağılımları değiştirilmez. Her görevin fiili süreleri, soruları ve çalışma günleri kendi kaydında kalır. Başka görevin gerçekleşmesi kopyalanmaz. Bu nedenle eski planın paydası korunur ve gerçek çalışma süresi iki kez sayılmaz. Değişen tamamlanma/kalan kapsam için önceki durumu, gerekçeyi ve kaynak görevi/sürümü içeren `ContinuationReconciliation` geçmişi eklenir.

Güncel kısmi çalışmada kalan kapsam hiç belirtilmemişse süre farkından tahmin yapılmaz; önce aktif görevde kalan kapsamın belirtilmesi istenir. Aynı kökten birden fazla devam veya geri açılacak planda eksik korunmuş çalışma varsa işlem reddedilir. Görev, plan durumu, karar bağlantısı ve yayın aynı öğrenci kilidi ve veritabanı işlemi içindedir; hata durumunda birlikte geri alınır.

## Geçmiş gerçekleşme düzeltmesi

Geçmiş kaydın gerekçeli düzeltmesi ile aynı kökün aktif devamı tek işlemde uzlaştırılır:

- Geçmiş çalışma tamamlandı olarak düzeltilirse aktif devam da kaynak gösterilerek tamamlanır; devamın kendi gerçekleşmesi değiştirilmez ve olmayan gerçekleşme oluşturulmaz.
- Henüz ek çalışma/kapsam kaydı olmayan devam, düzeltilmiş açık kalan kapsamı alır.
- Aktif devamda sonradan gerçekleşme veya kalan kapsam kaydı varsa, geçmiş kapsam/durum düzeltmesi için **aktif devamın güncel kalan kapsamı ayrıca açıkça belirtilir**. Eski kısmi kaydın kalan kapsamı ile bugünkü devamın kalan kapsamı ayrı alanlardır. Aktif plan kimliği ve sürümü doğrulanır; eski form yeni ilerlemeyi ezemez.
- Yalnız fiili miktar veya tarih düzeltiliyorsa sonraki kalan kapsam değiştirilmez.
- Sonradan bağımsız olarak tamamlanmış devamın kendi tamamlanması korunur.
- Tamamlanmanın kaynak kaydı geri alınırsa, ona dayanan devralınmış tamamlanmalar da kaldırılır. Arşivlerdeki devralınmış durum bağımsız kazanım kredisi olarak kalmaz. Sonraki bağımsız tamamlanma varsa geçerliliğini sürdürür.

Uzlaştırma aktif plan/görev sürümlerini ilerletir. Önceden hazırlanmış, artık eski kalan kapsama dayanan devam taslağı yayımlanamaz. Aynı düzeltmenin tekrarı yeni gerçekleşme veya uzlaştırma geçmişi üretmez. Önceki sürümden kalmış, kaynakta tamamlanmış fakat devamda açık görünen kayıt da aynı tamamlanmayı yeniden doğrulama yoluyla, kaynak gerçekleşmeyi yeniden yazmadan uzlaştırılabilir.

Öğrenci ekranı aktif devamı ve gerekli güncel kapsam alanlarını gösterir. Tamamlanmayı başka kayıttan devralmış görev, fiili çalışmanın kaynak kayıtta kaldığını açıklar. Gerçekleşme geçmişi uzlaştırma kayıtlarını ayrı adlandırır. Tamamlanma kaynağı veri dışa aktarımına ve personelin plan görünümüne de eklenmiştir.

## Veri geçişi ve doğrulama

`20261024232232_StudyProgressReconciliation`, göreve nullable `CompletionSourceTaskId` ekler. Mevcut satırlar ve geçmiş JSON kayıtları geçiş sırasında yeniden yazılmaz. Tamamlanma kısıtı, gerçekleşmesi başka görevde bulunan devralınmış tamamlanmayı destekler. Devralınmış tamamlanma varken bu bilgiyi kaybettirecek sürüm düşürme engellenir.

Yeni testler, ardışık planları geri açmayı, güncel kalan kapsamı, tamamlanan çalışma ve kazanım kredisini, fiili günlerin/sürelerin tek sayılmasını, eski kayıtların korunmasını, kapsam düzeltmesini, tamamlanmayı geri almayı, sonraki bağımsız tamamlanmayı, eski form sürümünü, taslak geçersizleşmesini, işlem geri almayı, eski kayıtları yeniden doğrulamayı, dışa aktarımı, öğrenci/çalışma kökü sınırını ve kayıpsız geçişi kapsar.

Eski bir manuel plan testindeki “boş girdi boş plan üretir” beklentisi, korunmuş devam çalışmalarını dikkate alacak şekilde güncellendi. Korunmuş devam içeren taslak boş sayılmaz; gerçekten boş taslağın yayımlanması ayrı öğrenciyle doğrulanarak reddedilmeye devam eder.

Testler geçici PostgreSQL veritabanlarında çalıştırıldı. Canlı veritabanına geçiş veya dağıtım yapılmadı. Günlükler ve TRX sonuçları: `artifacts/coaching-progress-reconciliation-20261025/`.

Sonuçlar:

- PostgreSQL destekli sunucu regresyonu: 263 geçti, 0 hata, 0 atlanan.
- Son değişikliklerle 13 yeni uzlaştırma testi ayrıca yeniden doğrulandı; tamamı geçti.
- Manuel plan API, rapor hesabı ve veri koruma: 25 geçti.
- Öğrenci/admin arayüzü: 169 geçti; son ekran değişikliğiyle ilgili 44 test ayrıca yeniden doğrulandı.
- Öğretmen/personel arayüzü: 147 geçti.
- Tekrar koşumlar hariç toplam: 604 başarılı test.
- API Release: 0 hata, 0 uyarı. Öğrenci/admin ve personel üretim derlemeleri başarılı; mevcut paket/stil boyutu bütçesi uyarıları sürüyor.
