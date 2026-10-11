# Göz Takibi — yaş/seviye pilot ayarları

Bu paket **4 yaş grubu × 5 seviye = 20 normal Göz Takibi kaydı** için süre ve tempo düzenler. Egzersiz kimlikleri, hedef noktasının mevcut boyutu/biçimi, program atamaları, seviye tespiti, sonuçlar ve açık oturumlar korunur. Metin veya soru eklenmez. Otomatik uyarlama eklenmez.

## Süre ve tempo

Her hücre **çalışma süresi (saniye) / tam hareket döngüsü (saniye)** biçimindedir. Döngü süresi küçüldükçe hareket hızlanır. Seviye 1 yatay gidiş–dönüş, 2–3 daire, 4–5 sekiz yoludur. Daire ve sekiz aynı süreyi kullansa da geometrileri farklı olduğundan aynı doğrusal hız anlamına gelmez.

| Yaş grubu | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
|---|---|---|---|---|---|
| Çocuk (9–12) | 45 / 4,0 | 60 / 3,8 | 75 / 3,6 | 90 / 3,4 | 105 / 3,2 |
| Genç (13–16) | 60 / 3,6 | 75 / 3,4 | 90 / 3,2 | 105 / 3,0 | 120 / 2,8 |
| Genç yetişkin (17–21) | 75 / 3,4 | 90 / 3,2 | 105 / 3,0 | 120 / 2,8 | 135 / 2,6 |
| Yetişkin (22+) | 90 / 3,2 | 105 / 3,0 | 120 / 2,8 | 135 / 2,6 | 150 / 2,4 |

Bunlar kullanıcı denemesiyle düzenlenecek **ürün pilot değerleridir**, bilimsel yaş normu veya tedavi protokolü değildir. Egzersiz yalnız ekrandaki hedefi izleme pratiği sunar. Kamera, gerçek bakış takibi, doğruluk ya da okuma hızı ölçümü yoktur. [Okuma ve hızlı okuma üzerine araştırma derlemesi](https://doi.org/10.1177/1529100615623267) bu pilot sayısal değerleri doğrulamaz; egzersizden doğrudan okuma hızı/anlama kazanımı çıkarımı yapılmaz. Rahatsızlık halinde durdurma yönergesi gösterilir.

## Oyuncu ve özel önizleme

- Kayıtlı `timing.speedMs` tam döngü süresidir. “Hız seviyesi” yerine özel ayarlarda **Hareket döngüsü / geçiş aralığı (ms)** kullanılır; değiştirilmeden başlatıldığında kayıtlı tempo korunur.
- Yatay/dikey: bir gidiş–dönüş; daire/sekiz: bir tam tur. Önizlemedeki sıçramalı seçeneklerde aynı sayı **iki konum arasındaki beklemedir**. Bunlar normal katalogda kullanılmaz ve seçenek adı “sıçramalı” olarak gösterilir.
- Eski `movement.speedLevel` verisi, `speedMs` yoksa aynı eski formülden milisaniyeye çevrilir (en yakın ms). Sıçramalı eski kayıtlar kendi `jumpIntervalMs` değerini kullanır.
- Süre duraklatmada işlemez; sıçrama beklemesinin kalanı ve sürekli hareketin fazı korunur. Kayıtlı oturumlar backend süresi tamamlanmadan bitirilemez; farklı kullanıcı tamamlayamaz. Sonuç `NotMeasured` olarak kaydedilir, WPM/okunan kelime/puan uydurulmaz.
- Admin/öğretmen özel önizlemesi yalnız yereldir; katalog, sonuç veya ilerleme kaydetmez. Sabitleme ve Sakkad ayar akışları değiştirilmez.

## Paket ve kontrollü yayın

Bu turda **canlıya veya kalıcı uygulama veritabanına uygulanmamıştır**. Yalnız geçici Testcontainers PostgreSQL üzerinde denenir.

1. `node content-packs/eye-tracking/v1/generate.mjs` ve `node --test content-packs/eye-tracking/v1/pack.test.mjs` çalıştırın. Generator yaş kataloglarını değiştirmez; bu paket katalogların üzerine uygulanan ayrı bir ayar geçişidir. Yeni kurulumda yaş kataloglarından sonra uygulanmalıdır.
2. Hızlı Okuma veritabanının yedeğini alın ve geri yüklemeyi doğrulayın. Ayrı test veritabanında güncel canlı envanterle prova yapın.
3. Sonraki yayın onayında Hızlı Okuma API’sini kısa süre durdurup yalnız Hızlı Okuma DB’sine `apply.sql` dosyasını `psql -X -v ON_ERROR_STOP=1` ile uygulayın. Bu işlem Identity, Koçluk veya Notification DB’sine uygulanmaz.
4. Script tip/yaş/seviye/aktiflik/mükerrer kayıt ve eski-yeni JSON eşleşmesini denetler. Yönetici sonradan ayar değiştirdiyse sessizce üzerine yazmaz; tüm geçiş iptal olur. Tekrar uygulama sürüm/tarih değiştirmez.
5. Yeni frontend’i yayınlayın, API’yi açın; kayıtlı ve özel önizleme, duraklatma/devam, sonuç ve yaş/seviye listelerini kontrol edin.
6. Geri dönüşte API’yi durdurup `rollback.sql` uygulayın ve önceki frontend’i geri koyun. Script yalnız 20 katalog ayarını eski haline getirir, hiçbir eğitim geçmişini silmez; sonradan düzenlenmiş kaydı ezmez.

## Doğrulama

- Ayar paketi testi: tam 20 hücre, süre/tempo/yol, yalnız izinli alanlar, deterministik JSON/SQL.
- Angular motor testleri: özel/varsayılan tempo, kök/iç içe ayarlar, eski süre yuvarlama, kare hızından bağımsız hareket, görünür alan sınırları, duraklatma ve sıçrama aralığı.
- PostgreSQL: 20 hücrede gerçek oturum ve sonuç; erken/yabancı/duraklatılmış bitirme engeli; idempotent uygulama/geri dönüş; katalog değişikliği, mükerrer kayıt, yapay güncelleme hatası ve geri dönüş koruması.
- Playwright: gerçek yerel UI/motor, Identity/API fixture; masaüstü ve mobil kayıtlı/özel/sıçramalı akışlar, varsayılana dönüş, geçersiz değer, süre/konum, duraklatma ve önizlemede sıfır API yazımı. Bu, gerçek Identity oturumlu canlı test değildir.

### Son yerel sonuçlar — 12 Ekim 2026

- Paket: **2/2**, Hızlı Okuma backend birim testleri: **1206/1206**.
- Hızlı Okuma Angular testleri: **917/917**; özel Göz Takibi/Sabitleme/Sakkad alt kümesi **72/72** (genel toplama dahildir).
- Gerçek PostgreSQL Göz Takibi entegrasyonu: **6/6**; normal geçiş senaryosunda 20 hücrenin tamamıyla gerçek oturum/sonuç doğrulandı.
- Playwright masaüstü/mobil: **8/8**.
- Üretim derlemesi başarılı. Değiştirilmemiş oyuncu SCSS dosyasında mevcut boyut uyarısı bulunuyor (128,99 kB / 120 kB bütçe).
- Kapsam raporu: ortak hareket motorunda satır kapsamı %87,77; özel önizleme ayarlarında %99,39. Tüm frontend satır kapsamı %64,47; bu rapor tüm projede %100 kod kapsamı iddiası değildir.
- Yapısal kod incelemesi: özel ayarlar rol/önizleme sınırını koruyor; ayar paketi kimlikleri sabit, geçişler atomik ve sonradan yapılan düzenlemeyi ezmiyor. Kimlik bilgisi, kullanıcı verisi veya gizli anahtar eklenmedi. Canlıya gönderim yok.
