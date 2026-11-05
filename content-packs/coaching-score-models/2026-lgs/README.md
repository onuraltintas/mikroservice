# 2026 LGS puan modeli (yaklaşık) — Paket B

`puan-modelleri-2026-lgs.csv`, yönetim panelindeki **Puan tahmini verileri** sayfasından (`/coaching/score-models`)
**Yıl = 2026** ile yüklenir: önce önizleme, hata yoksa uygula. Dosya yalnız **LGS** puan türünü içerir; sıralama/yüzdelik
tablosu **yoktur** (MEB puan dağılımı ya da yüzdelik ↔ puan eşlemesi yayımlamıyor). Üretime hiçbir şey otomatik uygulanmaz.

MEB'in kendi hesabı değildir. MEB test ortalama ve standart sapmalarını, en küçük ve en büyük TASP'yi yayımlamadığı için
model **kalibre edilmiş bir yaklaşımdır**: ekran "Yaklaşık tahmin" etiketi, ± aralık ve "tam nete uzaklaştıkça aralık
genişler" notuyla gösterir.

## Dosyalar

| Dosya | İçerik |
|---|---|
| `puan-modelleri-2026-lgs.csv` | İçe aktarılacak dosya: `taban`, 6 `katsayi`, `belirsizlik`, `belirsizlik_siniri`. |
| `kaynak-meb-2026-lgs-testler.csv` | Soru sayıları ve ağırlıklar (Kılavuz), ortalama madde güçlüğü p̄ ve güvenirlik (Merkezî Sınav Raporu). |
| `derive.py` | Dosyayı yeniden üretir, örnek profilleri yazar (`--check`: değişmediğini doğrular). |

## Kaynaklar

1. MEB, **2026 LGS Başvuru ve Uygulama Kılavuzu**, Bölüm 11 "Sınavın Değerlendirilmesi" (11.1 Ham Puan, 11.2 TASP, 11.3
   Muafiyet) — <https://www.meb.gov.tr/meb_iys_dosyalar/2026_04/06110219_LGS_Basvuru_ve_Uygulama_Kilavuzu_2026.pdf>
2. MEB ÖDSGM, **2026 Liselere Geçiş Sistemi Kapsamında Merkezî Sınav Raporu** (Tablo 7–9) —
   <https://odsgm.meb.gov.tr/meb_iys_dosyalar/2026_07/6a50862c73d81732286292_2026_Merkez%C3%AE_S%C4%B1nav_Raporu.pdf>
   (p̄: Türkçe 0,47 · Matematik 0,28 · Fen 0,45 · İnkılap 0,53 · Din 0,63 · Yabancı Dil 0,44; güvenirlik 0,82 · 0,86 · 0,87 ·
   0,81 · 0,72 · 0,88; tüm soruları doğru yanıtlayan **452** öğrenci; 994.358 katılımcı).

## Resmî yöntem

Ham puan = doğru − yanlış/3 (platformdaki LGS netiyle aynı, `PracticeExamRules` bölen 3) → standart puan SP = 50 + 10 ×
(HP − ort)/ss → ağırlıklar Türkçe, Matematik, Fen **4**; İnkılap Tarihi, Din Kültürü, Yabancı Dil **1** → TASP = Σ ağırlık × SP
→ MSP = 100 + 400 × (TASP − TASP_min) / (TASP_max − TASP_min).

Ortalamalar formülden düşer: MSP = 500 − Σ cᵢ × (nᵢ − netᵢ), cᵢ = 400 × (wᵢ/ssᵢ) / Σⱼ (wⱼ/ssⱼ)(nⱼ − min_netⱼ). Gerekenler
(1) dersler arası oranlar wᵢ/ssᵢ ve (2) ölçeğin eğimi, yani TASP_min'in nerede olduğu.

## Kalibrasyon ve varsayımlar

| Adım | Seçim | Gerekçe |
|---|---|---|
| Üst çapa | **Tam net = 500** | En büyük TASP tüm öğrencilerden alınır; 2026'da 452 öğrenci tüm soruları doğru yanıtladı. Sağlam çapa. |
| Ders oranları | ssᵢ, KR-20'den geri hesaplandı: σ² = k·p̄(1−p̄) / (1 − r(k−1)/k) → Türkçe 4,75 · Matematik 4,69 · Fen 5,34 · İnkılap 3,03 · Din 2,57 · Yabancı Dil 3,44 | Raporun p̄ ve güvenirlikleri. Mutlak değerler üst sınırdır, ama formül yalnız **oranlara** bakar; oranlar 2025 ve 2026'da ±%6 içinde. |
| Eğim (TASP_min) | En küçük TASP'li öğrenci her testte **soru sayısının −1/5'i** kadar net (20 soruda 2 doğru 18 yanlış: −4 net) | Hiçbir negatif net olmaması (0) ile her sorunun yanlış olması (−n/3) arasında gerçekçi bir uç: 994 bin öğrenci içinde en düşük toplam, sistematik çok yanlışlı bir kâğıttır, hepsi yanlış değil. Ortalama eğim **3,7 puan/net**; yayınevlerinin geçmiş yıl bant simülasyonlarıyla (45–59 net 330–389, 60–74 net 390–439, 75–84 net 440–479) uyumlu (çapraz kontrol, kaynak değil). |
| ± aralık | **0,2 × (500 − puan), en çok ±30** (`belirsizlik` 0,2, `belirsizlik_siniri` 30) | TASP_min'in tüm olası yeri (0 ile −n/3 arası) eğimi −%10 / +%20 değiştirir; aralık büyük olanı alır. 450 üstünde ±10'un altı, ortada ±10–30: araştırmanın mühendislik tahminiyle (üstte ±5–10, ortada ±10–30) aynı. Ölçülmüş değildir. |
| Alt sınır | 100 | MSP ölçeği 100–500. |
| Muafiyet | Modellenmedi | Platformun LGS denemesi her dersi ister. |

Sonuç katsayıları (puan / net): Türkçe 4,763 · Matematik 4,818 · Fen 4,234 · İnkılap 1,865 · Din 2,197 · Yabancı Dil 1,643;
taban 166,7 (alt sınır 100'le kesilir). Bir Türkçe, Matematik ya da Fen neti, bir İnkılap, Din ya da Yabancı Dil netinin 2–3 katıdır.

## Örnek çıktılar (`derive.py`)

| Profil (her dersin şu kadarı net) | Toplam net | Puan | ± |
|---|---|---|---|
| %100 | 90 | 500,0 | 0 |
| %90 | 81 | 466,7 | 6,7 |
| %80 | 72 | 433,3 | 13,3 |
| %70 | 63 | 400,0 | 20,0 |
| %60 | 54 | 366,7 | 26,7 |
| %50 | 45 | 333,3 | 30 |
| Tam net, Matematik'te 1 yanlış | 88,67 | 493,6 | 1,3 |

## Sınırlamalar ve sonraki adım

- Eğim tek varsayıma dayanır; en doğru iyileştirme **kendi verimizle kalibrasyondur**: 2026 LGS'ye giren öğrencilerimizin
  gerçek MSP'leri ve son denemelerindeki ders netleriyle tek parametre (eğim) en küçük kareler ile kestirilir, 500 çapası sabit
  kalır. Veri toplanınca `MIN_NET_SHARE` yerine kestirilen eğim kullanılmalı ve `belirsizlik` daraltılmalıdır.
- Yüzdelik/sıralama yok: Rota Maarif okul verisi (taban puan, yüzdelik) alınabilirse puan → yüzdelik tablosu `siralama`
  satırlarıyla eklenebilir (model sıralama tablosunu isteğe bağlı kabul eder).
- 2027 için yeni rapor çıkınca yeni klasör (`2027-lgs/`) üretilmelidir.
