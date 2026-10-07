# Hızlı Okuma — canlı eğitim programları envanteri

İnceleme: 30 Eylül 2026. Kaynak: VPS, `speedreading_owned_db`, `speed_reading.program_templates` ve yaş grubu tablosu. Salt-okunur inceleme; canlı veriler değiştirilmedi.

## Kapsam ve sayım

19 aktif eğitim programı kaydı, 18 farklı eğitim programı adı ve 4 aktif seviye tespit şablonu vardır. Silinmemiş kayıtların tamamı bu envantere alınmıştır; bu sorguda pasif kayıt yoktur.

Egzersiz sayıları `WeeklyPatternJson` içindeki Count değerlerinin toplamıdır: çalışma adımı/oturum sayısını ifade eder. Benzersiz egzersiz veya benzersiz metin sayısı değildir. Katalogdaki gerçek içeriklerin yeterliliği ve her programın öğrenciyle çalıştırılması ayrıca doğrulanmalıdır. Günlük dakika bilgisi bu alanlarda bulunmadığı için süre tahmini yapılmamıştır. Zorluk sütunları şablon değerleridir; öğrenci ilerlemesi gerçek zorluğu etkileyebilir.

## Mükerrer kayıt

`22+ Yaş Orta Seviye` için iki kayıt vardır. Bu incelemede karşılaştırılan ad, açıklama, yaş grubu, puan aralığı, gün/hafta, başlangıç/azami zorluk, artış sıklığı, tür, sıra ve günlük egzersiz JSON'u aynıdır.

- `eaf89bb3-7427-4287-a6de-d19f49b782b6`: 1 öğrenci program ataması.
- `f035811f-31ae-4665-bc53-bb7df2d197f4`: 0 öğrenci program ataması.

Atama sayısı toplam program ilerleme kayıtlarını sayar; aktif öğrenci sayısı değildir. Kod iki eşit öncelikli kaydı kimliğe göre sıralamadığı için hangisinin seçileceği garanti edilmez. Öneri: bağımlılıkları ayrıca denetleyip kullanılmayan kaydı pasife almak; bu rapor kapsamında yapılmadı.

## Otomatik atamanın gerçek sonucu

Kod yaş grubu ve puan uygunluğunu süzer; sonra MinAssessmentScore, ardından DisplayOrder ile ilk kaydı seçer. ProgramType/ExamType ile genel eğitim–sınav–maraton ayrımı yapmaz. Aşağıdaki sonuç canlı katalog ve bu seçim kuralından hesaplanmıştır; kullanıcıya yeni atama yapılmamıştır. Puan kodda tam sayıya dönüştürülür.

| Yaş | Tam sayı puan | Seçilen program |
|---|---:|---|
| Genç | 0–40 | 13-16 Yaş Başlangıç |
| Genç | 41–100 | LGS Hazırlık Temel |
| Genç Yetişkin | 0–40 | 17-21 Yaş Başlangıç |
| Genç Yetişkin | 41–100 | YKS Hazırlık (TYT-AYT) |
| Yetişkin | 0–60 | 22+ Yaş Orta Seviye |
| Yetişkin | 61–100 | Hızlı Okuma Maratonu (8 Hafta) |
| Çocuk | 0–40 | 9-12 Yaş Başlangıç |
| Çocuk | 41–70 | 9-12 Yaş Orta Seviye |
| Çocuk | 71–100 | 9-12 Yaş İleri Seviye |

Bu nedenle 13–16 ve 17–21 yaş orta/ileri genel programlar; yetişkin ileri ve hızlandırılmış başlangıç programı bu katalogla normal ilk otomatik seçimde öne çıkmaz. Özel program seçimi ayrı bir iş kuralına bağlanmalıdır. 40/70/60 sınırlarında aralıklar kapsayıcıdır ve birden fazla kayıt uygun olabilir.

## Programların özeti

| Program | Yaş | Puan | Gün | Günlük adım | Toplam adım | Zorluk başlangıç→azami |
|---|---|---:|---:|---:|---:|---:|
| 13-16 Yaş Başlangıç | 13–16 | 0–40 | 28 | 6–10 | 218 | 1→3 |
| 13-16 Yaş Orta Seviye | 13–16 | 40–70 | 28 | 6–10 | 218 | 2→4 |
| 13-16 Yaş İleri Seviye | 13–16 | 70–100 | 28 | 6–10 | 218 | 3→5 |
| 17-21 Yaş Başlangıç | 17–21 | 0–40 | 28 | 7–11 | 246 | 2→3 |
| 17-21 Yaş Orta Seviye | 17–21 | 40–70 | 28 | 7–11 | 246 | 3→4 |
| 17-21 Yaş İleri Seviye | 17–21 | 70–100 | 28 | 7–11 | 246 | 4→5 |
| 22+ Yaş Orta Seviye (eaf89bb3) | 22–+ | 0–60 | 28 | 7–11 | 246 | 3→4 |
| 22+ Yaş Orta Seviye (f035811f) | 22–+ | 0–60 | 28 | 7–11 | 246 | 3→4 |
| 22+ Yaş İleri Seviye | 22–+ | 60–100 | 28 | 7–11 | 246 | 4→5 |
| 9-12 Yaş Başlangıç | 9–12 | 0–40 | 28 | 6–8 | 185 | 1→2 |
| 9-12 Yaş Orta Seviye | 9–12 | 40–70 | 28 | 6–8 | 185 | 2→3 |
| 9-12 Yaş İleri Seviye | 9–12 | 70–100 | 28 | 6–8 | 185 | 3→4 |
| Hızlı Okuma Maratonu (8 Hafta) | 22–+ | 40–100 | 56 | 7–11 | 492 | 2→5 |
| LGS Hazırlık Temel | 13–16 | 0–100 | 28 | 7–10 | 241 | 2→4 |
| LGS Hazırlık Yoğun | 13–16 | 0–100 | 28 | 7–10 | 241 | 3→5 |
| LGS Sınav Kampı (Sprint) | 13–16 | 0–100 | 14 | 7–11 | 134 | 3→5 |
| YKS Hazırlık (TYT-AYT) | 17–21 | 0–100 | 28 | 7–10 | 241 | 3→5 |
| YKS Sınav Kampı (Sprint) | 17–21 | 0–100 | 14 | 7–11 | 134 | 3→5 |
| Yetişkin Başlangıç (Hızlandırılmış) | 22–+ | 0–40 | 14 | 4–7 | 64 | 1→2 |

## Gün gün eğitim programları

Her satırda egzersiz türü × oturum sayısı ve parantez içinde tanımlı zorluk vardır. Haftanın her gününde çalışma tanımlıdır; dinlenme günü ayrıca tanımlanmamıştır.

### 13-16 Yaş Başlangıç — eaf4af5b

- Kimlik: `eaf4af5b-0202-4b16-b452-ca58a9ee0db2`
- Hedef yaş: 13–16 (Genç). Puan: 0–40.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 1; azami: 3; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 4.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Gençler için temel hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z1); Sakkadik göz hareketi ×3 (Z1); Göz takibi ×2 (Z1); Schulte tablosu ×2 (Z1) | 9 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z1); Sabitleme ×2 (Z1); Odaklanma ×2 (Z1); Takistoskop ×2 (Z1) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z1); Schulte tablosu ×3 (Z1); Sabitleme ×2 (Z1) | 8 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z1); Odaklanma ×2 (Z1); Göz takibi ×2 (Z1); Schulte tablosu ×2 (Z1) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z1); Sakkadik göz hareketi ×2 (Z1); Takistoskop ×2 (Z1); Sabitleme ×2 (Z1) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z1); Sabitleme ×2 (Z1); Schulte tablosu ×2 (Z1); Sakkadik göz hareketi ×2 (Z1); Odaklanma ×2 (Z1) | 10 |
| 7 | 1. hafta / 7. gün | Takistoskop ×2 (Z1); Odaklanma ×2 (Z1); Göz takibi ×2 (Z1); Schulte tablosu ×2 (Z1) | 8 |
| 8 | 2. hafta / 1. gün | Görsel genişleme ×2 (Z1); Kelime gruplama ×2 (Z1); Seri görsel sunum (RSVP) ×2 (Z1); Sakkadik göz hareketi ×2 (Z1) | 8 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z1); Görsel genişleme ×2 (Z1); Schulte tablosu ×2 (Z1) | 7 |
| 10 | 2. hafta / 3. gün | Seri görsel sunum (RSVP) ×3 (Z1); Görsel genişleme ×2 (Z1); Takistoskop ×2 (Z1) | 7 |
| 11 | 2. hafta / 4. gün | Kelime gruplama ×2 (Z1); Seri görsel sunum (RSVP) ×2 (Z1); Sakkadik göz hareketi ×2 (Z1); Göz takibi ×2 (Z1) | 8 |
| 12 | 2. hafta / 5. gün | Görsel genişleme ×3 (Z1); Seri görsel sunum (RSVP) ×2 (Z1); Kelime gruplama ×2 (Z1) | 7 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z1); Kelime gruplama ×2 (Z1); Schulte tablosu ×2 (Z1); Odaklanma ×2 (Z1) | 8 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z1); Takistoskop ×2 (Z1); Görselleştirme ×2 (Z1); Kelime gruplama ×2 (Z1) | 8 |
| 15 | 3. hafta / 1. gün | Seri görsel sunum (RSVP) ×2 (Z2); İç ses azaltma ×2 (Z2); Hızlı okuma ×2 (Z2); Kelime gruplama ×2 (Z2) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×2 (Z2); Geri dönüş azaltma ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2) | 6 |
| 17 | 3. hafta / 3. gün | İç ses azaltma ×2 (Z2); Solan metin ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 8 |
| 18 | 3. hafta / 4. gün | Hızlı okuma ×2 (Z2); Solan metin ×2 (Z2); Kelime gruplama ×2 (Z2); Geri dönüş azaltma ×2 (Z2) | 8 |
| 19 | 3. hafta / 5. gün | Seri görsel sunum (RSVP) ×3 (Z2); İç ses azaltma ×2 (Z2); Hızlı okuma ×2 (Z2) | 7 |
| 20 | 3. hafta / 6. gün | Geri dönüş azaltma ×2 (Z2); Solan metin ×2 (Z2); Schulte tablosu ×2 (Z2); Odaklanma ×2 (Z2) | 8 |
| 21 | 3. hafta / 7. gün | Hızlı okuma ×2 (Z2); İç ses azaltma ×2 (Z2); Görselleştirme ×2 (Z2) | 6 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z2); Tarama ×2 (Z2); Hızlı okuma ×2 (Z2) | 7 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z2); Anlama ×2 (Z2); Kelime çalışması ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2) | 8 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z2); Anlama ×2 (Z2); Hata analizi ×2 (Z2) | 7 |
| 25 | 4. hafta / 4. gün | Hızlı okuma ×2 (Z2); Anlama ×3 (Z2); Göz gezdirme ×2 (Z2) | 7 |
| 26 | 4. hafta / 5. gün | Anlama ×3 (Z2); Tarama ×2 (Z2); Kelime çalışması ×2 (Z2) | 7 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z2); Hızlı okuma ×2 (Z2); Anlama ×2 (Z2); Odaklanma ×2 (Z2) | 8 |
| 28 | 4. hafta / 7. gün | Anlama ×2 (Z2); Serbest okuma ×2 (Z2); Kelime çalışması ×2 (Z2); Görselleştirme ×2 (Z2) | 8 |

### 13-16 Yaş Orta Seviye — f6191558

- Kimlik: `f6191558-2759-452d-ac09-06bb724cc219`
- Hedef yaş: 13–16 (Genç). Puan: 40–70.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 2; azami: 4; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 5.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Gençler için orta seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z2); Sakkadik göz hareketi ×3 (Z2); Göz takibi ×2 (Z2); Schulte tablosu ×2 (Z2) | 9 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z2); Sabitleme ×2 (Z2); Odaklanma ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z2); Schulte tablosu ×3 (Z2); Sabitleme ×2 (Z2) | 8 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z2); Odaklanma ×2 (Z2); Göz takibi ×2 (Z2); Schulte tablosu ×2 (Z2) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z2); Sakkadik göz hareketi ×2 (Z2); Takistoskop ×2 (Z2); Sabitleme ×2 (Z2) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z2); Sabitleme ×2 (Z2); Schulte tablosu ×2 (Z2); Sakkadik göz hareketi ×2 (Z2); Odaklanma ×2 (Z2) | 10 |
| 7 | 1. hafta / 7. gün | Takistoskop ×2 (Z2); Odaklanma ×2 (Z2); Göz takibi ×2 (Z2); Schulte tablosu ×2 (Z2) | 8 |
| 8 | 2. hafta / 1. gün | Görsel genişleme ×2 (Z2); Kelime gruplama ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 8 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z2); Görsel genişleme ×2 (Z2); Schulte tablosu ×2 (Z2) | 7 |
| 10 | 2. hafta / 3. gün | Seri görsel sunum (RSVP) ×3 (Z2); Görsel genişleme ×2 (Z2); Takistoskop ×2 (Z2) | 7 |
| 11 | 2. hafta / 4. gün | Kelime gruplama ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Sakkadik göz hareketi ×2 (Z2); Göz takibi ×2 (Z2) | 8 |
| 12 | 2. hafta / 5. gün | Görsel genişleme ×3 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Kelime gruplama ×2 (Z2) | 7 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z2); Kelime gruplama ×2 (Z2); Schulte tablosu ×2 (Z2); Odaklanma ×2 (Z2) | 8 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z2); Takistoskop ×2 (Z2); Görselleştirme ×2 (Z2); Kelime gruplama ×2 (Z2) | 8 |
| 15 | 3. hafta / 1. gün | Seri görsel sunum (RSVP) ×2 (Z3); İç ses azaltma ×2 (Z3); Hızlı okuma ×2 (Z3); Kelime gruplama ×2 (Z3) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×2 (Z3); Geri dönüş azaltma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 6 |
| 17 | 3. hafta / 3. gün | İç ses azaltma ×2 (Z3); Solan metin ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 8 |
| 18 | 3. hafta / 4. gün | Hızlı okuma ×2 (Z3); Solan metin ×2 (Z3); Kelime gruplama ×2 (Z3); Geri dönüş azaltma ×2 (Z3) | 8 |
| 19 | 3. hafta / 5. gün | Seri görsel sunum (RSVP) ×3 (Z3); İç ses azaltma ×2 (Z3); Hızlı okuma ×2 (Z3) | 7 |
| 20 | 3. hafta / 6. gün | Geri dönüş azaltma ×2 (Z3); Solan metin ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3) | 8 |
| 21 | 3. hafta / 7. gün | Hızlı okuma ×2 (Z3); İç ses azaltma ×2 (Z3); Görselleştirme ×2 (Z3) | 6 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z3); Tarama ×2 (Z3); Hızlı okuma ×2 (Z3) | 7 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z3); Anlama ×2 (Z3); Kelime çalışması ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 8 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z3); Anlama ×2 (Z3); Hata analizi ×2 (Z3) | 7 |
| 25 | 4. hafta / 4. gün | Hızlı okuma ×2 (Z3); Anlama ×3 (Z3); Göz gezdirme ×2 (Z3) | 7 |
| 26 | 4. hafta / 5. gün | Anlama ×3 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3) | 7 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z3); Hızlı okuma ×2 (Z3); Anlama ×2 (Z3); Odaklanma ×2 (Z3) | 8 |
| 28 | 4. hafta / 7. gün | Anlama ×2 (Z3); Serbest okuma ×2 (Z3); Kelime çalışması ×2 (Z3); Görselleştirme ×2 (Z3) | 8 |

### 13-16 Yaş İleri Seviye — 009836a5

- Kimlik: `009836a5-4b44-437f-9847-6bd8ce811010`
- Hedef yaş: 13–16 (Genç). Puan: 70–100.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 3; azami: 5; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 6.
- Toplam atama kaydı: 1.
- Kayıt açıklaması: Gençler için ileri seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z3); Sakkadik göz hareketi ×3 (Z3); Göz takibi ×2 (Z3); Schulte tablosu ×2 (Z3) | 9 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z3); Sabitleme ×2 (Z3); Odaklanma ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z3); Schulte tablosu ×3 (Z3); Sabitleme ×2 (Z3) | 8 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z3); Odaklanma ×2 (Z3); Göz takibi ×2 (Z3); Schulte tablosu ×2 (Z3) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z3); Sakkadik göz hareketi ×2 (Z3); Takistoskop ×2 (Z3); Sabitleme ×2 (Z3) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z3); Sabitleme ×2 (Z3); Schulte tablosu ×2 (Z3); Sakkadik göz hareketi ×2 (Z3); Odaklanma ×2 (Z3) | 10 |
| 7 | 1. hafta / 7. gün | Takistoskop ×2 (Z3); Odaklanma ×2 (Z3); Göz takibi ×2 (Z3); Schulte tablosu ×2 (Z3) | 8 |
| 8 | 2. hafta / 1. gün | Görsel genişleme ×2 (Z3); Kelime gruplama ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 8 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z3); Görsel genişleme ×2 (Z3); Schulte tablosu ×2 (Z3) | 7 |
| 10 | 2. hafta / 3. gün | Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3); Takistoskop ×2 (Z3) | 7 |
| 11 | 2. hafta / 4. gün | Kelime gruplama ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Sakkadik göz hareketi ×2 (Z3); Göz takibi ×2 (Z3) | 8 |
| 12 | 2. hafta / 5. gün | Görsel genişleme ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3) | 7 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3) | 8 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z3); Takistoskop ×2 (Z3); Görselleştirme ×2 (Z3); Kelime gruplama ×2 (Z3) | 8 |
| 15 | 3. hafta / 1. gün | Seri görsel sunum (RSVP) ×2 (Z4); İç ses azaltma ×2 (Z4); Hızlı okuma ×2 (Z4); Kelime gruplama ×2 (Z4) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×2 (Z4); Geri dönüş azaltma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 6 |
| 17 | 3. hafta / 3. gün | İç ses azaltma ×2 (Z4); Solan metin ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Sakkadik göz hareketi ×2 (Z4) | 8 |
| 18 | 3. hafta / 4. gün | Hızlı okuma ×2 (Z4); Solan metin ×2 (Z4); Kelime gruplama ×2 (Z4); Geri dönüş azaltma ×2 (Z4) | 8 |
| 19 | 3. hafta / 5. gün | Seri görsel sunum (RSVP) ×3 (Z4); İç ses azaltma ×2 (Z4); Hızlı okuma ×2 (Z4) | 7 |
| 20 | 3. hafta / 6. gün | Geri dönüş azaltma ×2 (Z4); Solan metin ×2 (Z4); Schulte tablosu ×2 (Z4); Odaklanma ×2 (Z4) | 8 |
| 21 | 3. hafta / 7. gün | Hızlı okuma ×2 (Z4); İç ses azaltma ×2 (Z4); Görselleştirme ×2 (Z4) | 6 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z4); Tarama ×2 (Z4); Hızlı okuma ×2 (Z4) | 7 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z4); Anlama ×2 (Z4); Kelime çalışması ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 8 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z4); Anlama ×2 (Z4); Hata analizi ×2 (Z4) | 7 |
| 25 | 4. hafta / 4. gün | Hızlı okuma ×2 (Z4); Anlama ×3 (Z4); Göz gezdirme ×2 (Z4) | 7 |
| 26 | 4. hafta / 5. gün | Anlama ×3 (Z4); Tarama ×2 (Z4); Kelime çalışması ×2 (Z4) | 7 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4); Anlama ×2 (Z4); Odaklanma ×2 (Z4) | 8 |
| 28 | 4. hafta / 7. gün | Anlama ×2 (Z4); Serbest okuma ×2 (Z4); Kelime çalışması ×2 (Z4); Görselleştirme ×2 (Z4) | 8 |

### 17-21 Yaş Başlangıç — fe66d56c

- Kimlik: `fe66d56c-6c43-48f1-8f0e-9b80c6d5a30e`
- Hedef yaş: 17–21 (Genç Yetişkin). Puan: 0–40.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 2; azami: 3; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 9.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Genç yetişkinler için temel hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z2); Sakkadik göz hareketi ×3 (Z2); Göz takibi ×2 (Z2); Schulte tablosu ×2 (Z2); Odaklanma ×2 (Z2) | 11 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z2); Sabitleme ×2 (Z2); Schulte tablosu ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z2); Odaklanma ×2 (Z2); Schulte tablosu ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 9 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z2); Sabitleme ×2 (Z2); Göz takibi ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z2); Sakkadik göz hareketi ×2 (Z2); Odaklanma ×2 (Z2); Sabitleme ×2 (Z2) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z2); Sabitleme ×2 (Z2); Sakkadik göz hareketi ×2 (Z2); Schulte tablosu ×2 (Z2); Takistoskop ×2 (Z2) | 10 |
| 7 | 1. hafta / 7. gün | Odaklanma ×2 (Z2); Schulte tablosu ×2 (Z2); Göz takibi ×2 (Z2); Görselleştirme ×2 (Z2) | 8 |
| 8 | 2. hafta / 1. gün | Seri görsel sunum (RSVP) ×3 (Z2); Kelime gruplama ×2 (Z2); Görsel genişleme ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 9 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Schulte tablosu ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 10 | 2. hafta / 3. gün | Görsel genişleme ×3 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Kelime gruplama ×2 (Z2); Göz takibi ×2 (Z2) | 9 |
| 11 | 2. hafta / 4. gün | Seri görsel sunum (RSVP) ×3 (Z2); Görsel genişleme ×2 (Z2); Sabitleme ×2 (Z2); Kelime gruplama ×2 (Z2) | 9 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z2); Seri görsel sunum (RSVP) ×3 (Z2); Görsel genişleme ×2 (Z2) | 8 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z2); Kelime gruplama ×2 (Z2); Schulte tablosu ×2 (Z2); Odaklanma ×2 (Z2); Takistoskop ×2 (Z2) | 10 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Görselleştirme ×2 (Z2); Kelime gruplama ×2 (Z2) | 8 |
| 15 | 3. hafta / 1. gün | İç ses azaltma ×2 (Z3); Hızlı okuma ×2 (Z3); Solan metin ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×3 (Z3); Geri dönüş azaltma ×2 (Z3); Kelime gruplama ×2 (Z3) | 7 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z3); İç ses azaltma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Hızlı okuma ×2 (Z3) | 8 |
| 18 | 3. hafta / 4. gün | Geri dönüş azaltma ×2 (Z3); Hızlı okuma ×3 (Z3); Kelime gruplama ×2 (Z3); Solan metin ×2 (Z3) | 9 |
| 19 | 3. hafta / 5. gün | İç ses azaltma ×3 (Z3); Solan metin ×2 (Z3); Hızlı okuma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z3); Geri dönüş azaltma ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3) | 8 |
| 21 | 3. hafta / 7. gün | İç ses azaltma ×2 (Z3); Hızlı okuma ×2 (Z3); Görselleştirme ×2 (Z3); Solan metin ×2 (Z3) | 8 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z3); Tarama ×2 (Z3); Göz gezdirme ×2 (Z3); Hızlı okuma ×2 (Z3) | 9 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z3); Anlama ×3 (Z3); Kelime çalışması ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z3); Hızlı okuma ×2 (Z3); Anlama ×2 (Z3); Hata analizi ×2 (Z3) | 9 |
| 25 | 4. hafta / 4. gün | Anlama ×3 (Z3); Göz gezdirme ×2 (Z3); Hızlı okuma ×2 (Z3); Tarama ×2 (Z3) | 9 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×2 (Z3); Anlama ×2 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3) | 8 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z3); Anlama ×2 (Z3); Hızlı okuma ×2 (Z3); Odaklanma ×2 (Z3); Schulte tablosu ×2 (Z3) | 10 |
| 28 | 4. hafta / 7. gün | Serbest okuma ×2 (Z3); Anlama ×2 (Z3); Kelime çalışması ×2 (Z3); Görselleştirme ×2 (Z3) | 8 |

### 17-21 Yaş Orta Seviye — 6fe886c0

- Kimlik: `6fe886c0-15f9-4323-869d-bff50c641580`
- Hedef yaş: 17–21 (Genç Yetişkin). Puan: 40–70.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 3; azami: 4; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 10.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Genç yetişkinler için orta seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z3); Sakkadik göz hareketi ×3 (Z3); Göz takibi ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3) | 11 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z3); Sabitleme ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z3); Odaklanma ×2 (Z3); Schulte tablosu ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 9 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z3); Sabitleme ×2 (Z3); Göz takibi ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z3); Sakkadik göz hareketi ×2 (Z3); Odaklanma ×2 (Z3); Sabitleme ×2 (Z3) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z3); Sabitleme ×2 (Z3); Sakkadik göz hareketi ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 10 |
| 7 | 1. hafta / 7. gün | Odaklanma ×2 (Z3); Schulte tablosu ×2 (Z3); Göz takibi ×2 (Z3); Görselleştirme ×2 (Z3) | 8 |
| 8 | 2. hafta / 1. gün | Seri görsel sunum (RSVP) ×3 (Z3); Kelime gruplama ×2 (Z3); Görsel genişleme ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 9 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 10 | 2. hafta / 3. gün | Görsel genişleme ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Göz takibi ×2 (Z3) | 9 |
| 11 | 2. hafta / 4. gün | Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3); Sabitleme ×2 (Z3); Kelime gruplama ×2 (Z3) | 9 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z3); Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3) | 8 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3); Takistoskop ×2 (Z3) | 10 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Görselleştirme ×2 (Z3); Kelime gruplama ×2 (Z3) | 8 |
| 15 | 3. hafta / 1. gün | İç ses azaltma ×2 (Z4); Hızlı okuma ×2 (Z4); Solan metin ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×3 (Z4); Geri dönüş azaltma ×2 (Z4); Kelime gruplama ×2 (Z4) | 7 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z4); İç ses azaltma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Hızlı okuma ×2 (Z4) | 8 |
| 18 | 3. hafta / 4. gün | Geri dönüş azaltma ×2 (Z4); Hızlı okuma ×3 (Z4); Kelime gruplama ×2 (Z4); Solan metin ×2 (Z4) | 9 |
| 19 | 3. hafta / 5. gün | İç ses azaltma ×3 (Z4); Solan metin ×2 (Z4); Hızlı okuma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z4); Geri dönüş azaltma ×2 (Z4); Schulte tablosu ×2 (Z4); Odaklanma ×2 (Z4) | 8 |
| 21 | 3. hafta / 7. gün | İç ses azaltma ×2 (Z4); Hızlı okuma ×2 (Z4); Görselleştirme ×2 (Z4); Solan metin ×2 (Z4) | 8 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z4); Tarama ×2 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z4); Anlama ×3 (Z4); Kelime çalışması ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z4); Hızlı okuma ×2 (Z4); Anlama ×2 (Z4); Hata analizi ×2 (Z4) | 9 |
| 25 | 4. hafta / 4. gün | Anlama ×3 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4); Tarama ×2 (Z4) | 9 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×2 (Z4); Anlama ×2 (Z4); Tarama ×2 (Z4); Kelime çalışması ×2 (Z4) | 8 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z4); Anlama ×2 (Z4); Hızlı okuma ×2 (Z4); Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4) | 10 |
| 28 | 4. hafta / 7. gün | Serbest okuma ×2 (Z4); Anlama ×2 (Z4); Kelime çalışması ×2 (Z4); Görselleştirme ×2 (Z4) | 8 |

### 17-21 Yaş İleri Seviye — 50f6efdb

- Kimlik: `50f6efdb-f690-49fd-9922-8d8fbd8816c2`
- Hedef yaş: 17–21 (Genç Yetişkin). Puan: 70–100.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 4; azami: 5; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 11.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Genç yetişkinler için ileri seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z4); Sakkadik göz hareketi ×3 (Z4); Göz takibi ×2 (Z4); Schulte tablosu ×2 (Z4); Odaklanma ×2 (Z4) | 11 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z4); Sabitleme ×2 (Z4); Schulte tablosu ×2 (Z4); Takistoskop ×2 (Z4) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z4); Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4); Sakkadik göz hareketi ×2 (Z4) | 9 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z4); Sabitleme ×2 (Z4); Göz takibi ×2 (Z4); Takistoskop ×2 (Z4) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z4); Sakkadik göz hareketi ×2 (Z4); Odaklanma ×2 (Z4); Sabitleme ×2 (Z4) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z4); Sabitleme ×2 (Z4); Sakkadik göz hareketi ×2 (Z4); Schulte tablosu ×2 (Z4); Takistoskop ×2 (Z4) | 10 |
| 7 | 1. hafta / 7. gün | Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4); Göz takibi ×2 (Z4); Görselleştirme ×2 (Z4) | 8 |
| 8 | 2. hafta / 1. gün | Seri görsel sunum (RSVP) ×3 (Z4); Kelime gruplama ×2 (Z4); Görsel genişleme ×2 (Z4); Sakkadik göz hareketi ×2 (Z4) | 9 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Schulte tablosu ×2 (Z4); Takistoskop ×2 (Z4) | 9 |
| 10 | 2. hafta / 3. gün | Görsel genişleme ×3 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Kelime gruplama ×2 (Z4); Göz takibi ×2 (Z4) | 9 |
| 11 | 2. hafta / 4. gün | Seri görsel sunum (RSVP) ×3 (Z4); Görsel genişleme ×2 (Z4); Sabitleme ×2 (Z4); Kelime gruplama ×2 (Z4) | 9 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z4); Seri görsel sunum (RSVP) ×3 (Z4); Görsel genişleme ×2 (Z4) | 8 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z4); Kelime gruplama ×2 (Z4); Schulte tablosu ×2 (Z4); Odaklanma ×2 (Z4); Takistoskop ×2 (Z4) | 10 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Görselleştirme ×2 (Z4); Kelime gruplama ×2 (Z4) | 8 |
| 15 | 3. hafta / 1. gün | İç ses azaltma ×2 (Z5); Hızlı okuma ×2 (Z5); Solan metin ×2 (Z5); Seri görsel sunum (RSVP) ×2 (Z5) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×3 (Z5); Geri dönüş azaltma ×2 (Z5); Kelime gruplama ×2 (Z5) | 7 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z5); İç ses azaltma ×2 (Z5); Seri görsel sunum (RSVP) ×2 (Z5); Hızlı okuma ×2 (Z5) | 8 |
| 18 | 3. hafta / 4. gün | Geri dönüş azaltma ×2 (Z5); Hızlı okuma ×3 (Z5); Kelime gruplama ×2 (Z5); Solan metin ×2 (Z5) | 9 |
| 19 | 3. hafta / 5. gün | İç ses azaltma ×3 (Z5); Solan metin ×2 (Z5); Hızlı okuma ×2 (Z5); Seri görsel sunum (RSVP) ×2 (Z5) | 9 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z5); Geri dönüş azaltma ×2 (Z5); Schulte tablosu ×2 (Z5); Odaklanma ×2 (Z5) | 8 |
| 21 | 3. hafta / 7. gün | İç ses azaltma ×2 (Z5); Hızlı okuma ×2 (Z5); Görselleştirme ×2 (Z5); Solan metin ×2 (Z5) | 8 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z5); Tarama ×2 (Z5); Göz gezdirme ×2 (Z5); Hızlı okuma ×2 (Z5) | 9 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z5); Anlama ×3 (Z5); Kelime çalışması ×2 (Z5); Seri görsel sunum (RSVP) ×2 (Z5) | 9 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z5); Hızlı okuma ×2 (Z5); Anlama ×2 (Z5); Hata analizi ×2 (Z5) | 9 |
| 25 | 4. hafta / 4. gün | Anlama ×3 (Z5); Göz gezdirme ×2 (Z5); Hızlı okuma ×2 (Z5); Tarama ×2 (Z5) | 9 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×2 (Z5); Anlama ×2 (Z5); Tarama ×2 (Z5); Kelime çalışması ×2 (Z5) | 8 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z5); Anlama ×2 (Z5); Hızlı okuma ×2 (Z5); Odaklanma ×2 (Z5); Schulte tablosu ×2 (Z5) | 10 |
| 28 | 4. hafta / 7. gün | Serbest okuma ×2 (Z5); Anlama ×2 (Z5); Kelime çalışması ×2 (Z5); Görselleştirme ×2 (Z5) | 8 |

### 22+ Yaş Orta Seviye — eaf89bb3

- Kimlik: `eaf89bb3-7427-4287-a6de-d19f49b782b6`
- Hedef yaş: 22–+ (Yetişkin). Puan: 0–60.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 3; azami: 4; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 13.
- Toplam atama kaydı: 1.
- Kayıt açıklaması: Yetişkinler için orta seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z3); Sakkadik göz hareketi ×3 (Z3); Göz takibi ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3) | 11 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z3); Sabitleme ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z3); Odaklanma ×2 (Z3); Schulte tablosu ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 9 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z3); Sabitleme ×2 (Z3); Göz takibi ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z3); Sakkadik göz hareketi ×2 (Z3); Odaklanma ×2 (Z3); Sabitleme ×2 (Z3) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z3); Sabitleme ×2 (Z3); Sakkadik göz hareketi ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 10 |
| 7 | 1. hafta / 7. gün | Odaklanma ×2 (Z3); Schulte tablosu ×2 (Z3); Göz takibi ×2 (Z3); Görselleştirme ×2 (Z3) | 8 |
| 8 | 2. hafta / 1. gün | Seri görsel sunum (RSVP) ×3 (Z3); Kelime gruplama ×2 (Z3); Görsel genişleme ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 9 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 10 | 2. hafta / 3. gün | Görsel genişleme ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Göz takibi ×2 (Z3) | 9 |
| 11 | 2. hafta / 4. gün | Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3); Sabitleme ×2 (Z3); Kelime gruplama ×2 (Z3) | 9 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z3); Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3) | 8 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3); Takistoskop ×2 (Z3) | 10 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Görselleştirme ×2 (Z3); Kelime gruplama ×2 (Z3) | 8 |
| 15 | 3. hafta / 1. gün | İç ses azaltma ×2 (Z4); Hızlı okuma ×2 (Z4); Solan metin ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×3 (Z4); Geri dönüş azaltma ×2 (Z4); Kelime gruplama ×2 (Z4) | 7 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z4); İç ses azaltma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Hızlı okuma ×2 (Z4) | 8 |
| 18 | 3. hafta / 4. gün | Geri dönüş azaltma ×2 (Z4); Hızlı okuma ×3 (Z4); Kelime gruplama ×2 (Z4); Solan metin ×2 (Z4) | 9 |
| 19 | 3. hafta / 5. gün | İç ses azaltma ×3 (Z4); Solan metin ×2 (Z4); Hızlı okuma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z4); Geri dönüş azaltma ×2 (Z4); Schulte tablosu ×2 (Z4); Odaklanma ×2 (Z4) | 8 |
| 21 | 3. hafta / 7. gün | İç ses azaltma ×2 (Z4); Hızlı okuma ×2 (Z4); Görselleştirme ×2 (Z4); Solan metin ×2 (Z4) | 8 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z4); Tarama ×2 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z4); Anlama ×3 (Z4); Kelime çalışması ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z4); Hızlı okuma ×2 (Z4); Anlama ×2 (Z4); Hata analizi ×2 (Z4) | 9 |
| 25 | 4. hafta / 4. gün | Anlama ×3 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4); Tarama ×2 (Z4) | 9 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×2 (Z4); Anlama ×2 (Z4); Tarama ×2 (Z4); Kelime çalışması ×2 (Z4) | 8 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z4); Anlama ×2 (Z4); Hızlı okuma ×2 (Z4); Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4) | 10 |
| 28 | 4. hafta / 7. gün | Serbest okuma ×2 (Z4); Anlama ×2 (Z4); Kelime çalışması ×2 (Z4); Görselleştirme ×2 (Z4) | 8 |

### 22+ Yaş Orta Seviye — f035811f

- Kimlik: `f035811f-31ae-4665-bc53-bb7df2d197f4`
- Hedef yaş: 22–+ (Yetişkin). Puan: 0–60.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 3; azami: 4; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 13.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Yetişkinler için orta seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z3); Sakkadik göz hareketi ×3 (Z3); Göz takibi ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3) | 11 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z3); Sabitleme ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z3); Odaklanma ×2 (Z3); Schulte tablosu ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 9 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z3); Sabitleme ×2 (Z3); Göz takibi ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z3); Sakkadik göz hareketi ×2 (Z3); Odaklanma ×2 (Z3); Sabitleme ×2 (Z3) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z3); Sabitleme ×2 (Z3); Sakkadik göz hareketi ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 10 |
| 7 | 1. hafta / 7. gün | Odaklanma ×2 (Z3); Schulte tablosu ×2 (Z3); Göz takibi ×2 (Z3); Görselleştirme ×2 (Z3) | 8 |
| 8 | 2. hafta / 1. gün | Seri görsel sunum (RSVP) ×3 (Z3); Kelime gruplama ×2 (Z3); Görsel genişleme ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 9 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 10 | 2. hafta / 3. gün | Görsel genişleme ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Göz takibi ×2 (Z3) | 9 |
| 11 | 2. hafta / 4. gün | Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3); Sabitleme ×2 (Z3); Kelime gruplama ×2 (Z3) | 9 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z3); Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3) | 8 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3); Takistoskop ×2 (Z3) | 10 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Görselleştirme ×2 (Z3); Kelime gruplama ×2 (Z3) | 8 |
| 15 | 3. hafta / 1. gün | İç ses azaltma ×2 (Z4); Hızlı okuma ×2 (Z4); Solan metin ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×3 (Z4); Geri dönüş azaltma ×2 (Z4); Kelime gruplama ×2 (Z4) | 7 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z4); İç ses azaltma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Hızlı okuma ×2 (Z4) | 8 |
| 18 | 3. hafta / 4. gün | Geri dönüş azaltma ×2 (Z4); Hızlı okuma ×3 (Z4); Kelime gruplama ×2 (Z4); Solan metin ×2 (Z4) | 9 |
| 19 | 3. hafta / 5. gün | İç ses azaltma ×3 (Z4); Solan metin ×2 (Z4); Hızlı okuma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z4); Geri dönüş azaltma ×2 (Z4); Schulte tablosu ×2 (Z4); Odaklanma ×2 (Z4) | 8 |
| 21 | 3. hafta / 7. gün | İç ses azaltma ×2 (Z4); Hızlı okuma ×2 (Z4); Görselleştirme ×2 (Z4); Solan metin ×2 (Z4) | 8 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z4); Tarama ×2 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z4); Anlama ×3 (Z4); Kelime çalışması ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z4); Hızlı okuma ×2 (Z4); Anlama ×2 (Z4); Hata analizi ×2 (Z4) | 9 |
| 25 | 4. hafta / 4. gün | Anlama ×3 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4); Tarama ×2 (Z4) | 9 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×2 (Z4); Anlama ×2 (Z4); Tarama ×2 (Z4); Kelime çalışması ×2 (Z4) | 8 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z4); Anlama ×2 (Z4); Hızlı okuma ×2 (Z4); Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4) | 10 |
| 28 | 4. hafta / 7. gün | Serbest okuma ×2 (Z4); Anlama ×2 (Z4); Kelime çalışması ×2 (Z4); Görselleştirme ×2 (Z4) | 8 |

### 22+ Yaş İleri Seviye — 5f325bf0

- Kimlik: `5f325bf0-159b-4bbd-861c-bac8f44b5dae`
- Hedef yaş: 22–+ (Yetişkin). Puan: 60–100.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 4; azami: 5; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 14.
- Toplam atama kaydı: 1.
- Kayıt açıklaması: Yetişkinler için ileri seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z4); Sakkadik göz hareketi ×3 (Z4); Göz takibi ×2 (Z4); Schulte tablosu ×2 (Z4); Odaklanma ×2 (Z4) | 11 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z4); Sabitleme ×2 (Z4); Schulte tablosu ×2 (Z4); Takistoskop ×2 (Z4) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z4); Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4); Sakkadik göz hareketi ×2 (Z4) | 9 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z4); Sabitleme ×2 (Z4); Göz takibi ×2 (Z4); Takistoskop ×2 (Z4) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z4); Sakkadik göz hareketi ×2 (Z4); Odaklanma ×2 (Z4); Sabitleme ×2 (Z4) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z4); Sabitleme ×2 (Z4); Sakkadik göz hareketi ×2 (Z4); Schulte tablosu ×2 (Z4); Takistoskop ×2 (Z4) | 10 |
| 7 | 1. hafta / 7. gün | Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4); Göz takibi ×2 (Z4); Görselleştirme ×2 (Z4) | 8 |
| 8 | 2. hafta / 1. gün | Seri görsel sunum (RSVP) ×3 (Z4); Kelime gruplama ×2 (Z4); Görsel genişleme ×2 (Z4); Sakkadik göz hareketi ×2 (Z4) | 9 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×3 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Schulte tablosu ×2 (Z4); Takistoskop ×2 (Z4) | 9 |
| 10 | 2. hafta / 3. gün | Görsel genişleme ×3 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Kelime gruplama ×2 (Z4); Göz takibi ×2 (Z4) | 9 |
| 11 | 2. hafta / 4. gün | Seri görsel sunum (RSVP) ×3 (Z4); Görsel genişleme ×2 (Z4); Sabitleme ×2 (Z4); Kelime gruplama ×2 (Z4) | 9 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z4); Seri görsel sunum (RSVP) ×3 (Z4); Görsel genişleme ×2 (Z4) | 8 |
| 13 | 2. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z4); Kelime gruplama ×2 (Z4); Schulte tablosu ×2 (Z4); Odaklanma ×2 (Z4); Takistoskop ×2 (Z4) | 10 |
| 14 | 2. hafta / 7. gün | Görsel genişleme ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Görselleştirme ×2 (Z4); Kelime gruplama ×2 (Z4) | 8 |
| 15 | 3. hafta / 1. gün | İç ses azaltma ×2 (Z5); Hızlı okuma ×2 (Z5); Solan metin ×2 (Z5); Seri görsel sunum (RSVP) ×2 (Z5) | 8 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×3 (Z5); Geri dönüş azaltma ×2 (Z5); Kelime gruplama ×2 (Z5) | 7 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z5); İç ses azaltma ×2 (Z5); Seri görsel sunum (RSVP) ×2 (Z5); Hızlı okuma ×2 (Z5) | 8 |
| 18 | 3. hafta / 4. gün | Geri dönüş azaltma ×2 (Z5); Hızlı okuma ×3 (Z5); Kelime gruplama ×2 (Z5); Solan metin ×2 (Z5) | 9 |
| 19 | 3. hafta / 5. gün | İç ses azaltma ×3 (Z5); Solan metin ×2 (Z5); Hızlı okuma ×2 (Z5); Seri görsel sunum (RSVP) ×2 (Z5) | 9 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z5); Geri dönüş azaltma ×2 (Z5); Schulte tablosu ×2 (Z5); Odaklanma ×2 (Z5) | 8 |
| 21 | 3. hafta / 7. gün | İç ses azaltma ×2 (Z5); Hızlı okuma ×2 (Z5); Görselleştirme ×2 (Z5); Solan metin ×2 (Z5) | 8 |
| 22 | 4. hafta / 1. gün | Anlama ×3 (Z5); Tarama ×2 (Z5); Göz gezdirme ×2 (Z5); Hızlı okuma ×2 (Z5) | 9 |
| 23 | 4. hafta / 2. gün | Göz gezdirme ×2 (Z5); Anlama ×3 (Z5); Kelime çalışması ×2 (Z5); Seri görsel sunum (RSVP) ×2 (Z5) | 9 |
| 24 | 4. hafta / 3. gün | Tarama ×3 (Z5); Hızlı okuma ×2 (Z5); Anlama ×2 (Z5); Hata analizi ×2 (Z5) | 9 |
| 25 | 4. hafta / 4. gün | Anlama ×3 (Z5); Göz gezdirme ×2 (Z5); Hızlı okuma ×2 (Z5); Tarama ×2 (Z5) | 9 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×2 (Z5); Anlama ×2 (Z5); Tarama ×2 (Z5); Kelime çalışması ×2 (Z5) | 8 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z5); Anlama ×2 (Z5); Hızlı okuma ×2 (Z5); Odaklanma ×2 (Z5); Schulte tablosu ×2 (Z5) | 10 |
| 28 | 4. hafta / 7. gün | Serbest okuma ×2 (Z5); Anlama ×2 (Z5); Kelime çalışması ×2 (Z5); Görselleştirme ×2 (Z5) | 8 |

### 9-12 Yaş Başlangıç — e5fca714

- Kimlik: `e5fca714-36e1-47b9-9656-10314aa2e9ec`
- Hedef yaş: 9–12 (Çocuk). Puan: 0–40.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 1; azami: 2; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 1.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Çocuklar için temel hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z1); Sakkadik göz hareketi ×2 (Z1); Schulte tablosu ×2 (Z1); Görsel genişleme ×2 (Z1) | 8 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z1); Sabitleme ×2 (Z1); Odaklanma ×2 (Z1) | 7 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z1); Schulte tablosu ×2 (Z1); Görsel genişleme ×2 (Z1) | 7 |
| 4 | 1. hafta / 4. gün | Sabitleme ×3 (Z1); Sakkadik göz hareketi ×2 (Z1); Göz takibi ×2 (Z1) | 7 |
| 5 | 1. hafta / 5. gün | Sakkadik göz hareketi ×3 (Z1); Schulte tablosu ×3 (Z1); Odaklanma ×2 (Z1) | 8 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z1); Görsel genişleme ×2 (Z1); Schulte tablosu ×2 (Z1); Sakkadik göz hareketi ×2 (Z1) | 8 |
| 7 | 1. hafta / 7. gün | Schulte tablosu ×3 (Z1); Odaklanma ×2 (Z1); Göz takibi ×2 (Z1) | 7 |
| 8 | 2. hafta / 1. gün | Görsel genişleme ×3 (Z1); Sakkadik göz hareketi ×2 (Z1); Göz takibi ×2 (Z1) | 7 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×2 (Z1); Görsel genişleme ×2 (Z1); Sabitleme ×2 (Z1) | 6 |
| 10 | 2. hafta / 3. gün | Takistoskop ×2 (Z1); Kelime gruplama ×2 (Z1); Schulte tablosu ×2 (Z1) | 6 |
| 11 | 2. hafta / 4. gün | Görsel genişleme ×3 (Z1); Sakkadik göz hareketi ×2 (Z1); Takistoskop ×2 (Z1) | 7 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z1); Göz takibi ×2 (Z1); Odaklanma ×2 (Z1) | 7 |
| 13 | 2. hafta / 6. gün | Takistoskop ×2 (Z1); Görsel genişleme ×2 (Z1); Schulte tablosu ×3 (Z1) | 7 |
| 14 | 2. hafta / 7. gün | Kelime gruplama ×2 (Z1); Sabitleme ×2 (Z1); Görselleştirme ×2 (Z1) | 6 |
| 15 | 3. hafta / 1. gün | Seri görsel sunum (RSVP) ×2 (Z2); Kelime gruplama ×2 (Z2); Görsel genişleme ×2 (Z2) | 6 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 6 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Takistoskop ×2 (Z2) | 6 |
| 18 | 3. hafta / 4. gün | Kelime gruplama ×2 (Z2); Hızlı okuma ×2 (Z2); Görsel genişleme ×2 (Z2) | 6 |
| 19 | 3. hafta / 5. gün | Seri görsel sunum (RSVP) ×3 (Z2); Solan metin ×2 (Z2); Odaklanma ×2 (Z2) | 7 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z2); Kelime gruplama ×2 (Z2); Schulte tablosu ×2 (Z2) | 6 |
| 21 | 3. hafta / 7. gün | Seri görsel sunum (RSVP) ×2 (Z2); Görsel genişleme ×3 (Z2); Görselleştirme ×2 (Z2) | 7 |
| 22 | 4. hafta / 1. gün | Anlama ×2 (Z2); Hızlı okuma ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2) | 6 |
| 23 | 4. hafta / 2. gün | Tarama ×2 (Z2); Anlama ×2 (Z2); Kelime gruplama ×2 (Z2) | 6 |
| 24 | 4. hafta / 3. gün | Kelime çalışması ×2 (Z2); Göz gezdirme ×2 (Z2); Anlama ×2 (Z2) | 6 |
| 25 | 4. hafta / 4. gün | Hızlı okuma ×2 (Z2); Tarama ×2 (Z2); Görsel genişleme ×2 (Z2) | 6 |
| 26 | 4. hafta / 5. gün | Anlama ×3 (Z2); Kelime çalışması ×2 (Z2); Odaklanma ×2 (Z2) | 7 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z2); Hızlı okuma ×2 (Z2); Schulte tablosu ×2 (Z2) | 6 |
| 28 | 4. hafta / 7. gün | Anlama ×2 (Z2); Serbest okuma ×2 (Z2); Görselleştirme ×2 (Z2) | 6 |

### 9-12 Yaş Orta Seviye — dc591065

- Kimlik: `dc591065-8748-491d-8c7f-306ffe8fd175`
- Hedef yaş: 9–12 (Çocuk). Puan: 40–70.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 2; azami: 3; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 2.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Çocuklar için orta seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z2); Sakkadik göz hareketi ×2 (Z2); Schulte tablosu ×2 (Z2); Görsel genişleme ×2 (Z2) | 8 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z2); Sabitleme ×2 (Z2); Odaklanma ×2 (Z2) | 7 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z2); Schulte tablosu ×2 (Z2); Görsel genişleme ×2 (Z2) | 7 |
| 4 | 1. hafta / 4. gün | Sabitleme ×3 (Z2); Sakkadik göz hareketi ×2 (Z2); Göz takibi ×2 (Z2) | 7 |
| 5 | 1. hafta / 5. gün | Sakkadik göz hareketi ×3 (Z2); Schulte tablosu ×3 (Z2); Odaklanma ×2 (Z2) | 8 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z2); Görsel genişleme ×2 (Z2); Schulte tablosu ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 8 |
| 7 | 1. hafta / 7. gün | Schulte tablosu ×3 (Z2); Odaklanma ×2 (Z2); Göz takibi ×2 (Z2) | 7 |
| 8 | 2. hafta / 1. gün | Görsel genişleme ×3 (Z2); Sakkadik göz hareketi ×2 (Z2); Göz takibi ×2 (Z2) | 7 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×2 (Z2); Görsel genişleme ×2 (Z2); Sabitleme ×2 (Z2) | 6 |
| 10 | 2. hafta / 3. gün | Takistoskop ×2 (Z2); Kelime gruplama ×2 (Z2); Schulte tablosu ×2 (Z2) | 6 |
| 11 | 2. hafta / 4. gün | Görsel genişleme ×3 (Z2); Sakkadik göz hareketi ×2 (Z2); Takistoskop ×2 (Z2) | 7 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z2); Göz takibi ×2 (Z2); Odaklanma ×2 (Z2) | 7 |
| 13 | 2. hafta / 6. gün | Takistoskop ×2 (Z2); Görsel genişleme ×2 (Z2); Schulte tablosu ×3 (Z2) | 7 |
| 14 | 2. hafta / 7. gün | Kelime gruplama ×2 (Z2); Sabitleme ×2 (Z2); Görselleştirme ×2 (Z2) | 6 |
| 15 | 3. hafta / 1. gün | Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Görsel genişleme ×2 (Z3) | 6 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 6 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Takistoskop ×2 (Z3) | 6 |
| 18 | 3. hafta / 4. gün | Kelime gruplama ×2 (Z3); Hızlı okuma ×2 (Z3); Görsel genişleme ×2 (Z3) | 6 |
| 19 | 3. hafta / 5. gün | Seri görsel sunum (RSVP) ×3 (Z3); Solan metin ×2 (Z3); Odaklanma ×2 (Z3) | 7 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z3); Kelime gruplama ×2 (Z3); Schulte tablosu ×2 (Z3) | 6 |
| 21 | 3. hafta / 7. gün | Seri görsel sunum (RSVP) ×2 (Z3); Görsel genişleme ×3 (Z3); Görselleştirme ×2 (Z3) | 7 |
| 22 | 4. hafta / 1. gün | Anlama ×2 (Z3); Hızlı okuma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 6 |
| 23 | 4. hafta / 2. gün | Tarama ×2 (Z3); Anlama ×2 (Z3); Kelime gruplama ×2 (Z3) | 6 |
| 24 | 4. hafta / 3. gün | Kelime çalışması ×2 (Z3); Göz gezdirme ×2 (Z3); Anlama ×2 (Z3) | 6 |
| 25 | 4. hafta / 4. gün | Hızlı okuma ×2 (Z3); Tarama ×2 (Z3); Görsel genişleme ×2 (Z3) | 6 |
| 26 | 4. hafta / 5. gün | Anlama ×3 (Z3); Kelime çalışması ×2 (Z3); Odaklanma ×2 (Z3) | 7 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z3); Hızlı okuma ×2 (Z3); Schulte tablosu ×2 (Z3) | 6 |
| 28 | 4. hafta / 7. gün | Anlama ×2 (Z3); Serbest okuma ×2 (Z3); Görselleştirme ×2 (Z3) | 6 |

### 9-12 Yaş İleri Seviye — e4202043

- Kimlik: `e4202043-0767-4969-9d30-8ef07ceb9b21`
- Hedef yaş: 9–12 (Çocuk). Puan: 70–100.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 3; azami: 4; artış ayarı: 2 haftada bir.
- Program tür kodu: 0; sınav etiketi: yok; görüntüleme sırası: 3.
- Toplam atama kaydı: 3.
- Kayıt açıklaması: Çocuklar için ileri seviye hızlı okuma programı. 21 farklı egzersiz tipi ile 4 haftalık bilimsel progresyon.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z3); Sakkadik göz hareketi ×2 (Z3); Schulte tablosu ×2 (Z3); Görsel genişleme ×2 (Z3) | 8 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z3); Sabitleme ×2 (Z3); Odaklanma ×2 (Z3) | 7 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z3); Schulte tablosu ×2 (Z3); Görsel genişleme ×2 (Z3) | 7 |
| 4 | 1. hafta / 4. gün | Sabitleme ×3 (Z3); Sakkadik göz hareketi ×2 (Z3); Göz takibi ×2 (Z3) | 7 |
| 5 | 1. hafta / 5. gün | Sakkadik göz hareketi ×3 (Z3); Schulte tablosu ×3 (Z3); Odaklanma ×2 (Z3) | 8 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z3); Görsel genişleme ×2 (Z3); Schulte tablosu ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 8 |
| 7 | 1. hafta / 7. gün | Schulte tablosu ×3 (Z3); Odaklanma ×2 (Z3); Göz takibi ×2 (Z3) | 7 |
| 8 | 2. hafta / 1. gün | Görsel genişleme ×3 (Z3); Sakkadik göz hareketi ×2 (Z3); Göz takibi ×2 (Z3) | 7 |
| 9 | 2. hafta / 2. gün | Kelime gruplama ×2 (Z3); Görsel genişleme ×2 (Z3); Sabitleme ×2 (Z3) | 6 |
| 10 | 2. hafta / 3. gün | Takistoskop ×2 (Z3); Kelime gruplama ×2 (Z3); Schulte tablosu ×2 (Z3) | 6 |
| 11 | 2. hafta / 4. gün | Görsel genişleme ×3 (Z3); Sakkadik göz hareketi ×2 (Z3); Takistoskop ×2 (Z3) | 7 |
| 12 | 2. hafta / 5. gün | Kelime gruplama ×3 (Z3); Göz takibi ×2 (Z3); Odaklanma ×2 (Z3) | 7 |
| 13 | 2. hafta / 6. gün | Takistoskop ×2 (Z3); Görsel genişleme ×2 (Z3); Schulte tablosu ×3 (Z3) | 7 |
| 14 | 2. hafta / 7. gün | Kelime gruplama ×2 (Z3); Sabitleme ×2 (Z3); Görselleştirme ×2 (Z3) | 6 |
| 15 | 3. hafta / 1. gün | Seri görsel sunum (RSVP) ×2 (Z4); Kelime gruplama ×2 (Z4); Görsel genişleme ×2 (Z4) | 6 |
| 16 | 3. hafta / 2. gün | Hızlı okuma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Sakkadik göz hareketi ×2 (Z4) | 6 |
| 17 | 3. hafta / 3. gün | Solan metin ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4); Takistoskop ×2 (Z4) | 6 |
| 18 | 3. hafta / 4. gün | Kelime gruplama ×2 (Z4); Hızlı okuma ×2 (Z4); Görsel genişleme ×2 (Z4) | 6 |
| 19 | 3. hafta / 5. gün | Seri görsel sunum (RSVP) ×3 (Z4); Solan metin ×2 (Z4); Odaklanma ×2 (Z4) | 7 |
| 20 | 3. hafta / 6. gün | Hızlı okuma ×2 (Z4); Kelime gruplama ×2 (Z4); Schulte tablosu ×2 (Z4) | 6 |
| 21 | 3. hafta / 7. gün | Seri görsel sunum (RSVP) ×2 (Z4); Görsel genişleme ×3 (Z4); Görselleştirme ×2 (Z4) | 7 |
| 22 | 4. hafta / 1. gün | Anlama ×2 (Z4); Hızlı okuma ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 6 |
| 23 | 4. hafta / 2. gün | Tarama ×2 (Z4); Anlama ×2 (Z4); Kelime gruplama ×2 (Z4) | 6 |
| 24 | 4. hafta / 3. gün | Kelime çalışması ×2 (Z4); Göz gezdirme ×2 (Z4); Anlama ×2 (Z4) | 6 |
| 25 | 4. hafta / 4. gün | Hızlı okuma ×2 (Z4); Tarama ×2 (Z4); Görsel genişleme ×2 (Z4) | 6 |
| 26 | 4. hafta / 5. gün | Anlama ×3 (Z4); Kelime çalışması ×2 (Z4); Odaklanma ×2 (Z4) | 7 |
| 27 | 4. hafta / 6. gün | Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4); Schulte tablosu ×2 (Z4) | 6 |
| 28 | 4. hafta / 7. gün | Anlama ×2 (Z4); Serbest okuma ×2 (Z4); Görselleştirme ×2 (Z4) | 6 |

### Hızlı Okuma Maratonu (8 Hafta) — 226afedb

- Kimlik: `226afedb-99cd-4b60-a284-d7af0f6ca458`
- Hedef yaş: 22–+ (Yetişkin). Puan: 40–100.
- Süre: 8 hafta / 56 gün. Başlangıç zorluğu: 2; azami: 5; artış ayarı: 3 haftada bir.
- Program tür kodu: 3; sınav etiketi: yok; görüntüleme sırası: 16.
- Toplam atama kaydı: 4.
- Kayıt açıklaması: Kalıcı alışkanlık kazanmak isteyenler için 8 haftalık uzun soluklu gelişim programı. İstikrarlı ilerleme.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Sabitleme ×2 (Z2); Sakkadik göz hareketi ×3 (Z2); Göz takibi ×2 (Z2); Schulte tablosu ×2 (Z2); Odaklanma ×2 (Z2) | 11 |
| 2 | 1. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z2); Sabitleme ×2 (Z2); Schulte tablosu ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 3 | 1. hafta / 3. gün | Göz takibi ×3 (Z2); Odaklanma ×2 (Z2); Schulte tablosu ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 9 |
| 4 | 1. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z2); Sabitleme ×2 (Z2); Göz takibi ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 5 | 1. hafta / 5. gün | Schulte tablosu ×3 (Z2); Sakkadik göz hareketi ×2 (Z2); Odaklanma ×2 (Z2); Sabitleme ×2 (Z2) | 9 |
| 6 | 1. hafta / 6. gün | Göz takibi ×2 (Z2); Sabitleme ×2 (Z2); Sakkadik göz hareketi ×2 (Z2); Schulte tablosu ×2 (Z2); Takistoskop ×2 (Z2) | 10 |
| 7 | 1. hafta / 7. gün | Odaklanma ×2 (Z2); Schulte tablosu ×2 (Z2); Göz takibi ×2 (Z2); Görselleştirme ×2 (Z2) | 8 |
| 8 | 2. hafta / 1. gün | Sabitleme ×2 (Z2); Sakkadik göz hareketi ×3 (Z2); Göz takibi ×2 (Z2); Schulte tablosu ×2 (Z2); Odaklanma ×2 (Z2) | 11 |
| 9 | 2. hafta / 2. gün | Sakkadik göz hareketi ×3 (Z2); Sabitleme ×2 (Z2); Schulte tablosu ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 10 | 2. hafta / 3. gün | Göz takibi ×3 (Z2); Odaklanma ×2 (Z2); Schulte tablosu ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 9 |
| 11 | 2. hafta / 4. gün | Sakkadik göz hareketi ×3 (Z2); Sabitleme ×2 (Z2); Göz takibi ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 12 | 2. hafta / 5. gün | Schulte tablosu ×3 (Z2); Sakkadik göz hareketi ×2 (Z2); Odaklanma ×2 (Z2); Sabitleme ×2 (Z2) | 9 |
| 13 | 2. hafta / 6. gün | Göz takibi ×2 (Z2); Sabitleme ×2 (Z2); Sakkadik göz hareketi ×2 (Z2); Schulte tablosu ×2 (Z2); Takistoskop ×2 (Z2) | 10 |
| 14 | 2. hafta / 7. gün | Odaklanma ×2 (Z2); Schulte tablosu ×2 (Z2); Göz takibi ×2 (Z2); Görselleştirme ×2 (Z2) | 8 |
| 15 | 3. hafta / 1. gün | Seri görsel sunum (RSVP) ×3 (Z2); Kelime gruplama ×2 (Z2); Görsel genişleme ×2 (Z2); Sakkadik göz hareketi ×2 (Z2) | 9 |
| 16 | 3. hafta / 2. gün | Kelime gruplama ×3 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Schulte tablosu ×2 (Z2); Takistoskop ×2 (Z2) | 9 |
| 17 | 3. hafta / 3. gün | Görsel genişleme ×3 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Kelime gruplama ×2 (Z2); Göz takibi ×2 (Z2) | 9 |
| 18 | 3. hafta / 4. gün | Seri görsel sunum (RSVP) ×3 (Z2); Görsel genişleme ×2 (Z2); Sabitleme ×2 (Z2); Kelime gruplama ×2 (Z2) | 9 |
| 19 | 3. hafta / 5. gün | Kelime gruplama ×3 (Z2); Seri görsel sunum (RSVP) ×3 (Z2); Görsel genişleme ×2 (Z2) | 8 |
| 20 | 3. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z2); Kelime gruplama ×2 (Z2); Schulte tablosu ×2 (Z2); Odaklanma ×2 (Z2); Takistoskop ×2 (Z2) | 10 |
| 21 | 3. hafta / 7. gün | Görsel genişleme ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Görselleştirme ×2 (Z2); Kelime gruplama ×2 (Z2) | 8 |
| 22 | 4. hafta / 1. gün | Seri görsel sunum (RSVP) ×3 (Z3); Kelime gruplama ×2 (Z3); Görsel genişleme ×2 (Z3); Sakkadik göz hareketi ×2 (Z3) | 9 |
| 23 | 4. hafta / 2. gün | Kelime gruplama ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Schulte tablosu ×2 (Z3); Takistoskop ×2 (Z3) | 9 |
| 24 | 4. hafta / 3. gün | Görsel genişleme ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Göz takibi ×2 (Z3) | 9 |
| 25 | 4. hafta / 4. gün | Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3); Sabitleme ×2 (Z3); Kelime gruplama ×2 (Z3) | 9 |
| 26 | 4. hafta / 5. gün | Kelime gruplama ×3 (Z3); Seri görsel sunum (RSVP) ×3 (Z3); Görsel genişleme ×2 (Z3) | 8 |
| 27 | 4. hafta / 6. gün | Seri görsel sunum (RSVP) ×2 (Z3); Kelime gruplama ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3); Takistoskop ×2 (Z3) | 10 |
| 28 | 4. hafta / 7. gün | Görsel genişleme ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Görselleştirme ×2 (Z3); Kelime gruplama ×2 (Z3) | 8 |
| 29 | 5. hafta / 1. gün | İç ses azaltma ×2 (Z3); Hızlı okuma ×2 (Z3); Solan metin ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 8 |
| 30 | 5. hafta / 2. gün | Hızlı okuma ×3 (Z3); Geri dönüş azaltma ×2 (Z3); Kelime gruplama ×2 (Z3) | 7 |
| 31 | 5. hafta / 3. gün | Solan metin ×2 (Z3); İç ses azaltma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Hızlı okuma ×2 (Z3) | 8 |
| 32 | 5. hafta / 4. gün | Geri dönüş azaltma ×2 (Z3); Hızlı okuma ×3 (Z3); Kelime gruplama ×2 (Z3); Solan metin ×2 (Z3) | 9 |
| 33 | 5. hafta / 5. gün | İç ses azaltma ×3 (Z3); Solan metin ×2 (Z3); Hızlı okuma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 34 | 5. hafta / 6. gün | Hızlı okuma ×2 (Z3); Geri dönüş azaltma ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3) | 8 |
| 35 | 5. hafta / 7. gün | İç ses azaltma ×2 (Z3); Hızlı okuma ×2 (Z3); Görselleştirme ×2 (Z3); Solan metin ×2 (Z3) | 8 |
| 36 | 6. hafta / 1. gün | İç ses azaltma ×2 (Z3); Hızlı okuma ×2 (Z3); Solan metin ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 8 |
| 37 | 6. hafta / 2. gün | Hızlı okuma ×3 (Z3); Geri dönüş azaltma ×2 (Z3); Kelime gruplama ×2 (Z3) | 7 |
| 38 | 6. hafta / 3. gün | Solan metin ×2 (Z3); İç ses azaltma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Hızlı okuma ×2 (Z3) | 8 |
| 39 | 6. hafta / 4. gün | Geri dönüş azaltma ×2 (Z3); Hızlı okuma ×3 (Z3); Kelime gruplama ×2 (Z3); Solan metin ×2 (Z3) | 9 |
| 40 | 6. hafta / 5. gün | İç ses azaltma ×3 (Z3); Solan metin ×2 (Z3); Hızlı okuma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 41 | 6. hafta / 6. gün | Hızlı okuma ×2 (Z3); Geri dönüş azaltma ×2 (Z3); Schulte tablosu ×2 (Z3); Odaklanma ×2 (Z3) | 8 |
| 42 | 6. hafta / 7. gün | İç ses azaltma ×2 (Z3); Hızlı okuma ×2 (Z3); Görselleştirme ×2 (Z3); Solan metin ×2 (Z3) | 8 |
| 43 | 7. hafta / 1. gün | Anlama ×3 (Z4); Tarama ×2 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 44 | 7. hafta / 2. gün | Göz gezdirme ×2 (Z4); Anlama ×3 (Z4); Kelime çalışması ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 45 | 7. hafta / 3. gün | Tarama ×3 (Z4); Hızlı okuma ×2 (Z4); Anlama ×2 (Z4); Hata analizi ×2 (Z4) | 9 |
| 46 | 7. hafta / 4. gün | Anlama ×3 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4); Tarama ×2 (Z4) | 9 |
| 47 | 7. hafta / 5. gün | Sınav simülasyonu ×2 (Z4); Anlama ×2 (Z4); Tarama ×2 (Z4); Kelime çalışması ×2 (Z4) | 8 |
| 48 | 7. hafta / 6. gün | Göz gezdirme ×2 (Z4); Anlama ×2 (Z4); Hızlı okuma ×2 (Z4); Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4) | 10 |
| 49 | 7. hafta / 7. gün | Serbest okuma ×2 (Z4); Anlama ×2 (Z4); Kelime çalışması ×2 (Z4); Görselleştirme ×2 (Z4) | 8 |
| 50 | 8. hafta / 1. gün | Anlama ×3 (Z4); Tarama ×2 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 51 | 8. hafta / 2. gün | Göz gezdirme ×2 (Z4); Anlama ×3 (Z4); Kelime çalışması ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 52 | 8. hafta / 3. gün | Tarama ×3 (Z4); Hızlı okuma ×2 (Z4); Anlama ×2 (Z4); Hata analizi ×2 (Z4) | 9 |
| 53 | 8. hafta / 4. gün | Anlama ×3 (Z4); Göz gezdirme ×2 (Z4); Hızlı okuma ×2 (Z4); Tarama ×2 (Z4) | 9 |
| 54 | 8. hafta / 5. gün | Sınav simülasyonu ×2 (Z4); Anlama ×2 (Z4); Tarama ×2 (Z4); Kelime çalışması ×2 (Z4) | 8 |
| 55 | 8. hafta / 6. gün | Göz gezdirme ×2 (Z4); Anlama ×2 (Z4); Hızlı okuma ×2 (Z4); Odaklanma ×2 (Z4); Schulte tablosu ×2 (Z4) | 10 |
| 56 | 8. hafta / 7. gün | Serbest okuma ×2 (Z4); Anlama ×2 (Z4); Kelime çalışması ×2 (Z4); Görselleştirme ×2 (Z4) | 8 |

### LGS Hazırlık Temel — e23aaae0

- Kimlik: `e23aaae0-881e-4f85-b312-da972da7405d`
- Hedef yaş: 13–16 (Genç). Puan: 0–100.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 2; azami: 4; artış ayarı: 2 haftada bir.
- Program tür kodu: 1; sınav etiketi: LGS; görüntüleme sırası: 7.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: LGS sınavına hazırlık programı. Paragraf anlama, tarama ve kelime çalışmaları. Kapsamlı 4 haftalık yoğun program.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Anlama ×3 (Z2); Tarama ×2 (Z2); Kelime çalışması ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2) | 9 |
| 2 | 1. hafta / 2. gün | Anlama ×3 (Z2); Göz gezdirme ×2 (Z2); Hızlı okuma ×2 (Z2); Kelime gruplama ×2 (Z2) | 9 |
| 3 | 1. hafta / 3. gün | Anlama ×4 (Z2); Tarama ×2 (Z2); Kelime çalışması ×2 (Z2) | 8 |
| 4 | 1. hafta / 4. gün | Anlama ×3 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Göz gezdirme ×2 (Z2); Hızlı okuma ×2 (Z2) | 9 |
| 5 | 1. hafta / 5. gün | Anlama ×4 (Z2); Tarama ×2 (Z2); Kelime çalışması ×2 (Z2) | 8 |
| 6 | 1. hafta / 6. gün | Anlama ×3 (Z2); Hızlı okuma ×2 (Z2); Göz gezdirme ×2 (Z2); Odaklanma ×2 (Z2) | 9 |
| 7 | 1. hafta / 7. gün | Anlama ×3 (Z2); Kelime çalışması ×2 (Z2); Görselleştirme ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2) | 9 |
| 8 | 2. hafta / 1. gün | Anlama ×4 (Z2); Tarama ×2 (Z2); Hızlı okuma ×2 (Z2); Seri görsel sunum (RSVP) ×2 (Z2) | 10 |
| 9 | 2. hafta / 2. gün | Anlama ×4 (Z2); Göz gezdirme ×2 (Z2); Kelime çalışması ×2 (Z2) | 8 |
| 10 | 2. hafta / 3. gün | Anlama ×4 (Z2); Tarama ×3 (Z2); Hızlı okuma ×2 (Z2) | 9 |
| 11 | 2. hafta / 4. gün | Anlama ×4 (Z2); Seri görsel sunum (RSVP) ×2 (Z2); Göz gezdirme ×2 (Z2); Kelime gruplama ×2 (Z2) | 10 |
| 12 | 2. hafta / 5. gün | Anlama ×5 (Z2); Tarama ×2 (Z2); Kelime çalışması ×2 (Z2) | 9 |
| 13 | 2. hafta / 6. gün | Anlama ×4 (Z2); Hızlı okuma ×2 (Z2); Göz gezdirme ×2 (Z2); Hata analizi ×2 (Z2) | 10 |
| 14 | 2. hafta / 7. gün | Anlama ×3 (Z2); Sınav simülasyonu ×2 (Z2); Kelime çalışması ×2 (Z2) | 7 |
| 15 | 3. hafta / 1. gün | Anlama ×5 (Z3); Tarama ×2 (Z3); Hızlı okuma ×2 (Z3) | 9 |
| 16 | 3. hafta / 2. gün | Anlama ×5 (Z3); Göz gezdirme ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 17 | 3. hafta / 3. gün | Sınav simülasyonu ×2 (Z3); Anlama ×4 (Z3); Tarama ×2 (Z3) | 8 |
| 18 | 3. hafta / 4. gün | Anlama ×5 (Z3); Hızlı okuma ×2 (Z3); Kelime çalışması ×2 (Z3) | 9 |
| 19 | 3. hafta / 5. gün | Anlama ×6 (Z3); Tarama ×2 (Z3) | 8 |
| 20 | 3. hafta / 6. gün | Sınav simülasyonu ×2 (Z3); Anlama ×4 (Z3); Göz gezdirme ×2 (Z3) | 8 |
| 21 | 3. hafta / 7. gün | Anlama ×4 (Z3); Sınav simülasyonu ×2 (Z3); Kelime çalışması ×2 (Z3) | 8 |
| 22 | 4. hafta / 1. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Tarama ×2 (Z3) | 9 |
| 23 | 4. hafta / 2. gün | Anlama ×6 (Z3); Hızlı okuma ×2 (Z3) | 8 |
| 24 | 4. hafta / 3. gün | Sınav simülasyonu ×3 (Z3); Anlama ×4 (Z3) | 7 |
| 25 | 4. hafta / 4. gün | Anlama ×6 (Z3); Tarama ×2 (Z3); Göz gezdirme ×2 (Z3) | 10 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×3 (Z3); Anlama ×4 (Z3) | 7 |
| 27 | 4. hafta / 6. gün | Anlama ×5 (Z3); Sınav simülasyonu ×2 (Z3); Hızlı okuma ×2 (Z3) | 9 |
| 28 | 4. hafta / 7. gün | Sınav simülasyonu ×2 (Z3); Anlama ×4 (Z3); Serbest okuma ×2 (Z3) | 8 |

### LGS Hazırlık Yoğun — 2bc6871d

- Kimlik: `2bc6871d-9a8d-48c5-97f2-2cb8c0adcc1b`
- Hedef yaş: 13–16 (Genç). Puan: 0–100.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 3; azami: 5; artış ayarı: 2 haftada bir.
- Program tür kodu: 1; sınav etiketi: LGS Yoğun; görüntüleme sırası: 8.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: LGS sınavına hazırlık programı. Paragraf anlama, tarama ve kelime çalışmaları. Kapsamlı 4 haftalık yoğun program.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Anlama ×3 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 2 | 1. hafta / 2. gün | Anlama ×3 (Z3); Göz gezdirme ×2 (Z3); Hızlı okuma ×2 (Z3); Kelime gruplama ×2 (Z3) | 9 |
| 3 | 1. hafta / 3. gün | Anlama ×4 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3) | 8 |
| 4 | 1. hafta / 4. gün | Anlama ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Göz gezdirme ×2 (Z3); Hızlı okuma ×2 (Z3) | 9 |
| 5 | 1. hafta / 5. gün | Anlama ×4 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3) | 8 |
| 6 | 1. hafta / 6. gün | Anlama ×3 (Z3); Hızlı okuma ×2 (Z3); Göz gezdirme ×2 (Z3); Odaklanma ×2 (Z3) | 9 |
| 7 | 1. hafta / 7. gün | Anlama ×3 (Z3); Kelime çalışması ×2 (Z3); Görselleştirme ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 8 | 2. hafta / 1. gün | Anlama ×4 (Z3); Tarama ×2 (Z3); Hızlı okuma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 10 |
| 9 | 2. hafta / 2. gün | Anlama ×4 (Z3); Göz gezdirme ×2 (Z3); Kelime çalışması ×2 (Z3) | 8 |
| 10 | 2. hafta / 3. gün | Anlama ×4 (Z3); Tarama ×3 (Z3); Hızlı okuma ×2 (Z3) | 9 |
| 11 | 2. hafta / 4. gün | Anlama ×4 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Göz gezdirme ×2 (Z3); Kelime gruplama ×2 (Z3) | 10 |
| 12 | 2. hafta / 5. gün | Anlama ×5 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3) | 9 |
| 13 | 2. hafta / 6. gün | Anlama ×4 (Z3); Hızlı okuma ×2 (Z3); Göz gezdirme ×2 (Z3); Hata analizi ×2 (Z3) | 10 |
| 14 | 2. hafta / 7. gün | Anlama ×3 (Z3); Sınav simülasyonu ×2 (Z3); Kelime çalışması ×2 (Z3) | 7 |
| 15 | 3. hafta / 1. gün | Anlama ×5 (Z4); Tarama ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 16 | 3. hafta / 2. gün | Anlama ×5 (Z4); Göz gezdirme ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 17 | 3. hafta / 3. gün | Sınav simülasyonu ×2 (Z4); Anlama ×4 (Z4); Tarama ×2 (Z4) | 8 |
| 18 | 3. hafta / 4. gün | Anlama ×5 (Z4); Hızlı okuma ×2 (Z4); Kelime çalışması ×2 (Z4) | 9 |
| 19 | 3. hafta / 5. gün | Anlama ×6 (Z4); Tarama ×2 (Z4) | 8 |
| 20 | 3. hafta / 6. gün | Sınav simülasyonu ×2 (Z4); Anlama ×4 (Z4); Göz gezdirme ×2 (Z4) | 8 |
| 21 | 3. hafta / 7. gün | Anlama ×4 (Z4); Sınav simülasyonu ×2 (Z4); Kelime çalışması ×2 (Z4) | 8 |
| 22 | 4. hafta / 1. gün | Sınav simülasyonu ×2 (Z4); Anlama ×5 (Z4); Tarama ×2 (Z4) | 9 |
| 23 | 4. hafta / 2. gün | Anlama ×6 (Z4); Hızlı okuma ×2 (Z4) | 8 |
| 24 | 4. hafta / 3. gün | Sınav simülasyonu ×3 (Z4); Anlama ×4 (Z4) | 7 |
| 25 | 4. hafta / 4. gün | Anlama ×6 (Z4); Tarama ×2 (Z4); Göz gezdirme ×2 (Z4) | 10 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×3 (Z4); Anlama ×4 (Z4) | 7 |
| 27 | 4. hafta / 6. gün | Anlama ×5 (Z4); Sınav simülasyonu ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 28 | 4. hafta / 7. gün | Sınav simülasyonu ×2 (Z4); Anlama ×4 (Z4); Serbest okuma ×2 (Z4) | 8 |

### LGS Sınav Kampı (Sprint) — 39c5a702

- Kimlik: `39c5a702-ee93-4219-8db1-cd284bc820ab`
- Hedef yaş: 13–16 (Genç). Puan: 0–100.
- Süre: 2 hafta / 14 gün. Başlangıç zorluğu: 3; azami: 5; artış ayarı: 2 haftada bir.
- Program tür kodu: 4; sınav etiketi: LGS; görüntüleme sırası: 17.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Sınav öncesi son hazırlık için yoğunlaştırılmış kamp programı. Günde 45+ dakika çalışma gerektirir.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Anlama ×6 (Z3); Tarama ×3 (Z3); Hızlı okuma ×2 (Z3) | 11 |
| 2 | 1. hafta / 2. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Hata analizi ×2 (Z3) | 9 |
| 3 | 1. hafta / 3. gün | Anlama ×7 (Z3); Tarama ×2 (Z3); Göz gezdirme ×2 (Z3) | 11 |
| 4 | 1. hafta / 4. gün | Sınav simülasyonu ×3 (Z3); Anlama ×4 (Z3) | 7 |
| 5 | 1. hafta / 5. gün | Anlama ×6 (Z3); Hızlı okuma ×3 (Z3); Tarama ×2 (Z3) | 11 |
| 6 | 1. hafta / 6. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Kelime çalışması ×2 (Z3) | 9 |
| 7 | 1. hafta / 7. gün | Anlama ×7 (Z3); Serbest okuma ×2 (Z3) | 9 |
| 8 | 2. hafta / 1. gün | Anlama ×6 (Z3); Tarama ×3 (Z3); Hızlı okuma ×2 (Z3) | 11 |
| 9 | 2. hafta / 2. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Hata analizi ×2 (Z3) | 9 |
| 10 | 2. hafta / 3. gün | Anlama ×7 (Z3); Tarama ×2 (Z3); Göz gezdirme ×2 (Z3) | 11 |
| 11 | 2. hafta / 4. gün | Sınav simülasyonu ×3 (Z3); Anlama ×4 (Z3) | 7 |
| 12 | 2. hafta / 5. gün | Anlama ×6 (Z3); Hızlı okuma ×3 (Z3); Tarama ×2 (Z3) | 11 |
| 13 | 2. hafta / 6. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Kelime çalışması ×2 (Z3) | 9 |
| 14 | 2. hafta / 7. gün | Anlama ×7 (Z3); Serbest okuma ×2 (Z3) | 9 |

### YKS Hazırlık (TYT-AYT) — 2ab60ad6

- Kimlik: `2ab60ad6-3d89-4170-ac6a-66d4879ed958`
- Hedef yaş: 17–21 (Genç Yetişkin). Puan: 0–100.
- Süre: 4 hafta / 28 gün. Başlangıç zorluğu: 3; azami: 5; artış ayarı: 2 haftada bir.
- Program tür kodu: 1; sınav etiketi: YKS; görüntüleme sırası: 12.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: YKS sınavına hazırlık programı. TYT-AYT paragraf soruları ve analitik okuma stratejileri. Profesyonel seviye.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Anlama ×3 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 2 | 1. hafta / 2. gün | Anlama ×3 (Z3); Göz gezdirme ×2 (Z3); Hızlı okuma ×2 (Z3); Kelime gruplama ×2 (Z3) | 9 |
| 3 | 1. hafta / 3. gün | Anlama ×4 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3) | 8 |
| 4 | 1. hafta / 4. gün | Anlama ×3 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Göz gezdirme ×2 (Z3); Hızlı okuma ×2 (Z3) | 9 |
| 5 | 1. hafta / 5. gün | Anlama ×4 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3) | 8 |
| 6 | 1. hafta / 6. gün | Anlama ×3 (Z3); Hızlı okuma ×2 (Z3); Göz gezdirme ×2 (Z3); Odaklanma ×2 (Z3) | 9 |
| 7 | 1. hafta / 7. gün | Anlama ×3 (Z3); Kelime çalışması ×2 (Z3); Görselleştirme ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 9 |
| 8 | 2. hafta / 1. gün | Anlama ×4 (Z3); Tarama ×2 (Z3); Hızlı okuma ×2 (Z3); Seri görsel sunum (RSVP) ×2 (Z3) | 10 |
| 9 | 2. hafta / 2. gün | Anlama ×4 (Z3); Göz gezdirme ×2 (Z3); Kelime çalışması ×2 (Z3) | 8 |
| 10 | 2. hafta / 3. gün | Anlama ×4 (Z3); Tarama ×3 (Z3); Hızlı okuma ×2 (Z3) | 9 |
| 11 | 2. hafta / 4. gün | Anlama ×4 (Z3); Seri görsel sunum (RSVP) ×2 (Z3); Göz gezdirme ×2 (Z3); Kelime gruplama ×2 (Z3) | 10 |
| 12 | 2. hafta / 5. gün | Anlama ×5 (Z3); Tarama ×2 (Z3); Kelime çalışması ×2 (Z3) | 9 |
| 13 | 2. hafta / 6. gün | Anlama ×4 (Z3); Hızlı okuma ×2 (Z3); Göz gezdirme ×2 (Z3); Hata analizi ×2 (Z3) | 10 |
| 14 | 2. hafta / 7. gün | Anlama ×3 (Z3); Sınav simülasyonu ×2 (Z3); Kelime çalışması ×2 (Z3) | 7 |
| 15 | 3. hafta / 1. gün | Anlama ×5 (Z4); Tarama ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 16 | 3. hafta / 2. gün | Anlama ×5 (Z4); Göz gezdirme ×2 (Z4); Seri görsel sunum (RSVP) ×2 (Z4) | 9 |
| 17 | 3. hafta / 3. gün | Sınav simülasyonu ×2 (Z4); Anlama ×4 (Z4); Tarama ×2 (Z4) | 8 |
| 18 | 3. hafta / 4. gün | Anlama ×5 (Z4); Hızlı okuma ×2 (Z4); Kelime çalışması ×2 (Z4) | 9 |
| 19 | 3. hafta / 5. gün | Anlama ×6 (Z4); Tarama ×2 (Z4) | 8 |
| 20 | 3. hafta / 6. gün | Sınav simülasyonu ×2 (Z4); Anlama ×4 (Z4); Göz gezdirme ×2 (Z4) | 8 |
| 21 | 3. hafta / 7. gün | Anlama ×4 (Z4); Sınav simülasyonu ×2 (Z4); Kelime çalışması ×2 (Z4) | 8 |
| 22 | 4. hafta / 1. gün | Sınav simülasyonu ×2 (Z4); Anlama ×5 (Z4); Tarama ×2 (Z4) | 9 |
| 23 | 4. hafta / 2. gün | Anlama ×6 (Z4); Hızlı okuma ×2 (Z4) | 8 |
| 24 | 4. hafta / 3. gün | Sınav simülasyonu ×3 (Z4); Anlama ×4 (Z4) | 7 |
| 25 | 4. hafta / 4. gün | Anlama ×6 (Z4); Tarama ×2 (Z4); Göz gezdirme ×2 (Z4) | 10 |
| 26 | 4. hafta / 5. gün | Sınav simülasyonu ×3 (Z4); Anlama ×4 (Z4) | 7 |
| 27 | 4. hafta / 6. gün | Anlama ×5 (Z4); Sınav simülasyonu ×2 (Z4); Hızlı okuma ×2 (Z4) | 9 |
| 28 | 4. hafta / 7. gün | Sınav simülasyonu ×2 (Z4); Anlama ×4 (Z4); Serbest okuma ×2 (Z4) | 8 |

### YKS Sınav Kampı (Sprint) — 69fb38c7

- Kimlik: `69fb38c7-f6f9-402d-ba12-7fbe59335345`
- Hedef yaş: 17–21 (Genç Yetişkin). Puan: 0–100.
- Süre: 2 hafta / 14 gün. Başlangıç zorluğu: 3; azami: 5; artış ayarı: 2 haftada bir.
- Program tür kodu: 4; sınav etiketi: YKS; görüntüleme sırası: 18.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Sınav öncesi son hazırlık için yoğunlaştırılmış kamp programı. Günde 45+ dakika çalışma gerektirir.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Anlama ×6 (Z3); Tarama ×3 (Z3); Hızlı okuma ×2 (Z3) | 11 |
| 2 | 1. hafta / 2. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Hata analizi ×2 (Z3) | 9 |
| 3 | 1. hafta / 3. gün | Anlama ×7 (Z3); Tarama ×2 (Z3); Göz gezdirme ×2 (Z3) | 11 |
| 4 | 1. hafta / 4. gün | Sınav simülasyonu ×3 (Z3); Anlama ×4 (Z3) | 7 |
| 5 | 1. hafta / 5. gün | Anlama ×6 (Z3); Hızlı okuma ×3 (Z3); Tarama ×2 (Z3) | 11 |
| 6 | 1. hafta / 6. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Kelime çalışması ×2 (Z3) | 9 |
| 7 | 1. hafta / 7. gün | Anlama ×7 (Z3); Serbest okuma ×2 (Z3) | 9 |
| 8 | 2. hafta / 1. gün | Anlama ×6 (Z3); Tarama ×3 (Z3); Hızlı okuma ×2 (Z3) | 11 |
| 9 | 2. hafta / 2. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Hata analizi ×2 (Z3) | 9 |
| 10 | 2. hafta / 3. gün | Anlama ×7 (Z3); Tarama ×2 (Z3); Göz gezdirme ×2 (Z3) | 11 |
| 11 | 2. hafta / 4. gün | Sınav simülasyonu ×3 (Z3); Anlama ×4 (Z3) | 7 |
| 12 | 2. hafta / 5. gün | Anlama ×6 (Z3); Hızlı okuma ×3 (Z3); Tarama ×2 (Z3) | 11 |
| 13 | 2. hafta / 6. gün | Sınav simülasyonu ×2 (Z3); Anlama ×5 (Z3); Kelime çalışması ×2 (Z3) | 9 |
| 14 | 2. hafta / 7. gün | Anlama ×7 (Z3); Serbest okuma ×2 (Z3) | 9 |

### Yetişkin Başlangıç (Hızlandırılmış) — 87861a17

- Kimlik: `87861a17-36f8-4798-9f28-07d5729b893e`
- Hedef yaş: 22–+ (Yetişkin). Puan: 0–40.
- Süre: 2 hafta / 14 gün. Başlangıç zorluğu: 1; azami: 2; artış ayarı: 2 haftada bir.
- Program tür kodu: 2; sınav etiketi: yok; görüntüleme sırası: 15.
- Toplam atama kaydı: 0.
- Kayıt açıklaması: Hızlı okumaya yeni başlayanlar için hızlandırılmış adaptasyon programı. Temel göz kası ve odaklanma çalışmaları.

| Program günü | Hafta/gün | Egzersiz dağılımı | Toplam |
|---:|---|---|---:|
| 1 | 1. hafta / 1. gün | Schulte tablosu ×2 (Z1); Sabitleme ×2 (Z1) | 4 |
| 2 | 1. hafta / 2. gün | Göz takibi ×2 (Z1); Schulte tablosu ×2 (Z1) | 4 |
| 3 | 1. hafta / 3. gün | Sakkadik göz hareketi ×2 (Z1); Odaklanma ×2 (Z1) | 4 |
| 4 | 1. hafta / 4. gün | Schulte tablosu ×3 (Z1); Sabitleme ×2 (Z1) | 5 |
| 5 | 1. hafta / 5. gün | Görsel genişleme ×2 (Z1); Göz takibi ×2 (Z1) | 4 |
| 6 | 1. hafta / 6. gün | Sakkadik göz hareketi ×2 (Z1); Schulte tablosu ×2 (Z1) | 4 |
| 7 | 1. hafta / 7. gün | Odaklanma ×2 (Z1); Görselleştirme ×2 (Z1) | 4 |
| 8 | 2. hafta / 1. gün | Schulte tablosu ×3 (Z1); Sakkadik göz hareketi ×2 (Z1); Görsel genişleme ×2 (Z1) | 7 |
| 9 | 2. hafta / 2. gün | Göz takibi ×3 (Z1); Sabitleme ×2 (Z1) | 5 |
| 10 | 2. hafta / 3. gün | Kelime gruplama ×2 (Z1); Schulte tablosu ×2 (Z1) | 4 |
| 11 | 2. hafta / 4. gün | Görsel genişleme ×3 (Z1); Odaklanma ×2 (Z1) | 5 |
| 12 | 2. hafta / 5. gün | Sakkadik göz hareketi ×3 (Z1); Seri görsel sunum (RSVP) ×2 (Z1) | 5 |
| 13 | 2. hafta / 6. gün | Schulte tablosu ×3 (Z1); Görselleştirme ×2 (Z1) | 5 |
| 14 | 2. hafta / 7. gün | Göz takibi ×2 (Z1); Serbest okuma ×2 (Z1) | 4 |

## Seviye tespit şablonları

Bunlar eğitim programı değil, ölçüm şablonudur. Veritabanında her biri 1 gün olarak işaretlidir; week1 içindeki liste gün listesi değil değerlendirme egzersizleri listesidir. İstemcinin gönderdiği expectedExerciseCount=3 tek başına gerçek sayıyı kanıtlamaz; canlı şablonda 4 öğe bulunur.

### Seviye Tespit - Genç

Yaş: 13–16. Kimlik: `990738b8-d111-4f39-82ff-156c42dd7dae`.

| Sıra | Tür | Egzersiz adı | Zorluk |
|---:|---|---|---:|
| 1 | Anlama | Kritik Okuma | 2 |
| 2 | Takistoskop | Hız Antrenmanı | 2 |
| 3 | Görsel genişleme | Görüş Genişletme | 2 |
| 4 | Sabitleme | Okuma Fiksasyonu | 2 |

### Seviye Tespit - Genç Yetişkin

Yaş: 17–21. Kimlik: `654e55eb-de3a-4d82-933b-c1e522dba1b1`.

| Sıra | Tür | Egzersiz adı | Zorluk |
|---:|---|---|---:|
| 1 | Anlama | Rapor Değerlendirme | 2 |
| 2 | Takistoskop | Hızlı Algı | 2 |
| 3 | Görsel genişleme | İş Verimliliği Görüş | 2 |
| 4 | Sabitleme | İş Odağı | 2 |

### Seviye Tespit - Yetişkin

Yaş: 22–+. Kimlik: `55ac59f6-e75b-42ee-822a-ed4acd9cdddb`.

| Sıra | Tür | Egzersiz adı | Zorluk |
|---:|---|---|---:|
| 1 | Anlama | Rapor Değerlendirme | 2 |
| 2 | Takistoskop | Hızlı Algı | 2 |
| 3 | Görsel genişleme | İş Verimliliği Görüş | 2 |
| 4 | Sabitleme | İş Odağı | 2 |

### Seviye Tespit - Çocuk

Yaş: 9–12. Kimlik: `1d65530e-13c2-4f09-a6cf-b717009fd959`.

| Sıra | Tür | Egzersiz adı | Zorluk |
|---:|---|---|---:|
| 1 | Anlama | Dikkatli Okuma | 2 |
| 2 | Takistoskop | Hızlı Gözler | 2 |
| 3 | Görsel genişleme | Geniş Gözler | 2 |
| 4 | Sabitleme | Göz Takibi | 2 |

## İçerik açıklamasıyla gerçek tanım arasındaki fark

Bazı açıklamalar 21 farklı tip olduğunu söylüyor. Fiili tanımda çocuk standart programlarında 17, 13–16 yaş standart programlarında 20; genç yetişkin/yetişkin standart programlarında 21 tip bulunuyor. Açıklamalardaki bilimsel progresyon ifadesi veritabanı metnidir; bu envanter bilimsel etkinlik doğrulaması değildir.

## Önerilen düzeltme sırası

1. Mükerrer yetişkin orta seviye kaydının tüm bağımlılıklarını kontrol ederek kullanılmayanı pasife almak.
2. Genel programları otomatik atamada ayrı filtrelemek; sınav, hızlandırılmış ve maraton tercihini açıkça belirlemek.
3. Puan sınırlarını çakışmasız tanımlamak ve eşitlik durumunda kararlı sıralama uygulamak.
4. Katalogdaki her günün ihtiyaç duyduğu aktif egzersiz/metin yeterliliğini doğrulamak.
5. Eğitim sonrası değerlendirmeyi program bitişine bağlamak; öğrenci ekranında gerçek süre ve program adını göstermek.

