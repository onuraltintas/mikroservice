# 2026 YKS puan modelleri (yaklaşık) — KM4

`puan-modelleri-2026.csv`, yönetim panelindeki **Puan tahmini verileri** sayfasından (`/coaching/score-models`)
**Yıl = 2026** ile yüklenir: önce önizleme, hata yoksa uygula. Dosya beş puan türünü içerir: TYT, SAY, EA, SÖZ, DİL.
LGS modeli **yoktur** (aşağıdaki "Eksikler"e bakın). Üretime hiçbir şey otomatik uygulanmaz.

Bu veriler ÖSYM'nin kendi hesabı değildir; resmî istatistiklerden türetilmiş **yaklaşık** bir modeldir. Öğrenci, veli ve
öğretmen ekranları tahmini "Yaklaşık tahmin" etiketiyle ve tek satırlık yöntem notuyla gösterir.

## Dosyalar

| Dosya | İçerik |
|---|---|
| `puan-modelleri-2026.csv` | İçe aktarılacak dosya (`tur;puan_turu;sinav;ders;deger;siralama;yuzdelik`). |
| `kaynak-osym-2026-yks-testler.csv` | ÖSYM 2026-YKS Sayısal Bilgiler: testlerin geçerli soru sayısı, ortalama ve standart sapması (son sınıf ve tüm adaylar). |
| `kaynak-osym-2026-yks-dagilim.csv` | ÖSYM 2026-YKS Sayısal Bilgiler: sınav ve yerleştirme puanlarının yığınsal dağılımı ("… ve üstü" aday sayısı). |
| `derive.py` | Dosyayı kaynaklardan yeniden üretir ve doğrulama özetini yazar. |

Yeniden üretmek / değişmediğini doğrulamak için (katalog dışa aktarımı depoya **konmaz**):

```
python3 -I derive.py <coaching-catalog-dışa-aktarımı>/target_catalog.csv          # dosyayı yazar
python3 -I derive.py <coaching-catalog-dışa-aktarımı>/target_catalog.csv --check  # farklıysa hata verir
```

## Kaynaklar

1. ÖSYM, **2026-YKS Sayısal Bilgiler** — <https://cdn.osym.gov.tr/pdfdokuman/2026/YKS/SB/sayisal_ykdd21072026.pdf>
   (test ortalama/standart sapmaları; "2026–YKS Sınav Puanlarının Yığınsal Dağılımı" ve "… Yerleştirme Puanlarının Yığınsal Dağılımı").
2. ÖSYM, **2020 YKS Değerlendirme Raporu** (s. 12–13, Tablo 1–2) — <https://dokuman.osym.gov.tr/pdfdokuman/2020/GENEL/yksdegraporweb27112020.pdf>
   (standart puan yöntemi, test ağırlıkları, 150/170 puan çapaları, 100–500 ölçeği).
3. ÖSYM, **2026-YKS Sıkça Sorulan Sorular** (S41, S47, S49–S52) — <https://dokuman.osym.gov.tr/pdfdokuman/2026/YKS/sss_yksd19022026.pdf>
   (0,5 ham puan koşulu, TYT'nin %40 ağırlığı, OBP = diploma notu × 5, OBP × 0,12; 2025'te yerleşenlerde yarıya düşer).
4. Üretim hedef kataloğu dışa aktarımı (01.11.2026, salt okunur): 2026 yerleştirmesindeki her programın taban (en küçük)
   yerleştirme puanı ve başarı sırası (kaynağı YÖK Atlas tercih kılavuzu verisi). Dosya depoya konmadı.

## Yöntem

### Net → puan katsayıları

ÖSYM yöntemi (kaynak 2): her test için ham puan (doğru − yanlış/4) son sınıf adaylarının ortalama ve standart sapmasıyla
standart puana çevrilir (SP = 50 + 10 × (net − ort) / ss); SP'ler Tablo 1–2 ağırlıklarıyla toplanır (ASP); ASP en büyüğü
500 olacak biçimde ölçeklenir ve TYT'de "Türkçe ve Temel Matematik'ten 5, diğer testlerden 2,5 ham puan alan aday 150",
SAY/SÖZ/EA/DİL'de "her testten soru sayısının %20'si kadar ham puan alan aday 170" puan alır.

Bu iki çapa (150/170 ve en yüksek = tam net = 500) ölçeğin çapa üstündeki kısmını tamamen belirler:

    puan = taban + Σ katsayı_i × net_i,   katsayı_i = (500 − çapa) × (w_i / ss_i) / Σ_j w_j × (n_j − çapa_net_j) / ss_j

Ortalamalar formülden düşer; yalnız standart sapmalar ve ağırlıklar gerekir. Sonuç: TYT tabanı 100, diğerlerinde 87,5
(çapa profilleri tam netin sabit bir oranı olduğu için). Platformun deneme bölümleri ÖSYM testlerine eşlenir (TYT
Tarih/Coğrafya/Felsefe/Din → Sosyal Bilimler testi; Fizik/Kimya/Biyoloji → Fen Bilimleri testi; AYT alt testleri birebir).
İptal edilen sorular (AYT Türk Dili ve Edebiyatı 23, Matematik 39 geçerli soru) ÖSYM'nin geçerli soru sayılarıyla hesaba katıldı.

| Puan türü | Taban | Katsayılar (puan / net) |
|---|---|---|
| TYT | 100 | Türkçe 3,319 · Sosyal 3,683 · Temel Mat. 3,485 · Fen 2,710 |
| SAY | 87,5 | TYT: Türkçe 1,518 · Sosyal 1,684 · Mat. 1,594 · Fen 1,239 — AYT: Mat. 3,184 · Fizik 2,611 · Kimya 2,611 · Biyoloji 2,683 |
| EA | 87,5 | TYT: 1,473 · 1,634 · 1,546 · 1,202 — AYT: Mat. 3,089 · Edebiyat 3,244 · Tarih-1 2,430 · Coğrafya-1 2,603 |
| SÖZ | 87,5 | TYT: 1,440 · 1,598 · 1,512 · 1,176 — AYT: Edebiyat 3,172 · Tarih-1 2,376 · Coğ-1 2,546 · Tarih-2 3,188 · Coğ-2 2,933 · Felsefe 3,927 · Din 2,083 |
| DİL | 87,5 | TYT: 1,579 · 1,752 · 1,658 · 1,289 — YDT İngilizce 2,778 |

OBP katsayısı her YKS puan türünde 0,12'dir (kaynak 3); ekranda diploma notu girilince eklenir.

### Puan → sıralama tabloları

Sıralama tablosu **yerleştirme puanı** (OBP dahil) → başarı sırası eşlemesidir; hedef programların taban puanı ve başarı
sırası da aynı ölçektedir.

1. 2026 yerleştirmesindeki programların (taban yerleştirme puanı, başarı sırası) çiftleri puan türüne göre ayrıldı
   (katalogdaki `YDT` = `DİL`). TYT 8.329, SAY 4.641, EA 3.153, SÖZ 1.556, DİL 572 program.
2. Puan düştükçe sıra kötüleşmeli: log(sıra) üzerinde monoton (izotonik, havuzlama) uyum; 2 puanlık aralıklarla seyreltme.
3. Tablo, verinin dolu olduğu aralıkta 2 puanda bir okunur; uçlarda (ör. TYT 460 üstü, tüm türlerde 170 altı) ÖSYM'nin
   yığınsal yerleştirme dağılımı ("X ve üstü" aday sayısı = X puanlı adayın sırası) kullanılır; 560 puan = 1. sıra.
4. Yüzdelik = sıra / puanı olan aday sayısı × 100 (ÖSYM dağılımının son satırı).

## Doğrulama (derive.py çıktısı)

**Sıralama tabloları** — programların %20'si rastgele ayrıldı, tablo kalan %80'den kuruldu ve ayrılan programların
taban puanından, sunucunun okuma biçimiyle (satırlar arası doğrusal) başarı sırası tahmin edildi:

| Tür | Program | Geri tutulan | Medyan hata | %90 | En kötü | ÖSYM dilimleriyle fark (medyan / en kötü) |
|---|---|---|---|---|---|---|
| TYT | 8.329 | 1.665 | %0,01 | %0,02 | %1,9 | %0,00 / %0,1 |
| SAY | 4.641 | 928 | %0,01 | %0,06 | %2,2 | %0,01 / %1,3 |
| EA | 3.153 | 630 | %0,01 | %0,05 | %1,0 | %0,01 / %2,0 |
| SÖZ | 1.556 | 311 | %0,02 | %0,12 | %0,9 | %0,00 / %1,4 |
| DİL | 572 | 114 | %0,03 | %0,12 | %0,4 | %0,01 / %2,7 |

Katalogdan kurulan eğri ile ÖSYM'nin bağımsız yayımladığı yığınsal dağılım 150–550 aralığındaki 20 puanlık dilimlerde
neredeyse aynıdır (en büyük farklar verinin seyrek olduğu üst uçta). Yani **2026 için puan verildiğinde sıralama hatası
ihmal edilebilir**; asıl belirsizlik net → puan adımındadır.

**Net → puan** — bireysel aday verisi yayımlanmadığı için doğrudan geri test yapılamaz. Yapılan kontroller:

- ÖSYM çapaları gerçek içe aktarıcı ve tahmin servisiyle yeniden üretildi (test: TYT 150 profili → 150,0; SAY %20 profili → ~169).
- Standart sapmalar ÖSYM'de 0,1 hassasiyetle yayımlanıyor; ±0,05 yuvarlama, ortalamanın 0,5–2 ss üstündeki adaylarda en fazla
  ±0,5 puan fark yaratıyor.
- Ortalama kontrolü (ortalama netlerle model puanı / ÖSYM sınav puanı dağılımının ortalaması): TYT 240,6 / 248,2;
  SÖZ 223,6 / 223,5; DİL 259,9 / 260,8; SAY 205,6 / 223,9; EA 205,0 / 220,9. SAY ve EA'daki fark, bu puanları alan
  adayların (AYT'ye giren, sayısal testlerden net çıkaran) TYT ortalamalarının yayımlanmamasından kaynaklanan bilinen bir
  karşılaştırma yanlılığıdır; model hatası olarak okunmamalıdır. TYT'deki ~%3'lük fark, 20 puanlık dilim orta noktası
  yaklaşımı ve puanı olmayan adayların ayıklanmasıyla kısmen açıklanır.

**Beklenen hata**: aynı netlerle ÖSYM puanına göre tipik olarak ±%3 (orta puanlarda ±5–10 puan); en büyük risk
ÖSYM'nin 2026'da çapa/ölçek kuralını değiştirmiş olmasıdır (aşağıya bakın). Bundan büyük belirsizlik zaten denemelerin
kendisindedir (deneme zorluğu gerçek sınavdan farklıdır); ekran bu yüzden aralık ve "Yaklaşık tahmin" gösterir.

## Sınırlamalar ve açık konular

- **Yöntemin 2026 geçerliliği**: ağırlıklar ve 150/170 çapaları ÖSYM'nin 2020 raporundan; 2026 kılavuzunun puan hesabı
  bölümü (Kılavuz 3.10.1, Tablo 1C–1E, <https://dokuman.osym.gov.tr/pdfdokuman/2026/YKS/basvuru_kilavuz06022026.pdf>)
  araçla okunamadı. 2026 SSS'si TYT'nin %40 ağırlığını ve OBP kurallarını doğruluyor. Yüklemeden önce bir kişinin
  kılavuz s. 29 ve s. 47–48'deki tabloyla karşılaştırması önerilir.
- **En yüksek puan = tam net varsayımı**: TYT'de 5 aday 500 aldığı için güvenli; SAY/EA/SÖZ'de 500 alan tek aday tam
  net yapmadıysa katsayılar biraz (SAY/EA <%1, SÖZ birkaç %) düşük kalır.
- **Çapa altı puanlar** (TYT < 150, diğerleri < 170) ÖSYM'de farklı eğimle hesaplanır; model aynı doğruyu sürdürür.
- **Diploma notu girilmezse** puan sınav puanıdır ama sıralama yerleştirme puanı tablosundan okunur; sıra olduğundan kötü
  görünür. Ekran bunu "Diploma notu eklenmediği için YKS sıralaması olduğundan geride görünebilir." diye söyler. OBP katsayısı
  2025'te yerleşmiş adaylar için 0,06'dır; model 0,12 kullanır.
- **DİL** yalnız İngilizce YDT ile hesaplanır (platformda yalnız YDT_EN denemesi var); sıralama tablosu tüm dilleri kapsar.
- **Bir yıllık veri**: tablolar 2026 adaylarına göredir; 2027 sınavında dağılım değişir. Her yıl ÖSYM sayısal bilgileri ve
  yeni yerleştirme sonuçları çıkınca bu paket yeniden üretilmelidir (yeni klasör, ör. `2027/`).

## Eksikler: LGS

LGS modeli **üretilmedi**. MEB'in puan yöntemi biliniyor (2026 LGS Başvuru ve Uygulama Kılavuzu, Bölüm 11:
SP = 50 + 10 × (HP − ort)/ss, ağırlıklar Türkçe/Matematik/Fen 4, diğerleri 1, MSP = 100 + 400 × (TASP − min)/(max − min);
<https://www.meb.gov.tr/meb_iys_dosyalar/2026_04/06110219_LGS_Basvuru_ve_Uygulama_Kilavuzu_2026.pdf>), ancak:

- 2026 testlerinin ortalama ve **standart sapmaları** ile en küçük/en büyük TASP bulunamadı. "2026 LGS Kapsamında Merkezî
  Sınav Raporu" sayfası (<https://odsgm.meb.gov.tr/www/2026-lgs-kapsaminda-merkezi-sinav-raporu/icerik/1695/tr>) bu oturumda
  erişilemedi; MEB duyurusunda (<https://www.meb.gov.tr/2026-lgs-kapsaminda-merkezi-sinav-raporu-yayimlandi/haber/41333/tr>)
  ve 2026 yerleştirme raporunda (<https://odsgm.meb.gov.tr/meb_iys_dosyalar/2026_08/6a72dd74c263a920303943_lgs_2026_yerlestirme_raporu_kamuoyu_0508.pdf>)
  bu istatistikler yok.
- Puan → yüzdelik/sıra için MEB'in puan dağılımı ya da okul taban **yüzdelikleri** gerekir; katalogdaki 3.150 okulun yalnız
  taban puanı var, yüzdeliği yok.

Bu iki veri MEB'den alınabilirse aynı yöntemle LGS satırları eklenebilir.
