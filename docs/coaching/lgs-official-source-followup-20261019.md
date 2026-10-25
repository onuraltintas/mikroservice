# LGS 2026 — ek resmî kaynak araştırması

19 Ekim 2026. Bu tur kaynak araştırmasıdır; uygulama kodu, canlı katalog ve ilişkiler değiştirilmedi. 2025 ve öncesi için veri aktarımı yapılmadı.

## Bulunan kaynaklar

| Kaynak | Elde edilebilen bilgi | Katalogda kullanım kararı |
|---|---|---|
| Menderes İlçe MEM, 17 Ağustos 2026 duyurusu | İki okul için 2026 başlığı altında taban puan, yüzdelik, dil ve süre | Yıl/alan tutarlılığı incelemesi gerekli; henüz aktarılmamalı |
| Karacabey MTAL, 1 Ekim 2026 duyurusu | İki LGS alanı için 2026 taban puan, kontenjan ve yüzdelik | Resmî yerel aday kaynak; kesin yerleştirme aşaması ve kabul kimliği eksik |
| İstanbul İl MEM Bölüm Analizi | Kurum kodu, ilçe, okul, program, alan, dil, yerleşme türü ve 2026 kontenjanı; toplam 1.387 karma LGS/OBP satırı | 2026 okul/alan/kontenjan metaverisi için uygun aday; taban puan sütunu 2025 olduğundan 2026 puanı olarak kullanılamaz |
| ÖDSGM 2026 ilk yerleştirme sonuç raporu | Ulusal toplam ve dağılım istatistikleri, yerleştirme takvimi | Genel sayım/kapsam doğrulaması için; okul bazında puan listesi değil |

### Menderes: resmî sayfa da alan bazında doğrulanmalı

[Resmî duyuru](https://menderes.meb.gov.tr/www/menderes-liselerinin-lgs-basari-tablosu-ve-taban-puanlari-belli-oldu/icerik/1720/tr) normal tarayıcıda açılarak satırlar doğrulandı:

| Okul | Duyurudaki puan | Duyurudaki yüzdelik |
|---|---:|---:|
| Menderes Fatma-Ramazan Büküşoğlu Anadolu Lisesi | 415,685 | 8,28 |
| Şehit Kaymakam Muhammet Fatih Safitürk Anadolu İmam Hatip Lisesi | 311,926 | 44,31 |

Karşılaştırmada [LGS Puan okul sayfası](https://lgspuan.com.tr/lise/menderes-fatma-ramazan-bukusoglu-anadolu-lisesi), 8,28 değerini 2025'e bağlıyor ve 2026 için farklı bir yüzdelik veriyor. [HamPuan'ın İzmir tablosu](https://hampuan.com/izmir-lise-taban-puanlari/) da ilgili eski yüzdeliği 2025 sütununda gösteriyor. Bunlar resmî düzeltme kanıtı değildir; **resmî duyuruda 2026 puanı ile farklı yılın yüzdeliğinin karışmış olabileceğine dair inceleme işaretidir**. İkincil sitelerdeki alternatif yüzdelikler resmî değer yerine alınmadı. İki satır da onaylanmış aktarım dışında kalmalı.

İl içi okul sıra konumu öğrencinin Türkiye başarı sırası değildir; duyurudaki 32/137 gibi okul konumları öğrenci hedef sıralaması alanına taşınamaz. Sayfada kontenjan veya resmî kabul kodu bulunmuyor.

### Karacabey: küçük ama açıkça 2026 etiketli kaynak

[Resmî okul duyurusu](https://karacabeyeml.meb.k12.tr/icerikler/2026yilindaokulumuzayerlesenogrencilerintabanvetavanpuanlari_17719793.html) LGS ve OBP alanlarını ayrı başlıklarla veriyor. Yalnız LGS bölümündeki aday satırlar:

| Alan | Kontenjan | Taban puan | Yüzdelik |
|---|---:|---:|---:|
| Endüstriyel Otomasyon Teknolojileri | 30 | 264,76 | 48,43 |
| Makine ve Tasarım Teknolojileri | 30 | 219,14 | 63,62 |

Kaynak yılı açık olsa da ilk yerleştirme/1. nakil/2. nakil ayrımı ve kesin kabul kodu açıklanmıyor. Yuvarlanmış puanlar ülke çapındaki tam hassasiyetli sonuç yerine geçirilmemeli. Yerel aday olarak saklanabilir; ulusal listenin doğrulandığı anlamına gelmez. OBP satırları LGS verisine alınmaz.

### İstanbul: kontenjan yılı ve puan yılı farklı

[Resmî bölüm analizi](https://istanbul.meb.gov.tr/meslek/bolum_analiz.php) sayfası 2026 kontenjanını açıkça ayrı, taban puanı ise 2025 sütununda gösteriyor. Kurum+alan+dil eşleştirmesi ve filtre seçeneklerini tamamlamaya yarar. 1.387 satırın tamamı LGS değildir; yerleşme türü filtresi şarttır. Yıldızlı kontenjanlarda dipnot/ortak kontenjan olasılığı çözülmeden satırları toplayarak okul toplamı üretilemez.

## Ulusal kaynak erişim kontrolü

- [Rota Maarif'in açılış duyurusu](https://iegb.meb.gov.tr/lgs-tercih-surecinde-rehber-olacak-rota-maarif-erisime-acildi/haber/41334/tr) LGS bilgilerini sunduğunu açıklıyor. Tercih sitesinde yayımlanmış doğrudan resmî `/Home/Lgs` bağlantısı da bulundu ve kontrol edildi: **404**. Bu adres tahmin edilmedi; yayımlanmış bağlantıdan alındı.
- [e-Okul alternatif resmî ana sayfası](https://eokulyd.meb.gov.tr/logineokul.aspx) arama indeksinde Ağustos duyurusu içerse de güncel içerikte yıllık tablo bağlantısı artık görünmüyor. İndeks özeti güncel dosya varlığı sayılmadı.
- Daha önce kontrol edilen yıllık `SNV08008.ASPX` bağlantısı giriş sayfasına dönüyor; mevcut `OOK06006.aspx` ekranı güncel nakil/emsal puan gösteriyor.
- [2026 ilk yerleştirme sonuç raporu](https://odsgm.meb.gov.tr/meb_iys_dosyalar/2026_08/6a72dd74c263a920303943_lgs_2026_yerlestirme_raporu_kamuoyu_0508.pdf), 30 sayfa: içindekiler ve tablo dizini incelendi. Ulusal istatistik raporu; bütün okulların taban/tavan puan ve yüzdeliklerini içeren satır listesi yok.
- Kahramanmaraş'ın 2026 tercih rehberinde geçmiş yıl tabloları var; 2026 yerleştirme sonuçları olarak kullanılmadı.

## Sonuç ve uygulanabilir yol

**Ülke genelini kapsayan, yılı ve yerleştirme aşaması açık, indirilebilir resmî 2026 LGS sonuç listesi bu araştırmada da doğrulanamadı.** Buna karşılık yerel resmî kaynaklarla kısmi tamamlama yapılabileceği doğrulandı.

İki ayrı veri işi olarak devam etmek doğru olur:

1. Kurum kodu, okul/alan, dil ve kontenjan metaverisini resmî yerel kataloglardan, 2026 etiketiyle toplamak.
2. Taban puan/yüzdelik için resmî yıllık sonuç veya açıkça aşaması belirtilmiş yerel sonuç istemek; eksik veya çelişkili satırları onaylı kataloğa çıkarmamak.

Bu turda dört yerel aday satır saptandı; ikisi yıl tutarlılığı incelemesi, ikisi aşama/kimlik doğrulaması bekliyor. Hiçbiri veritabanına onaylı sonuç olarak yazılmadı. Tam ulusal veri için MEB/il MEM tarafından sağlanan 2026 merkezî yerleştirme dışa aktarımı hâlâ en sağlam kaynak olur. Böyle bir veri için kurumlara mesaj veya başvuru gönderilmedi.
