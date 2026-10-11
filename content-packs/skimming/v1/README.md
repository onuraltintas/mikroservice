# Göz Gezdirme — içerik ve ayar paketi v1

Yerel geliştirme tarihi: 12 Ekim 2026. **Bu paketin hazırlanması canlı veritabanına uygulanması anlamına gelmez.**

## İşleyiş

Kullanıcı başlık, giriş, paragraf başlangıçları ve sonuç üzerinden metnin genel anlamını inceler. Her kelimeyi okumak zorunda değildir. Minimum inceleme süresinden sonra sorulara geçebilir. Süre sınırı dolarsa sorular açılır; inceleme eksik sayılır. Soru yanıtlama zamanı inceleme sınırına dahil değildir. Duraklatma inceleme süresini artırmaz.

Sonuçta ana fikir başarısı ve inceleme süresi gösterilir; kelime/dakika, okuma hızı veya KDP üretilmez. Eksik inceleme kayıtlı oturumda program/ödev ilerlemesi kazandırmaz. Önizleme ve özel ayarlar kullanıcı geçmişine yazılmaz.

Mevcut dört yaş grubunun yalnız **3–5. seviyeleri** korunur. Yeni alt seviye, otomatik zorluk veya ikinci görev eklenmez.

## Pilot parametreler

Bu değerler ürün pilot ayarlarıdır; doğrulanmış bilimsel yaş normu veya tam okuma hızı hedefi değildir. Kullanıcı testleriyle değerlendirilmelidir.

| Yaş grubu | Seviye | Metin (kelime) | En erken bitirme | İnceleme sınırı | Metin boyutu |
|---|---:|---:|---:|---:|---:|
| Çocuk | 3 | 200–280 | 10 sn | 75 sn | 24 px |
| Çocuk | 4 | 280–360 | 12 sn | 90 sn | 24 px |
| Çocuk | 5 | 360–450 | 15 sn | 105 sn | 24 px |
| Genç | 3 | 300–400 | 12 sn | 90 sn | 22 px |
| Genç | 4 | 400–520 | 15 sn | 105 sn | 22 px |
| Genç | 5 | 520–650 | 18 sn | 120 sn | 22 px |
| Genç yetişkin | 3 | 450–600 | 15 sn | 105 sn | 20 px |
| Genç yetişkin | 4 | 600–750 | 18 sn | 120 sn | 20 px |
| Genç yetişkin | 5 | 750–900 | 20 sn | 135 sn | 20 px |
| Yetişkin | 3 | 500–650 | 15 sn | 120 sn | 20 px |
| Yetişkin | 4 | 650–850 | 20 sn | 135 sn | 20 px |
| Yetişkin | 5 | 850–1050 | 25 sn | 150 sn | 20 px |

Her çalışmada metnin kendi **dört genel anlam sorusu** kullanılır: konu, ana düşünce, amaç ve uygun başlık. Cevap konumları metinler arasında değişir. Seçenekler ve gerekçeler metne bağlıdır. Başlangıç havuzu her ayar için üç seçkidir; 12 ayar, toplam 36 metin ve 144 soru. Eski yayımlanmış metinler silinmez; yeni kelime bandını ve dört geçerli soru şartını karşılayanlar seçime katılabilir.

## İçerik kaynağı ve seçim

`generate.mjs`, depodaki mevcut özgün metinlerden tam paragraflar seçip konuya özgü sonuç paragrafı ekler. `content.json` kaynak yolunu ve SHA-256 değerini içerir. Bu dosyalar yalnız paket üretiminde kullanılır; çalışma zamanında sabit/yedek metin yüklenmez.

Kayıtlı oturumda aktif, silinmemiş, uygun yaş/seviye ve egzersiz bağlantısındaki metinler kelime bandı ve geçerli soru sayısıyla süzülür. Kullanıcının daha az kullandığı metinler önceliklidir. Önizleme aynı içerik koşullarını uygular; önizleme kullanım sayacı yereldir ve sunucu geçmişinden bağımsızdır. Uygun içerik yoksa çalışma başlamaz ve anlaşılır uyarı gösterilir.

Özel ayarlarda inceleme sınırı ve yazı boyutu değiştirilebilir; süre minimum incelemeden uzun olmalıdır. Metin seçiminin yaş, seviye, kelime bandı ve soru sayısı temel ayardan gelir. Eski `rules.timeLimit` dahil süre alanları artık önizleme ve özel ayar formunda da okunur. Kayıtlı oturumun sürelerinde sunucu yanıtı esas alınır.

## Üretme ve doğrulama

Depo kökünden:

```powershell
node content-packs/skimming/v1/generate.mjs
node --test content-packs/skimming/v1/content.test.mjs
dotnet test tests/Unit/SpeedReading.Application.UnitTests/SpeedReading.Application.UnitTests.csproj --no-restore
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SpeedReadingSkimming"
```

Tarayıcı testi `tests/E2E/speed-reading-skimming.config.mjs` ile yerel Angular uygulamasını açar. Identity ve API yanıtları test verisidir; canlı oturum/şifre kullanılmaz. Gerçek PostgreSQL testi yalnız geçici Docker veritabanına erişir.

Üretici `settings.json` içindeki önceki ayarları geri dönüş tabanı olarak korur. Eski yaş katalogları ve diğer içerik paketlerini değiştirmez. Yeni kurulumda temel yaş katalogları oluşturulduktan sonra bu ek paket ayrıca uygulanmalıdır. Başka paketler tarafından katalog tekrar yüklenirse bu paketin ayarlarını ezmemesine dikkat edilmelidir.

## Kontrollü yayın ve geri dönüş

Bu adımlar **ayrı canlı yayın onayıyla** yürütülmelidir; burada uygulanmış değillerdir.

1. Hızlı Okuma veritabanı yedeğini ve mevcut API/ön yüz sürümünü saklayın. Önceki ayarların `settings.json` tabanıyla eşleştiğini doğrulayın. Uyuşmazlıkta zorla devam etmeyin.
2. Test edilmiş API/ön yüz çıktısını hazırlayın; yalnız Hızlı Okuma API'sinin yeni oturum oluşturmasını kısa süre durdurun.
3. Yalnız Hızlı Okuma veritabanında `psql -v ON_ERROR_STOP=1 -f content-packs/skimming/v1/apply.sql` çalıştırın. Bağlantı sırlarını komut geçmişine yazmayın.
4. API ve ön yüzü birlikte yayımlayın; sağlık, 12 ayar, 36 yeni metin, 144 soru ve gerçek oturum akışını doğrulayın. Identity/Koçluk veritabanlarına dokunmayın.
5. Geri dönüş gerektiğinde API'yi durdurun, aynı veritabanında `rollback.sql` çalıştırıp önceki sürümü geri yükleyin.

SQL işlemleri tek transaction, zaman sınırı, danışman kilidi ve tablo kilidiyle korunur. Tekrarlama kayıt çoğaltmaz veya gereksiz sürüm artırmaz. Beklenen katalog, metin ya da soru sonradan değişmişse işlem bütünüyle durur; yönetici düzenlemesi ezilmez. Geri dönüş yalnız bu paketin metinlerini pasifleştirip önceki 12 ayarı geri getirir. Sorular, sonuçlar, programlar ve açılmış oturum anlık görüntüleri silinmez. Geri dönüşten sonra tekrar uygulama arşivlenmiş metinleri sessizce etkinleştirmez; yeniden yayın ayrı karar gerektirir.

## Test ve inceleme durumu

- Ön yüz regresyonu: 912/912 başarılı.
- Hızlı Okuma birim testleri: 1206/1206 başarılı.
- Gerçek PostgreSQL: 7/7 başarılı. 12 yaş/seviye oturumu, en erken bitirme, dört soru, cevap anahtarının başlangıçta gizlenmesi, metin rotasyonu ve tekrarlanan tamamlama doğrulandı. Hatalı katalog/metin/soru ve kayıt hatasında atomik geri alma; tekrarlı uygulama ve geçmişi koruyan geri dönüş test edildi.
- Paket üretim kontrolü: 2/2 başarılı; parametreler, soru kanıtları, cevap konumları ve üretilen JSON/SQL eşleşmesi denetlendi.
- Masaüstü/mobil tarayıcı kontrolleri: 10/10 başarılı. Kayıtlı manuel/süre sonu, admin katalog önizlemesi, öğretmen özel ayarları, duraklatma, dört soru, yetersiz soru havuzu ve önizlemede yazma yapılmaması doğrulandı.
- Üretim ön yüz derlemesi başarılı. Mevcut ortak oyuncu SCSS dosyası 120 kB bütçesini yaklaşık 9 kB aşıyor; bu çalışmada stil eklenmedi ve uyarı derlemeyi engellemiyor.
- Skimming motoru kapsamı: satır/ifade/fonksiyon %100, dal %94,54. Bütün ön yüzün mevcut toplam kapsamı yaklaşık %63; tüm projeye %100 kapsam iddiası yoktur.

Yapılandırılmış öz inceleme güvenlik, doğruluk, performans ve bakım açısından yapıldı: SQL veri/ayar uyuşmazlıkları güvenli biçimde durduruluyor; sunucu tarafındaki soru ve süre denetimleri korunuyor; yeni içerik sayıları sınırlı; önizleme yazma yapmıyor. Cevapların her metinde aynı A–B–C–D düzeninde olması giderildi. Tarayıcı testindeki asenkron sonuç düğmesi, anlık `count()` kontrolü yerine görünür düğmeyi bekleyen tıklamayla doğrulanıyor. Görsel kontrolde bitişik görünen inceleme süresi ve başarı etiketleri ayrı satırlara alındı ve regresyon testine eklendi.

Sınırlar: Bu testler pedagojik etkililik veya canlı Identity oturumu doğrulaması değildir. Yeni içeriklerin editoryal zorluğu ve pilot süreler gerçek kullanıcı geri bildirimiyle yeniden değerlendirilmelidir.
