# N-back normal eğitim ayarları — v1

Bu paket dört yaş grubunun beşer normal `Focus` kaydını günceller. Seviye tespiti, eğitim atamaları ve geçmiş sonuçlar değiştirilmez. Her hücre `N / uyaran aralığı (ms) / uyaran sayısı / süre (sn)` biçimindedir.

| Yaş grubu | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
| --- | --- | --- | --- | --- | --- |
| Çocuk (9–12) | 1 / 3000 / 32 / 96 | 1 / 2800 / 40 / 112 | 1 / 2600 / 48 / 124,8 | 2 / 3000 / 48 / 144 | 2 / 2800 / 56 / 156,8 |
| Genç (13–16) | 1 / 2800 / 40 / 112 | 1 / 2500 / 48 / 120 | 2 / 2800 / 48 / 134,4 | 2 / 2500 / 56 / 140 | 3 / 3000 / 56 / 168 |
| Genç yetişkin (17–21) | 1 / 2600 / 48 / 124,8 | 2 / 2900 / 48 / 139,2 | 2 / 2600 / 56 / 145,6 | 3 / 3000 / 56 / 168 | 3 / 2700 / 64 / 172,8 |
| Yetişkin (22+) | 1 / 2700 / 48 / 129,6 | 2 / 3000 / 48 / 144 | 2 / 2700 / 56 / 151,2 | 3 / 3100 / 56 / 173,6 | 3 / 2800 / 64 / 179,2 |

Normal eğitim konum modunda, 3×3 tabloda çalışır. N oturum boyunca sabittir. 1-back'te aynı konum en fazla iki kez art arda, arada görünür boşlukla gösterilir. Daha yüksek N'de ardışık aynı konum üretilmez.

## Kısa alıştırma

Öğrenci normal N>1 egzersizini başlatırken önce sekiz uyaranlık, puanlanmayan alıştırma çalışır. Ardından hazır ekranına dönülür; öğrenci asıl egzersizi başlatır. Bu kontrol her yeni oynatıcı oturumunda uygulanır, hesapta kalıcı bir öğrenme durumu tutulmaz. Önizlemede alıştırma isteğe bağlıdır.

Önceki N ile alıştırma seçeneği atanan programı değiştirmez ve mevcut N alıştırmasının yerine geçmez. Alıştırma sonuç kaydetmez, puanlanan oturumun saatini başlatmaz ve günlük ilerlemeyi tamamlamaz. Normal sayfa açılışındaki mevcut oturum hazırlama çağrısı korunur; alıştırma ayrı sunucu oturumu oluşturmaz.

## Süreler

Tablodaki süre uyaran sayısı × uyaran aralığıdır; kısa alıştırma ve kullanıcı duraklamaları dahil değildir. Bunlar ürün ayarlarıdır, doğrulanmış bilimsel normlar veya okuma hızı ölçümleri değildir.

Günlük API, Focus için bu süreyi yukarı yuvarlayarak dakika tahmini üretir. Diğer egzersizlerin mevcut beş dakikalık tahmini değişmez. Eski veya hatalı Focus yapılandırmasında da mevcut beş dakikalık tahmin korunur; günlük liste bozulmaz.

`duration-impact.json`, 20 programın 560 gününde tekrarlı Focus görevlerini ayrı ayrı sayar. Dosyanın günlük toplamları diğer egzersizlerin gerçek sürelerini garanti etmez.

## Üretme ve doğrulama

Depo kökünden:

```powershell
node content-packs/focus/v1/generate.mjs
node --test tests/E2E/focus-catalog.test.mjs
node --test tests/E2E/focus-catalog.postgres.test.mjs
```

PostgreSQL testleri geçici Docker veritabanı kullanır. Tarayıcı kontrolleri için `tests/E2E` dizininde:

```powershell
npx playwright test --config=speed-reading-focus.config.mjs
```

## Kontrollü veritabanı geçişi

Canlı geçiş bu geliştirme kapsamında yapılmadı. Ayrı yayın onayı, Hızlı Okuma veritabanı yedeği ve geri dönüş hazırlığı gerekir. Geçişten önce aktif Focus oturumlarını bitirin veya yeni oturum açılmasını durdurun; eski oturumların yapılandırmasını ortasında değiştirmeyin.

`apply.sql` tek transaction içinde yalnız hedef 20 kaydın üç ayarını (`nLevel`, `speedMs`, `totalSteps`) değiştirir. Beklenen eski/yeni yapılandırma dışında sapma, eksik kayıt veya mükerrer aktif kayıt varsa işlem iptal edilir. İkinci çalıştırma değişiklik ve yeni audit sürümü üretmez. Kullanıcı geçmişi silinmez.
