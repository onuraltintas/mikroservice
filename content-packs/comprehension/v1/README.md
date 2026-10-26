# Anlama çalışma/değerlendirme içerik paketi — v1

`settings.json`, `content.json` ve `apply.sql` birlikte uygulanır: 40 normal egzersizin yaş/seviye ve çalışma/değerlendirme ayarları, egzersiz başına iki metin kaydı ve toplam 588 açıklamalı soru. Dört ayrı seviye tespit egzersizi bu paketin kapsamı dışındadır. Yayın durumu doğrulama raporunda ayrıca belirtilir; dosyaların bulunması canlıya uygulanmış olduğu anlamına gelmez.

Her yaş/seviyedeki ilk mevcut egzersiz `learning`, ikincisi `evaluation` olarak eşlenmiştir. Mevcut kimlikleri korunur; yeni adlarda amaç açıkça belirtilir. `readingPurpose=evaluation`, her iki modda soruların yüklenmesini sağlar; `comprehensionMode` cevap gösterimini ve rapor ayrımını belirler. Bunlar iki farklı kavramdır; çalışma modunu `readingPurpose=practice` yapmayın, sorular kaldırılır.

## Pilot parametreler

Değerler akademik yaş normu veya standardize sınav değildir. Her hücre **kelime aralığı / soru sayısı** gösterir.

| Yaş | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
|---|---|---|---|---|---|
| Çocuk, 9–12 | 180–250 / 5 | 220–300 / 6 | 280–360 / 6 | 340–430 / 7 | 400–500 / 8 |
| Genç, 13–16 | 300–400 / 6 | 350–450 / 6 | 400–550 / 7 | 500–650 / 8 | 600–750 / 8 |
| Genç yetişkin, 17–21 | 400–550 / 6 | 500–650 / 7 | 600–750 / 8 | 700–900 / 8 | 850–1050 / 10 |
| Yetişkin, 22+ | 450–600 / 6 | 550–700 / 7 | 650–850 / 8 | 800–1000 / 10 | 950–1200 / 10 |

Varsayılan okuma ve soru süre sınırı yoktur. Pacer ve oturum içi otomatik zorluk değişimi yoktur. Özel önizlemede kullanıcı bilerek okuma sınırı seçebilir; sınav simülasyonu ve seviye tespiti mevcut kurallarıyla ayrı kalır.

## Motor ve kayıt kuralları

- Çalışma: sorular sırasında metne dönülebilir, cevaptan sonra doğruluk ve açıklama gösterilir.
- Değerlendirme: sorular sırasında metin gösterilmez, doğruluk ve açıklamalar tamamlamadan sonra gösterilir. Sunucu, ilk yanıt ve tekrarında anahtarı gizler; tamamlanan normal oturumda yalnız o oturumun soruları ve cevapları açılır. Seviye tespiti cevap anahtarı açılmaz.
- Anlama ana puanı yalnız doğru cevap yüzdesidir; hızla yükselmez. Değerlendirmede geçerli ilk okuma WPM'si ayrı ölçülür. Çalışmada metne geri dönülebildiği için bağımsız WPM verilmez.
- Önizleme de sunucuyla aynı teknik hız geçerliliğini kullanır: 3 saniyeden kısa okumalar veya 20–1500 kelime/dakika aralığı dışındaki değerler hız olarak gösterilmez. Bu eşikler yaş normu değildir. Hızın geçersiz olması, cevaplardan hesaplanan anlama puanını geçersiz kılmaz. Sonuç ekranında ilk okuma süresi ve soruları yanıtlama süresi ayrı gösterilir.
- Çalışma sonucu egzersiz sonucu olarak, mod bilgisiyle saklanır. Bağımsız okuma geçmişine ve otomatik anlama profilinin ölçümlerine eklenmez. Günlük egzersiz ve genel faaliyet takibi ayrı mekanizmalardır; çalışma tamamlaması silinmez.
- Anlama motoru artık örnek/yedek metne düşmez. Gerçek metin ve gerektiğinde yeterli sayıda geçerli A–D sorusu yoksa anlaşılır hata verilir. Doğrudan motorun serbest okuma davranışı bu değişikliğin kapsamı dışındadır.
- Soru türleri 1=gerçek anlam, 2=çıkarım, 3=değerlendirmedir; hepsi korunur. Soru sırası editoryal sıralamadır. Bloom etiketlerinin anlamsal doğruluğu otomatik olarak kanıtlanmaz.

## Geçiş öncesi salt okunur canlı envanter — 10 Ekim 2026

`live-inventory-20261010.json` 40 egzersizi tek tek denetler. Ölçüt: aktif/silinmemiş, eşleşen yaş/seviye, egzersize bağlı veya ortak metin, hedef kelime aralığı; en az hedef sayıda A–D anahtarlı, dört dolu seçenekli, türü 1–3 ve Bloom düzeyi 1–6 olan soru.

| Yaş | S1 | S2 | S3 | S4 | S5 |
|---|---:|---:|---:|---:|---:|
| Çocuk | 0 | 0 | 3 | 9 | 0 |
| Genç | 7 | 0 | 14 | 0 | 4 |
| Genç yetişkin | 0 | 3 | 13 | 0 | 0 |
| Yetişkin | 5 | 3 | 2 | 0 | 0 |

Her iki modda sayılar aynı çıktı. **Geçiş öncesinde 20 yaş/seviye havuzunun 10'u boştu.** Yeni paket 11 Ekim 2026'da yayımlandı; 40 yaş/seviye/mod havuzunun her birinde iki yeni uygun metin doğrulandı. Güncel durum içerik ve canlı geçiş raporunda. Envanter sayısal uygunluk kontrolüdür; telif/kullanım yetkisi, soru kalitesi, açıklamaların doğruluğu ve pedagojik ilerleme kontrolü yerine geçmez.

## İçerik kaynağı ve sınırlar

80 veritabanı metin kaydı, **35 farklı metin gövdesi** içerir; 80 bağımsız hikâye değildir. Altı özgün kaynak hikâyenin tam paragraf sınırında biten seçkileridir. Çocuk/genç için projede bulunan dört hikâye yeniden kullanılır; tamir atölyesi ve ortak arşiv hikâyeleri bu paket için yazılmıştır. Genç yetişkin/yetişkin kaynakları, çalışma/değerlendirme metinleri ortaktır. Kaynak dosya ve SHA-256 manifestte yer alır; dolgu veya yarım paragraf yoktur.

89 kaynak soru farklı seçkilere eşlenerek 588 kayıt üretir. Her sorunun ilgili seçkide kanıtı, açıklaması ve dört farklı seçeneği vardır. Alt seviyeler ağırlıklı açık bilgi; ilerleyen seviyeler çıkarım ve değerlendirme içerir. Anahtarlar dengeli dağıtılır. Aynı hikâyeyi veya soruları daha önce gören kullanıcının değerlendirmesi bağımsız bir sınav formu sayılmaz. Bunlar test edilebilir pilot içeriklerdir; öğretmen editör incelemesi ve öğrenci pilotundan elde edilen güçlük/ayırt edicilik analizi akademik standardizasyon için ayrıca gerekir.

Eski metin ve sorular silinmez veya bu pakette yeniden yazılmaz. Seçim motoru yaş/seviye/uzunluk/soru uygunluğunu ve kullanım geçmişini kontrol eder; uygun eski ortak metinler de aday olabilir. Bu paket eski havuzların tümü için editoryal kalite onayı vermez.

## Üretim, doğrulama ve geri dönüş

1. `node content-packs/comprehension/v1/generate.mjs`: kaynaklardan deterministik manifest, uygulama ve geri dönüş SQL'i üretir.
2. `node --test content-packs/comprehension/v1/content.test.mjs`: içerik, kaynak, soru ve üretilen SQL kontrolleri.
3. `SpeedReadingComprehensionContentPostgresTests`: gerçek geçici PostgreSQL'de 40 akış; yeniden uygulama, uyuşmazlıkta geri alma, sonuç kaydı ve kapsamlı geri dönüş kontrolü.
4. `tests/E2E/speed-reading-comprehension.config.mjs`: gerçek Angular arayüzü, fixture API ile tüm yaş/seviye/modların masaüstü ve mobil akışları; canlı Identity testi değildir.
5. Canlı için önce güncel Hızlı Okuma yedeği, geri yüklenmiş geçici veritabanında prova ve diğer kayıtların bütünlük denetimi gerekir. Yalnız Hızlı Okuma API'sini durdur; `psql -X -v ON_ERROR_STOP=1` ile `apply.sql` uygula, doğrula, aynı API imajını tekrar başlat. Identity ve Koçluk veritabanına dokunma.

`apply.sql` atomik/idempotenttir; eski veya bu paketin beklenen yeni ayarı dışında bir yönetici değişikliği varsa durur. Kimlik çakışması veya ekleme hatası bütün işlemi geri alır. Yalnız 40 normal ayar ve 80 metin/588 soru hedeflenir; programlar, lisanslar, kullanıcılar, eski içerik ve açık oturum anlık görüntüleri korunur.

Gerekirse API durdurulup yeni yedek alındıktan sonra `rollback.sql` çalıştırılır. Yalnız bilinen ayarlar/adlar eski hâline gelir; yeni metinler pasife alınır, sorular ve kullanıcı sonuçları **silinmez**. Yönetici hedef ayarı/metni sonradan değiştirmişse otomatik geri dönüş durur. Geri dönüş de tekrar uygulanabilir; arşivlenmiş paketi `apply.sql` ile körlemesine yeniden açmak reddedilir. Yeniden yayın ayrı bir inceleme gerektirir. Kullanıcı yazıları başladıktan sonra tüm veritabanını eski yedekle değiştirmeyin.

Motor geliştirme raporu: Anlama modları — 11 Ekim 2026. İçerik testleri ve canlı yayın: Anlama içerik paketi — 11 Ekim 2026.
