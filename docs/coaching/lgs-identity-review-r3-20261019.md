# 2026 LGS kimlik incelemesi ve üç ilde kontenjan genişletmesi

19 Ekim 2026. Kanıtlı bölüm canlı veritabanına uygulandı. Türkiye geneli katalog henüz tamamlanmadı.

## Sonuç

| Durum | Önce | Sonra |
|---|---:|---:|
| Kaynak kaydı | 701 | 704 |
| Doğrulanmış | 434 | 438 |
| Çoklu kimlik / belirsiz eşleşme | 260 | 262 |
| Bekleyen | 5 | 4 |
| Kaynak çelişkisi | 2 | 0 |

**Çelişki sayısının sıfırlanması, iki Şile kaydının kullanılabilir olduğu anlamına gelmez.** Doğru kurum kodu kanıtlandı; eski kimlikler arasındaki ayrım kanıtlanamadığı için bu iki kayıt `Ambiguous` durumuna geçirildi. Öğrenci/öğretmen aramasına doğrulanmış kayıt olarak açılmadı. Başlangıçtaki 267 kayıttan yalnız birinin okul bağlantısı kesin olarak çözüldü; kalan 266 kayıtta kimlik incelemesi sürüyor.

## 260 çoklu kimlik kaydı

267 inceleme kaydı canlıdan salt okunur olarak alındı. Dışarı alınan alanlar yalnız kamuya açık okul/katalog bilgileridir; hesap, öğrenci, hedef ve çalışma bilgileri dışarı çıkarılmadı.

Türkçe büyük/küçük harf, boşluk ve ilçe adındaki `(MERKEZ)` eki dikkate alınarak; önceki resmî ad eşleştirmelerinin bütün yazım biçimleriyle tekrar karşılaştırıldı. PostgreSQL'in varsayılan `upper()` sonucu Türkçe eşleştirme için tek başına kullanılmadı.

260 kaynak kaydı, ad/il/ilçe bakımından 67 farklı kaynak grubuna karşılık geliyor. Eski katalog adaylarının dağılımı:

| Aynı ad ve konumdaki eski kimlik sayısı | Kaynak kaydı |
|---|---:|
| 2 | 133 |
| 3 | 73 |
| 4 | 45 |
| 7 | 9 |

Hiçbirinde tek bir eski kimliği seçtiren kanıt yok. Eski `SourceId` değerleri çoğunlukla uygulama kimlikleri; resmî kurum kodu değiller. Aynı okulun dil/alan kayıtlarını, puan benzerliğine veya GUID sırasına göre eski kimliklere dağıtmak kanıtlı eşleştirme sayılmadı.

Bu 260 kaydın durumu, bağlantıları ve kaynak hücreleri değiştirilmedi. Satır bazında aday kimlikleri [inceleme dosyasında](../../data/target-catalog/2026/lgs-review-r3-audit.json) bulunuyor. Bu dosyada bekleyen kısaltmalı okul adlarının adayları yalnız otomatik ad karşılaştırmasını gösterir; aşağıdaki resmî tam ad incelemesi ayrıca geçerlidir.

Güvenli sonraki adım: eski aktarım kaynağından okul/alan/dil anahtarlarını ve kurum kodunu getirmek. Bu mümkün olmazsa ayrı bir resmî kurum kimliği oluşturup eski kimlikleri koruyan açık bir ilişki modeli kurmak gerekir. Bu işte eski kimlikler birleştirilmedi, silinmedi veya rastgele yeniden bağlanmadı.

## Beş bekleyen kayıt

- `lgs-kayseri-mtal-research:1`: [resmî okul sitesi](https://kayserihavacilikuzaymtal.meb.k12.tr/) tam adı ve KAYSERİ/MELİKGAZİ konumunu doğruluyor. Tam adla başka mevcut okul kimliği bulunmadığı için bir yeni okul kimliği oluşturuldu ve mevcut kaynak satırı ona bağlandı. Kaynağın kısaltılmış adı ve boş ilçe hücresi değiştirilmedi; doğrulanmış ilçe bağlı okul kimliğinde bulunuyor.
- `:9`, `:10`: [Arif Molu MTAL](https://arifmolu.meb.k12.tr/38/15/216158/okulumuz_hakkinda.html), KOCASİNAN; kurum kodu 216158. Eski kimlikler `82644d21-c840-420e-8cc9-7cc70d6c131f` ve `8d2991c0-ad6b-4416-ba16-0c3f85d50ff2`. Hangisinin ilgili alan kaydını temsil ettiği kanıtlanamadı; iki kayıt bekliyor.
- `:12`, `:16`: [Türkiye Tekstil Sanayi İşverenleri Sendikası MTAL](https://ttvkayserimtal.meb.k12.tr/), MELİKGAZİ. Eski kimlikler `3fbad1ae-780b-44ef-8d19-7e516eb2f608` ve `68b12064-2550-4091-aa62-4d8e8b782292`. Alan bazındaki eski kimlik ayrımı kanıtlanamadı; iki kayıt bekliyor.

## İki kaynak kodu çelişkisi

Şile Ayet Azer Aran Savunma Sanayi MTAL için kaynaktaki `751600` kodu yanlış:

- [768129 resmî kaydı](https://mtegm.meb.gov.tr/kurumlar/?kurumkodu=768129&p=kurum): Şile Ayet Azer Aran Savunma Sanayi MTAL; ŞİLE. Endüstriyel Otomasyon ile Makine ve Tasarım alanları da kayıtta bulunuyor.
- [751600 resmî kaydı](https://mtegm.meb.gov.tr/kurumlar/?kurumkodu=751600&p=kurum): Sultangazi Atatürk MTAL.

İki kaydın `Issue` alanına doğru kod ve kanıt adresi yazıldı; `Conflict` → `Ambiguous` geçişi yapıldı. Özgün `ExternalCode`, okul adı, alan, dil, kontenjan ve bütün kaynak hücreleri aynen korundu. İki eski Şile okul kimliğinden biri seçilmedi. MTEGM sayfasındaki mevcut öğrenci ve pansiyon sayıları yıllık kontenjan olarak alınmadı.

## Yeni 2026 kontenjanları

| İl / ilçe | Okul | 2026–2027 toplam kontenjan | Resmî kaynak |
|---|---|---:|---|
| ESKİŞEHİR / TEPEBAŞI | Borsa İstanbul Fen Lisesi | 120 | [Okul bilgileri](https://bif.meb.k12.tr/tema/okulumuz_hakkinda.html) |
| ANKARA / BEYPAZARI | Hatice-Cemil Ercan Fen Lisesi | 90 | [Okul bilgileri](https://hcesb.meb.k12.tr/tema/okulumuz_hakkinda.php) |
| KONYA / ÇUMRA | Haydar-Fadim Kocaer Fen Lisesi | 60 | [Okul bilgileri](https://haydarfadimkocaerfenlisesi.meb.k12.tr/tema/okulumuz_hakkinda.html) |

Üç kayıt `Catalog`, yıl 2026 ve `Verified` olarak canlıya alındı. Birinci yabancı dil İngilizce, okul türü Fen Lisesi olarak resmî sayfalardan kaydedildi. Ankara sayfasındaki önceki yılların 120 kontenjanı kullanılmadı; 2026–2027 karşılığı olan 90 alındı. Üç sayfadaki eski puanlar ve başarı oranları aktarılmadı. Yeni puan/yüzdelik kaydı: **0**.

Doğrulanmış kayıt bulunan il sayısı 7'den 10'a çıktı. Bu, bu illerdeki bütün okulların tamamlandığı anlamına gelmez. Bursa, Samsun ve Antalya araştırmasında bulunan pansiyon başvuru/kontenjan sayfaları yıllık LGS okul kontenjanı yerine kullanılmadı.

## Koruma ve testler

- Canlıdan alınan yedek aynı sunucuda saklandı; aynı sunucuda ayrı veritabanına geri yüklenerek prova yapıldı. Tam veritabanı yerel bilgisayara indirilmedi.
- Prova ve canlı aktarımda tüm önceden mevcut okul satırlarının, önceki kaynak hücrelerinin, önceki kaynak partilerinin ve kapsam dışındaki katalog eşleşmelerinin sayı/özetleri karşılaştırıldı.
- Diğer bütün Coaching tablolarının satır sayısı ve içerik özetleri önce/sonra aynı kaldı. Hedefler, mevcut planlar, tamamlanan çalışmalar ve kazanım bağlantıları korundu. Identity üzerinde işlem yapılmadı.
- Paketlerin tekrar aktarımı yeni kayıt üretmedi. Kimlik incelemesinin tekrar çalışması bütün okul/kaynak kayıtlarının özetini değiştirmedi.
- Prova veritabanında kaynak ilini kasten yanlış yapan işlem reddedildi; işlem geri alındı ve bütün katalog özeti tekrar aynı çıktı.
- Python veri hazırlama/ayrıştırma testleri: **20 geçti**. Bozulan kanıt özeti ve yanlış yıl da reddediliyor.
- Canlı son kontrol: **704 kayıt = 438 Verified + 262 Ambiguous + 4 Pending**. Üç yeni okulun kontenjanları ve Kayseri bağlantısı doğrulandı. `Catalog` aşamasında puan veya yüzdelik bulunan satır: **0**.
- İlk prova paketinde Konya dosyasının konteynere kopyalanma yolu hatalıydı; canlıya yazmadan önce düzeltildi ve genişletilmiş prova yeniden geçti. Uygulama sürümü/şeması değiştirilmedi.

Kanıt kopyaları ve SHA256 manifesti: `data/target-catalog/2026/raw/lgs-review-r3-20261019/`.
Paketler: `data/target-catalog/2026/lgs-reviewed-r3-20261019/`.
Canlı iş klasörü: `/var/lib/eduivme/releases/lgs-review-r3-20261019/`.
Yerel test ve doğrulama günlükleri: `artifacts/lgs-review-r3-20261019/`.
