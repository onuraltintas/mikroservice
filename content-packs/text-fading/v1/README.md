# Metin Solma — tüm seviyeler, dört soru

Yerel yayın paketi; **canlıya uygulanmadı**. Dört yaş grubunda 1–5. seviyeler: 11 mevcut ayar güncellenir, 9 yeni ayar eklenir. Kaynak katalog dosyaları geçmiş yayın referansı olarak korunur.

| Yaş | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 | Başlangıç beklemesi |
| --- | --- | --- | --- | --- | --- | --- |
| Çocuk | 60 | 80 | 100 | 120 | 140 | 5 saniye |
| Genç | 150 | 180 | 210 | 240 | 270 | 2,5 saniye |
| Genç yetişkin | 180 | 210 | 240 | 270 | 300 | 2 saniye |
| Yetişkin | 200 | 230 | 260 | 290 | 320 | 2 saniye |

Değerler kelime/dakika **gösterim temposudur**, ölçülmüş okuma hızı veya bilimsel yaş normu değildir. Yeni alt seviyeler pilot ürün ayarıdır; mevcut üst seviyeler korunur. Otomatik tempo değişimi ve zaman sınırı yoktur. Aktif gösterim süresi `başlangıç beklemesi + gerçek kelime sayısı × 60 / tempo` saniyedir. Duraklama süreye dahil değildir.

Metinler kısaltılmaz, yeni soru üretilmez. Aktif, aynı seviyede, uygun yaşta ve ortak havuzda/aynı egzersize ait metinlerden dört geçerli sorusu bulunanlar seçilir. Metnin kendi sıralamasından ilk dört geçerli soru oturum snapshot'ına alınır. Kendi geçmişinde daha az kullanılan metinler önceliklidir. Aynı filtreleme ve soru sayısı önizleme için de uygulanır; uygun içerik yoksa örnek metinle devam edilmez. Özel önizleme sadece gösterim parametrelerini değiştirir; soru sayısını veya metin bağını değiştirmez.

11 Ekim 2026 salt okunur canlı envanterinde her yaş/seviyede en az 11 ortak, dört geçerli sorulu metin bulundu. Bu, pedagojik içerik incelemesi değildir. Daha kısa süre hedefi ayrıca içerik planlaması gerektirir: mevcut çocuk metinleri yaklaşık 314–462 kelimedir; düşük tempoda birkaç dakika sürer. Bu pakette süreyi kısaltmak için metin/soru bağı değiştirilmez.

## Uygulama ve geri dönüş

1. `node content-packs/text-fading/v1/generate.mjs` ile deterministik çıktıyı üret, testleri çalıştır.
2. API/frontend'i birlikte yayımla; dört soru kuralını desteklemeyen eski API ile SQL'i uygulama.
3. Yalnız Speed Reading DB'nin doğrulanmış yedeğini ve hedef 20 egzersizin önceki satırlarını al. Identity/Koçluk kapsam dışıdır.
4. `psql -v ON_ERROR_STOP=1 -f apply.sql`: tek transaction; beklenen kaynak ayar değişmişse, mükerrer aktif egzersiz varsa veya dört sorulu metin yoksa tüm işlem reddedilir. Tekrar uygulama audit/version değişikliği oluşturmaz.
5. Her yaşta beş aktif Metin Solma seviyesi ve yeni kayıtta dört soru bulunduğunu doğrula.
6. Geri dönüşte yalnız bu paketin hedef satırlarını doğrulanmış önceki değerlerine getir. Yeni dokuz kayda bağlı oturum oluşmuşsa silme; pasife al ve geçmişi koru. API/frontend'i uyumlu sürüme döndür.

Seviye tespit, günlük program görev sırası, öğrenci schedule'ları, açık oturum snapshot'ları, sonuçlar, metinler ve sorular değiştirilmez. Alt seviyelere açılması katalog erişimini genişletir; günlük programa otomatik yeni görev eklemez. Gerçek canlı Identity oturumuyla kabul testi yayın sonrasında ayrıca yapılmalıdır.
