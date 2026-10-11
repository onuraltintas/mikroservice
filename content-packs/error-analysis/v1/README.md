# Hata Analizi — hazırlanmış yazım hataları

Yerel test sonuçları ve yayın sınırları: [doğrulama raporu](verification.md).

Bu paket dört yaş grubunda mevcut 4–5. seviyeleri günceller. Yeni seviye veya eğitim programı eklemez. Her hücrede beş, toplam 40 egzersize özel metin seçkisi vardır; 40 bağımsız hikâye değildir. Kaynak metinler ve başka egzersizlerin soruları değiştirilmez.

| Yaş | Seviye 4 | Seviye 5 |
|---|---|---|
| Çocuk 9–12 | 180–240 kelime; 4 hata; 240 sn | 240–300 kelime; 5 hata; 240 sn |
| Genç 13–16 | 240–320 kelime; 5 hata; 300 sn | 320–400 kelime; 6 hata; 360 sn |
| Genç yetişkin 17–21 | 300–380 kelime; 5 hata; 300 sn | 380–460 kelime; 6 hata; 300 sn |
| Yetişkin 22+ | 340–420 kelime; 5 hata; 360 sn | 420–500 kelime; 6 hata; 420 sn |

Süre ve hata sayıları korunur. Kelime aralıkları ürün başlangıç ayarlarıdır, bilimsel yaş normları değildir. Seviye 5 daha uzun metin ve en az iki harf sırası hatası kullanır. Önceden tanımlanan `pairs.json` çiftleri harf eksikliği, fazlalığı ve sırası hataları içerir; çalışma sırasında rastgele harf silinmez. Başlangıç çifti, konumu, tam kelime ve noktalama her metin için hazırlanır. İçerik editörünün yaşa uygunluk ve yazım çiftlerini ayrıca gözden geçirmesi önerilir.

Metin seçimi yalnız tanımlı metin kimlikleri, aynı yaş, aynı seviye ve uzunluk bandında yapılır. Az kullanılan metinler tercih edilir. Eksik çift, yetersiz hedef sayısı veya metin değişikliği sessizce ikame edilmez. Yeni kayıtlı oturumda öğrenciye hatasız özgün metin ve hedef anahtarı verilmez. Önceden başlamış oturumların snapshotları ve kullanıcı sonuçları değiştirilmez.

Puanlama korunur: bulunan hedef yüzdesi eksi yanlış seçim başına 5 puan (toplam ceza en fazla 30). İpucu kullanımı ayrı kaydedilir. Okuma hızı ve anlama ölçümü değildir.

## Geçiş

Bu çalışmada canlıya uygulanmadı. Yalnız Hızlı Okuma DB'si için doğrulanmış yedek ve kontrollü API durdurması gerekir. Önce sekiz kaydın `settings.json` içindeki eski ayarlarla eşleştiği doğrulanır. `psql -v ON_ERROR_STOP=1 -f apply.sql` atomik ve tekrar çalıştırılabilir; ayar sapması veya metin kimliği çakışmasında işlem geri alınır. Yalnız yeni 40 metin eklenir; eski içerik ve geçmiş silinmez. API ve frontend birlikte yayımlanmalıdır. Geri dönüş, API durdurularak doğrulanmış yayın öncesi DB yedeği ve önceki uygulama sürümüyle yapılır.

Yeniden üretim: `node content-packs/error-analysis/v1/generate.mjs`. Önceki ayar tabanı `settings.json` içinde korunur.
