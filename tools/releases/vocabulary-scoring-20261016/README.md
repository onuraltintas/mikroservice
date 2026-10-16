# Kelime puanlama düzeltmesi — 16 Ekim 2026

Bu paket yalnız Hızlı Okuma API'sindeki `SpeedReading.Infrastructure.dll` ve PDB dosyasını, mevcut canlı API imajının üzerine ekler. Öğrenci, admin, kimlik ve diğer servis imajları korunur. Migration veya geçmiş puanları yeniden hesaplayan işlem yoktur.

## Kaynak ve paket

Çalışma ağacında ilgisiz değişiklikler vardır. `.tmp/day-advance-20261015/backend` canlı kaynak tabanından `.tmp/vocabulary-scoring-20261016/backend` oluşturuldu; üretim farkı yalnız `OwnedSpeedReadingExerciseSessions.cs` içindeki iki düzeltmedir:

- Aynı işlemde eklenen XP kaydını `UserGamifications.Local` üzerinden yeniden kullanmak.
- Sabit kelime kimliklerinin sorgusunda PostgreSQL/EF ile uyumlu `Enumerable.Contains` kullanmak.

Runtime kaynak revizyonu: `e354768142b922f3f0dbe322a3b868e24d8ea0bc`. Derleme API ve doğrulayıcı için aynı `SourceRevisionId` ile yapılır. Yalnız Infrastructure DLL/PDB, `payload/speed-api` dizinine alınır; doğrulama aracı publish çıktısı `payload/verification` dizinine alınır. İki Infrastructure DLL hash'i eşit olmalıdır. Manifest yolları POSIX ve satır sonları LF olmalıdır.

## Kontroller ve sıra

`deploy.sh` yalnız `/var/lib/eduivme/releases/vocabulary-scoring-20261016` konumunda, iki mevcut yayın kilidi alınarak çalışır:

1. `prepare`: mevcut API imajını tam kimliğiyle doğrular; servis imajlarını ve mevcut API ortamını özel izinli dosyalarda dondurur. Parolalar çıktıya yazılmaz.
2. `build`: ağ erişimi olmadan mevcut API üzerine yalnız iki dosya ekler; revizyon ve DLL hash'ini doğrular.
3. `drill`: üretim yedeğini yalnız `vocabulary_scoring_drill_20261016` veritabanına açar. Gerçek aday imajındaki DLL'lerle dört kelime senaryosunu çalıştırır. Yeni ve mevcut puan, öğrenme/test modu, farklı DbContext'lerden tekrar tamamlama sınanır. Mevcut bütün satırların tam JSON hash'i ve çokluğu korunmalıdır. Geçici veritabanı başarıda ve hatada kaldırılır.
4. `promote`: geri dönüş koruması kurulur, API yazıcısı durdurulur, güncel yedek alınır. Verifier üretimde yalnız metadata ve SELECT çalıştırır; bekleyen migration/model farkı veya çift puan kaydı varsa durur. Aday konteyner `up --no-start --no-deps` ile oluşturulur. İkinci veri karşılaştırması yine yazıcı kapalıyken yapılır; sonra `up -d --no-deps --no-recreate` ile açılır. Sağlık ve diğer konteyner kimlikleri doğrulanır.
5. `validate`: tam aday imajı, readiness, restart sayısı ve diğer servis kimliklerini doğrular. Sonrasında en az 15 dakika readiness/yeni hata izlemesi ve gerçek öğrenci arayüzü testi yapılır.

Üretimde `--production-read-only` dışında verifier çalıştırılmaz. Mutasyon içeren senaryolar yalnız adı sabitlenmiş klon veritabanında çalışır. Verifier `Migrate`, backfill, puan silme veya yeniden hesaplama içermez.

## Geri dönüş

Promote adımı hata verirse önceden dondurulan compose dosyasıyla yalnız API geri açılır; eski tam imaj kimliği ve readiness doğrulanır. Veritabanı değişmediğinden şema veya veri geri yükleme gerekmez. Yedekler, bağlantı ayarları, tam hash dökümleri ve ilk deneme kayıtları sunucudaki özel yayın dizininde tutulur.

İlk canlı denemede `compose start` bağımlılık grafiğindeki bulunmayan `identity-migrations` nedeniyle durdu; koruma eski API'yi doğrulanmış biçimde açtı. Başlatma komutu yukarıdaki `up -d --no-deps --no-recreate` olarak düzeltildi. Aynı aday DLL ve başarılı klon provası korunarak tekrar yayınlandı; ilk deneme kayıtları `first-attempt/` altında saklandı.

Sonuçlar ve kanıtlar: [Yayın raporu](../../../docs/reports/speed-reading-vocabulary-scoring-20261016.md).
