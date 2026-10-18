# LGS onaylı kazanım aktarımı — 18.10.2026

## Sonuç

Canlı `coaching_db` veritabanına yalnız LGS için beş dersin **217 kazanımı**, **342 onaylı konu eşleştirmesi** ve **217 yayımlanmış çalışma süresi** kaydedildi. Matematikteki önceki 52 kazanım, 58 eşleştirme ve süre sürümleri değiştirilmedi.

| Ders | Eklenen kazanım | Eşleşen alt konu | Onaylı bağlantı | Toplam başlangıç çalışma tahmini |
|---|---:|---:|---:|---:|
| Fen Bilimleri | 61 | 22 | 61 | 5.280 dk |
| Din Kültürü ve Ahlak Bilgisi | 28 | 26 | 28 | 2.880 dk |
| T.C. İnkılap Tarihi ve Atatürkçülük | 39 | 39 | 39 | 2.880 dk |
| Türkçe | 35 | 22 | 111 | 3.315 dk |
| İngilizce | 54 | 23 | 103 | 4.445 dk |
| **Yeni toplam** | **217** | **132** | **342** | **18.800 dk** |

Canlıda Matematik dahil toplam **6 program, 269 kazanım, 269 yayımlanmış süre ve 400 onaylı eşleştirme** bulunuyor. Taslak süre, onaysız eşleştirme ve yayımlanmış kazanıma bağlanmamış aktif LGS alt konusu sayısı **0**.

Bir kazanım birden fazla konuya bağlanabildiğinden bağlantı sayısı kazanım sayısından büyüktür. Özellikle numaralı Türkçe konu bölümleri ortak kapsamı paylaşır; bunların özgün video bölüm içeriklerinin farklı olduğu iddia edilmedi. Plan motorunun kimlik üzerinden tekilleştirmesi her ders için test edildi.

## Kaynak ve sınav kapsamı

Kaynak karşılaştırması [kapsam teyidi raporunda](lgs-kapsam-teyidi-20261018.md) bulunuyor. Genel 8. sınıf için yeni bir ders kataloğu oluşturulmadı. Sınıf numarası yalnız kaynak metadata olarak 8; sınav türü her pakette `LGS`, geçerlilik eğitim yılı başlangıcı **2026**.

- [TTKB LGS konu, kazanım ve açıklamaları, 2022](https://ttkb.meb.gov.tr/meb_iys_dosyalar/2022_03/29162211_2022_LGS.pdf): Türkçe 35 ve İngilizce 54 kodun seçiminde kullanılan sınava özel kapsam referansı.
- [2026 merkezî sınav kılavuzu](https://odsgm.meb.gov.tr/meb_iys_dosyalar/2026_04/69cfb9daaf8f2557696260_LGS_Basvuru_ve_Uygulama_K%C4%B1lavuzu_2026.pdf): altı ders ve çoktan seçmeli sınav yapısı.
- [TEGM 2026–2027 program uygulama açıklaması](https://tegm.meb.gov.tr/www/2026-2027-egitim-ogretim-yili-taslak-cerceve-planlar-yayinlandi/icerik/1316): yeni programın ortaokul 5, 6 ve 7. sınıflarda uygulanması.
- [ÖDSGM güncel LGS çalışma kitapları açıklaması](https://odsgm.meb.gov.tr/www/liselere-giris-sinavi-kapsaminda-yapilacak-merkezi-sinav039a-hazirlanan-ogrencilere-yonelik-dort-calisma-kitabi-seti-yayimlandi/icerik/1585/).

Özgün kazanım metinleri, SHA256 ile doğrulanan resmî Fen (PID 325), Din Kültürü (318), İnkılap (355), Türkçe (663) ve İngilizce (327) program PDF'lerinden alındı. Her program kaydında kaynak adresi ve belge özeti; her kazanımda özgün kod ve program PDF sayfası saklandı. MEBİ kazanım UUID'leri uydurulmadı.

2022 belgesi 2027 tarihli yeni bir karar olarak sunulmadı. 2026 kılavuzu da 2027 kılavuzu değildir. Yeni resmî sınav kapsamı yayımlanırsa mevcut sürüm değiştirilmeden ayrıca değerlendirilmelidir. Otomatik yayımlama sonraki eğitim yıllarına taşınmaz; 2027 eğitim yılı başlangıcı için bu paketlerin sonuç döndürmediği test edildi.

Türkçe genel programındaki 76 kazanımın tamamı alınmadı: yalnız `T.8.3.5`–`T.8.3.35` ve `T.8.4.16/18/19/20` alındı. Genel şiir/hikâye yazma, konuşma/dinleme için LGS konusu eklenmedi. İngilizcede genel programın 70 kazanımı yerine sınava özel listede bulunan 54 kod alındı. İngilizce resmî beceri adları korunurken notlarda LGS'nin yazılı metin/diyalog ve çoktan seçmeli çalışma kapsamı açıklandı; ayrıca proje, poster veya sözlü performans ödevi oluşturulmadı.

## Konu eklemesi

Yalnız Din Kültürü / Hz. Muhammed’in Örnekliği altında **“Hz. Muhammed’in Örnek Davranışları ve Hikmetli Sözleri”** alt konusu eklendi. Resmî `8.4.7` ve `8.4.8` kazanımları buraya bağlandı. Mevcut 25 Din Kültürü alt konusu ve diğer derslerin bütün konu kimlikleri korundu.

## Sürelerin anlamı

**Yayımlanmış/onaylı olması, bireysel öğrenme dakikalarının MEB tarafından ölçüldüğü anlamına gelmez.** Süreler `ReviewedStudy`, güven düzeyi `Low`, yayımlanmış durumunda saklandı. İlk sürüm numarası 1; taslak başlangıç sürümü oluşturulmadı. Mevcut Matematik süre sürümlerine dokunulmadı.

- Fen: resmî alt konu bütçelerinin toplamı 132 ders saati. Kazanım kodu bulunmayan 12 ders saatlik bilim uygulaması bütçesi eklenmedi.
- Din Kültürü ve İnkılap: her dersin resmî ünite bütçesi toplam 72 ders saati.
- Türkçe ve İngilizce: resmî kazanım/konu bazında süre bulunmadığından [Mayıs 2025 Tebliğler Dergisi PDF 6](https://dhgm.meb.gov.tr/meb_iys_dosyalar/2025_05/14103336_mayistebligler.pdf) genel haftalık ders çizelgesi bir **referans** olarak kullanıldı. Türkçe 5, yabancı dil 4 saat/hafta; 36 hafta model varsayımıdır. Genel yıllık bütçeden seçili kazanım oranı (35/76 ve 54/70) ayrıldı. Bu işlem resmî LGS konu süresi kararı değildir.
- Ders saati başına 40 dakika, grup içi eşit paylaştırma, toplamı koruyan 5 dakika adımları; yaklaşık %40 öğrenme, %50 alıştırma/soru, %10 kontrol. Bu dağılım model varsayımıdır.
- Video süresi ve mola süreleri kullanılmadı. Her sürenin kanıt alanında kaynak, varsayım ve sınırlamalar kayıtlı.

## Korunan veriler

| Veri | Aktarım sonrası | Doğrulama |
|---|---:|---|
| Çalışma planı | 9; 1 aktif | Eski satırların tüm içerik özetleri aynı |
| Çalışma görevi | 681 | Eski satırların tüm içerik özetleri aynı |
| Tamamlanan görev | 6 | Tamamlama bilgileri değişmedi |
| Kaydedilmiş çalışma süresi | 115 dk | Değişmedi |
| Plan–kazanım–süre bağlantısı | 676 | Eski satırların tüm içerik özetleri aynı |
| Hesap/rol/ürün erişimi | Korundu | Identity karşılaştırması geçti |

Sadece bu tablolara bakılmadı: audit/outbox gibi işlem kayıtları dışında Coaching şemasındaki **bütün eski satırların** içerik hash'leri aktarım sonrasında tekrar bulunarak kontrol edildi. Önceki Matematik katalog kayıtları, profiller, öğrenme bağlamı ve haftalık taslak da bu karşılaştırmaya dahil. Yeni kazanımlar eski planları geriye dönük değiştirmedi. Uygulama konteyneri yeniden dağıtılmadı; veritabanı şema geçişi gerekmedi.

## Testler

1. Python kapsam/paket testleri: **6/6 geçti**, aktarım paketini hazırlayan modülün satır kapsamı standart Python `trace` ölçümünde **%91**.
2. Aktarım aracı canlıda çalışan Domain/Infrastructure derlemeleriyle derlendi; dosya SHA256 eşleşmeleri sunucuda doğrulandı.
3. Canlı yedeğinden ayrı veritabanı oluşturuldu. Beş paketin tamamı tek işlemde aktarıldı. Hata durumunda işlem geri alındı; eski satırların değişmediği doğrulandı.
4. Her alt konuda yayımlanmış kazanımların okunması, her dersin kazanımlarının tekilleştirilmesi ve sonraki eğitim yılına kapsam sızmaması test edildi.
5. Canlı sürümün öğrenci plan önizleme servisiyle her dersin bir yayımlanmış kazanımı denendi. Planlanan + sığmayan dakika toplamı seçilen süreye eşit çıktı. Yeni öğrenci planı/çalışması kaydedilmedi.
6. Yinelenen kazanım kodlu paketin reddedildiği doğrulandı.
7. Prova aktarımı ikinci kez çalıştırıldı: **0 yeni kazanım**, tüm veri satırları aynı.
8. Aynı paket canlıya tek işlemde kaydedildi; canlı tekrar aktarımında **0 yeni kazanım**, tüm veri satırları aynı.
9. Son canlı sorgu: **269 yayımlanmış süre, 0 taslak, 400 onaylı bağlantı, 0 onaysız bağlantı, 0 eşleşmemiş aktif LGS alt konusu**.

Aktarım aracı için provada bulunan .NET 10 / EF koleksiyon sorgusu uyumsuzluğu `Enumerable.Contains` kullanılarak giderildi. Canlı aktarım, bu düzeltmeden sonraki başarılı prova ile yapıldı. NuGet bağımlılıkları mevcut önbellekten çözüldü; araç için üretim uygulamasının bağımlılıkları değiştirilmedi.

## Dosyalar ve operasyon kanıtları

- Paketler: `data/coaching-outcomes/lgs-other-2026/`.
- Hazırlama aracı ve testleri: `tools/coaching-outcomes/prepare_lgs_other.py`, `test_prepare_lgs_other.py`.
- Tek işlemli aktarım ve doğrulama aracı: `tools/releases/coaching-lgs-other-20261018/`.
- Yerel test özetleri: `artifacts/coaching-lgs-other-20261018/live-result.json`, `live-replay-result.json`, `drill-result.json`, `drill-replay-result.json`, `coverage-summary.txt`.
- Sunucudaki korumalı yedek ve kanıt dizini: `/var/lib/eduivme/releases/coaching-lgs-other-20261018/`. Identity ve Coaching yedekleri, aktarımın hemen öncesindeki ek Coaching yedeği ve dosya özetleri burada tutulur. Anahtarlar ve bağlantı bilgileri rapora/yerel test çıktısına alınmadı.

Bu görev katalog aktarımıdır; yeni öğrenci planının oluşturulması veya mevcut planın yeni kazanımlarla yeniden üretilmesi yapılmadı.
