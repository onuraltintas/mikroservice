# Hızlı Okuma Platformu İncelemesi

Tarih: 8 Eylül 2026
Kapsam: `services/speed-reading-service`, `clients/speed-reading`, `clients/admin-panel`, ilgili testler ve yönetim belgeleri.

## Yönetici özeti

Bu proje, özellik kapsamı ve servis altyapısı bakımından güçlü bir hızlı okuma ürün iskeleti. Egzersiz motorları, okuma metinleri ve sorular, yaş grupları, program şablonları, öğrenme yolları, günlük plan, gamification, uyarlamalı metin, raporlar, öğretmen/veli görünümü ve merkezi admin paneli mevcut.

Ancak mevcut haliyle “bilimsel olarak doğrulanmış, kişiye güvenilir biçimde seviye atlatan modern hızlı okuma programı” olarak hazır değildir. En önemli sorunlar kod kapsamından çok bu parçaların birbirine bağlanma biçimindedir:

1. Yaş grubuna özel seviye tespit şablonları admin’de yönetiliyor, fakat normal öğrenci akışı yaş grubunu isteğe göndermiyor; sunucu da şablondaki egzersizleri seçerken yaş grubunu kullanmıyor.
2. Bağımsız öğrenci okuma endpoint’i `CorrectAnswer` alanını öğrenci tarayıcısına gönderiyor. Aynı akış istemcinin gönderdiği süre ve anlama puanını kabul ediyor ve tamamlamayı idempotent biçimde korumuyor.
3. Seviye 1–8 kararı esas olarak ortalama WPM eşiklerinden veriliyor. Kavrama puanı seviye kararını sınırlamıyor; sıfıra yakın kavramayla yüksek WPM alan kullanıcı ileri seviyeye çıkabilir.
4. Egzersiz, metin, program, yaş grubu ve gamification seviyeleri farklı aralıklar kullanıyor. Admin arayüzü ile domain doğrulamaları da bazı alanlarda uyuşmuyor.
5. Günlük programda egzersiz seçimi tür + zorluk + ID sırasına bağlı; yeterli içerik yoksa yalnızca daha düşük zorluğa iner ve aynı egzersizi aynı gün tekrar edebilir.

Bu nedenle 8 Eylül 2026 tarihli başlangıç değerlendirmem:

| Boyut | Puan | Yorum |
| --- | ---: | --- |
| Platform mühendisliği | 78/100 | Owned veri modeli, snapshot, idempotency, yetki, audit ve katmanlar iyi. |
| Admin yönetilebilirliği | 84/100 | Katalog ve program kapsamı geniş; bilimsel/rubrik kuralları panelden yönetilemiyor. |
| İçerik ve egzersiz kapsamı | 74/100 | Çok sayıda motor ve içerik türü var; kalite ve kalibrasyon kanıtı yok. |
| Kişiselleştirme ve program akışı | 62/100 | Yaş, seviye ve uyarlama modelleri var; assessment bağlantısı kopuk. |
| Ölçme ve değerlendirme | 43/100 | WPM ağırlıklı, normlama ve transfer/kalıcılık tasarımı zayıf. |
| Veri bütünlüğü | 52/100 | Ana egzersiz oturumu güvenli; bağımsız okuma ve self-report yolu zayıf. |
| Bilimsel etkililik hazır oluşu | 45/100 | İçeride pilot/RCT veya Türkçe norm kanıtı bulunmuyor. |
| **Modern etkili program için genel hazır oluş** | **64/100** | İyi bir ürün temeli; P0/P1 konuları çözülmeden yaygın kullanıma hazır değil. |

Bu puanlar üretim trafiği veya canlı veri ölçümü değildir; statik kod ve belge incelemesine dayalı başlangıç ürün hazır oluş puanıdır. 10 Eylül düzeltme turunda önemli yazılım kusurları kapatıldı; canlı PostgreSQL/migration ve E2E kanıtı olmadan bu tabloya yapay bir güncel puan eklenmedi.

## Sistem nasıl çalışıyor?

### İçerik ve egzersiz katmanı

Katalogda `ExerciseType`, `Exercise`, `ReadingText` ve `ReadingQuestion` var. Ön yüzdeki universal player şu motorları destekliyor: grid interaction, motion path, text stream, text fade, word highlight, visual expansion, scan/find, reading comprehension, exam simulation, free reading, regression reduction, subvocalization reduction, visualization, attention/focus, vocabulary builder ve error analysis; bazıları alias olarak tekrar kayıtlı.

Egzersiz konfigürasyonu serbest JSON olarak tutuluyor. Backend JSON nesnesini doğruluyor ancak motor tipine göre şema doğrulamıyor. Admin’de egzersiz türü, yaş grubu ID’si, zorluk ve ham `configurationJson` metin alanı elle giriliyor. Bu, teknik ekip için esnek; içerik editörü için hataya açık.

Okuma metinlerinde başlık, içerik, dil, kategori, kelime sayısı, zorluk, yaş grubu, önerilen seviye aralığı ve etiketler bulunuyor. Kelime sayısı içerikten backend’de yeniden hesaplanıyor; bu doğru bir koruma. Metin soruları dört seçenek ve A–D cevap anahtarı ile kaydediliyor, Bloom seviyesi 1–6 ve soru zorluğu 0–10 aralığında doğrulanıyor.

### Profil, yaş grubu ve başlangıç ölçümü

Öğrenci profil kurulumunda doğum tarihi/yaş grubuna göre `AgeGroupConfiguration` seçiliyor. Yaş grubu WPM minimum/önerilen/maksimum, önerilen kavrama, günlük dakika ve varsayılan zorluk taşıyor. Yaş araması aktif aralıklarda yapılıyor.

Assessment attempt modeli baseline, post-training, retention ve transfer fazlarını; form version, snapshot ve prerequisite durumlarını saklıyor. Bu, tekrar ölçümlerde içeriğin sonradan değişmesiyle geçmiş sonucun bozulmasını önleyen iyi bir tasarım.

Fakat normal öğrenci component’i attempt başlatırken yalnızca faz, form version, dil ve `expectedExerciseCount: 3` gönderiyor; `ageGroupConfigurationId` göndermiyor. `BuildPinnedFormItemsAsync` de yaş grubu veya admin şablonu yerine aktif tüm egzersizleri tür adına göre seçiyor. Admin’de “her yaş grubu için seviye tespit egzersizi” oluşturulabilmesi ile öğrenciye gerçekten uygulanması arasında kopukluk var.

### Seviye hesaplama

`OwnedSpeedReadingAssessment.CalculateAsync` sonuçlardan:

- pozitif RawWpm ortalamasını,
- kavrama sorularından kavrama ortalamasını,
- tüm sonuçlardan skor ortalamasını,
- rol bazlı comprehension/visual/focus/tachistoscope skorlarını

hesaplıyor. Seviyeyi yalnızca ortalama WPM ile şu sabit eşiklerde veriyor: `<100` seviye 1, `<150` seviye 2, `<200` seviye 3, `<250` seviye 4, `<300` seviye 5, `<400` seviye 6, `<500` seviye 7, üstü seviye 8.

Hedef WPM assessment ortalamasının `1.2` katı, hedef kavrama ise en az `%70`. Bu katsayı ve eşikler admin’den değiştirilemiyor. Daha önemlisi, kavrama seviyesi WPM kararına bir alt sınır koymuyor. Kavrama 0 olsa bile WPM yüksekse kullanıcı ileri seviyeye gelebilir.

Assessment template’leri `ProgramTemplate` olarak oluşturuluyor. Hesaplama sırasında önerilen program sorgusu `IsAssessment` veya öğrencinin yaş grubunu filtrelemiyor; yalnızca aktiflik ve `MinAssessmentScore/MaxAssessmentScore` aralığına bakıyor. Bu yüzden sonuçta önerilen seri, yaş grubuna uygun assessment sonrası eğitim programı olmak zorunda değil.

Faz planı prerequisite sırasını doğru kuruyor; ancak retention fazının `AvailableAt` zamanı post-training tamamlanma zamanına eşit. Kodda bir hafta/iki hafta bekleme penceresi yok. Bu akış uzun süreli kalıcılığı değil, hemen sonraki performansı ölçebilir.

### Günlük egzersiz serileri ve seviye durumu

Program şablonu `WeeklyPatternJson` içinden hafta/gün için tür, adet ve zorluk ister. Sistem o tür ve zorluktaki aktif egzersizleri ID sırasıyla alır; bulunamazsa daha düşük zorluklara iner. İstenen adet içerik sayısından fazlaysa `index % candidates.Count` ile aynı egzersizi tekrarlar.

Program ilerlemesi gün, hafta, tamamlanan egzersiz, ortalama başarı, streak ve mevcut zorluk seviyesini tutuyor. Gün tamamlandığında hafta ilerliyor; `WeeksPerDifficultyIncrease` dolunca seviye bir artırılıyor. Bu mekanizma zamana dayalı; öğrencinin kavrama güven aralığı, son N oturum performansı veya metin transfer başarısı ile yükselmiyor.

Metin seçiminde öğrenci seviyesi etrafında ±2 zorluk aralığı kullanılıyor. Uyarlamalı metin önerisi seviye uyumu `%50`, metin ortalama kavraması `%30`, kategori tercihi `%20` ağırlıklı basit bir skor kullanıyor. Bu kullanılabilir bir başlangıç heuristiği; ancak metin zorluğu ve öğrenci becerisi için kalibre edilmiş bir ölçme modeli değil.

### Bağımsız okuma akışı

`GET /student-reading/{textId}/start` metinle birlikte soruların `CorrectAnswer` değerini dönüyor. Bu endpoint’i kullanan öğrenci tarayıcısı doğru cevapları okuyabilir; puan güvenilir bir ölçüm olmaktan çıkar.

`POST /student-reading/{textId}/complete` için sunucu tarafında bir reading session oluşturuluyor. `TimeSpentSeconds` istemciden geliyor; sorular eksik, tekrarlı veya hiç gönderilmemiş olabilir. Hiç cevap yoksa istemcinin `ComprehensionScore` değeri kullanılıyor. Aynı metin için aynı kullanıcı tekrar tekrar tamamlayabilir; completion idempotency veya active session sahipliği yok.

Ana universal exercise-session akışı bundan daha iyi: cevap anahtarı public state’ten çıkarılıyor, oturum sonucu server’da hesaplanıyor, WPM 20–1500 aralığında doğrulanıyor ve ölçüm durumu saklanıyor. İki farklı okuma sonucu yazma yolunun farklı güven modelleri kullanması raporları karşılaştırılamaz hale getiriyor.

## Admin paneli neyi yönetiyor?

### Gerçekten yönetilebilenler

- Egzersiz türleri: ad, görünen ad, engine type, ikon, renk, sıra, aktiflik.
- Egzersizler: başlık, tip ID, zorluk, yaş grubu ID’si, açıklama, ham JSON.
- Okuma metinleri: içerik, dil, kategori, etiket, yaş grubu, zorluk, önerilen seviye aralığı, aktiflik; CSV/Excel import ve PDF/DOCX export.
- Okuma soruları: soru, seçenekler, Bloom seviyesi, zorluk, açıklama, doğru seçenek, sıra.
- Yaş grupları: yaş aralığı, WPM hedefleri, kavrama, günlük dakika, varsayılan zorluk.
- Seviye tespit template’leri: yaş grubu, seçilen egzersizler, sıra ve özel başlık/açıklama.
- Program template’leri: yaş grubu, assessment puan aralığı, ilk/maksimum zorluk, hafta/gün, haftalık JSON, aktiflik.
- Learning path, node ve node içeriği; başarı/rozet kriterleri ve XP; görselleştirme sahneleri; vocabulary ve exam question bank.
- Öğrenci program ilerlemesi, raporlar, snapshot’lar, öğretmen analitiği, kurum analitiği, abonelik/ürün kataloğu, CMS ve iletişim.

Permission, SystemAdmin/MFA, append-only audit, pagination ve server-side authorization tarafında kapsamlı bir temel var. Admin paneli yalnızca arayüz gizlemiyor; backend permission/policy denetimleri de mevcut.

### Panelden yönetilemeyen veya yalnızca kısmen yönetilebilenler

- WPM’den 1–8 seviyeye geçiş eşikleri.
- Kavrama alt sınırı ve hız/kavrama ağırlıkları.
- Hedef WPM’nin `1.2` katsayısı.
- Assessment fazlarının bekleme/retention pencereleri ve ölçüm güven aralıkları.
- Assessment formunda rol zorunlulukları ve aday seçme hash’i.
- Günlük programın içerik seçim algoritması, tekrar önleme ve alt zorluk fallback’i.
- Uyarlamalı metin ağırlıkları (`50/30/20`) ve varsayılan WPM 200.
- Ölçüm durumu, client self-report akışlarının güven seviyesi ve answer-key redaction politikası.
- Motorların parametre şeması ve önizleme/simülasyon doğrulaması.

Admin’de zorluk seviyeleri ham sayısal alan olarak giriliyor; ayrı bir “seviye sözlüğü”, açıklaması, hedefi, geçme koşulu veya versiyonu yönetilemiyor. Ön yüzdeki `DIFFICULTY_LEVELS` ise bazı motorlar için sabit 1–5 tanımları taşıyor. Backend program ve metin zorluklarını 0–10, assessment’i 1–8, kullanıcı seviyesini 1–10 aralıklarında kullanıyor. Bu dört sözlük aynı şey değil.

### Panel doğrulama tutarsızlıkları

Admin yaş grubu formu varsayılan zorluğu 1–10 kabul ediyor; domain `DefaultDifficultyLevel` için 1–5 kabul ediyor. Egzersiz ve metin formları zorluğu 1–10 gösteriyor; backend bazı katalog alanlarında 0’a izin veriyor. Soru formu tür/Bloom için 0’dan başlıyor; backend tür 1–3, Bloom 1–6 istiyor. Editör geçerli görünen bir formu gönderdiğinde server reddi veya farklı ölçek algısı oluşabilir.

## Bilimsel ve ürün karşılaştırması

Rayner, Schotter, Masson, Potter ve Treiman’ın kapsamlı incelemesi hızlı okuma ile yüksek kavramanın aynı anda genellenebilir bir “özel beceri” olarak gösterilmediğini; skimming’in daha hızlı fakat orta düzey kavrama ile bir takas olduğunu bildiriyor: [So Much to Read, So Little Time](https://pubmed.ncbi.nlm.nih.gov/26769745/).

Brysbaert’in 190 çalışma ve 18.573 katılımcıya dayalı meta-analizi yetişkin sessiz okuma ortalamasını İngilizce kurgu dışı metinlerde 238, kurgu metinlerde 260 WPM olarak tahmin ediyor; çocuklar, yaşlılar, metin zorluğu ve ikinci dil okuyucuları için hızların daha düşük olduğunu vurguluyor: [How many words do we read per minute?](https://doi.org/10.1016/j.jml.2019.104047). Bu sayılar Türkçe çocuk normu olarak doğrudan kullanılmamalı.

2023 tarihli app tabanlı speed-reading çalışmasında hız artışı görülürken eğitim grupları ile kontrol arasında kavrama farkı bulunmadı. Bu, hız metriğini tek başına başarı kabul etmeme gereğini destekliyor: [Klimovich et al., 2023](https://onlinelibrary.wiley.com/doi/10.1111/1467-9817.12417).

What Works Clearinghouse 4–9. sınıf önerileri akıcı okuma etkinliklerini; kelime ve dünya bilgisi oluşturmayı; anlamayı izleme ve soru-cevap uygulamalarını birlikte öneriyor: [Providing Reading Interventions for Students in Grades 4–9](https://ies.ed.gov/ncee/WWC/PracticeGuide/29/Published). Adölesanlar için açık kelime öğretimi ve açık kavrama stratejileri güçlü kanıt olarak listeleniyor: [Improving Adolescent Literacy](https://ies.ed.gov/ncee/wwc/PracticeGuide/8/WhatWeDo).

Türkçe için TURead, 196 katılımcı ve 192 kısa metinle kelime uzunluğu, sıklığı, ek yapısı ve göz hareketlerini inceleyen bir veri seti sunuyor: [TURead](https://doi.org/10.3758/s13428-023-02120-6). Projede Türkçeye özgü kelime uzunluğu, eklemeli yapı, sıklık ve sınıf/yaş normlarını kullanan bir kalibrasyon katmanı görünmüyor.

Rakip konumlandırması:

| Platform | Öne çıkan yaklaşım | Bu projeye göre durum |
| --- | --- | --- |
| HIZLIGO | Yaş/sınıf/hız/çalışma düzenine göre günlük plan, 36 adım, 18 egzersiz, hız testi, rapor ve video iddiası: [HIZLIGO](https://www.hizligo.com/sss) | Bu proje teknik olarak daha geniş admin, program, audit ve servis altyapısına sahip; HIZLIGO’nun görünür eğitim akışı kadar tutarlı yaşa özel assessment bağlantısı henüz yok. |
| HızlıOkurum | Sınıf seçimi, hız testi, yaşa göre egzersiz, gamification ve veli paneli: [HızlıOkurum](https://hizliokurum.com/) | Bu projede benzer öğrenci/öğretmen/veli analitiği var; answer-key ve seviye bütünlüğü düzeltilmeli. |
| Maraton | İlkokul/ortaokul/lise için üç program ve yaşa göre başlangıç metni: [Maraton](https://www.maratonhizliokuma.com/kurslar.html) | Bu proje daha esnek yaş grubu ve program modeli sunuyor; ancak yaş grubu template’ini gerçekten uygulama zorunlu. |
| ReadTheory | İlk gün adaptif benchmark ve sonraki çalışmalarda sürekli seviye ayarlama: [ReadTheory](https://readtheory.org/) | Bu projede adaptive text heuristiği var; gerçek item-level adaptasyon, kalibrasyon ve sürekli benchmark yok. |
| Reading Plus | Adaptif değerlendirme, akıcılık, kavrama, kelime ve motivasyonu birlikte hedefliyor; şirket araştırma portföyü ve ESSA iddiası sunuyor: [Reading Plus research](https://www.readingplus.com/research-results/) | Bu projede özellik karşılığı var; bağımsız etkililik çalışması, sonuç ölçütü ve kanıt zinciri yok. |
| Spreeder | RSVP, kütüphane ve AI özetleri: [Spreeder](https://www.spreeder.com/) | Bu proje ölçme, öğretmen ve admin tarafında daha geniş; Spreeder’in ürün odaklı içerik/özet deneyimi ayrıca değerlendirilebilir. |

Rakiplerin pazarlama iddiaları bağımsız akademik kanıt olarak alınmamalıdır. Reading Plus gibi kanıt iddiası olan ürünlerde de ürünün kendi araştırması ile bağımsız doğrulama ayrılmalıdır.

## Öncelikli kusurlar

### P0 — Yaş grubu assessment’i uygulanmıyor

**Etkisi:** Öğrenci yanlış başlangıç egzersizi, yanlış metin ve yanlış program alabilir; admin’de yapılan yaş grubu değişiklikleri beklenen davranışı değiştirmez.

**Düzeltme:** Profildeki yaş grubunu server’da tek kaynaktan çöz; attempt başlatırken istemci değerine güvenme. Seçilen template’i attempt’e bağla, template exercise’lerini role/order/difficulty ile pinle ve snapshot’a template + age-group version yaz. Template yoksa kontrollü varsayılan ve açık audit olayı üret.

### P0 — Cevap anahtarı sızıntısı ve self-report sonucu

**Etkisi:** Öğrenci soruların doğru cevaplarını okuyabilir; WPM/anlama sonucunu manipüle edebilir; tekrar POST’larla geçmişi şişirebilir.

**Düzeltme:** StudentReading DTO’sundan `CorrectAnswer` kaldır; cevap kontrolünü server’da completion sırasında yap. Start endpoint’ine session nonce/attempt ID ekle, completion’ı o oturumla sınırla ve idempotency key veya unique session constraint uygula. Süreyi server olaylarından ölç; istemci değeri yalnız telemetry olsun.

### P1 — Seviye kararı kavramayı zorunlu kılmıyor

**Etkisi:** Hızlı fakat anlamayan kullanıcı ileri seviyeye çıkar; seri ve hedefler yanlış belirlenir.

**Düzeltme:** En az üç ayrı karar üret: hız, kavrama, verimlilik (`WCPM × comprehension`). Seviye geçişinde kavrama tabanı ve güven aralığı kullan. Ölçülmemiş/tek metin/çok düşük soru sayısını “seviye kararı için yetersiz” olarak işaretle.

### P1 — Level sözlükleri birleştirilmeli

**Etkisi:** Admin 7 yazarken öğrenci 1–5 rozet, assessment 1–8 ve metin 1–10 mantığıyla çalışabilir; raporlar karşılaştırılamaz.

**Düzeltme:** `SkillLevelDefinition` ile ortak seviye kataloğu oluştur: code, display name, ordinal, WPM bandı, comprehension floor, difficulty bandı, age/grade applicability, version. Egzersiz parametresi ile öğrenci yeterlik seviyesini ayrı modeller olarak adlandır.

### P1 — Assessment sonucu yanlış seri seçebilir

**Etkisi:** `IsAssessment` ve yaş grubu filtrelenmediği için başlangıç değerlendirmesi normal bir programı veya başka yaş grubunun programını önerebilir.

**Düzeltme:** Sorguda `!IsAssessment`, target age group, active version ve valid score range ayrımını açıkça uygula; atama kararını `AssessmentPlacement` tablosunda snapshot’la.

### P1 — Metin/egzersiz ilerlemesi deterministik ve tekrar üretiyor

**Etkisi:** Öğrenci aynı içeriği tekrar tekrar görür; exposure bias ve öğrenilmiş cevap etkisi oluşur.

**Düzeltme:** Content exposure kaydı, son N içerik hariç tutma, dengeli randomization/seed, aynı tür içinde çeşitlendirme, üst zorlukta kontrollü deneme ve yeterli içerik yoksa admin uyarısı ekle.

## Önerilen 90 günlük yol haritası

### 0–30 gün

- Answer-key redaction ve bağımsız okuma akışını tek server-authoritative session modeline taşı.
- Yaş grubu → assessment template → pinned form → program placement zincirini düzelt.
- WPM-only seviye kararını kavrama tabanı ve “yetersiz veri” durumlarıyla güvenli hale getir.
- StudentReading completion için idempotency, session ownership, duplicate answer ve süre sınırı ekle.
- P0/P1 için integration test: yaş grubu A/B, yanlış cevap anahtarı erişimi, tekrar POST, yüksek WPM/düşük kavrama, template placement.

### 31–60 gün

- Ortak level/difficulty sözlüğü ve admin editörü oluştur.
- Metin metadata’sına Türkçe readability, kelime uzunluğu, cümle uzunluğu, kelime sıklığı, ek/morfoloji, sınıf/yaş ve tür ekle.
- Question bank için item exposure, doğru cevap oranı, ayırt edicilik, Bloom dağılımı ve kalite inceleme kuyruğu ekle.
- Retention/transfer fazlarına en az 7 ve 28 günlük zaman pencereleri ve alternate-form içerikleri ekle.
- Admin’de ham JSON yerine motor tipine göre form schema, validation, preview ve dry-run günlük planı ekle.

### 61–90 gün

- Türkçe normatif pilot: sınıf/yaş/dil geçmişi bazlı baseline ve WCPM + kavrama normları.
- “Normal okuma”, “skimming/tarama” ve “derin okuma” modlarını ayrı hedeflerle tanımla; her metrik için hangi okuma amacının ölçüldüğünü göster.
- Öğretmen/veli raporlarında hız, doğru kelime, kavrama, verimlilik ve transferi ayrı göster.
- Bağımsız kontrol grubu veya eşleştirilmiş pilotla pre/post/retention/transfer etki ölçümü yap. Pazarlama iddiası yalnız bu sonuçlardan sonra yazılmalı.

## Son karar

Proje bırakılacak durumda değil; doğru bir platform temeli ve rakiplerle rekabet edebilecek yönetim kapsamı var. Fakat bugün en doğru ürün tanımı “çok özellikli hızlı okuma egzersiz platformu” olur. “Anlayarak hızlı okumayı bilimsel olarak kanıtlanmış biçimde artırır” iddiası için ölçüm güvenliği, yaşa özel assessment bağlantısı, Türkçe normlama ve bağımsız etki çalışması eksik.

İlk sürüm önceliği yeni egzersiz eklemek değil, mevcut akışın tek ve güvenilir ölçüm zincirine indirilmesi olmalı. Bu düzeltmelerden sonra mevcut egzersiz ve admin kapsamı güçlü bir v2 eğitim ürününe dönüşebilir.

## Uygulama sonrası düzeltme durumu (2026-09-10)

İncelemede bulunan P0/P1 yazılım kusurlarının önemli bölümü düzeltildi. Öğrenciye dönen bağımsız okuma sorularında cevap anahtarı kaldırıldı; okuma başlatma artık kullanıcıya bağlı bir `StudentReadingAttempt` ve `SessionId` üretiyor; tamamlama yalnızca bu oturumun sahibi tarafından yapılabiliyor, cevaplar sunucudaki anahtarla değerlendiriliyor, süre sunucu olaylarından hesaplanıyor ve aynı oturumun tekrar gönderimi aynı sonucu döndürüyor.

Universal egzersiz oturumlarında öğrenci sahipliği, oturum-egzersiz eşleşmesi, sunucu zamanlaması, WPM sınırları, soru tekrarları, idempotency ve concurrency kontrolleri uygulanıyor. İstemcinin gönderdiği WPM, skor, kavrama ve tamamlanma zamanı placement veya günlük ilerleme kararında kaynak kabul edilmiyor. Geçerli bir server WPM’si bulunan soru içermeyen okuma oturumları artık ölçülebiliyor; geçerli sinyal üretmeyen pasif/uyumsuz motorlar `NotMeasured` olarak kalıyor.

Admin katalog ekranında egzersizin aktif/pasif durumu artık özetleniyor ve düzenleme formundan iki veri yolunda da değiştirilebiliyor. Admin içerik yapılandırmasında assessment’ın kullandığı ortak seviye sözlüğü de görünür; eşiklerin kod içinde kopyalanması kaldırıldı. Öğrenci istemcisinde doğrulama isteği başarısız olduğunda focus/visualization motorlarının bekleyen aksiyonu serbest bırakılıyor; geçici ağ hatası assessment’ı sessizce kilitlemiyor.

Assessment akışı profil yaş grubunu istemci beyanından üstün tutuyor, aktif yaş grubunu ve yaşa bağlı formu sabitliyor, rol/sıra ile içerik snapshot’ı oluşturuyor, prerequisite ve retention için yedi günlük bekleme uyguluyor. Seviye kararı hız ile kavramanın düşük olanını kullanıyor; ölçülmemiş sonuçlar placement hesabına alınmıyor. Önerilen program yalnızca aktif, assessment olmayan ve yaş/puan aralığı uyumlu şablonlardan seçiliyor.

Bağımsız okuma akışında da profilin aktif yaş grubu kategori, metin listesi ve başlatma sorgularında uygulanıyor; yaş grubu dışı aktif metinler öğrenciye sunulmuyor.

Günlük ilerleme, aktif ve silinmemiş içeriklerden seçim yapıyor; aynı gün içinde tekrarları azaltıyor, yeterli içerik yoksa kontrollü alt zorluk fallback’i kullanıyor ve tamamlamayı server oturum sonucuna bağlıyor. Owned store’da oturum bağlantılı benzersiz günlük kayıt ve yarış durumu ele alındı. Legacy store’da aynı ölçüm durumu için `StudentExerciseResults.IsMeasured` alanı eklendi; `016_exercise_progress_gamification_compatibility.sql` scripti bu alanı ve önceki session uyumluluk alanlarını oluşturuyor.

Formal assessment ve practice oturumlarının öğrenciye dönen state/configuration payload’larında cevap anahtarları, hedef indeksleri ve (assessment için) açıklamalar gönderilmiyor. Practice akışında motorların çalışabilmesi için gerekli sequence/mode/N-back kuralı korunuyor; aktif soru cevapları sunucuya sırayla gönderilip sonuçtan gelen doğru/yanlış ve açıklama kullanılıyor, süre aşımı da açık bir timeout işaretiyle kaydediliyor. Visualization sahne ve soruları session state’e config’ten veya owned/legacy soru bankasından yükleniyor; öğrenciye açık scene endpoint’leri ve session state cevap anahtarlarından arındırılıyor, `answer_question` server state üzerinden değerlendiriliyor ve completion isteği istemciden boş/yanlış bir cevap listesi göndermeden server state’i kullanıyor. Focus/attention egzersizlerinde position/word match aksiyonları owned ve legacy oturumlarda state, sıra ve trial zaman penceresiyle doğrulanıyor; practice modunda server geri bildirimindeki hit/miss/false-alarm toplamlarıyla yerel durum eşitleniyor. Assessment modunda sequence ve hedef dizileri istemciye gönderilmiyor; istemci her trial için `focus_step` ile yalnızca o anki uyaranı alıyor, sunucu sunulan trial indeksini ve cevap penceresini bağlıyor, istemci hedef/başarı hesabı ve geri bildirimi kapatıyor, kaçırılan hedefler tamamlamada hesaba katılıyor ve completion bekleyen aksiyon kuyruğu bitmeden yapılmıyor. Schulte/grid motorunda da sunucu sahipli yerleşim ve beklenen sıra `grid_click` aksiyonuyla doğrulanıyor; yanlış tıklama adımı ilerletmeden hata sayacına yazılıyor. Sunucu assessment completion’ı tüm trial’lar sunulmadan kabul etmiyor; hiç eşleşme yapılmasa bile cevapsız hedefleri miss olarak kaydediyor. Mod için gerekli stimulus dizisi eksikse oturum başlatma/ölçüm reddediliyor; eksik fixture yanlışlıkla başarılı ölçüm üretmiyor. Ağ hatasında istemci bekleyen aksiyonu serbest bırakıyor; tamamlanmış sonuç state’i de aynı sanitizer’dan geçiriliyor. Desteklenmeyen visual expansion, memory ve pasif motorlar güvenilir server sinyali üretmediği sürece ölçülmemiş kalıyor. Öğrenci katalog listelerinde de configuration cevap anahtarlarından arındırılıyor; yönetim yetkisi olan admin çağrıları ham configuration görebiliyor.

Günlük tamamlamada istemciden gelen skor, WPM, doğru/yanlış sayıları, cevap süresi ve result JSON’u artık kaynak kabul edilmiyor; owned ve legacy store, tamamlanmış server session/result kayıtlarından süre ve sayıları yeniden kuruyor. Aynı session ve aynı program slotu için benzersiz indeksler ile yarış yakalama eklendi. Integrity migration’ı index oluşturmadan önce mevcut duplicate slot ve assessment-session kayıtlarını silmeden ayırıyor; owned günlük tablonun tarihsel PascalCase kolonları için migration kolon adları da düzeltildi. Yeni oturumlarda timeout ve elapsed süre, ilk gerçek server aksiyonuyla başlayan zaman penceresinden hesaplanıyor; eski state JSON’ları için session oluşturma zamanı geriye dönük varsayılan olarak korunuyor. WPM de yalnızca sunucunun doğruladığı okuma başlangıç/bitiş olayları ve izinli okuma motorlarında ölçülüyor.

Doğrulama sonucu: Speed Reading unit testleri **252 başarılı, 0 başarısız, 0 atlanan**; öğrenci Angular testleri **89 başarılı**, admin Angular testleri **157 başarılı**; Speed Reading API ve iki Angular uygulamasının production derlemeleri başarılı. Derlemede yalnızca NuGet güvenlik akışı için `NU1900` uyarısı görüldü; paket kaynağına ağ erişimi olmadığı için güvenlik feed’i ayrıca doğrulanamadı. Docker/aktif PostgreSQL ortamı bu çalışma alanında bulunmadığından gerçek migration, canlı veri backfill’i ve uçtan uca tarayıcı senaryosu çalıştırılamadı.

Kalan teknik sınırlar:

- Focus/attention assessment akışında sequence istemciden gizlenip mevcut uyaran trial bazında sunuluyor; indeks/sıra ve sunum penceresi server state’ine bağlı. Practice akışında sequence istemciye açık kalıyor; bu nedenle bu motorun practice sonucu placement kanıtı olarak kullanılmamalı. Canlı assessment kullanımı için deterministik zamanlama, pause/resume ve kötü niyetli istemci senaryolarının E2E testleri ayrıca gerekli.
- Visual expansion ve bazı memory/pasif motorlar için action seviyesinde server-side doğrulama henüz yok; bu motorlar placement’a ölçülmüş puan olarak girmiyor. Schulte/grid doğrulaması tamamlandı; kalan motorlar assessment formunda zorunlu rol olarak kullanılmamalı veya deterministik server fixture/adaptörleri tamamlanana kadar `NotMeasured` kabul edilmelidir.
- Yeni oluşturulan session’larda timeout ve progress sayacı ilk server aksiyonuna bağlandı; eski state JSON’ları için oluşturma zamanı fallback’i nedeniyle canlı migration sonrası eski açık oturumların ayrıca sonlandırılması gerekir. Focus/assessment için pause/resume ve kötü niyetli istemci senaryolarının canlı E2E testi yine gereklidir.
- Legacy deployment’ta versioned assessment attempt/history/comparison/phase-plan endpoint’leri owned data kapalıyken desteklenmiyor; bu açıkça hata olarak dönüyor ve iki veri yolunun tamamen eşitlenmesi için ayrı migration çalışması gerekiyor.
- `IsMeasured`, `IsAssessmentMode`, `AssessmentAttemptId`, `Exercises.IsActive` ve günlük session bağlarının production’da kullanılabilmesi için deployment sırasında `--migrate-only` akışı çalıştırılmalı. Legacy migration artık eski client-writable süre/WPM/kavrama alanlarından otomatik olarak `Measured` üretmiyor; eski satırlar bağımsız doğrulama veya yeniden oynatma yapılana kadar `NotMeasured` kalıyor.
- Hız/kavrama eşikleri tek bir seviye sözlüğünde toplanıp admin’de salt okunur olarak gösteriliyor; ancak henüz sürümlü/persisted bir admin kataloğundan düzenlenmiyor. Türkçe yaş/sınıf normları, alternate-form kalibrasyonu ve bağımsız kontrol gruplu etki çalışması tamamlanmadan platform bilimsel olarak standardize edilmiş kabul edilmemeli.

Bu nedenle mevcut durum, ölçüm güvenliği ve yazılım davranışı açısından belirgin biçimde standardize edilmiş bir temel; akademik geçerlilik ve tüm motorlarda tam server-authoritative değerlendirme açısından ise tamamlanmamış bir v2 adayıdır.
