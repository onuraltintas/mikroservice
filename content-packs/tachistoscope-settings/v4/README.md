# Kısa Gösterim (Takistoskop) — süre odaklı düzenleme v4 (26.11.2026)

Bu sürüm yalnız tarih kaydıdır: **SQL dosyası yoktur**. Ayar değişikliği, Hızlı Okuma servisi açılırken otomatik
çalışan EF veri geçişiyle uygulanır: `20261126110000_TachistoscopeShorterMinimumDurations`. v2 ve v3 değiştirilmedi.

## Amaç

Kısa Gösterim tanıma süresini kısaltmayı çalıştırır. Asıl değişken gösterim süresidir; uzunluk ikinci adımdır.
Bakış açısı Görsel Genişleme'nin, öbekle tempolu okuma Kelime Gruplama'nın işidir. "Görme alanını genişletir"
gibi bir iddia kullanılmaz (30.10.2026 kararı).

## Değişenler

| | v3 | v4 |
|---|---|---|
| En kısa gösterim süresi | Çocuk 400, Genç 300, Genç yetişkin / Yetişkin 250 ms | Çocuk 300, Genç 200, Genç yetişkin / Yetişkin 150 ms |
| 3/3 doğru | Süre %15 kısalır | Aynı |
| 3/3 doğru, süre en kısada | Uzunluk +1, süre başlangıca döner | Uzunluk +1, süre en kısa sürenin 1,5 katına döner (Çocuk 450, Genç 300, diğerleri 225 ms; başlangıcı aşmaz) |
| 2/3 doğru | Değişmez | Değişmez |
| 0–1 doğru | Süre %15 uzar; süre en uzundaysa uzunluk −1 | Hiçbir şey değişmez: süre uzamaz, uzunluk kısalmaz |
| Sonuç ekranı | Başlangıç → son süre | Doğru tanınan en kısa gösterim ve en uzun sayı/kelime/öbek |
| 15 ve 16 harflik öbekler | 21 ve 14 | En az 30'ar |

Değişmeyenler: içerik türleri (seviye 1–2 sayı, 3–4 kelime, 5 iki kelimelik öbek), başlangıç uzunlukları,
uzunluk sınırları, başlangıç süreleri, en uzun süre sınırı, tur sayıları, bekleme, yazarak cevap, "+" odak işareti.
Örtü yoktur; ulaşılan uzunluk bir sonraki oturuma taşınmaz (her oturum seviyenin ayarıyla açılır, seviye haftada bir
programla artar). Seviye tespitindeki dört Kısa Gösterim egzersizi değişmez.

## Ayarlar (başlangıç uzunluğu (sınır) / başlangıç süresi / en kısa süre / tur)

| Yaş grubu | Seviye 1 (sayı) | Seviye 2 (sayı) | Seviye 3 (kelime) | Seviye 4 (kelime) | Seviye 5 (öbek) |
|---|---|---|---|---|---|
| Çocuk 9–12 | 3 (3–5) / 1200 / 300 / 15 | 3 (3–5) / 1120 / 300 / 19 | 4 (3–6) / 1040 / 300 / 23 | 5 (4–7) / 960 / 300 / 26 | 8 (6–10) / 880 / 300 / 30 |
| Genç 13–16 | 3 (3–6) / 800 / 200 / 20 | 4 (3–6) / 720 / 200 / 24 | 5 (4–7) / 640 / 200 / 28 | 6 (5–8) / 560 / 200 / 31 | 10 (8–12) / 480 / 200 / 35 |
| Genç yetişkin 17–21 | 4 (4–6) / 640 / 150 / 20 | 4 (4–6) / 560 / 150 / 24 | 6 (4–8) / 480 / 150 / 28 | 7 (5–9) / 440 / 150 / 31 | 11 (9–14) / 400 / 150 / 35 |
| Yetişkin 22+ | 4 (4–7) / 640 / 150 / 20 | 5 (4–7) / 560 / 150 / 25 | 6 (5–9) / 480 / 150 / 30 | 7 (6–10) / 440 / 150 / 35 | 12 (10–16) / 400 / 150 / 40 |

Bu değerler onaylanan ürün ayarlarıdır; akademik yaş normu veya okuma hızı ölçümü olarak sunulmaz.

## Geçiş ve geri alma

Geçiş yalnız `engineConfig.adaptive.minDurationMs` değerini değiştirir ve bir satırı yalnız v3 ayarlarını ve eski en kısa
süreyi aynen taşıyorsa değiştirir; elle değiştirilmiş satırlara, seviye tespiti ve silinmiş egzersizlere dokunmaz.
İkinci çalıştırma bir şey değiştirmez. Down adımı yeni değeri hâlâ taşıyan satırları eski en kısa süreye döndürür.
Başlamış oturumlar kendi başlangıç ayarlarıyla biter; yeni uyarlama kuralı ise kod yayınlanınca bütün oturumlarda geçerlidir.
