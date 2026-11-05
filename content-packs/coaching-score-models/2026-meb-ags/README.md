# 2026 MEB-AGS puan modelleri (yaklaşık) — Paket B

`puan-modelleri-2026-meb-ags.csv`, yönetim panelindeki **Puan tahmini verileri** sayfasından (`/coaching/score-models`)
**Yıl = 2026** ile yüklenir: önce önizleme, hata yoksa uygula (gerekçe yazılır, geçmişe sürüm olarak düşer). Dosya 19 puan
türünü içerir: **P1**, her ÖABT alanı için **P2_<alan>** (16 alan) ve Din Kültürü alanı için **P2_16** ile **P2_17**. Sıralama
tablosu **yoktur** (ÖSYM MEB-AGS için puan → sıralama verisi yayımlamıyor). Üretime hiçbir şey otomatik uygulanmaz.

Bu veriler ÖSYM'nin kendi hesabı değildir; resmî istatistiklerden türetilmiş **yaklaşık** bir modeldir. Ekranlar puanı
"Yaklaşık tahmin" etiketi, ± aralık ve tek satırlık yöntem notuyla gösterir. **P3 (AGS + YDS) modellenmedi**: dil bazlı YDS
ortalama ve standart sapmaları yayımlanmıyor.

## Dosyalar

| Dosya | İçerik |
|---|---|
| `puan-modelleri-2026-meb-ags.csv` | İçe aktarılacak dosya (`tur;puan_turu;sinav;ders;deger;siralama;yuzdelik`): `taban`, `katsayi`, `belirsizlik`. |
| `kaynak-osym-2026-meb-ags-testler.csv` | ÖSYM 2026 MEB-AGS Sayısal Bilgiler: her testin soru sayısı, ham puan (net) ortalaması, standart sapması ve aday sayısı. |
| `derive.py` | Dosyayı kaynaktan yeniden üretir ve örnek profilleri yazar. |

```
python3 -I derive.py           # dosyayı yazar
python3 -I derive.py --check   # dosya kaynakla aynı değilse hata verir (tests/E2E/coaching-score-models-2026-all-exams.contract.test.mjs)
```

## Kaynaklar

1. ÖSYM, **2026 MEB-AGS Sınav Sonuçlarına İlişkin Sayısal Bilgiler** (31.08.2026; "sınavı geçerli olan adaylar üzerinden") —
   <https://dokuman.osym.gov.tr/web/2026/8/2026-meb-ags-sinav-sonuclarina-liskin-sayisal-bilgiler-3p3l3d-31125057.pdf>,
   SHA-256 `d9984f77f6334e287cc5ffb177dae9a535aadceec6f4cf6785d59205c3dc6cfb`. AGS: 369.008 aday; ÖABT alanları ve İHL alt
   testleri `kaynak-osym-2026-meb-ags-testler.csv` içinde.
2. ÖSYM, **2026 MEB-AGS Başvuru Kılavuzu**, madde 3.10 "Değerlendirme" —
   <https://dokuman.osym.gov.tr/web/2026/7/basvuru-kilavuzu-p9djhk-30105733.pdf>,
   SHA-256 `e5414dff5d0badeef0abef67f88fa311ecd52f3b6364d0d470bb684ed8d65c6b`. Formül, ağırlıklar ve 1 net koşulu sunucu
   operatörü tarafından resmî PDF'ten doğrulandı (05.11.2026).

## Yöntem (Kılavuz 3.10)

1. **Ham puan** = doğru − yanlış/4, her test için. Platformda AGS ve ÖABT denemelerinin neti zaten böyle hesaplanır
   (`PracticeExamRules`, bölen 4).
2. **Standart puan** (her test) = 50 + 10 × (ham puan − ortalama) / standart sapma; 2026 oturumunun ortalama ve ss'si.
3. **Ağırlıklar (%)**:

   | Puan türü | Sözel Yet. | Sayısal Yet. | Tarih | Türkiye Coğ. | Eğitimin Temelleri ve TMES | Mevzuat | ÖABT |
   |---|---|---|---|---|---|---|---|
   | P1 | 20 | 20 | 15 | 10 | 25 | 10 | — |
   | P2 | 10 | 10 | 7,5 | 5 | 12,5 | 5 | 50 |
   | P2-16 (Din Kült. ve Ahlak Bil.) | 10 | 10 | 7,5 | 5 | 12,5 | 5 | Ortak Alan 15 · Temel İslam Bil. 15 · İslam Tarihi, Felsefe ve Din Bil. 20 |
   | P2-17 (İHL Meslek Dersleri) | 10 | 10 | 7,5 | 5 | 12,5 | 5 | Ortak Alan 15 · Temel İslam Bil. 20 · İslam Tarihi, Felsefe ve Din Bil. 15 |

4. **Ağırlıklı puan** AP = Σ ağırlık × standart puan / 100.
5. **Sınav puanı (0–100)** = 70 + 30 × [2(AP − X) − S] / [2(B − X) − S]; X = AP ortalaması, S = AP standart sapması,
   B = en büyük AP.
6. **1 net koşulu**: ham puanı 1'in altındaki teste standart puan verilmez; o testi kullanan puan türleri hesaplanmaz
   (ekran: "Bu puan türünde puan hesaplanmaz: Mevzuat testinden en az 1 net gerekir.").

Bütün adımlar netlerde doğrusal olduğu için model **taban + Σ katsayı × net** biçimine iner; dosya YKS dosyasıyla aynı
satırları (`taban`, `katsayi`) kullanır. Tahmin, YKS'de olduğu gibi son üç denemenin ortalamasıdır; 1 net koşulu da aynı
denemelerin test ortalamalarına uygulanır.

## Varsayımlar (ÖSYM X, S ve B'yi yayımlamıyor)

| Değer | Seçim | Gerekçe |
|---|---|---|
| X | **50** | AP, ortalaması 50 olan standart puanların ağırlıklı ortalaması; ağırlıklar toplamı %100. (1 net koşulu nedeniyle hesaplanan adayların ortalaması biraz üstte olabilir.) |
| B | **Tam net profilinin AP'si** (P1: 74,48) | 369.008 adayda tam nete çok yakın bir aday bulunması beklenir. Kimse tam net yapmadıysa gerçek B biraz küçüktür ve üst uçtaki gerçek puanlar modelden birkaç onda bir yüksek çıkar. |
| S | **Testler arası korelasyon ρ = 0,5** ile S = 10 × √(Σa² + ρ(1 − Σa²)), a = ağırlık/100. P1: 7,70; P2: 8,05; P2-16/17: 7,52 | Bilgi ve yetenek testlerinin standart puanları arasında orta düzey (0,4–0,6) korelasyon beklenir; 0,5 tüm olası değerlerin (0–1) ortası. |
| ± aralık | **ρ = 0 ile ρ = 1 arasındaki her S** | Aralık hiçbir korelasyon varsayımına dayanmaz. Formülde tüm S değerleri AP = B'de 100'de buluşur; fark (100 − puan) ile orantılıdır: P1 ±0,077 × (100 − puan), P2 alanları ±0,040–0,071, P2-16 ±0,085, P2-17 ±0,086 (`belirsizlik` satırları). |
| P2-16 / P2-17 | ÖABT denemesindeki tek bölümün neti alt testlere **soru sayısı oranında** (10 / 23 / 17) bölünür; 1 net koşulu ÖABT'nin tamamına uygulanır | Platformun ÖABT denemesi alt testleri ayrı kaydetmiyor. |
| Hedef puan türü | ÖABT alanı seçen öğrenci: o alanın **P2**'si; Din Kültürü alanı: **P2-16** (P2-17 yanında gösterilir); alan seçmeyen: **P1** | Din Kültürü ÖABT'si hem Din Kültürü ve Ahlak Bilgisi (P2-16) hem İHL Meslek Dersleri (P2-17) öğretmenliğine sayılır; alan kodu ikisini ayırmıyor, P2-16 daha geniş kadrolu alandır. |

## Örnek çıktılar (`derive.py`)

| Profil | P1 | P2 · İlköğretim Matematik | P2-16 | P2-17 |
|---|---|---|---|---|
| Her testte 2026 ortalaması | 64,4 ± 2,7 | 65,2 ± 1,7 | 64,5 ± 3,0 | 64,5 ± 3,1 |
| Her testte ortalama + 1 ss | 78,9 ± 1,6 | 77,2 ± 1,1 | 77,9 ± 1,9 | 77,8 ± 1,9 |
| Her testte ortalama + 2 ss (tam neti aşmadan) | 92,6 ± 0,6 | 88,8 ± 0,6 | 90,7 ± 0,8 | 90,7 ± 0,8 |
| Tam net | 100 | 100 | 100 | 100 |

P1'de bir net Tarih ≈ 1,04, Sözel ≈ 0,91, Türkiye Coğrafyası ≈ 0,86, Sayısal ≈ 0,75, Mevzuat ≈ 0,61, Eğitim Bilimleri ≈ 0,58
puandır (ağırlık / ss). Hiç net olmayan profil formülde 41,7 alır; ama 1 net koşulu nedeniyle böyle bir puan hesaplanmaz.

## Beklenen doğruluk ve sınırlamalar

- Ortadaki adaylar için S belirsizliği ±2–3 puan, üst uçta ±1 puanın altında; ÖABT standart sapmaları 7–12 net olduğu için
  P2 alanları biraz daha dar. Bundan büyük belirsizlik denemenin kendisindedir (deneme zorluğu gerçek sınavdan farklıdır);
  ekran aralığı son denemelerin dağılımıyla birleştirir (en az ±%2).
- İstatistikler 2026 oturumuna aittir; 2027 sınavından sonra ÖSYM'nin yeni sayısal bilgileriyle yeni klasör (`2027-meb-ags/`)
  üretilmelidir. Ağırlıklar ve formül değişirse `derive.py` güncellenir.
- Puan → sıralama/yüzdelik yoktur; akademiye yerleştirme MEB-AGS puanıyla yapılır, taban puan verisi yayımlanmıyor.
