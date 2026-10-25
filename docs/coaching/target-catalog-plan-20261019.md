# Hedef kataloğu denetimi ve geliştirme planı — 19 Ekim 2026

## Kapsam ve sonuç

Canlı PostgreSQL kataloğu iki salt okunur işlemle incelendi. Uygulama verisi, şema, aktiflik veya hedef ilişkisi değiştirilmedi. Bu belge uygulama/aktarım onayı yerine geçmez; geliştirme planıdır. Ham SELECT sonuçları: [denetim çıktısı](target-catalog-audit-20261019.txt).

Katalog kullanılabilir bir başlangıç içeriyor; güncel ve kaynağı doğrulanmış tercih kataloğu olarak henüz yeterli değil. Bütün taban puanların yılı belirsiz. Resmî liste bütünü indirilip satır bazında karşılaştırılmadığından, eksik okul/program adedi veya ülke kapsam yüzdesi henüz hesaplanmış değildir.

## Canlı envanter

| Ölçüm | LGS okul kataloğu | YKS program kataloğu |
|---|---:|---:|
| Toplam / aktif | 3.010 / 3.010 | 21.602 / 21.602 |
| Kaynak etiketi | approved-catalog | approved-catalog |
| Puan yılı boş | 3.010 | 21.602 |
| Taban puan boş | 0 | 0 |
| Taban puan sıfır | 0 | 779 |
| Doğrulanmış il/ilçe kimliği eksik | 3.010 | Modelde alan yok |
| Kapsam | 81 farklı il metni | 227 farklı üniversite adı metni |
| Program kodu eksik / tekrar | Modelde resmî kod yok | 0 / 0 |

227 adı doğrudan 227 doğrulanmış resmî kurum olarak yorumlamıyoruz. Puan türleri: TYT 9.337, SAY 5.653, EA 3.987, SÖZ 1.948, DİL 677. Sıfır puanlar sırasıyla 149, 389, 145, 78, 18. DİL programı sayısı, İngilizce YDT programı sayısı değildir; program eğitim dili ile adayın YDT dili farklı kavramlardır.

455 okul adı+il+ilçe grubunda birden fazla kayıt mevcut. Bu sayı mükerrer satır sayısı değildir. Alan, dil veya kabul programı ayrımı olabilir; mevcut model bu ayrımı temsil etmiyor. Otomatik silme/birleştirme yapılmayacak.

14 akademik hedef var; şu anda katalog okul/program yabancı anahtarına bağlı hedef sayısı 0/0. Yine de bütün hedefler ve katalog Id değerleri korunacak. Başka API/istemci referansları ve denetim geçmişi de uygulamada taranacak.

## Mevcut yapının sınırları

`target_schools`: ad, şehir/ilçe metni ve opsiyonel kimlikleri, tek taban puan/yıl, kaynak/dış kimlik, aktiflik. Resmî okul kodu, kabul programı, dil, okul türü, yüzdelik dilim, kontenjan ve yıllık sonuç geçmişi yok.

`target_university_programs`: üniversite adı, program adı/kodu, puan türü, tek taban puan/yıl, kaynak/dış kimlik, aktiflik. Ayrı kurum kimliği, derece, fakülte, yerleşke, dil, burs, ücret kategorisi, kontenjan, sıralama ve yıllık sonuç geçmişi yok.

`academic_goals`: hedef sahibi/öğretmen/kurum, kategori, sınav, metin ders, puan ve ölçeği, tarih, ilerleme/tamamlanma ve tek lise veya program bağlantısı mevcut. Mevcut FK silme davranışı Restrict; korunmalı.

Kaynak etiketi approved-catalog doğrulama tarihi veya resmî kaynak belgesi değildir. SourceId örnekleri UUID; bunları MEB okul kodu olarak kullanamayız. Mevcut puanların hepsine tahminen 2025/2026 yazılmayacak. Program kodunun varlığı da güncel aktif program olduğunu tek başına kanıtlamaz.

Arama servisi bugün okul adı/şehir/ilçe ve program adı/üniversite/kod/puan türü arıyor. Güncel yıl, derece, dil, burs, sıra/yüzdelik ve veri doğrulama filtresi bulunmuyor. Kaynak kod: CoachingTargetSearchService, TargetCatalogController, TargetCatalogConfigurations, TargetCatalog.cs.

Mevcut CoachingCatalogImporter tek pakette ders/konu ve okul/program dosyaları istiyor. Aynı kaynak kimliğinde içerik değişirse Stage metodu çakışma hatası veriyor; güncelleyici değildir. Yeni resmî kaynağı farklı Source ile tekrar eklemek de aynı kurum/program için ikinci kayıt yaratabilir. Bu nedenle eski tam müfredat paketini tekrar çalıştırmak yerine hedeflere özel önizleme/onaylı aktarım geliştirilmeli. Mevcut silinmiş kaynak kimliği korumaları korunmalı.

## Resmî kaynaklar ve elde etme yolu

### LGS

- [MEB e-Okul ana duyuruları](https://eokul.meb.gov.tr/logineOkul.aspx): güncel yıl ve yerleştirme aşaması etiketini kontrol et.
- [Merkezî tercih rehberi](https://e-okul.meb.gov.tr/SinavIslemleri/BasvuruIslemleri/OKSTERCIH/MerkeziTercih.ASPX): kurum/kabul seçeneği, il/ilçe, dil/alan ve kontenjan için esas liste adayı.
- [Sınavlı okul taban puan/boş kontenjan ekranı](https://e-okul.meb.gov.tr/OrtaOgretim/OKL/OOK06006.aspx): aşamaya bağlı sonuçları doğrulama.
- [2026 resmî bağlantı dizini](https://yalova.meb.gov.tr/www/2026-yili-ortaogretim-kurumlarina-iliskin-merkezi-sinav/icerik/5788/): rehber, kılavuz ve önceki yıllara erişim. Sayfada bir yerleştirme tarihinin yıl hatası var; bağlantıdaki dosya ve başlık ayrıca kontrol edilmeli. Sınav sonuç tarihi ile yerleştirme sonucu tarihi karıştırılmamalı.

Bu incelemede e-Okul tercih ekranı araştırma aracına 403 döndürdü. Açık, belgelenmiş toplu API doğrulanmadı. Resmî indirilebilir XLS/CSV/PDF varsa onu kullan; yoksa erişime izin verilen arayüzden dosya dışa aktarımı veya resmî veri temini. Giriş/CAPTCHA erişim engelleri aşılmayacak. Sayfa kaynağındaki tarih etiketinden tek başına veri yılı çıkarılmayacak.

İlk kapsam merkezî sınavla kabul edilen LGS seçenekleri. Yerel yerleştirme okulları puanlı LGS seçenekleriyle karıştırılmayacak; daha sonra ayrı kabul türüyle genişletilebilir. Önce en son doğrulanabilir tamamlanmış yerleştirme yılı; ardından erişilebilen önceki 2 yıl. Her yıl/aşama ayrı kayıt.

### YKS

- [ÖSYM 2026 programlar ve kontenjan kılavuzu](https://www.osym.gov.tr/2026-yuksekogretim-kurumlari-sinavi-yks-yuksekogretim-programlari-ve-kontenjanlari-kilavuzu): resmî sayfa Tablo-3 ve Tablo-4 için XLS bağlantıları sunuyor.
- [Tablo-3 XLS](https://dokuman.osym.gov.tr/web/2026/7/tablo-3-29u1s7pl.xls): ön lisans programlarını makineyle ayrıştırmak için ilk kaynak.
- [Tablo-4 XLS](https://dokuman.osym.gov.tr/web/2026/7/tablo-4-hohu0j-30164357.xls): lisans programları için ilk kaynak.
- [YÖK Atlas](https://yokatlas.yok.gov.tr/): program/kurum ve geçmiş başarı sıralarını çapraz kontrol etme kaynağı. Bu incelemede doğrudan erişim zaman aşımına uğradı; güncel bütün Atlas satırları alınmış sayılmaz.

XLS bağlantıları resmî sayfada doğrulandı; bu turda dosyaların tüm içeriği ayrıştırılmadı. İndirme aşamasında dosya başlığı, sütun açıklamaları, sürüm ve SHA256 kaydedilmeli. Kılavuz yılı, kontenjan yılı ve geçmiş taban puan/sıralamanın sonuç yılı farklı olabilir. Örneğin 2026 kılavuzundaki önceki yıl sonuç sütunları 2026 sonucu diye kaydedilemez. Ana yerleştirme ve ek yerleştirme de ayrı tutulmalı.

ÖSYM kurum/program kimliği ve kabul koşullarında birincil; YÖK Atlas yıllık sıralama ve program incelemesinde çapraz kaynak. Çelişkiler alan bazında raporlanıp onay bekler; otomatik son yazan kazanır yapılmaz. Üniversite siteleri program adı/dil açıklaması için destekleyici olabilir; resmî taban sonuçların yerine geçmez.

## Önerilen veritabanı — Coaching sınırı içinde

İlk fazda yeni servis veya bağımsız tercih ürünü gerekmez. Mevcut katalog Id değerlerini kanonik iç kimlik olarak koruyarak ek tablolar yeterli.

| Tablo / değişiklik | Alanlar ve amaç |
|---|---|
| target_schools genişletme | Nullable resmî MEB kurum kodu, okul türü, doğrulama durumu, doğrulanma tarihi. Mevcut konum alanlarını resmî il/ilçe diziniyle doldurma. |
| target_school_offerings | Id, SchoolId, resmî kabul/tercih kodu varsa, alan/program, eğitim dili, hazırlık süresi, kabul türü, pansiyon/cinsiyet koşulları varsa. Aynı okulun farklı kabul seçeneklerini ayırır. |
| target_universities | Id, resmî kurum kodu, ad, tür, ülke ve il kimliği. Ad içindeki parantezden kesin konum türetilmez. |
| target_university_programs genişletme | Nullable UniversityId, derece, fakülte/MYO, eğitim dili, süre, öğrenim biçimi, burs/ücret kategorisi, yerleşke, doğrulama durumu. Ücret tutarı ilk aşamada zorunlu değil. |
| target_admission_cycles | Id, sınav kodu, kontenjan/katalog yılı, yerleştirme sonuç yılı, aşama, ilgili kaynak belgesi. Alanların yıl anlamını açık tutar. |
| target_school_admission_results | OfferingId, CycleId, kontenjan türü, kontenjan/yerleşen/boş sayısı, taban/tavan puan ve yüzdelik, sonuç durumu, kaynak batch kimliği. |
| target_program_admission_results | ProgramId, CycleId, kontenjan türü, kontenjan/yerleşen/boş sayısı, taban/tavan puan, taban başarı sırası, sonuç durumu, kaynak batch kimliği. |
| target_catalog_sources / import_batches | Kaynak kurum, URL, dosya adı/hash, yayın/erişim tarihi, veri kapsamı/yılları, ayrıştırıcı sürümü, durum, inceleyen/onaylayan, gerekçe ve sayım özeti. |
| target_catalog_external_keys | Kaynak sistemi + varlık türü + dış kod → mevcut iç Id. Birden fazla kaynağı aynı kayıtla ilişkilendirir; eski Source/SourceId korunur. |
| target_import_rows / issues | Yalnız aktarıma gerekli ham satır, normalleştirilmiş aday, eşleşme, hata/çelişki ve onay kararı. Kaynak dosya tekrar indirilemese de denetlenebilir. |

Koşul kodları birden fazla olabileceği için program/teklif ile kaynak yılına bağlı ayrı ilişki kayıtlarında tutulmalı. Ayrıntılı kılavuz metni kaynak belgesine bağlanmalı; her programa aynı metin kopyalanmamalı.

Sıra pozitif tamsayı; puan decimal(10,4); yüzdelik decimal(7,4), 0–100 kontrolü. Kontenjanlar negatif olamaz. Bilinmeyen değer NULL; NotPublished/NotFilled/NewProgram gibi kaynakla doğrulanan sonuç durumları ayrı tutulur. 779 eski sıfır kaynak görülmeden NULL'a çevrilmez. Sıralama puandan hesaplanmaz; puan türlerinin tavanını varsayarak 500 üstü YKS puanları silinmez.

Resmî kodlar doğrulanana kadar nullable; doğrulanmış kodlarda uygun kapsamda unique index. Aynı kurumun çoklu LGS kabul seçenekleri için kurum kodu tek başına sonuç anahtarı değildir. Sonuç tekilliği: kabul seçeneği/program + döngü + kontenjan kategorisi; düzeltme sürümleri denetimle tutulur. Döngü içindeki iki doğrulanmış sonuç çelişirse aktarım durur. FK'lerde Restrict; öğrenci hedeflerine cascade silme yok.

SÖZ/DİL kaynak metinleri SOZ/DIL kanonik kodlarına açık eşleme ile dönüştürülmeli. SAY/EA/SOZ AYT sınav oturumunun hedef puan türleri; TYT ve DIL ile birlikte program filtrelemesinde kullanılır. Eğitim dili İngilizce olmayan program, aday YDT İngilizce diye otomatik dışlanmaz. Arapça için yeni sınav/müfredat kaydı oluşturulmaz; hedef katalog kapsamı İngilizce YDT ürün kararına göre gözden geçirilir, adında dil geçiyor diye mevcut katalog satırı silinmez.

Mevcut MinimumScore/ScoreYear ilk geçişte yerinde kalacak. Eski API'nin davranışı korunur; yeni API doğrulanmış sonucun puanı/yılı/aşamasını açıkça döndürür. Yılı belirsiz eski puanlar history tablosuna uydurma yıl verilerek taşınmaz. Arayüz bunları 'yılı doğrulanmamış eski veri' olarak ayırmalı; güncel sıralama önerisinde kullanmamalı.

## Eşleştirme ve aktarım akışı

1. Kaynak dosyayı sürümleyerek al, hash ve kapsam manifesti üret. Sayı/yıl/aşama/sütun açıklamalarını denetle.
2. Canlı yazmadan staging satırlarını ayrıştır. Türkçe harf/boşluk normalleştirmesi arama içindir; kaynak metni korunur.
3. YKS önce tam program koduyla mevcut kayda bağlanır. Üniversite, puan türü ve ad farkları ayrıca incelemeye düşer; aynı kodda kapsam değişimi sessizce kabul edilmez.
4. LGS önce doğrulanmış kurum+teklif kodu; eski kayıtta kod yoksa ad+il+ilçe+alan/dil kanıtıyla aday eşleştirme. 455 çoklu grup ve birden fazla aday insan onayına gider. Bulanık ad benzerliği tek başına güncelleme yetkisi vermez.
5. Önizleme: yeni, değişen, aynı, eşleşmeyen, çoklu aday, puanı/yılı çelişen ve kaynakta bulunamayan kayıt listeleri. Eksik okul/program sayısı ancak aynı kapsam/yıl üzerinde bu aşamada kesinleşir.
6. Onaylanan parti küçük ve geri izlenebilir işlemle kaydedilir; tekrar aynı hash aktarımı yeni satır üretmez. Katalog özelinde kilit ve optimistic concurrency kullanılır. Öğrenci/plan tabloları kilitlenmez.
7. Kaynakta bulunamayan eski kayıt otomatik silinmez/pasifleşmez. Kapanma veya kapsam dışında kalma doğrulanırsa yeni seçimlerden kaldırılır; eski hedeflerde okunmaya devam eder.
8. İşlem sonunda yeni/değişen sayıları, bütünlük, eski Id/FK'ler ve korunacak kayıt hash'leri kontrol edilir.

## Servis ve ekran işleri

- Hedeflere özel ImportReview/Approve akışı: yıl/aşama/kanıt, fark listesi, eşleştirme kararı, doğrulama ve onay. Mevcut ders kataloğundan bağımsız paket; var olan denetim/silinmiş dış kimlik korumasıyla uyumlu.
- Arama cevapları: kurum/program/teklif kimliği, doğrulama durumu, sonuç yılı/aşaması, puan, sıra/yüzdelik, dil, derece, burs ve kaynak özeti. Eski sözleşmeyi kırmadan yeni sürüm veya ek alanlar.
- Yönetici: LGS/YKS sekmeleri; kaynağı/yılı eksik, çoklu eşleşme, sıfır puan ve resmî listede bulunamayan filtreleri; yıllık geçmiş ve aktarım önizlemesi.
- Öğrenci/öğretmen: LGS'de okulun kabul seçeneği; YKS'de gerçek program kodu seçilir. Geçmiş sonuç yılı görünür. YKS sıra, LGS yüzdelik karşılaştırması; 'kesin kazanır' sonucu üretilmez.
- Hedefe SchoolOfferingId ve referans sonuç Cycle/Result kimliği opsiyonel eklenir. Mevcut TargetSchoolId/TargetUniversityProgramId aynen kalır. Birden fazla tercih ve net hedefleri sonraki hedef geliştirme fazıdır; bu katalog aktarımının önkoşulu değildir.

## Uygulama sırası ve kabul koşulları

### A — Model ve koruma

Yedek al/geri yükleme provası; hedef, katalog, plan/çalışma/kazanım ilişkileri için sayım+hash kaydet. Additive migration ile nullable alan/yeni tablolar ekle. Eski uygulama ile uyumluluk testi. Mevcut 14 hedef ve katalog kimlikleri değişmemeli.

### B — LGS

Resmî kaynak erişimini netleştir, en güncel doğrulanabilir yıl ve aşamayı al. Kurum/teklif ayrımı ve 81 il eşleştirmesi; 455 grup için karar listesi. Onaylı satırların kaynak/yıl/aşama bilgisi yüzde 100 dolu olmalı; belirsizler yayımlanmamalı. Resmî kapsam sayımı ile eksik/fazla liste tamamlanmalı.

### C — YKS

ÖSYM Tablo-3/4 dosyalarını al; mevcut 21.602 kodla karşılaştır. Kurum, lisans/önlisans ve puan türü kapsamlarını doğrula. YÖK Atlas erişilebildiğinde yıllık sıralamaları çapraz kontrol et. 779 sıfır için kaynak sonucu/durumu belirle. Kod değişikliklerini alias ile izlenebilir tut. Kontenjan yılı ile geçmiş sonuç yılı ayrı doğrulanmalı.

### D — Yönetim ve hedef seçimi

Yeni katalog filtreleri/kanıtları, öğrenci ve öğretmen hedef seçimi, eski hedeflerin açılması ve pasif programın eski hedefte görüntülenmesi. Belirsiz eski verinin güncel öneriye karışmaması.

### E — Test ve yayın

Ayrıştırıcı fixture testleri: Türkçe ondalık/nokta, eksik sütun, dipnot, sıfır, boş değer ve tarih yılı ayrımı. Entegrasyon: FK/unique/range, tekrar aktarım, çelişkili kaynak, yarıda rollback, çift onay, silinmiş kaydı diriltmeme. 455 çoklu grup için otomatik yanlış birleştirme olmadığını doğrula. Öğretmen yalnız atanmış öğrencinin hedefini düzenleyebilmeli.

Prova veritabanında tam aktarım ve karşılaştırma geçince canlı onaylı parti yayımlanır. Canlı doğrulama LGS/TYT/SAY/EA/SOZ/DIL hedef arama/seçme, yıl/aşama gösterimi, eski hedef açma ve plan kayıtları korunmasını kapsar. Bu tur yalnız denetim yapıldığı için bu gelecekteki testler çalıştırılmış sayılmaz.

Geri dönüş: yeni parti görünürlüğünü geri al, önceki onaylı partiye dön; yeni hedef bağlantılarını kıracak DROP/Down yapılmaz. Tam veritabanı restore yeni öğrenci kayıtlarını kaybettirebileceğinden rutin geri dönüş yolu değildir.

## Öncelikli karar

Önce veri yılı/kaynak ve LGS kurum–kabul seçeneği ayrımı çözülmeli. Sonra resmî kod eşleştirmesi ve yıllık sonuç tabloları tamamlanmalı. Mevcut puanlara topluca yıl atama veya kataloğu silip yeniden yükleme yapılmamalı. Hedef ölçümleri ve otomatik ilerleme bu güvenilir katalog üzerine eklenebilir.
