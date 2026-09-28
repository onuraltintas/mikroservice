# Hızlı Okuma CMS İçerik ve Görsel Çalışması

Durum: İçerik taslağı ve görsel yerel geliştirme ortamındadır. CMS'e kaydedilmedi, yayımlanmadı ve canlıya alınmadı. Canlı CMS yalnızca public GET/HEAD istekleriyle incelendi; canlıya yazma yapılmadı.

## Önizleme hatası ve mevcut CMS durumu

13:05 civarında görülen `500`, yerel CMS API'si çalışmadığı için tarayıcının API isteğini tamamlayamamasından kaynaklanıyordu. Yerel Speed Reading API'si ve frontend proxy'si şimdi çalışıyor; sağlık uç noktası `200 Healthy` dönüyor. Doğru public CMS uçları `/api/speed-reading/cms/...` altındadır.

Önizleme için kullanılan yerel Speed Reading veritabanı `speedreading_owned_db` boş: yayımlanmış blog yazısı ve ana sayfa CMS bloğu yok. Canlı CMS'in durumu farklıdır; aşağıdaki envanter canlı public CMS API'sinden 2026-09-28 tarihinde salt-okunur şekilde alındı.

## Canlı CMS envanteri — salt okunur kontrol

- `HomePage` grubunda 23 CMS bloğu mevcut.
- Canlı CMS API'si 3 yayımlanmış blog yazısı döndürüyor.
- Bu üç yazının `coverImageUrl` adresleri canlıda `404` döndürüyor; başlıklar/metinler mevcut olsa da kapak görselleri yüklenmiyor.
- Mevcut ana sayfa bloklarında “Okuma Hızınızı 3 Katına Çıkarın” başlığı ve “30 gün içinde ... ikiye katlayın” CTA metni bulunuyor.
- CMS ayrıca `10000` kullanıcı, `185` gelişim, `96` memnuniyet ve `250000` egzersiz değerlerini döndürüyor. Bu değerler için aynı CMS kaydında kaynak veya doğrulama bilgisi görünmüyor; kamuya açık gösterilmeden önce dayanakları doğrulanmalı.
- Üç yayımlanmış yazı: “Başarı Hikayesi: 200'den 600 WPM'e Yolculuk”, “5 Etkili Hızlı Okuma Egzersizi” ve “Hızlı Okuma Nedir ve Nasıl Öğrenilir?”. Başarı hikâyesi, ölçüm belgeleri ve yayımlama izni olmadan gerçek kullanıcı sonucu gibi sunulmamalı.

Canlıdaki CMS `home_page_config` yerine 23 ayrı eski anahtar döndürüyor. Yerel frontend parser'ına bu alanlar için geriye uyumlu eşleme eklendi; canlıya henüz değişiklik yapılmadı.

## Eski CMS alanlarının yeni ana sayfaya eşlemesi

| Canlıdaki eski alan | Yeni sayfa karşılığı | Uygulama kararı |
| --- | --- | --- |
| `hero_title`, `hero_subtitle`, `hero_cta_text`, `hero_cta_link` | Hero başlığı, açıklaması ve kayıt CTA'sı | İçerik güvenlik filtresinden geçer; CTA yalnızca uygulama içi yol kabul eder. |
| `features_title`, `features_subtitle`, `features_list` | Özellikler başlığı, açıklaması ve kartları | JSON biçimi, metinler ve ikon adları doğrulanır. |
| `pricing_title`, `pricing_subtitle` | Fiyatlandırma bölüm başlığı | Yalnız başlık/açıklama eşlenir; eski `pricing_plans` gösterilmez. Planlar güncel abonelik API'sinden gelir. |
| `faq_title`, `faq_items` (varsa `faq_list`) | SSS başlığı ve soru-cevapları | Filtrelenmiş içerik aynı sayfa modelinden beslenir; bölüm için yinelenen CMS isteği kaldırıldı. |
| `testimonials_title`, `testimonials_subtitle`, `testimonials_list` | Katılımcı deneyimleri | Güvensiz/iddialı metinleri geçen kayıtlar gösterilir; yalnız yayımlanmış CMS girdileri kullanılır. |
| `cta_title`, `cta_description`, `cta_button_text`, `cta_button_link` | Alt çağrı alanı ve kayıt CTA'sı | CTA yalnızca uygulama içi yola yönlenir. |
| `stats_users`, `stats_improvement`, `stats_satisfaction`, `stats_exercises` | Eşlenmez | Kaynak/doğrulama bilgisi olmadığı için sayaçlara aktarılmaz. İstatistik bölümü `EvidenceMetrics` kaynağını kullanmayı sürdürür. |

`home_page_config` yayımlanmışsa yeni yapılandırmadaki alanlar önceliklidir; boş bırakılan alanlar güvenli legacy değeriyle tamamlanır. JSON bozuksa legacy eşleme, legacy alan da bozuk/güvensizse güvenli varsayılan içerik kullanılır. Blog verisi kendi CMS API'sinden, fiyat planları abonelik API'sinden gelmeye devam eder. Böylece eski içerik kaybolmadan veri kaynağı çakışmaları önlenir.

## SSS ve yorumların yönetim ekranı

Hızlı Okuma admin CMS'inde SSS soruları ve katılımcı yorumları artık Ana sayfa formunda ayrı, yapılandırılmış listeler olarak düzenlenir; soru/yanıt, kategori, ad/rol, puan ve bölüm görünürlüğü yönetilebilir. Ham “Landing blokları” sekmesi admin arayüzünden kaldırıldı. Eski veritabanı blokları silinmedi: Ana sayfa ilk açıldığında mevcut `home_page_config` alanları öncelikle, eksik alanlar legacy bloklar ikinci olarak forma taşınır; kaydetme yeni yapılandırmayı `home_page_config` içine yazar. Eski kayıtları saklamak geri dönüş olanağını korur ve legacy okuma uyumluluğu sürer. Yeni yapılandırmada SSS veya yorum `items: []` ise bu, listeyi bilerek boşaltma olarak kabul edilir; eski listedeki satırlar yeniden görünmez.

Bu yönetim ekranı ve parser değişiklikleri yerel kaynak kodundadır; canlıya dağıtım ve canlı CMS'e yazma yapılmadı.

Doğrulama (2026-09-28): ana sayfa parser testleri 11/11, tüm Hızlı Okuma frontend testleri 359/359 ve admin panel testleri 292/292 geçti; her iki uygulamanın `npm run build` derlemesi başarılı. Derlemelerde bu değişiklikten bağımsız SCSS bütçe uyarıları sürüyor: Hızlı Okuma egzersiz oynatıcı stili 128.02 KB / 120 KB, admin katalog stili 7.31 KB / 4 KB. Canlı CMS'e yazma veya canlıya dağıtım yapılmadı.

## İçerik ilkesi

- Okuma hızı ile anlama sonucu ayrı ayrı ve birlikte izlenmeli; tek başına WPM başarı ölçütü gibi sunulmamalı.
- Platformun henüz kendi kullanıcı verisiyle doğrulanmamış etkileri vaat edilmemeli. “3 kat”, “garanti”, belirli bir sürede kesin kazanım gibi ifadeler kullanılmamalı.
- Bir araştırmanın katılımcı grubu ve kısıtları belirtilmeli; küçük bir çalışma tüm kullanıcılar veya bu platform için kanıt sayılmamalı.
- İçerik; okuma amacı, metin türü, zorluk ve ölçüm koşullarını dikkate almalı.

Bu dil tercihi, küçük örneklemli 2023 deneysel çalışmanın hızda sınırlı farklar bulmasına rağmen büyük reklam vaatlerini doğrulamaması ve araştırmacıların daha fazla/uzun dönem çalışmaya ihtiyaç belirtmesiyle uyumludur. [Klimovich, Tiffin-Richards & Richter (2023)](https://doi.org/10.1111/1467-9817.12417). National Reading Panel'in akıcılık bölümü çocuklarda rehberli/tekrarlı sesli okuma araştırmasını ele alır; bu bulgular yetişkinlere veya bu uygulamaya doğrudan genellenmemelidir. [NICHD, National Reading Panel (2000)](https://www.nichd.nih.gov/publications/pubs/nrp/report).

## Ana sayfa için yerel varsayılan metin

Ana sayfa varsayılanları, hız ve anlamayı “artırmayı” vaat etmek yerine platformun ölçebildiği sonuçları anlatacak şekilde güncellendi:

- Başlık: **Hız ve anlamayı birlikte takip edin**
- Açıklama: **Başlangıç ölçümünüzü yapın; okuma sürenizi ve anlama yanıtlarınızı ayrı ayrı izleyin.**
- CTA: **Başla**
- Sonuç notu: **Sonuçlar metin türüne, zorluğuna ve çalışma koşullarına göre değişebilir.**

CMS'te `home_page_config` yayımlanırsa yeni yapılandırma eşlenen legacy alanların önüne geçer. Hem JSON hem de legacy metinler için doğrulanmamış yüzde/WPM/kat ve başarı vaatleri filtrelenir; blog yazıları ise bu ana sayfa filtresinden ayrı olduğundan yayın öncesi editöryel inceleme gerektirir.

## İlk blog yazısı — CMS taslağı

- Başlık: **Okuma hızını nasıl takip etmeli? Anlama neden ölçümün parçası olmalı?**
- Slug: `okuma-hizini-anlama-ile-birlikte-takip-etmek`
- Özet: **Okuma hızını tek başına yorumlamak yanıltıcı olabilir. Okuma süresi, anlama yanıtları ve metin koşullarını birlikte değerlendirmenin pratik yollarını inceleyin.**
- Etiketler: `Okuma`, `Anlama`, `Ölçüm`, `Araştırma`
- Yazar: Yayın öncesinde gerçek yazar/editör adı girilmeli.
- Yayın: Taslak (`isPublished: false`)
- Kapak: `/assets/images/share-image-v2.png`
- SEO başlığı: **Okuma hızını ve anlamayı birlikte takip edin**
- SEO açıklaması: **WPM tek başına yeterli değildir. Okuma süresi, anlama yanıtları ve metin koşullarını birlikte değerlendirin.**
- Kaynak URL: `https://doi.org/10.1111/1467-9817.12417`

CMS `content` alanına aktarılabilecek HTML taslağı:

```html
<p>Okuma hızınızı takip etmek yararlı olabilir; ancak tek başına kaç kelime okuduğunuz, metni ne kadar anladığınızı göstermez. Daha anlamlı bir değerlendirme için okuma süresine, anlama yanıtlarına ve okuduğunuz metnin özelliklerine birlikte bakın.</p>

<h2>WPM neyi ölçer, neyi ölçmez?</h2>
<p>WPM (dakikada okunan kelime sayısı), belirli bir metni belirli bir sürede okuma hızını özetler. Metnin zorluğunu, konuya aşinalığınızı, okuma amacınızı veya ana fikirleri ne ölçüde anladığınızı tek başına açıklamaz. Bu yüzden farklı uzunlukta ve zorlukta metinlerden çıkan hızları doğrudan karşılaştırmak dikkat gerektirir.</p>

<h2>Hızın yanına anlama kontrolü ekleyin</h2>
<p>Okuma sonrasında birkaç iyi hazırlanmış soru, metinden ne anlaşıldığını hız ölçümüne ek bir açıdan gösterir. Bu kısa kontrol, bir sınav veya kapsamlı okuduğunu anlama değerlendirmesinin yerine geçmez; yalnızca o metne ilişkin bir geri bildirimdir.</p>
<p>2023'te yayımlanan küçük bir deneysel çalışmada, 30 Almanca konuşan üniversite öğrencisi üç gruba ayrıldı ve üç hafta boyunca uygulama temelli hızlı okuma eğitimi, metabilişsel eğitim veya eğitim almama koşuluna katıldı. Eğitim gruplarında son test okuma hızı kontrol grubundan yüksek bulundu; gruplar arasında anlama performansı farkı saptanmadı. Çalışma, hızlı okuma uygulamalarının vaat ettiği büyük kazanımları doğrulamadı. Örneklemin küçük ve belirli bir öğrenci grubuyla sınırlı olması, sonucu herkes veya başka bir platform için genellememeyi gerektirir.</p>

<h2>Karşılaştırmayı daha adil yapın</h2>
<ul>
  <li>Mümkünse benzer dilde, türde ve zorlukta metinleri karşılaştırın.</li>
  <li>Okuma süresini ve anlama yanıtlarını ayrı göstergeler olarak kaydedin.</li>
  <li>Tek bir oturumdan kesin sonuç çıkarmayın; birden fazla ölçümü ve çalışma koşullarını gözden geçirin.</li>
  <li>Metin amacınıza göre okuma biçiminizi değiştirin: belirli bir bilgiyi aramak, genel yapıyı anlamak ve ayrıntılı öğrenmek aynı okuma hedefi değildir.</li>
</ul>

<h2>Sonuç</h2>
<p>Okuma hızını anlamadan bağımsız bir hedef olarak ele almak yerine, iki sonucu birlikte izleyin. Buradaki ölçümler öğrenme sürecine dair geri bildirim sağlar; kişisel gelişim garantisi, tanı veya bilimsel yeterliği ayrıca doğrulanmış bir test anlamına gelmez.</p>

<h2>Kaynak</h2>
<p>Klimovich, M., Tiffin-Richards, S. P. ve Richter, T. (2023). <a href="https://doi.org/10.1111/1467-9817.12417" rel="noopener noreferrer" target="_blank">Does speed-reading training work, and if so, why?</a> <em>Journal of Research in Reading, 46</em>, 123–142.</p>
```

Bu yazı taslaktır; yayın öncesinde Türkçe eğitim/okuma uzmanı ve editör incelemesi yapılmalı, bağlantıların ve CMS önizlemesindeki biçimlendirmenin doğruluğu kontrol edilmelidir.

## Görsel yönü

Yeni sosyal paylaşım/kapak görseli `clients/speed-reading/public/assets/images/share-image-v2.png` konumuna eklendi. Marka görselindeki lacivert-mor ve altın tonlarından yararlanır; üzerinde yazı, oran, logo veya sonuç vaadi bulunmaz. Varsayılan `og:image` ve X paylaşım görseli buna yönlendirildi. Alternatif metin görseli betimler.

Görsel yaklaşımı:

- Açık kitap ve odaklanmış okur; acele, yarış veya “süper güç” iması yok.
- Kapaklara anlamı taşıyan metin görselin içine gömülmemeli; başlık HTML/CMS metni olmalı.
- CMS medya kütüphanesinde her dosyaya açıklayıcı alt metin girilmeli; mümkünse WebP/JPEG ve uygun kırpım/byte boyutu tercih edilmeli.
- Bu üretilmiş görsel taslak/varsayılan sosyal görseldir; makale iddiasını kanıtlayan veri görseli değildir.

## CMS'e geçişten önce

1. Makale ve ana sayfa metinlerini ürün sahibi onaylamalı.
2. Gerçek yazar/editör bilgisi eklenmeli.
3. CMS yönetim ekranında önce taslak kaydedilmeli ve önizlenmeli; görsel kırpımı, mobil kartlar, detay sayfası, alt metin ve SEO metaları doğrulanmalı.
4. Kaynak linkleri ve iddialar son kez editöryel/akademik gözden geçirilmeli.
5. Ancak bundan sonra yayımlanma kararı verilmeli.
