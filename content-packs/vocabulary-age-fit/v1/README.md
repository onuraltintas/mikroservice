# Kelime havuzu yaş uyumu v1 (19.11.2026)

Canlı kullanıcı testi bulgusu A2, karar K4: "uygunsuz olan kelimeleri uygun kelimelerle değiştir."

## Sorun
Kelime havuzu yaşlar arasında ortak; seviye 4 ve 5'teki 376 kelimenin hepsi akademik ya da mesleki terim (araştırma yöntemi, hukuk, tıp, felsefe, insan kaynakları; örnek: Transendental, Triangülasyon, Emansipasyon). Çocuk ve Genç programlarının seviye 4–5 kelime testleri bu havuzdan soruyordu. Sayılar 19.11.2026'da admin Kelime havuzu sayfasından salt okunur alındı: seviye 4'te 271, seviye 5'te 105 ortak kelime.

## Ne yapar
- Seviye 4–5'teki ortak kelimeler Yetişkin grubuna geçer; Genç yetişkin için birer kopyası açılır. Bu iki yaş aynı kelimeleri görmeye devam eder.
- Çocuk (seviye 4 ve 5'te 24'er) ve Genç (28'er) için yaşa uygun 104 yeni kelime eklenir (`words.json`).
- Seviye 1–3, egzersiz ayarları, ders ve müfredat kayıtları değişmez. Öğrenci geçmişi silinmez.
- Uygulama sonunda her yaşın seviye 4–5 testleri için yeterli ve belirsizliği olmayan kelime olduğu denetlenir; yetmezse işlem geri alınır.

## Bilinen etkiler
- Genç yetişkin kullanıcıların seviye 4–5 kelime tekrar geçmişi eski kayıtlarda kalır; kopyalar yeni kayıt olduğu için tekrar sırası sıfırdan başlar.
- Çocuk ve Genç'in eski seviye 4–5 kelime tekrarları artık önlerine gelmez.
- Seviye 3'teki iş dünyası ve ekonomi kelimeleri (Likidite, Cari Açık gibi) bu pakete girmedi; Çocuk için ayrıca ele alınmalı.

## Uygulama
1. Hızlı Okuma veritabanının yedeğini al.
2. `psql ... -v ON_ERROR_STOP=1 -f apply.sql`. Çıktıdaki tablo: Çocuk 24/24, Genç 28/28, Yetişkin 271/105, Genç yetişkin 271/105.
3. Ortak kelime sayısı değişmişse SQL "baseline changed" diye durur; zorlamadan bildir.

## Geri alma
`rollback.sql`: kelimeler ortak havuza döner, kopyalar ve yeni kelimeler yumuşak silinir, yedek tablosu kaldırılır. Geri almadan sonra paket tekrar uygulanamaz (yeni sürüm gerekir).

## Doğrulama
- `node --test content-packs/vocabulary-age-fit/v1/pack.test.mjs`
- Geçici PostgreSQL 16 üzerinde canlı sayılarla denendi: uygulama, ikinci kez uygulamanın reddi ve geri alma (376 kelime ortak havuza döndü).
- Üretim: `node content-packs/vocabulary-age-fit/v1/generate.mjs`
