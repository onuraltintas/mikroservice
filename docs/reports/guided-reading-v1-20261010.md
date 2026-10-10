# Hızlı Okuma sabit tempo geliştirmesi — 10 Ekim 2026

## Durum

Yerel geliştirme ve aşağıdaki doğrulamalar tamamlandı. Yerel TDD kontrol noktası commitleri oluşturuldu. **GitHub'a gönderilmedi, canlıya yayımlanmadı, canlı veritabanına paket uygulanmadı.** VPS yalnız salt okunur metin/ayar envanteri için kullanıldı. Eski test kullanıcılarının kayıtlarını silmek bu geçiş için gerekli değil; silinmediler.

## Tamamlananlar

1. Dört yaş grubu × beş seviye için mevcut hızlar korundu; onaylanan dakika hedeflerine karşılık gelen kelime aralıkları tanımlandı. Otomatik uyarlama kapalı, vurgulama tek kelimelik.
2. Kayıtlı oturum ve önizleme aktif, uygun yaş/seviye/egzersiz ve kelime aralığındaki gerçek metinleri seçiyor. Kullanım geçmişine göre az kullanılan metin öncelikli. Önizleme ayrıntısında gerçek kelime sayısı da doğrulanıyor; uygun içerik yoksa örnek metne geçilmiyor.
3. Hedef süre bir bitiş sınırı değil. Metin tamamen gösteriliyor; duraklama aktif süreye eklenmiyor. Eski süre sınırı arayüzde de gösterilmiyor.
4. Sunucu gösterimin başlangıç/bitişini ve asgari aktif süreyi doğruluyor; erken bitirme ve başka kullanıcı oturumuna erişme engelleniyor. Tamamlama tekrarında mükerrer sonuç oluşmuyor.
5. Sonuç ekranı kelime sayısını ve gösterim temposunu ayırıyor; kayıtlı sonuçta sunucunun değerlerini kullanıyor. Okuma hızı/anlama/doğruluk ölçülmüş gibi sunulmuyor, XP üretilmiyor. Önizleme kalıcı sonuç kaydı oluşturmuyor.
6. 20 ayar ve 22 metin eklemesi için atomik/idempotent PostgreSQL paketi hazır: 8 özgün anlatı, 14 tam kaynak metinden iki bölümlü seçki. Mevcut metinler ve diğer egzersizler korunuyor. Yayın öncesi yedek, uygulanma/geri dönüş ve editoryal sınırlar paket README'sinde açıklanıyor.

Parametre ve içerik ayrıntıları: `content-packs/guided-reading/v1/README.md`.

## Son doğrulamalar

| Kontrol | Sonuç |
| --- | --- |
| SpeedReading.Application.UnitTests, tüm paket | 1022 geçti, 0 başarısız, 0 atlandı |
| Speed Reading istemcisi, tüm Chrome/Jasmine paketi | 717 geçti, 0 başarısız |
| Gerçek PostgreSQL: yeni paket/oturum + mevcut gruplama | 5 geçti, 0 başarısız, 0 atlandı |
| Masaüstü/mobil Playwright | 6 geçti, 0 başarısız |
| Üretim frontend derlemesi | Başarılı; mevcut SCSS boyut uyarısı |
| Git whitespace kontrolü | Başarılı |

PostgreSQL testleri geçici Docker veritabanında 20 yaş/seviyenin oturumunu kontrol eder. Kaynak fixture içeriklerinin hashleri yalnız test belleğindeki SQL için eşlenir; üretim SQL dosyası değiştirilmez. Kaynak metin gerçekten değiştirildiğinde ve eski ayarlar uyuşmadığında paket tüm işlemi reddeder. Tekrar uygulama, kaynak koruma, kelime aralıkları, erken bitiş, sahiplik, duraklama, null ölçümler ve sonuç idempotency'si doğrulanır.

Playwright gerçek Angular uygulama/motoruyla admin özel önizleme, öğretmen içerik-yok durumu ve öğrenci kayıtlı akışını iki ekran boyutunda doğrular. Identity ve HTTP yanıtları test düzenekleridir; **bu canlı Identity + backend uçtan uca testi değildir**. Canlı yayın sonrası oturumlu kontrol ayrıca yapılmalıdır.

Geniş Chrome taramasında dört eski sonuç testinin eksik `route.snapshot` düzeneği düzeltildi; üretim davranışı bu sebeple gevşetilmedi. Ayrıca sunucudan kelime sayısı alma ve eski cutoff gösterimini kaldırma için iki yeni test önce başarısızlıkla doğrulandı, sonra düzeltildi. Üretim derlemesindeki stil uyarısı: `exercise-player.component.scss` 128,98 kB, uyarı bütçesi 120 kB; bu çalışma SCSS'e değişiklik yapmadı.

## Tekrar çalıştırma

Repo kökünden:

```powershell
dotnet test tests/Unit/SpeedReading.Application.UnitTests --no-restore --verbosity quiet
dotnet test tests/Integration/Identity.API.IntegrationTests --no-restore --filter 'FullyQualifiedName~SpeedReadingGuidedPracticePostgresTests|FullyQualifiedName~SpeedReadingGroupingPostgresTests' --verbosity quiet
```

`clients/speed-reading` dizininden, Chrome/Docker uygun erişimle açıkken:

```powershell
$env:CHROME_BIN='C:\Program Files\Google\Chrome\Application\chrome.exe'
npx ng test --watch=false --browsers=ChromeHeadless --reporters=dots
npx ng build --configuration production
```

`tests/E2E` dizininden:

```powershell
npx playwright test --config speed-reading-guided-practice.config.mjs
```

## Kod incelemesi ve kalan yayın işi

Güvenlik: metin/oturum sahipliği korunuyor, istemci sonuç ölçümleri otorite sayılmıyor; paket sır içermez ve canlı DB'de çalıştırılmadı. Performans: mevcut metin seçimi/kullanım sıralaması kullanılıyor, yeni uzak servis veya profil projeksiyonu yok. Doğruluk: aynı yaş/seviye, min/max alan tutarlılığı, tek kelimelik tempo, duraklama ve tam metin bitişi testli. Bakım: mevcut kelime-vurgulama protokolü geriye uyumlu genişletildi, yeni şema eklenmedi.

Yeni süre/kelime bantları ve özgün metin seviyeleri ürün pilot parametreleridir; akademik yaş normu veya kanıtlanmış okuma hızı artışı değildir. İçerik çeşitliliği asgari iki uygun kayıtla başlar; öğrenci pilot geri bildirimleri sonrasında genişletilebilir.

Kalan operasyon: yayın onayı/kapsamı, GitHub push, Speed Reading DB yedeği, kontrollü API/frontend yayını, paketin doğru DB'ye uygulanması ve canlı oturumlu sağlık/işlev kontrolü. Identity, Koçluk ve diğer servislerin veritabanlarına bu paket için değişiklik gerekmiyor.
