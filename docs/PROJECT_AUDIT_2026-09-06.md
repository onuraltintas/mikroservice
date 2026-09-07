# Proje incelemesi — 6 Eylül 2026

## Sonuç ve kapsam

Proje, modern mikroservis mimarisinin birçok temelini gerçekten uyguluyor: servis başına veri sahipliği, katman ayrımı, gateway, mesajlaşma, outbox/inbox, yetkilendirme, idempotency, üretim migration işleri ve ortak arayüz tokenları mevcut. Ancak işlev doğruluğu, eşzamanlı veri güncellemeleri, bazı güven sınırları, dağıtık çalışma ve operasyon doğrulamasında önemli açıklar var. “Best practice’lerle eksiksiz yapılmış” veya “100 bin eşzamanlı kullanıcıya hazır” sonucu desteklenmiyor.

İnceleme üç ajanla bölündü: servis mimarisi/iş mantığı; güvenlik/backend kalite; frontend/paneller/SEO. Ana inceleme CI, altyapı, gözlemlenebilirlik, test çalıştırma ve bulgu birleştirmesini kapsadı. Ajanlar ilk çalışmada kullanım sınırına takıldı; devam mesajından sonra raporlarını tamamladı.

Bu belge kaynak kod ve repository konfigürasyonlarının başlangıç incelemesidir. Canlı üretim sunucusuna erişilmedi; yük testi, pentest, Search Console/Core Web Vitals veya panellerde oturum açılarak görsel cihaz testi yapılmadı. Dolayısıyla görsel kalite ve kapasiteyle ilgili değerlendirmeler bu sınırlarla okunmalıdır. Her dosyanın her satırının kusursuz olduğu doğrulanmış değildir. Aşağıdaki durum notu, inceleme sonrasında yapılan düzeltmeleri bu tarihli snapshot'tan ayırır.

### 7 Eylül 2026 durum notu

Kampanyanın gönderilmediği halde başarılı görünmesi, koçluk aggregate'lerinde eşzamanlı güncelleme, veli ekranındaki eski yanıt yarışı, destek yanıtı HTML/konu enjeksiyonu ve Master admin'in ikinci yazma yüzeyi düzeltildi. CMS önizlemeleri güvenilir statik değerler dışında Angular'ın yerleşik sanitization'ını kullanıyor. Merkezi panelde toplu kullanıcı CSV işlemleri, dinamik rol seçimi, analitik seri birleştirme ve WPM/UUID aramaları eklendi. Kampanya gönderimi ve zamanlaması gerçek SMTP/worker kurulana kadar fail-closed biçimde reddediliyor; merkezi arayüz yalnızca taslak ve uygulama içi bildirim akışlarını gösteriyor. Bu rapordaki diğer P2/P3 maddeler açık backlog olarak kalır.

Öncelik: P1 ilk düzeltme grubu; P2 planlı iyileştirme; P3 bakım/temizlik. Riskin büyüklüğü trafik, rol yetkileri ve dağıtım şekline bağlı olduğunda ayrıca belirtilmiştir.

## 1. Öncelikli işlev ve veri doğruluğu sorunları

### P1 (düzeltildi) — Kampanya gerçekte gönderilmeden gönderildi görünüyor

`services/speed-reading-service/SpeedReading.Infrastructure/Legacy/LegacySpeedReadingEmail.cs:247` SMTP/kuyruk çalışanı olmadığını belirtiyor; devamında Sending/Sent ve SentAt değiştiriliyor. `SpeedReading.API/Controllers/EmailCampaignsController.cs:61` başarılı sonuç veriyor. Implementasyon owned çalışma modunda da DI üzerinden bağlı.

Bu bir iyileştirme fırsatından öte yanlış başarı bildirimidir. Kampanya alıcılarını güvenilir iş kuyruğuna/outbox’a yazmak, teslim denemelerini idempotent işlemek ve durumu gerçek gönderim sonucundan üretmek gerekir. Gerçek gönderim yokken başarılı gönderim sunulmamalı.

### P1 (düzeltildi) — Koçluk hedeflerinde eşzamanlı güncelleme veri ezebilir

`services/coaching-service/Coaching.Infrastructure/Repositories/Repositories.cs:454` AcademicGoal için bütün entity’yi Update ile modified yapıyor. `Coaching.Application/Commands/UpdateGoal/UpdateGoalCommand.cs:67` ve `Commands/UpdateGoalProgress/UpdateGoalProgressCommandHandler.cs:25` aynı kaydı ayrı işlemlerde yüklüyor. AcademicGoal konfigürasyonunda concurrency token bulunmuyor.

Örnek: iki istek %20 ilerlemeyi okur; öğrenci %80 kaydeder; öğretmen eski nesnede yalnız başlığı değiştirip kaydeder. Öğretmenin Update çağrısı eski %20 değerini de yazabilir. Tracking ile yüklenmiş nesnede gereksiz Update kaldırılmalı; aynı alan/state yarışları için version/xmin ve 409 conflict stratejisi eklenmeli. İki ayrı DbContext kullanan gerçek PostgreSQL testiyle doğrulanmalı. Idempotency bu sorunu çözmez: iki farklı meşru isteğin çakışmasıdır.

### P1 (düzeltildi) — Veli ekranında seçili çocuk ile gösterilen veri ayrışabilir

`clients/admin-panel/src/app/features/coaching-portal/pages/parent-children.component.ts:70` her seçimde yeni forkJoin başlatıyor. Önceki çağrı iptal edilmiyor; gelen cevabın hâlâ seçili çocuğa ait olduğu kontrol edilmiyor. A ardından B seçilince geciken A yanıtı B başlığı altında gösterilebilir. Load-more çağrılarında da seçili çocuk değişimi gözetilmiyor.

Seçimi switchMap ile yönetmek veya response identity kontrolü yapmak gerekir. Yalnız component destroy temizliği bu yarışı çözmez. Progress hatasının null’a çevrilmesi ve sayfalanmış kayıt sayılarının toplam gibi kullanılması da eksik veriyi yanıltıcı sunabilir; kısmi veri durumu görünür olmalı.

### P2 — Sonuç yazma yollarında farklı güven modeli

`services/speed-reading-service/SpeedReading.API/Controllers/ProgressController.cs:19` üzerinden `OwnedSpeedReadingProgressWriter.cs:46` istemcinin RawWpm, ComprehensionScore, WeightedKdp ve CompletedAt değerlerini kaydediyor. Aralık kontrolü var; fakat bu değerleri sunucu egzersizinden yeniden üretmiyor. Kullanıcı kendi sonuç geçmişine sahte ölçüm yazabilir.

Diğer egzersiz/assessment yollarında sunucu hesaplaması ve sahiplik kontrolü mevcut. Offline/self-report akışı kasıtlıysa sonuçlara kaynak/verified alanı eklenmeli ve raporlar ayrıştırılmalı. Bu bulgu bütün leaderboard veya assessment sisteminin manipüle edildiğini kanıtlamaz.

### P2 — Analitik metriklerinin anlamı sorunlu

`OwnedSpeedReadingAdminAnalytics.cs:159` teknik “System health” durumunu öğrencilerin anlama/başarı ortalamasından üretiyor. Bu uptime, gecikme veya hata oranı değildir. Teknik sağlık olarak sunulursa kullanıcıyı yanıltır. Eğitim etkililiği olarak adlandırılmalı; teknik sağlık telemetriden gelmeli.

Aynı dosya `:201` kurum performansında WPM ile yüzde anlama oranını doğrudan ortalıyor. Ölçü birimleri farklı olduğundan normalize edilmiş, belgelenmiş bir puanlama gerekir.

## 2. Mikroservis mimarisi ve kod yapısı

### Doğru yapılanlar

- Identity, Coaching, SpeedReading, Notification ve Gateway ayrılmış. Domain/Application/Infrastructure/API bağımlılık yönleri genel olarak anlamlı.
- Koçluk bağlamı ödev, teslim, dosya, sınav, hedef, seans ve ilerleme etrafında tutarlı.
- Koçlukta MassTransit EF outbox/inbox gerçekten var: `services/coaching-service/Coaching.API/Program.cs:83`; mesaj retry ve idempotency constraint’leri bulunuyor. “Outbox yok” demek yanlış olur.
- Hızlı okuma owned runtime’ında eski veritabanı connection’ının reddedilmesi bilinçli izolasyon: `services/speed-reading-service/SpeedReading.Infrastructure/DependencyInjection.cs:45`.
- Assessment snapshot, sahiplik kontrolü ve sunucuda hesaplama geçmiş değerlendirmelerin güvenilirliği için iyi tercihler.
- Koçluk sorgularında pagination/projection ve deterministik sıralama örnekleri mevcut.
- Üretimde migration’ların ayrı tek seferlik işlerde uygulanması, her web replikasının migration yarıştırmasından daha güvenli.

### Sınırların netleşmesi gereken yerler

Hızlı okuma servisi egzersiz, değerlendirme, program, gamification yanında CMS/blog, ödeme, abonelik, kampanya ve bildirim tercihlerini de taşıyor. Tek ürünün modüler servisi olabilir; büyüklük tek başına mikroservis ihlali değildir. Ancak Notification servisi varken kampanya/şablon/gönderim sahipliği açık değil. Önce modül API’leri ve veri sahipliği netleştirilmeli; her klasörü yeni mikroservise çevirmek gereksiz işletim yükü doğurabilir.

Koçluk yetki kararlarında Identity’ye senkron HTTP çağrısı yapıyor (`Coaching.Infrastructure/ExternalServices/IdentityAuthorizationClient.cs:43`). Fail-closed davranış güvenlik açısından doğru; Identity yavaşlaması koçluk işlemlerine yayılır. Client’larda timeout var; incelenen kayıtlarda circuit breaker/bulkhead görünmüyor. Yetki cache’i eklenirse iptal gecikmesi ayrıca tasarlanmalı.

Fiziksel PostgreSQL sunucusunu paylaşmak tek başına yanlış değildir; mantıksal veri sahipliği önemlidir. Ancak aynı sunucu arıza ve kaynak rekabeti sınırlarını ortaklaştırır. Servis başına ayrı DB rolü ve en az yetki uygulanması daha güçlü izolasyon sağlar.

## 3. Güvenlik

### Güçlü taraflar

- JWT imza, izin verilen algoritma, issuer, audience, lifetime doğrulaması ve fallback authenticated policy var (`shared/EduPlatform.Shared.Security/Extensions/SecurityExtensions.cs:35`).
- Hassas kullanıcı yönetiminde permission, SystemAdmin ve MFA politikaları birlikte kullanılıyor.
- Refresh cookie HttpOnly, üretimde Secure, SameSite Strict ve dar path kullanıyor (`Identity.API/Security/RefreshTokenCookiePolicy.cs:16`).
- Refresh rotation conditional update/transaction kullanıyor; eşzamanlı token yenilemede tek kazanan yaklaşımı var.
- Internal key doğrulaması constant-time ve eksik config’te güvenli kapanıyor.
- Kurum scope çözümü, profil erişiminde kurum üyelik kontrolü ve bildirimlerde UserId sahiplik filtresi mevcut. İncelenen bu yollarda basit IDOR iddiası desteklenmiyor.
- Koçluk dosyalarında boyut/uzantı kuralları, üretimde MinIO ve ClamAV yapılandırması var.
- Gateway Redis tabanlı atomik rate limit kullanıyor.

### Düzeltilecekler

**P1/P2 — Access token iptali gecikiyor.** `Identity.Infrastructure/Services/UserAccessManagementService.cs:66` gibi işlemler refresh tokenı iptal ediyor; mevcut JWT yalnız süresi bitince geçersizleşiyor. Permission handler token claim’lerini okuyor. Varsayılan süre 30 dakika, yapılandırılabilir üst sınır 1440 dakika (`TokenService.cs:104`). Stateless JWT için bilinen bir ödünleşim olsa da “tüm oturumları sonlandır” beklentisiyle uyuşmuyor. Kısa access ömrü, dağıtık session/security version kontrolü ve kritik işlemlerde güncel yetki doğrulaması gerekli.

**P2 — Destek e-postasında HTML injection.** `services/notification-service/Notification.Application/Commands/SubmitSupportRequest/SubmitSupportRequestHandler.cs:122` kullanıcı girdilerini HTML encode etmeden şablona koyuyor. Anonim formdan, girilen alıcıya kurum markasıyla zararlı bağlantı/HTML gönderilebilir. Bu, tarayıcıda script çalıştığı kanıtı değildir. HTML context encoding ve gönderim abuse kontrolleri gerekir.

**P2 — Refresh token DB’de ham saklanıyor.** `Identity.Domain/Entities/RefreshToken.cs:11` ve `Identity.Infrastructure/Repositories/Repositories.cs:162`. DB/backup okuma sızıntısında aktif bearer değerler doğrudan kullanılabilir. Yüksek entropili tokenın digest’i saklanmalı; ham token yalnız oluşturulurken istemciye verilmeli.

**P2 — CMS önizlemesi Angular sanitization’ını atlıyordu (tarihsel bulgu; çözüldü).**
6 Eylül snapshot'ındaki Master admin CMS önizlemeleri `bypassSecurityTrustHtml`
kullanıyordu. Eski yönetim ağacı kaldırıldı; içerik düzenleme artık merkezi
eduivme panelindeki sanitization akışıyla yapılır.

**P2 — Ortak simetrik JWT anahtarı geniş etki alanı yaratıyor.** Doğrulayıcı servislerdeki HS256 secret aynı zamanda token üretmeye yarar. Bir servisin ele geçirilmesinin etkisini küçültmek için Identity’de private key, diğer servislerde public key/JWKS; iç servis çağrılarında ayrı kimlik/audience değerlendirilmeli. Bu doğrudan dışarıdan erişim açığı kanıtı değildir.

**P2/P3 — Hata ayrıntıları istemciye dönebiliyor.** RegisterInstitution ve RegisterParent handler catch bloklarında `Database error: {ex.Message}` var. Genel istemci hatası ve loglarda correlation ID tercih edilmeli.

Tüm kurum/rol/nesne kombinasyonlarının negatif entegrasyon testleri tamamlanmadan “tenant isolation eksiksiz” denmemeli. Özellikle kurum A yöneticisinin kurum B öğrencisi, raporu, dosyası ve seansına erişimi ayrıca test edilmelidir.

## 4. 100 bin kullanıcı ve dağıtık çalışma

100 bin kayıtlı kullanıcı, 100 bin günlük aktif kullanıcı ve 100 bin eşzamanlı kullanıcı farklı kapasite hedefleridir. Koddan donanım veya kesin kullanıcı sınırı çıkarılamaz.

Örnek planlama hesabı, ölçüm değildir: 100 bin kayıtlı kullanıcının %2’si aynı anda aktif ve kişi başına 10 saniyede bir istek üretiyorsa yaklaşık 200 RPS oluşur. 100 bin eşzamanlı kullanıcı aynı davranışla yaklaşık 10 bin RPS üretir. Rapor, dosya, giriş ve sonuç yazma maliyetleri eşit değildir.

### Engeller

1. **Ham analitik materialization:** `OwnedSpeedReadingAdminAnalytics.cs:177` profiller ve tarih aralığındaki aktiviteleri belleğe alıp kurum başına yeniden tarıyor. 366 gün sınırı satır sayısını sınırlamaz. SQL aggregation, ön özet/read model, uygun indeksler ve rapor cache’i gerekir.
2. **Yerel CMS medya:** DI’da LocalCmsMediaStorage var. Production Compose aynı hostta kalıcı named volume bağlıyor (`docker-compose.production.yml:161` çevresi). Bu nedenle mevcut tek-host senaryosunda “dosyalar her deploy’da kesin kaybolur” doğru değildir. Çok host/pod dağıtımında ortak object storage olmadan dosya görünürlüğü bozulur.
3. **Süreç içi CMS cache:** `LegacySpeedReadingCms.cs:42` 10 dakikalık memory cache ve `:1098` yerel invalidation kullanıyor. Diğer replikalar eski içeriği TTL boyunca sunabilir.
4. **Login/refresh thread bloklama:** `Identity.Infrastructure/Services/TokenService.cs:104` async config okumasını GetAwaiter().GetResult ile blokluyor. Async veya cached typed config gerekir.
5. **Tek altyapı örnekleri:** Compose tek PostgreSQL, Redis ve RabbitMQ tanımlıyor. Replication/failover topolojisi gösterilmiyor. `docker-compose.scale.yml` yalnız gateway host portunu kaldırıyor; otomatik ölçek veya yüksek erişilebilirlik sağlamıyor.
6. **Bağlantı bütçesi:** Identity/Coaching/Notification pool sınırı varsayılan 30. Replika sayısıyla toplam DB bağlantısı büyür. 30’u tek başına yanlış saymak doğru değil; DB max connection, pool wait ve workload birlikte ölçülmeli.
7. **Bildirim toplu güncellemesi:** NotificationsController MarkAllAsRead tüm okunmamışları belleğe çekiyor; set-based update daha uygun.

### Kapasiteyi kanıtlama planı

- Hedef: kayıtlı, DAU, peak concurrent; kurum başına dağılım; aktivite geçmişi hacmi.
- Ayrı senaryolar: giriş/refresh dalgası; egzersiz açma/sonuç yazma; öğretmen-veli raporları; admin yıllık analitik; dosya yükleme; notification/SignalR.
- En az beklenen hacimde veri ve gerçekçi think-time ile ramp-up, spike, uzun süreli soak ve bağımlılık arızası testleri.
- p95/p99, hata oranı, CPU/RAM/GC, DB lock/IO/pool wait, queue lag, Redis, gerçek zamanlı bağlantılar ölçülmeli.
- Ürün SLO’su belirlenmeli; örneğin normal okumalarda p95 <500 ms ve hata <%1 bir başlangıç hedefi olabilir, mevcut sonuç değildir.
- CDN/static cache, SQL optimizasyonu, stateless API replikaları, ortak storage ve HA veri katmanı ölçümlere göre kurulmalı. Kubernetes kullanımı tek başına gereklilik veya kapasite kanıtı değildir.

## 5. Paneller, tasarım ve kullanılabilirlik

| Panel | Güçlü taraf | Düzeltilecek/ölçülecek taraf |
|---|---|---|
| Admin | Lazy routes, permission guard, ortak header/sidebar, tema, merkezi feedback | Mobil menü odak yönetimi; CMS güvenli önizleme; kampanya durum doğruluğu; uzun rapor maliyeti |
| Öğrenci | Assessment/profil/program/egzersiz ayrımı, route guard’ları, dashboard widget’ları | Egzersiz rotalarında assessment/subscription UX tutarlılığı; farklı sonuç yazma yolları; mobil gerçek kullanım testi |
| Veli | Çocuk seçimi, boş/yükleme/hata durumları, sayfalama, responsive kartlar | Çocuk seçimi yarışı; kısmi veriyi toplam gibi sunma; uzun sayfanın bilgi yoğunluğu |
| Öğretmen | Öğrenci/ödev/rapor/koçluk rotaları, Material ortak layout | Kurum yönetimiyle aynı ağaçta rol ayrımı; uzun tablolar/filtreler ve mobil görev akışları |
| Kurum | Öğretmen layout ve menülerini rol bazlı tekrar kullanma | Bağımsız panelden çok rol varyantı; institution-settings içinde any tipleri; açık yetki ve bilgi mimarisi |

Öğretmen kurum ayarlarına giden frontend rotasında ayrı rol guard eksikliği, backend yetki atlatıldığı anlamına gelmez; backend ayrıca korunmalıdır. Frontend de yetkisiz kullanıcılara kullanamayacakları sayfayı sunmamalı.

### Renkler ve tasarım sistemi

`clients/shared/styles/_platform-tokens.scss:5` indigo/mor marka, slate nötrler, semantik durum renkleri, font, spacing, radius, dark/sepia ve kontrol yüksekliği tanımlıyor. İki istemci ortak dosyaları import ediyor. Focus-visible ve reduced-motion desteği de var. Bu, merkezi bir tasarım sisteminin gerçek başlangıcıdır.

Ancak Tailwind sabit renk sınıfları, yerel hex değerleri, Material override’ları ve çok sayıda !important beraber yaşıyor (`clients/speed-reading/src/styles.scss:938`). Tema değiştirme ve bakım maliyeti artıyor. Yeni bileşenler semantik token tüketmeli; mevcut stiller ekran ekran konsolide edilmeli. Renk paleti kaynakta dengeli görünüyor, fakat bütün metin/zemin çiftleri için ölçülmüş kontrast sonucu yok.

### Responsive ve erişilebilirlik

- Koçluk layout yatay kaydırmalı menü, sm/lg boyutları ve max-width içerik kullanıyor; veli tablosunda overflow var.
- Admin mobil sidebar yalnız transform ile ekrandan çıkarılıyor (`dashboard-layout.html:31`). Inert/aria-hidden, odak tuzağı, Escape ve odak geri dönüşü eksik; görünmez linkler Tab sırasına kalabilir.
- Speed base layout logosu `div [routerLink]` (`base-layout.component.html:43`); gerçek anchor daha erişilebilir.
- 320/375/768/1280px, %200 zoom, klavye-only ve ekran okuyucu testleri gerekli. Kaynak incelemesi “tüm paneller modern, şık ve taşmasız” hükmünü desteklemez.

### Merkezi toaster

İki uygulamada ToasterService mevcut; MatSnackBar/MatDialog ve ortak feedback.types kullanılıyor. Başarı/hata/uyarı/bilgi, onay ve prompt bu serviste toplanmış. “Merkezi toaster yok” yanlış olur.

İki implementasyon ayrışmış: speed `toaster.service.ts:35` üçüncü title argümanını vaat eden overload sunuyor, implementation argümanı almıyor. İki serviste de duration için `||` kullanımı 0 değerini default’a çeviriyor. Ortak implementasyon veya sözleşme testleriyle davranışlar eşitlenmeli. Eşzamanlı mesajların tek snackbar’da birbirini kapatma davranışı ürün açısından kararlaştırılmalı.

## 6. SEO

### Mevcut iyi temel

Türkçe lang, title/description, canonical, OpenGraph/Twitter etiketleri, robots ve sitemap dosyaları, dinamik SeoService mevcut. Admin uygulamasında özel dashboard/koçluk rotalarının client render olması doğru; özel kullanıcı verilerinin indekslenmesi hedef değildir.

### Sorunlar

- Hızlı okuma public site CSR çalışıyor (`clients/speed-reading/angular.json:24`). İlk HTML tüm rotalarda ana sayfa canonical/OG taşıyor; JS sonrasında güncellenmesi her sosyal bot için yeterli değil. Public blog/CMS için SSR/prerender uygun.
- `index.html:313` priceValidUntil 2025-12-31; inceleme tarihinde geçmişte. Sabit 4.8/127 değerlendirme verisinin gerçek kaynağı doğrulanmalı. Ürün/FAQ/review JSON-LD her rotanın ortak kabuğunda durmamalı.
- `SeoService.updateTags():35` robots ve önceki article etiketlerini sıfırlamıyor. Noindex sayfadan başka public sayfaya SPA navigasyonunda durum taşınabilir.
- `public/sitemap.xml` statik 2025-01-01 lastmod değerleri taşıyor; içerik değişimlerinden otomatik üretilen sitemap daha doğru. Tarihin eski olması tek başına ceza değildir; gerçek değişimi yansıtmalıdır.
- Nginx bilinmeyen yolları index.html’e düşürüyor (`nginx.conf:18`); gerçek olmayan CMS/blog URL’lerinin 404 semantiği ayrıca doğrulanmalı. Yalnız ekranda bulunamadı yazmak HTTP 404 yerine geçmez.
- İki font ailesi, Material Icons, FontAwesome ve analytics ilk HTML’de yükleniyor. Gereksiz kaynakları azaltmak mobil performansa yardımcı olabilir; LCP/INP/CLS burada ölçülmedi.
- Robots disallow bir erişim güvenliği veya kesin deindex mekanizması değildir. Özel panellerin auth kontrolü ve uygun noindex davranışı korunmalı.

Google’ın teknik rehberi SSR/prerender’ın kullanıcılar ve crawler’lar için yararlı olduğunu; tüm botların JavaScript çalıştıramadığını açıklıyor: https://developers.google.com/search/docs/crawling-indexing/javascript/javascript-seo-basics

## 7. CI, operasyon, gözlemlenebilirlik

**P1 — İstemci Docker build context hatası.** `.github/workflows/ci.yml:435` context olarak clients/admin-panel veriyor; Dockerfile `COPY admin-panel/...` ve `COPY shared/...` bekliyor. Bu dizin yapısı clients bağlamını gerektiriyor. Speed-reading satır 438’de aynı uyuşmazlık var. Production Compose zaten clients bağlamını kullanıyor. CI container build işleri bu haliyle gerekli dosyaları bulamaz; yerelde Docker çalışmadığından build fiilen yeniden üretilmedi, yol uyuşmazlığı statik olarak kesin.

**P2 — Hızlı okuma CI test/migration boşluğu.** CI dotnet test komutu Identity.API.IntegrationTests projesini hedefliyor; ayrı SpeedReading.Application.UnitTests projesi iş akışında çalıştırılmıyor. Migration drift üç ana DB için var, owned hızlı okuma için yok. Yerelde geçen 210 testin CI kapısına dahil edilmesi gerekir.

**P2 — Hızlı okuma monitoring kapsamına tam alınmamış.** Uygulamada OpenTelemetry kaydı var; ancak observability overlay speed-reading için exporter ortamını vermiyor ve Prometheus readiness hedeflerinde servis yok. Kodda telemetry bulunması collector’a veri aktarıldığını garanti etmez.

**P2 — Alarm dış bildirim alıcısı yapılandırılmamış.** `monitoring/alertmanager/alertmanager.yml:12` default receiver boş. UI’da alarm görülebilir; repo konfigürasyonu dış bildirim sağlamaz. Canlı ortamda ayrıca ayarlanmış olabilir, doğrulanmadı.

**P2 — Backup kapsamı dokümanda eksik.** `docs/OPERATIONS_RUNBOOK.md:132` örnek DB backup döngüsü yalnız identity/coaching/notification içeriyor; owned hızlı okuma DB’si ve CMS medya bu döngüde yok. Başka mekanizma olabilir; repository bu kapsamı kanıtlamıyor. Tüm iş verisi ve object storage için backup, restore drill, RPO/RTO gerekli.

**P2 — Performans kapısı kapasiteyi temsil etmiyor.** Performance workflow manuel, GET-only, tek endpoint ve en çok 128 worker/600 saniye. tools/load-test.ps1 daha geniş seçenekler sunsa da gerçek iş akışlarının karışımı ve kanıtlanmış sonuç raporu yok. E2E workflow da manuel ve önemli senaryolar flag ile açılıyor; her PR’da geniş UI regresyon koruması sağlamıyor.

**P3 — Runtime ve container bakım planı.** Proje net9.0. 6 Eylül 2026 itibarıyla .NET 9 destekli ancak bakım döneminde; resmi destek bitişi 10 Kasım 2026. .NET 10 LTS geçişi planlanmalı. “.NET 9 zaten desteksiz” demek yanlış olur. Kaynak: https://dotnet.microsoft.com/en-us/platform/support/policy

Backend Dockerfile’larında açık USER direktifi görünmüyor; non-root çalıştırma container savunmasını güçlendirir. İmaj/sürüm pinleme, kaynak bütçesi ve ayrı workload identity de planlı sertleştirme konularıdır.

CI’de npm audit, NuGet vulnerability scan, warnings-as-errors, Compose validation ve monitoring config kontrolleri olması olumlu. Ancak yapılandırmanın bulunması son CI çalışmasının yeşil olduğunu kanıtlamaz.

## 8. Dead code ve bakım adayları

Güçlü kullanılmayan scaffold adayları (aktif route’lar .component.ts karşılıklarını yüklüyor):

- `clients/speed-reading/src/app/features/public/blog/blog-list/blog-list.ts` ve bağlı html/scss.
- `clients/speed-reading/src/app/features/public/blog/blog-detail/blog-detail.ts` ve bağlı html/scss.
- `clients/speed-reading/src/app/features/public/contact/contact.ts` ve bağlı html/scss.
- `clients/speed-reading/src/app/features/public/payment/payment.ts` ve bağlı html/scss.
- `clients/speed-reading/src/app/core/services/loading.service.ts`: sınıf referansı taramada yalnız tanımında bulundu; global loading akışına bağlı görünmüyor.

Backend adayları: UpdateUserCommandHandler içindeki atanan ama kullanılmayan `_identityService`; GetUserProfileQueryHandler son kurum-admin dalındaki sonucu kullanılmayan sorgu; AcademicGoal.Reopen ve explicit UnitOfWork transaction API’sinin çağrısız parçaları. Bunlar bütün dosyanın dead olduğu anlamına gelmez.

Legacy/Owned/backfill dosyaları isimlerinden ötürü silinmemeli. DI ve migration CLI gerçek kullanım içeriyor. Silme öncesi import/selector/DI/reflection/CLI ve test referans grafiği kontrol edilmeli; temiz build yapılmalı. README ve test kapsamı dokümanları da gerçek proje sürümleri/modülleriyle güncellenmeli.

## 9. Düzeltme sırası

1. Gerçek gönderim olmadan kampanya başarısı; koçluk lost update; veli seçim yarışı; CMS güvenli önizleme; CI Docker bağlamları.
2. Access-token iptal SLA’sı, refresh digest saklama, HTML e-posta encoding, negatif tenant test matrisi.
3. Analitik SQL/read model optimizasyonu; ölçüm güvenilirliği; dağıtık cache/media; login bloklama.
4. Hızlı okuma CI/monitoring/backup kapsamı; alarm alıcısı; restore ve gerçek iş akışlı kapasite testi.
5. Public SSR/prerender ve SEO lifecycle; schema/sitemap düzeltmeleri; mobil klavye erişimi; tema/toaster tutarlılığı.
6. Kanıtlı dead-code temizliği, büyük modüllerin iç sınırları, .NET LTS geçişi.

Mimari prensip referansı: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice
