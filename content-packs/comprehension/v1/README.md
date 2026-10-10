# Anlama çalışma/değerlendirme ayarları — v1 taslağı

**Henüz uygulanabilir/yayımlanmış içerik paketi değildir.** `settings.json` 40 normal egzersizin önerilen ayarlarını içerir. Canlı ayarlar, mevcut metinler, sorular, kullanıcılar ve sonuçlar değiştirilmedi. Dört ayrı seviye tespit egzersizi bu paketin kapsamı dışındadır.

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

## Salt okunur canlı envanter

`live-inventory-20261010.json` 40 egzersizi tek tek denetler. Ölçüt: aktif/silinmemiş, eşleşen yaş/seviye, egzersize bağlı veya ortak metin, hedef kelime aralığı; en az hedef sayıda A–D anahtarlı, dört dolu seçenekli, türü 1–3 ve Bloom düzeyi 1–6 olan soru.

| Yaş | S1 | S2 | S3 | S4 | S5 |
|---|---:|---:|---:|---:|---:|
| Çocuk | 0 | 0 | 3 | 9 | 0 |
| Genç | 7 | 0 | 14 | 0 | 4 |
| Genç yetişkin | 0 | 3 | 13 | 0 | 0 |
| Yetişkin | 5 | 3 | 2 | 0 | 0 |

Her iki modda sayılar aynı çıktı. **20 yaş/seviye havuzunun 10'u boş.** Bu yalnız sayısal uygunluk kontrolüdür; telif/kullanım yetkisi, soru kalitesi, açıklamaların doğruluğu ve pedagojik ilerleme kontrolü yerine geçmez. Canlı havuzlar daha sonra değişebilir; yayın öncesi yeniden denetlenmelidir.

## Kalan içerik/geçiş adımları

1. Boş 10 havuza yaş/seviye/bandına uygun, birbiriyle farklı özgün metinler ve hedef sayıda açıklamalı sorular ekle. Her modun seçiminde en az iki uygun metin hedefle. Mevcut metinleri sırf aralığa uydurmak için kısaltma/uzatma.
2. Dolu havuzları da içerik açısından gözden geçir: S1 açık bilgi, S2 ana fikir/sıra/neden, S3 çıkarım, S4 kanıt/karşılaştırma, S5 bütünleştirme/değerlendirme. Üst seviyelerde de kolay sorular bulunsun; tek doğru seçenek ve metin içi kanıt gerekir. Eski soru etiketlerini bilimsel doğrulama gibi sunma.
3. Kanonik metin/soru manifesti ve atomik, idempotent uygulama SQL'i hazırla. 40 eski ayarın/kimliğin eşleşmesi, içerik sayıları, kaynak/dosya bütünlüğü, diğer egzersizlere dokunulmaması ve geri dönüş denetlenmeli.
4. Paketi gerçek geçici PostgreSQL'de tüm 40 egzersiz için uygula; yeniden uygulama, uyuşmazlıkta tam geri alma ve yaş/seviye/metin/soru kontrollerini test et.
5. Ancak bundan sonra ayrıca onayla yedekli canlı yayın/geçiş ve gerçek kullanıcı oturum kontrolü yap. `settings.json` tek başına canlı veritabanına aktarılmamalıdır.

Bu klasörde henüz uygulanabilir `apply.sql` veya tamamlanmış metin/soru manifesti yoktur. Sayısal ayar testi, içeriğin tamamlandığı anlamına gelmez.

Yerel geliştirme ve doğrulama raporu: [Anlama modları — 11 Ekim 2026](../../../docs/reports/comprehension-modes-20261011.md).
