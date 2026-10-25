# Haftalık inceleme kuyruğunun büyüyen geçmişle sayfalanması

25 Ekim 2026. Öğretmenin takip merkezindeki haftalık inceleme kuyruğu, bütün değerlendirme geçmişini tek seferde yükleyip 10.000 satırda hata vermek yerine, geçmişi sıralı gruplar halinde tarar. İnceleme bekleyen kayıtların toplamı kesilmez; istenen sayfa döndürülür. Önceden incelenmiş kayıtların sonuçları değişmişse yeniden takip işi oluşturulması korunur.

## Sunucu davranışı

Değerlendirmelerin yalnız kimlik, öğrenci, hafta ve sürüm alanları 256 satırlık anahtar tabanlı gruplarla okunur. Sıralama hafta ve kimlik üzerinden ilerler; aynı haftadaki farklı öğrenciler sayfa sınırında kaybolmaz. Son inceleme öğretmen ve değerlendirme bazında belirlenir. Eski incelemelerin notları veya sonuç JSON belgeleri kuyruk sorgusunda yüklenmez.

Yeni teslimler, değerlendirme sürümü değişmiş kayıtlar ve sonuç kanıtı bulunmayan eski incelemeler doğrudan bekleyen iş sayılır. Aynı sürümde incelenmiş değerlendirmeler güncel sonuç kanıtıyla karşılaştırılır. Bu karşılaştırma öğrenci başına en çok 26 haftalık gruplarla yapılır. Birleştirilmiş dönem raporu 10.000 çalışma/karar sınırını aştığında hafta grubu bölünerek tekrar okunur; tek bir haftanın gerçek rapor sınırları korunur. Başka hatalar bu mekanizma tarafından gizlenmez.

Arama bekleyen işin öğrenci adı ve görünen başlığına uygulanır. Sayaçlar tüm eşleşen bekleyen işleri kapsar; kayıt türü filtrelemesi listeyi daraltır. Tüm kayıtlar görünümünde haftalık incelemeler diğer işlerle tarih, saat, tür ve kimlik sırasına göre birleştirilir. Sunucu yalnız istenen sayfayı ve gerektiğinde dönebileceği son sayfayı tutar. 1.000 sayfa üst sınırı kaldırılmıştır; büyük sayfa numaralarında ofset taşmasını önlemek için 64 bit hesap kullanılır. Son sayfadaki iş tamamlanmışsa sonraki istek son geçerli sayfaya çekilir.

Bir isteğin sayacı ve içeriği, PostgreSQL `RepeatableRead` işlemi içinde aynı veri görünümünden hesaplanır. Üretimdeki Npgsql yeniden deneme stratejisi bu işlemi kapsar. Sayfalar arasında yapılan yeni istek güncel kuyruğu gösterir; kuyruğun işlem yapılırken değişmesi beklenen davranıştır.

Eksik değerlendirme hesabı, öğrenci takibinin başlangıcından önceki ilgisiz değerlendirme geçmişini okumaz. Eski eksik haftaların takip davranışı korunur. Bu değişiklik haftalık inceleme geçmişinin büyüme sınırını kaldırır; diğer takip türlerinin mevcut toplu rapor sınırlarını değiştirmez. Tam sayım ve sonuç değişikliği denetimi geçmişi taramaya devam ettiğinden süre geçmiş büyüdükçe artabilir; geçmişe keyfi bir kesim tarihi uygulanmaz.

## Ekran ve veri koruma

Öğretmen önceki/sonraki sayfa düğmelerini veya doğrudan sayfa numarasını kullanabilir. Sayfa/toplam sayfa ve bütün sayfalara ait toplam iş sayısı gösterilir. Geçersiz sayfa girişi mevcut listeyi kaldırmaz. İncelemeden dönüş, uygulanan filtreleri koruyarak kuyruğu yeniler ve sunucunun döndürdüğü geçerli sayfa numarasını gösterir.

`20261025000514_WeeklyReviewQueuePaging` yalnız iki indeks ekler: değerlendirmelerde hafta/kimlik, incelemelerde öğretmen/değerlendirme/son inceleme sırası. Veri satırlarını dönüştürmez, silmez veya yeniden yazmaz. İndeks geçişinin yükseltme, tekrar çalıştırma ve geri alma işlemleri eski değerlendirme ve inceleme satırlarının tamamının JSON kontrol toplamlarıyla denetlenir. Kuyruk okuması da aynı kayıtların değişmediğini doğrular.

## Doğrulama

Testler geçici yerel PostgreSQL veritabanlarında çalıştırılır. Canlı veritabanına veya dağıtıma müdahale edilmemiştir. Test günlükleri ve TRX raporları `artifacts/coaching-review-queue-paging-20261025/` altındadır.

Yeni senaryolar:

- 10.025 bekleyen değerlendirme: tam sayaç, 1.001. sayfa, son sayfa ve çok büyük sayfa isteği; arama ve tüm kayıtlar görünümü.
- 10.030 değerlendirmelik incelenmiş geçmiş: yeni teslim, değişmiş sürüm, eksik eski kanıt ve değişmiş sonuç için dört doğru takip işi; diğer geçmiş kayıtlar kuyrukta yer kaplamaz.
- Aynı haftada 270 öğrenci: 256 satırlık tarama sınırında ve bütün sayfalar boyunca kayıp/tekrar olmaması; yetkisiz öğrencinin dışarıda kalması.
- Son işin incelenmesi ardından sayfanın daralması; değerlendirme ve inceleme geçmişinin korunması.
- 26 geçerli haftaya yayılan 10.400 çalışma: dönem gruplarının bölünmesi, diğer takip türleriyle sıralı birleşim ve sayfalama.
- İstek sürerken ikinci bağlantıdan inceleme: aynı istekte tutarlı toplam, sonraki istekte güncel kuyruk; yeniden deneme etkin bağlantı ve metadata alanları denetimi.
- Yalnız indeks ekleyen geçişin veri koruması ve model uyumu.

Sonuçlar:

- Sunucu regresyonu: 281 başarılı, hata veya atlanan test yok.
- Eşzamanlı inceleme ve yeniden deneme etkin bağlantı: ayrıca 1 başarılı test.
- Öğrenci/admin arayüzü: 180 başarılı test.
- Öğretmen/personel arayüzü: 152 başarılı test.
- Toplam 614 başarılı test.
- API Release derlemesi: 0 hata, 0 uyarı. Öğretmen arayüzü üretim derlemesi başarılı; mevcut paket ve stil boyutu bütçesi uyarıları sürüyor. Test projesindeki mevcut xUnit uyarısı ve arayüz regresyonundaki mevcut şablon uyarıları günlüklerde bulunuyor.

Dağıtım sırasında indeks geçişi uygulanmalıdır. Canlıya dağıtım ve canlı veritabanında geçiş yapılmamıştır.
