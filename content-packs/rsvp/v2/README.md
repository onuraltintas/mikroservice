# RSVP v2 — başlangıç sabitlemesi ve kontrollü içerik

Bu paket yalnız Hızlı Okuma'nın 11 mevcut RSVP egzersizini günceller. Yeni yaş/seviye, eğitim programı veya tablo oluşturmaz. Çocuk 4–5; diğer yaşlar 3–5. seviyelerde kalır. Otomatik tempo uyarlaması kapalıdır. Gösterim temposu, bağımsız ölçülmüş okuma hızı değildir; sonuçta `RawWPM` ve `WordsRead` boş kalır. Anlama puanı sunucunun değerlendirdiği beş cevaptan hesaplanır.

## Parametreler

Her yeni oturumun başında bir kez 300 ms `+` gösterilir. Kelimeler tek tek sabit tempoda sunulur; kelimeler arası ek boşluk 0 ms'dir. Aşağıdaki süreler yalnız sunumdur; soru çözümü dahil değildir. Değerler ürün başlangıç ayarlarıdır, bilimsel yaş normu veya başarı garantisi değildir.

| Yaş | Seviye | Metin kelime bandı | Kelime gösterimi | Yaklaşık akış temposu | Sunum süresi |
| --- | --- | --- | --- | --- | --- |
| Çocuk | 4 | 180–240 | 500 ms | 120 kelime/dk | 90–120 sn |
| Çocuk | 5 | 240–300 | 429 ms | 140 kelime/dk | 103–129 sn |
| Genç | 3 | 360–440 | 286 ms | 210 kelime/dk | 103–126 sn |
| Genç | 4 | 450–540 | 250 ms | 240 kelime/dk | 113–135 sn |
| Genç | 5 | 560–680 | 222 ms | 270 kelime/dk | 125–151 sn |
| Genç yetişkin | 3 | 580–640 | 250 ms | 240 kelime/dk | 145–160 sn |
| Genç yetişkin | 4 | 680–750 | 222 ms | 270 kelime/dk | 151–167 sn |
| Genç yetişkin | 5 | 770–850 | 200 ms | 300 kelime/dk | 154–170 sn |
| Yetişkin | 3 | 480–540 | 231 ms | 260 kelime/dk | 111–125 sn |
| Yetişkin | 4 | 580–650 | 207 ms | 290 kelime/dk | 120–135 sn |
| Yetişkin | 5 | 690–800 | 188 ms | 319 kelime/dk | 130–151 sn |

## İçerik ve seçim

- `content.json`: 22 metin seçkisi, her hücrede iki farklı tam metin ve her metinde beş soruyla toplam 110 soru. Kaynak, projenin `comprehension/v1/content.json` paketidir; kaynak metinler kesilmez ve kaynak paket değişmez. `sourceTextId` izlenebilirliği, `evidence` metin-soru eşleşmesinin otomatik kontrolünü sağlar.
- Bu 22 kayıt **22 bağımsız yeni hikâye değildir**: mevcut konu/hikâyelerin yaşa ve uzunluğa uygun sürümleri seçilmiştir. İçerik havuzu sonradan genişletilebilir.
- Kayıtlı ayarlarda seçim aktif, silinmemiş, doğru seviye, uygun yaş/ortak yaş ve egzersize bağlı/ortak metinler içinden kelime bandıyla yapılır. Bu paketin metinleri egzersize ve yaşa bağlıdır. Kullanım geçmişi daha az kullanılan metne öncelik verir; sonraki oturum farklı seçkiyi kullanır.
- Özel önizlemede aynı yaş/seviye ve kelime bandı korunur; kullanıcı gösterim süresini ve aralığı değiştirir. Diyalog tahmini süreyi yeniden hesaplar. Önizleme sonuç ve ilerleme kaydetmez.
- Yeterli geçerli soru veya uygun metin yoksa oturum başlamaz; demo/yedek metinle başarılı görünmez. Cevap anahtarı kayıtlı oturumun başlangıç yanıtına verilmez.

## Oturum uyumluluğu

Yeni oturumlar `rsvpProtocolVersion=2` kullanır: toplam sunum süresi `300 + kelimeSayısı × gösterim + (kelimeSayısı − 1) × aralık`. Başlangıç sabitlemesi kapalı özel ayarda 300 yerine 0 kullanılır. Kısmi tamamlanmada başlangıç sabitlemesi kelime sayımına dahil edilmez.

Mevcut v1/v2 snapshot'lar, metin kimliği açıkça verilse ve güncel katalog bandı değişse bile korunur. V1 tekrarlanan sabitleme davranışını kendi snapshot'ıyla sürdürür. Geçmiş sonuçlar silinmez. Protokolü olmayan eski oturumlar mevcut güvenli yeniden başlatma akışıyla ele alınır.

## Üretim geçişi — bu çalışma henüz yayımlanmadı

1. İlgili servis/ön yüz sürümünü ve Hızlı Okuma veritabanı yedeğini hazırlayın; geri yükleme prosedürünü doğrulayın.
2. Hızlı Okuma API'sinde yeni oturum/aktif içerik yazımlarını durdurun; Identity/Koçluk veritabanlarına dokunmayın.
3. `apply.sql` dosyasını yalnız doğrulanmış Hızlı Okuma veritabanında `psql -v ON_ERROR_STOP=1` ile uygulayın. Dosya transaction, kilit, baseline ve içerik çakışma kontrolleri içerir; beklenmeyen katalog değişikliğinde tümü geri alınır. Bilinmeyen canlı farkı zorla aşmayın.
4. 11 ayar, 22 metin ve 110 soruyu doğrulayın. Aynı SQL ikinci çalıştırmada değişiklik yapmaz. Kullanıcı sonuçları, açık oturum snapshot'ları, eğitim programları, seviye tespiti ve eski içerikler korunur.
5. API ve ön yüzü uyumlu sürümle açın. Yetkili oturumda içerik seçimi, başlangıç işareti, soru ve sonuç ekranını kontrol edin.
6. Hata halinde yeni yazımları durdurup hazırlanmış sürüm/veritabanı geri dönüş prosedürünü uygulayın; kullanıcı geçmişini rastgele silmeyin.

Yerel üretim:

```powershell
node content-packs/rsvp/v2/generate.mjs
node --test tests/E2E/rsvp-catalog.test.mjs
node --test tests/E2E/rsvp-catalog.postgres.test.mjs
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --filter "FullyQualifiedName~SpeedReadingRsvp"
```

PostgreSQL testleri yalnız geçici Docker/Testcontainers veritabanlarını kullanır. GitHub gönderimi ve canlı SQL uygulaması bu düzeltme kapsamında yapılmamıştır.
