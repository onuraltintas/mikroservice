# Türkiye geneli 2026 LGS — ikinci kaynak araştırması

19 Ekim 2026. Araştırma ve veri hazırlama çalışmasıdır. Uygulama kodu, canlı/yerel katalog tabloları, hesaplar, planlar ve çalışma kayıtları değiştirilmedi. Önceki İstanbul aktarım paketi değiştirilmedi; yeni bulgular ayrı dosyalardadır.

## Yeni kapsam

| Kaynak | Hazırlanan okul/program satırı | Kullanılabilir bilgi | Sınırlama |
|---|---:|---|---|
| Bafra RAM, Samsun merkezî öğrenci alan kurumlar tablosu | 53 | Resmî tercih kodu, ilçe, okul, tür, alan, süre, cinsiyet, pansiyon, dil, 2026–2027 kontenjanı | Yüzdelik sütunları 2025/2024/2023; hepsi dışarıda |
| Kocasinan RAM, Kayseri AL/SBL/AİHL/Fen tablosu | 35 | İlçe, kurum adı, süre, açıkça 2026 kontenjanı | Kurum/tercih kodları ve diller tabloda yok; ad üzerinden kesin eşleşme yapılmadı |
| Kocasinan RAM, Kayseri sınavlı MTAL yayını | 17 | Okul/alan bilgileri; 11 satırda kontenjan, altısında kontenjan bilinmiyor | Kısaltılmış okul adları ve eksik kurum kodları inceleme bekliyor; puanlar 2025 |
| Sadullah Gencer SBL, Bitlis/Tatvan | 2 | Hazırlık + 4 yıl için 30, hazırlıksız dört yıl için 60 kontenjan; İngilizce | 2026 yüzdelikleri yazıyor ama yerleştirme aşaması yok; kullanılmadı |
| Özay Gönlüm Fen Lisesi, Denizli/Tavas | 1 | 2026–2027'den itibaren fen lisesi türünde 60 kontenjan, İngilizce | Kurum/tercih kodu ve sonuç aşaması doğrulanmadı |

**Toplam 108 yeni araştırma satırı** hazırlandı; bu sayı 108 farklı okul veya veritabanına eklenmiş 108 kayıt anlamına gelmez. Kayseri'nin 35 satırı toplam **2.726** kontenjan ediyor; PDF'nin kendi 2026 özet toplamıyla aynı. Samsun'un 53 satırında toplam **3.312** kontenjan çıkarıldı; bu, yayımlanan tablonun toplamıdır, bağımsız ulusal kontrol değildir. Samsun'da 53 farklı tercih kodu var. Ülke geneli tamamlanmış değildir.

### Resmî kaynaklar

- [Kocasinan RAM 2026 duyurusu ve ekleri](https://kocasinanram.meb.k12.tr/icerikler/2026lgstercihdanismanligikayseritercihkilavuzuhazirladik_17633060.html). Yayın, bilgilerin 10 Temmuz 2026 Rota Maarif verileriyle güncellendiğini açıklıyor. Bu tarih sonuç tarihinden öncedir; puanları 2026 sonucu yapmaz.
- [Samsun/Bafra RAM 2026 bağlantıları](https://bafraram.meb.k12.tr/icerikler/liseyolu2026baglantilariinternetsitemizdeguncelleniyor_17559727.html). Bağlantıdaki PDF'nin dosya adı eski yılları da içeriyor; içerikteki 2026 başlığı ve 2026–2027 kontenjan sütunu görsel olarak doğrulandı.
- [Bitlis/Tatvan Sadullah Gencer SBL](https://sadullahgencersosyalbilimler.meb.k12.tr/13/07/969768/okulumuz_hakkinda.html).
- [Denizli/Tavas Özay Gönlüm Fen Lisesi](https://ozayfen.meb.k12.tr/tema/okulumuz_hakkinda.php).

PDF kaynaklarının tamamı ve yeni HTML kanıtları `data/target-catalog/2026/raw/lgs-research/` altında, hazırlanmış satırlar `lgs-samsun-research.json`, `lgs-kayseri-research.json`, `lgs-kayseri-mtal-research.json` ve `lgs-local-metadata-followup.json` dosyalarında saklandı. PDF paketleri SHA256 ile kaynaklarına bağlıdır. Hazırlanan JSON'lar doğrudan import paketi değildir; eksik kimlikler çözülmeden kullanılmamalıdır.

## 41 belirsizliğin incelemesi

### 39 kayıtta adlandırma belirsizliği çözüldü

Sekiz kurum kodu için farklı yazılmış adların aynı kurumu gösterdiğine ilişkin resmî kanıt kaydedildi:

| Kurum kodu | Doğrulanan kurum | Sorun |
|---|---|---|
| 235280 | İnönü MTAL, Bayrampaşa | Lisesi/Lissesi yazımı |
| 966950 | Ziya Kalkavan Denizcilik MTAL, Beşiktaş | Denizcilik sözcüğünün düşmesi |
| 965232 | İSOV Dinçkök YTÜ MTAL, Beşiktaş | YTÜ eki, tire ve Türkçe karakter farkları |
| 972018 | Büyükçekmece MTAL | Kısaltma |
| 763476 | Ali Kul MTAL, Esenyurt | Kısaltma ve Türkçe karakter |
| 162379 | Gültepe Şehit Cihan Erat MTAL, Kağıthane | Kısaltma |
| 966707 | Şile Borsa İstanbul Turizm MTAL | Kısaltma ve Turizm eki |
| 970805 | Hacı Rahime Ulusoy Denizcilik MTAL, Üsküdar | Denizcilik yazım hatası |

İlk altı için MTEGM'nin resmî kurum koduyla açılan kurum sayfaları; diğerlerinde resmî okul sayfası ve kurum kodunu taşıyan okul belgeleri kanıt olarak kaydedildi. Güncel okul adları okul siteleriyle de karşılaştırıldı. MTEGM'deki kayıtlı öğrenci sayıları ve pansiyon kapasitesi 2026 kabul kontenjanı yerine alınmadı.

Bu, **kaynak adlarının aynı kurumu gösterdiğinin çözülmesidir**. Eski veritabanı satırları birleştirilmedi veya silinmedi. Sonraki aktarımda kanıtlı ad eşlemeleri kullanılmalı; özgün kaynak adı ve anahtarı korunmalıdır. Önceki partinin özeti altında sessiz değişiklik yapılmamalıdır.

### Şile'deki iki kayıtta gerçek kaynak kodu hatası

İstanbul tablosu Şile Ayet Azer Aran Savunma Sanayi MTAL için `751600` kodunu veriyor. [MTEGM 751600 kaydı](https://mtegm.meb.gov.tr/kurumlar/?kurumkodu=751600&p=kurum) **Sultangazi Atatürk MTAL**'yi gösteriyor. [MTEGM 768129 kaydı](https://mtegm.meb.gov.tr/kurumlar/?kurumkodu=768129&p=kurum) ise **Şile Ayet Azer Aran Savunma Sanayi MTAL**'yi gösteriyor; iki mesleki alan da kurum sayfasında bulunuyor.

Dolayısıyla bu iki satır yalnızca eski katalogdaki mükerrer ad sorunu değildir; kaynak kurum kodu da yanlıştır. Özgün `751600` kanıtı korunarak doğrulanan kurum kimliği `768129` ayrıca kaydedildi. Eski iki okul kimliğinden birini seçmek için yeterli ayırt edici ilişki henüz yoktur; otomatik birleştirme yapılmadı. 2026 alan kontenjanlarının da doğru kurum altında yeniden doğrulanması gerekir.

Kanıtlı kararların tamamı `lgs-identity-resolutions-20261019.json` içindedir; 39 özgün alan anahtarı ve iki hatalı kod satırı ayrı listelenir.

## Puan/yüzdelik bulguları

### Yıl ve aşaması birlikte doğrulanan sonuç

[İzmir/Menemen Şehit Ahmet Özsoy Fen Lisesinin resmî duyurusu](https://menemensaofen.meb.k12.tr/icerikler/sehitahmetozsoyfenlisesibasarigrafiginiyukseltmeyedevamediyorlgs1nakilsonuclariaciklandi_17648214.html) hem 2026 yılını hem iki ayrı aşamayı açıkça belirtiyor:

| Yıl | Aşama | Taban puan | Yüzdelik |
|---|---|---:|---:|
| 2026 | İlk yerleştirme | 441,2318 | 4,08 |
| 2026 | 1. nakil | 442,6725 | 3,88 |

Kurum kodu `762133` okulun resmî içerik yolu ve kurum künyesiyle doğrulandı. Kanıtlar `lgs-menemen-results-research.json` içinde; henüz veritabanına aktarılmadı. İki aşama aynı sonuç satırı gibi birleştirilemez; bilinmeyen kontenjan/tercih kodu doldurulmaz.

### Ayrı tutulanlar

- [Sincan RAM 2026 yayını](https://sincanram.meb.k12.tr/icerikler/2026lgssincanliselerininmerkeziyerlestirmeveyerelyerlestirmebilgileriyayimlandi_17650928.html): merkezî yerleştirme görselinde 11 okul/program var. Yüzdelikler açıkça **tahmini**. Merkezî tablonun ilk/1. nakil/2. nakil aşaması açıklanmıyor; metindeki ilk yerleştirme ifadesi yerel OBP tablosuna ilişkin. Bu nedenle puanlar ve tahmini yüzdelikler `lgs-sincan-review-research.json` içinde inceleme verisi olarak kaldı. Diğer üç görsel OBP olduğu için LGS kataloğuna alınmadı.
- [Ahmet Fikret Evyap MTAL SSS](https://tuzlaahmetfikretevyapmtal.meb.k12.tr/icerikler/sikcasorulansorular_17627303.html): 2026 ilk yerleştirme açık; ATP grubu 279,56–392, AMP grubu 246,58–354. Bunlar alan bazında değil program grubu aralığıdır; her alana kopyalanamaz. `lgs-evyap-review-research.json` içinde ayrı tutuldu.
- Bitlis'in 2026 yüzdelikleri aşama eksikliği nedeniyle NULL kaldı.
- Akçakoca Fen Lisesinin 2026 ilk yerleştirme haberinde öğrenci listesi var, puan ve yüzdelik hücreleri boş. Öğrenci isimleri araştırma paketine alınmadı; 60 kişilik yerleşen listesi yıllık kontenjan yerine geçirilmedi.
- Yalova SBL tanıtımı 2026/2027 metni yanında 2025/2026 tablo başlığı taşıyor; 90 kontenjan kesin 2026 verisi diye alınmadı.
- Önceki Menderes/Karacabey belirsizlikleri için yeni kesin aşama kanıtı bulunmadı; önceki inceleme statüleri korundu.

## Ulusal kapsam ve doğrulama

Ulusal yıllık e-Okul bağlantısı bu turda web istemcisinde 403 döndü. Rota Maarif ana sayfasının mevcut kademe bağlantıları yine okul öncesi, ilkokul ve ortaokul. Yeni resmî il kaynakları kapsamı genişletiyor; 81 ilin tamamının 2026 LGS verisi doğrulanmış değildir. Güvenlik engelleri aşılmadı, kurumlara mesaj gönderilmedi.

Bu tur uygulama değişmediği için önceki 132 backend testi yeniden çalıştırılmadı. Yeni verilerde satır sayıları, benzersiz tercih kodları, 2026 sütunu, kaynak özeti, puanların NULL kalması, Kayseri kontenjan toplamı ve 39 ad eşleme kapsamı programatik olarak kontrol edildi. Samsun/Kayseri PDF başlıkları ve örnek/son satırlar görsel olarak incelendi. Kesin sonuçlar ile tahmini/aşaması eksik değerler ayrı dosyalarda tutuldu.
