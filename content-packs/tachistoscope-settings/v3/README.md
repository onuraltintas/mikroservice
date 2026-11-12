# Takistoskop — yaş/seviye ayarları v3 (Kasım 2026)

Bu sürüm yalnız tarih kaydıdır: **SQL dosyası yoktur**. Değişiklik, Hızlı Okuma servisi
açılırken otomatik çalışan EF veri geçişiyle uygulanır:
`20261112120000_TachistoscopeWordsAndShorterDurations`. v2 paketi değiştirilmedi.

Kapsam: 20 normal "Kısa Gösterimde Tanıma" egzersizi. Dört seviye tespit egzersizi,
tur sayıları (`content.count`, bkz. `20261112100000`), gösterimler arası bekleme ve
süre sınırları (`minDurationMs`/`maxDurationMs`) değişmez.

## İçerik türü

- Seviye 1–2: sayı (basamak sayısı ve sınırları değişmedi).
- Seviye 3–4: kelime (`vocabulary_items`, seviye ≤ egzersiz seviyesi, yaş etiketi boş ya da öğrencinin yaşı).
- Seviye 5: iki kelimelik kelime grubu. Kelimeler her oturumda rastgele eşleştirilir; eşleştirme
  oturumda saklanan bir tohumla sabittir (alfabetik komşu çiftler kullanılmaz).

Uzunluk harf sayısıdır; kelime grubunda aradaki boşluk da sayılır.

## Takistoskop kelime listesi (günlük kelimeler)

Takistoskop tanıdık kelimelerin hızlı tanınmasını çalıştırır; zorluk kelimenin zorluğundan değil,
gösterim süresinden ve uzunluktan gelir. Bu yüzden kelime ve kelime grubu havuzları:

- egzersiz seviyesi 3, 4 veya 5 olsa da yalnız **seviye 1–2** kelimelerini kullanır
  (`engineConfig.content.maxVocabularyLevel = 2`; geçiş bu anahtarı yalnız 12 kelime/kelime grubu
  satırına yazar, sayı satırlarına yazmaz, geri alma adımı siler; değer 1–5 arası tam sayı olmalıdır,
  anahtar yoksa eski davranış sürer: seviye ≤ egzersiz seviyesi);
- yalnız Takistoskop için tanımlı 68 kelimelik dışlama listesini (`TachistoscopeWordExclusions`)
  kelimelerde ve kelime grubu parçalarında uygular: Bej, Endişeli, Korkmuş, Kızgın, Mat, Çirkin, Abonelik,
  Adapte, Akor, Akım, Alegori, Analog, Antivirüs, Arp, Banner, Beğeni, Blog, Bluetooth, Captcha, Diatonik,
  Dize, Domain, Dram, Drone, Emoji, Epik, GIF, Global, Hashtag, Hikaye, Influencer, Kafiye, Kaos, Kaygı,
  Kriz, Laptop, Link, Lirik, Lokal, Miraz, Mısra, Nazım, Nesir, Not, Ping, Podcast, Prestige, Redif, Reels,
  Router, Sansür, Savaş, Scooter, Ses, Siber, Spam, Takipçi, Teşbih, Ton, Tweet, URL, USB, Uyak, Viral,
  VPN, WiFi, Wiki, Özçekim. Karşılaştırma Türkçe küçük harfle ve bütün kelimeyle yapılır; düzeltme işareti
  korunur, yani "Hikaye" dışlanır, "Hikâye" kalır;
- ilk harften sonra büyük harf içeren her kelimeyi (kısaltmalar, karışık yazımlı marka adları) dışlar.

Kelime Hazinesi bu kuraldan etkilenmez. Canlı havuzda 1161 farklı harf-yalnız kelimenin 614'ü kalır
(uzunluğa göre 2:5, 3:20, 4:61, 5:154, 6:119, 7:95, 8:79, 9:44, 10:25, 11:7, 12:4, 13:1); tüm kelime
aralıkları (3–10) karşılanır. Personel önizlemesi uygulamadaki gündelik kelime listesini kullanır.

## Başlangıç uzunluğu / başlangıç gösterim süresi (ms) — eski → yeni

| Yaş grubu | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
|---|---|---|---|---|---|
| Çocuk 9–12 | 3 / 1500 → 3 / 1200 | 3 / 1400 → 3 / 1120 | sayı 4 / 1300 → kelime 4 / 1040 | sayı 4 / 1200 → kelime 5 / 960 | sayı 5 / 1100 → grup 8 / 880 |
| Genç 13–16 | 3 / 1000 → 3 / 800 | 4 / 900 → 4 / 720 | sayı 4 / 800 → kelime 5 / 640 | sayı 5 / 700 → kelime 6 / 560 | sayı 6 / 600 → grup 10 / 480 |
| Genç yetişkin 17–21 | 4 / 800 → 4 / 640 | 4 / 700 → 4 / 560 | sayı 5 / 600 → kelime 6 / 480 | sayı 5 / 550 → kelime 7 / 440 | sayı 6 / 500 → grup 11 / 400 |
| Yetişkin 22+ | 4 / 800 → 4 / 640 | 5 / 700 → 5 / 560 | sayı 5 / 600 → kelime 6 / 480 | sayı 6 / 550 → kelime 7 / 440 | sayı 7 / 500 → grup 12 / 400 |

## Uyarlama uzunluk sınırları (min–maks)

| Yaş grubu | Seviye 1–2 (sayı) | Seviye 3 (kelime) | Seviye 4 (kelime) | Seviye 5 (grup) | Süre sınırı (değişmedi) |
|---|---|---|---|---|---|
| Çocuk | 3–5 | 3–6 | 4–7 | 6–10 | 400–2000 ms |
| Genç | 3–6 | 4–7 | 5–8 | 8–12 | 300–1500 ms |
| Genç yetişkin | 4–6 | 4–8 | 5–9 | 9–14 | 250–1500 ms |
| Yetişkin | 4–7 | 5–9 | 6–10 | 10–16 | 250–1500 ms |

Bekleme: çocuk 750 ms, diğerleri 500 ms (değişmedi). Her yeni başlangıç süresi kendi süre sınırının içindedir.

## Otomatik uyarlama (eğitim ve önizleme; seviye tespitinde kapalı)

Birbirinden bağımsız **üç** cevaplık bloklar değerlendirilir (v2: beş):

- 3/3 doğru: süre %15 kısalır; 2/3 doğru: aynı kalır; 0–1 doğru: süre %15 uzar.
- Süre alt sınırdaysa başarılı blokta uzunluk bir artar ve süre başlangıca döner.
- Süre üst sınırdaysa başarısız blokta uzunluk bir azalır ve süre başlangıca döner.
- Süre ve uzunluk aynı blokta birlikte zorlaştırılmaz; her şey sınırlar içinde kalır. Eksik son blok uyarlama yapmaz.

Cevaplarda büyük-küçük harf ve fazla boşluk fark etmez (Türkçe küçük harfe çevrilerek karşılaştırılır).

## Geri alma

Geçişin Down adımı yalnız yeni değerleri hâlâ taşıyan satırları v2 değerlerine döndürür;
yayından sonra elle değiştirilen satırlara dokunmaz. Başlamış oturumlar kendi başlangıç
yapılandırmasıyla devam eder.

Bu parametreler onaylanan ürün ayarlarıdır; akademik yaş normları veya okuma hızı
ölçümü olarak sunulmaz.
