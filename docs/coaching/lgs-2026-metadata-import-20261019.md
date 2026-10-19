# 2026 LGS resmî katalog aktarımı — 19 Ekim 2026

## Durum ve kapsam

Türkiye geneli katalog tamamlanmış değildir. Erişilebilen resmî İstanbul İl MEM mesleki program tablosu işlendi; paket yerel PostgreSQL'e aktarıldı. Canlı uygulama/veritabanına bu çalışma kapsamında yazılmadı. Mevcut kullanıcı, plan ve çalışma kayıtlarına dokunulmadı.

Kaynak: https://istanbul.meb.gov.tr/meslek/bolum_analiz.php

Kaynak sütunları açıkça **2026 kontenjanı** ve **2025 taban puanı** olarak ayrılıyor. Yalnız 2026 meta verileri okundu. Eski puanlar, eski kontenjanlar ve OBP satırları aktarılmadı. Resmî kurum kodu korunuyor; alan/program/dil bileşimi için üretilen `ist:<kurum kodu>:<özet>` anahtarı resmî tercih kodu olarak sunulmuyor.

## Sonuçlar

| Kontrol | Sonuç |
|---|---:|
| LGS filtresindeki kaynak satırı | 324 |
| Tekilleştirilmiş program/alan/dil kaydı | 322 |
| Farklı resmî kurum kodu | 143 |
| Sayısal 2026 kontenjanı bulunan kayıt | 301 |
| Kontenjanı bilinmiyor bırakılan kayıt | 21 |
| Kimliği eşleşen kayıt | 281 |
| Aynı kurum kodunun farklı adlarla kullanılması nedeniyle ayrılan kayıt | 39 |
| Birden fazla eski okul kaydına uyan kayıt | 2 |
| Onaylı 2026 puan/yüzdelik | 0 |

Kontenjanı bilinmeyenlerin 15'i boş, beşi yıldızlı okul toplamı, biri çelişkili. Kaynağın dipnotu yıldızlı değerin okul idaresince alanlara dağıtılacak 9. sınıf toplamı olduğunu söylüyor. Her alana aynı toplam atanmadı. Selimpaşa Borsa İstanbul MTAL, Konaklama ve Seyahat Hizmetleri aynı kimlikle 30 ve 90 kontenjanla iki kez göründüğünden kontenjanı NULL kaldı. Diğer mükerrer satırın değerleri aynıydı; bir kez alındı.

39 kimlik çelişkisi sekiz kurum koduna aittir. Eski okul satırları birleştirilmedi/silinmedi. Şile Ayet Azer Aran Savunma Sanayi MTAL'nin iki alan kaydı eski katalogda birden fazla okul karşılığı bulunduğu için bağlanmadı. Kaynak kimliği belirsiz kayıtlar arama sonuçlarına çıkmaz.

## Veri ve dosyalar

- `data/target-catalog/2026/lgs-istanbul-metadata.json`: aktarım paketi; `Stage=Catalog`, yıl 2026; puan, tavan, yüzdelik, sıralama ve yerleşen sayısı NULL.
- `lgs-istanbul-manifest.json`: yedi kaynak sayfası, SHA256, indirme zamanı, kapsam ve sayımlar.
- `lgs-istanbul-review.json`: boş, ortak ve çelişkili kontenjanlar.
- `lgs-placement-review.json`: Menderes ve Karacabey resmî duyurularındaki dört sonuç adayı; yerleştirme aşaması eksik olduğundan onaylı aktarım kapalı.
- `artifacts/target-catalog-20261019/lgs-istanbul-final-match-review.json`: yerel prova kimlik inceleme listesi, 41 kayıt.

Paket başına kaynak URL'si ve belge özeti mevcut eklemeli modelde saklanır. Bu aşamada yeni sütun veya tablo gerekmedi. Katalog aşamasına puan/yüzdelik eklenmesini aktarıcı reddeder. Farklı alanların aynı yeni okul için mükerrer okul satırları açması önlendi. Aynı kaynak özeti altında değiştirilmiş veri veya yeni inceleme bulguları sessizce geçilmez.

## Koruma ve doğrulama

Son paket `coaching_target2026_test` yerel prova veritabanına aktarıldı, ikinci aktarım aynı parti kimliğiyle idempotent döndü. Önceden bulunan **3.010 okul ve 23.235 program kaydının** kimlikleri ve serileştirilmiş tüm alanları önce/sonra karşılaştırılarak değişmediği doğrulandı. Program sayısı önceki YKS provasındaki yeni programları da içerir; canlıdaki eski program sayısı değildir.

Gerçek PostgreSQL'de eklemeli geçişin önceki çalışma görevlerini değiştirmediği ve filtre sorgusunun çalıştığı test edildi. Canlı hesap ve planlar, canlıya yazılmadığı için etkilenmedi.

Son testler:

- Tam C# regresyonu, yerel PostgreSQL testleri dahil: **132/132 başarılı, atlanan yok**. Log: `artifacts/target-catalog-20261019/lgs-full-regression.log`.
- Python kaynak ayrıştırma ve karşılaştırma testleri: **10/10 başarılı**; altısı yeni LGS kontrolleri.
- Hedef kataloğunun alan modeli/aktarıcı/sorgu C# satır kapsamı: **%90,77 (187/206)**; tüm uygulamanın kapsamı değildir.
- Üretilen paket ayrıca UTF-8 bütünlüğü, 322 benzersiz kayıt, 39 kimlik inceleme kaydı ve sıfır puan aktarımı bakımından kontrol edildi.
- İlk geniş test denemesinde aynı çıktı dosyalarını kullanan ikinci test derlemesi kilit çakışmasına yol açtı. Göreve ait test süreci durduruldu; son tam regresyon tek süreçte, yeniden derleme olmadan başarılı çalıştı.

## Kalan resmî veri engeli

Ulusal yıllık liste: https://e-okul.meb.gov.tr/SinavIslemleri/BasvuruIslemleri/OKSTERCIH/SNV08008.ASPX

2026 resmî duyuruları bu ekrana yönlendiriyor; mevcut erişimde giriş sayfasına dönüyor. Güncel nakil ekranındaki boş kontenjanlar ve emsal puanlar 2026 ilk yerleştirme sonucu olarak kullanılmadı. Rota Maarif'in mevcut genel araması yıllık LGS dışa aktarımını sağlamadı; doğrudan servis çağrısı 403 verdiği için güvenlik engeli aşılmadı.

Ulusal tamamlanma için 2026 yılı açıkça belirtilen okul/kurum kodu, tercih/program/alan kodu, dil ve yıllık kontenjan içeren resmî liste gerekir. Puan/yüzdelik için ayrıca ilk yerleştirme, birinci nakil veya ikinci nakil aşaması satır düzeyinde doğrulanmalıdır. Bir haberin 2026'da yayımlanması tek başına bu aşamayı kanıtlamaz.

Bu eksik giderilmeden İstanbul mesleki katalog verileri Türkiye geneli katalog olarak sunulmamalıdır.
