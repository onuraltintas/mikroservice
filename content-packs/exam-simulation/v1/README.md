# Sınav Simülasyonu v1 — süreli paragraf testi

Bu paket mevcut üç normal Sınav Simülasyonu egzersizinin ayarlarını günceller. Çocuk grubu veya yeni seviyeler eklenmez. Tek metin üzerinde süreli anlama soruları sunar; tam LGS/TYT/AYT denemesi veya standardize edilmiş bir sınav değildir. Parametreler mevcut içerikle uyumlu pilot ürün ayarlarıdır, bilimsel yaş normları değildir.

| Yaş grubu | Seviye | Metin uzunluğu | İlk okuma üst sınırı | Soru sayısı | Soru başına süre | Azami toplam etkin süre |
| --- | --- | --- | --- | --- | --- | --- |
| Genç (13–16) | 5 | 540–650 kelime | 180 sn | 8 | 48 sn | 9 dk 24 sn |
| Genç yetişkin (17–21) | 5 | 430–650 kelime | 180 sn | 8 | 60 sn | 11 dk |
| Yetişkin (22+) | 5 | 480–700 kelime | 240 sn | 8 | 60 sn | 12 dk |

İlk okuma en az 10 saniyedir. Kullanıcı minimum süre geçtikten sonra üst sınırı beklemeden bitirebilir; üst sınırda sorular açılır. Sorular sırasında paragraf görülebilir. Bu nedenle bağımsız okuma hızı veya hafıza ölçümü iddiasında bulunulmaz. Sonuç doğru cevap oranıdır; yanlış cevap cezası/net puan hesabı yoktur. WPM boş tutulur. Sunucu okuma ve soru sürelerini doğrular; aradaki bekleme toplam etkin süreye katılmaz. Sınav duraklatılamaz. Cevap anahtarı ve doğruluk yalnız tamamlanan sonuçta açılır.

## İçerik ve önizleme

Metin Hızlı Okuma veritabanından seçilir: aktif/yayımlanmış, yaş grubuyla uyumlu, seviye 5, egzersize bağlı veya genel, kelime aralığı içinde ve en az sekiz geçerli dört seçenekli sorusu olmalı. Normal oturumlarda az kullanılan metinler önceliklidir. Uygun içerik yoksa başlatılmaz; örnek metinle gerçek sonuç üretilmez.

Admin/öğretmen önizlemesi de gerçek katalog içeriği kullanır, kayıt oluşturmaz. Özel ayarlar ilk okuma üst sınırı, soru başına süre, yazı boyutu ve satır aralığını geçici değiştirir; yaş/seviye/içerik bandı değiştirilmez. Önizleme istemci değerlendirmesi olduğundan güvenli ölçme sınavı olarak kullanılmaz.

## Üretme ve doğrulama

Depo kökünde:

```powershell
node content-packs/exam-simulation/v1/generate.mjs
node --test content-packs/exam-simulation/v1/settings.test.mjs
dotnet test tests/Unit/SpeedReading.Application.UnitTests/SpeedReading.Application.UnitTests.csproj --filter "FullyQualifiedName~Exam"
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --filter "FullyQualifiedName~SpeedReadingExam"
```

PostgreSQL testleri Docker/Testcontainers üzerinde geçici test veritabanında çalışır; canlı DB kullanılmaz.

## Kontrollü yayın ve geri dönüş

Bu turnün sonunda paket canlıya uygulanmamıştır. Yayın için ayrıca onay gerekir.

1. Yalnız Hızlı Okuma DB yedeği al; geri yüklenebilirliğini kontrol et.
2. Mevcut üç katalog satırının `previousConfiguration` veya yeni `configuration` ile aynı olduğunu, içerik koşullarını ve hedef DB adını doğrula. Kimlikler `settings.json` içindedir.
3. Hızlı Okuma API’sini kısa süre durdur; yeni API ve frontend’i birlikte yayımla; `apply.sql` dosyasını `psql -X -v ON_ERROR_STOP=1` ile yalnız Hızlı Okuma DB üzerinde çalıştır.
4. API’yi aç; sağlık, öğrenci oturumu, admin/öğretmen önizlemesi ve kayıt sonucunu doğrula.
5. Geri dönüşte API’yi durdurup `rollback.sql` uygula; önceki kodu geri yükle; yeniden sağlık kontrolü yap.

SQL tek işlemde çalışır ve katalog/içerik tablolarını kilitler. Beklenmedik katalog değişikliği veya yaş başına üçten az uygun metin varsa tüm işlem iptal olur. Tekrar uygulama ayarları/version’ı yeniden değiştirmez. Yalnız üç `exercises.configuration_json` alanı ve güncelleme/version metadatası değişir. Metinler, sorular, program şablonları, kullanıcı geçmişi ve seviye tespiti değiştirilmez. Geri dönüş de geçmişi silmez. Eski oturumlar kendi ayar snapshot’larıyla korunur.
