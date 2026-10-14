# Kurs Yönetimi — Ürün Planı ve Geliştirme Yol Haritası

Tarih: 14 Ekim 2026

## Güncel tamamlanma ve kalan yayın kapıları — 14 Ekim 2026

Kurs Yönetimi P0–P7 işlevleri ayrı servis, veritabanı, öğrenci uygulaması ve personel/admin modülleri olarak uygulanmıştır. P0 hesap silme akışı Identity değerlendirme ekranının birleşik sonucunu kullanır; sonuç hazır değilse, eksikse veya yasal hold içeriyorsa Course Management silme işlemi fail-closed kalır. Kullanıcı tarafından karara bağlanan iptal/telafi, manuel TRY tahsilat, katılım/aktarımı, öğrenci finans özeti, saklama, abonelik salt-okunur ve rol kuralları kod ve kabul testlerinde yer alır.

Kod, `codex/course-management-isolated` dalında ayrılmıştır. Dalın temel aldığı `origin/codex/platform-hardening` ile farkı 224 dosya yoludur; korunan Coaching ve Hızlı Okuma uygulama yollarında fark yoktur. `course-management-foundation.contract.test.mjs` kaynak envanteri/hash ve Gateway sınırları dahil `12/12` geçti. Önceki `10/12` sonucu ana checkout’taki korunan ve izlenmeyen dosyalardan kaynaklanan eski kontroldü; izole dalın güncel sonucu değildir.

İzole dalda tekrar edilen yerel doğrulama: Course Management Release birim testleri `67/67`, PostgreSQL/RabbitMQ entegrasyon testleri `15/15`, Identity Kurs Yönetimi oturum/üyelik/bildirim/MFA ve silme-değerlendirme filtresi `72/72` geçti. Identity assessment summary/readiness alt kümesi bu 72 testin içinde `5/5` geçti. Course Management API, Identity API ve Identity entegrasyon test projesinin Release derlemeleri `--warnaserror` ile `0 uyarı / 0 hata` verdi. İlk sandbox koşusunda testhost başlatılamamıştı; aynı testler yükseltilmiş yerel koşuda başladı ve geçti.

Önceki yerel P8 kayıtları da geçerlidir: disposable Docker privacy E2E `6/6`; 26 migration ve ikinci `--migrate-only` çağrısının no-op olması; PostgreSQL API/abonelik/backup/restore sayımlarının `26,6,14,32,1` eşleşmesi; sabit ClamAV ile EICAR/temiz dosya smoke'u; OTEL scrape/trace/metric smoke'u; öğrenci production build'i `214.04 kB`; Course Management admin-panel hedefli testleri `7/7` ve staff-portal hedefli testleri `14/14`. Bunlar yerel doğrulamadır, production deploy kanıtı değildir.

P8 için hâlâ dış ortam veya onay gerektiren kapılar vardır: hosted CI ve migration artifact; genel kişisel veri saklama/purge matrisi ve KVKK/işleme dayanağı; production host, yedekleme/geri yükleme/rollback provası, SLO/RPO/RTO ve alarm teslimatı; mobil/erişilebilirlik kontrolü; pilot ölçümü ve yayın kararı. Korunan Hızlı Okuma uygulama dosyaları değiştirilmemiştir; önceki tam frontend suite kaydı 991/993 olup fixation-player ve visual-expansion-player testleri başarısızdı. Bu turdaki yalnız bu iki testi hedefleyen tekrar Angular build aşamasında 2 dakikadan uzun süre sonuç üretmedi; tam ürün regresyon kapısı temiz olarak doğrulanmış sayılmaz. Bu kapılar kapanmadan ürün production-ready veya pilot-ready sayılmaz. Pilot gözlemi için 2–4 takvim haftası ayrıca gereklidir.

Hosted CI için ayrılmış dal GitHub'da yayımlanmıştır; mevcut commit için Actions run'ı, status check ve açık PR bulunmuyor. CI workflow'u yalnız PR, `main`/`master` push'u veya manuel `workflow_dispatch` ile tetiklenir. PR oluşturma GitHub connector'ında 403 `Resource not accessible by integration` ile reddedildiğinden migration artifact kapısı PR açılması veya repo sahibi tarafından workflow dispatch edilmesi sonrasında doğrulanabilir.

## Kodla karşılaştırma (13 Ekim 2026)

Bu çalışma MVP veya üretim hazır olduğu anlamına gelmez. Faz bazında güncel durum ve kalan çıkış işleri Bölüm 9'dadır; ayrıntılı uygulama/doğrulama kaydı [geliştirme durum belgesinde](COURSE_MANAGEMENT_IMPLEMENTATION_STATUS.md) tutulur.

13 Ekim P8 tarama/telemetry smoke doğrulaması: Course Management production overlay'i kendi ClamAV 1.5.4 servisini ve imza volume'ünü kullanır; ortak `clamav` servisi ve onu kullanan ürünler değiştirilmedi. Resmî release notları sürüm pinini destekler. Contract testi ve birleşik Compose config kontrolü geçti; production overlay servisi `up --wait` healthcheck ve `clamdscan --ping` ile `PONG` verdi. Bu turda `course-management-clamav.smoke.ps1` exit 0 ile tamamlandı: E2E EICAR PDF reddi/temiz PDF kabulünü, 26 migration'ın uygulanıp no-op tekrarını, DB restore sayımlarını ve attachment SHA-256 manifestini doğruladı; disposable container'lar temizlendi. `course-management-otel.smoke.ps1` de exit 0 verdi; 3 trace, 128 Course Management metric serisi ve Collector export hatası olmadığını doğruladı, geçici container/network temizlendi. CI aynı smoke testlerini çalıştırıyor; hosted sonuç henüz yoktur. Ayrı tarayıcı 1.5 GiB RAM/1 CPU'ya kadar kaynak kullanabilir; host kapasite onayı toplam yükü hesaba katmalıdır. Son foundation contract bu çalışma ağacında 9/11: aynı iki başarısız kapı korunan ürün yollarına eklenmiş 26 dosyayı ve `coaching-institution-student-report.component.html` hash farkını bildiriyor. Bu dosyalar değiştirilmedi ve baseline yenilenmedi.

| Alan | Kodda bulunan | Eksik veya doğrulanmamış kapsam |
|---|---|---|
| P0 — karar ve kontratlar | Ürün sınırı, veri sahipliği, korunan alanlar, rol/izin matrisi ve kaynak-hedef veri akışı belgelenmiştir. Öğrenci katılımı, veli portalı, seçili aktarım, temel iptal/telafi, finans MVP'si; kurum öğretmeni için dar grup/öğrenci/veli sınırı; bağımsız öğretmenin Owner yetkisi ve yalnız abonelik olaylarını gösteren SystemAdmin audit kapsamı ürün sahibi tarafından karara bağlandı ve ilgili API/UI kabul testleriyle doğrulandı. Identity değerlendirme ekranının birleşik sonucu P0 erasure kontrolüne bağlandı. | Yalnız eski kaynak API'lerinin sunmadığı ek alanların aktarımı, KVKK/saklama, retention/purge, SLO/RPO/RTO ve öğrenci uygulaması production host/erişilebilirlik kararları açık. Abonelik dışı admin kullanıcı/üyelik erişimi MVP dışında bırakılmıştır. |
| P1 — temel | Ayrı API/DB modeli, Identity ürün erişimi, Gateway yolu, öğrenci uygulaması, personel modülü ve admin toplu operasyon ekranı var; migration zinciri izole PostgreSQL'e uygulandı ve API hazır olma/entegrasyon akışı doğrulandı. Personel oturum seçimi sekme başına tutulur; kurs refresh cookie/path'i ve MFA/login/refresh/revoke yolları eski ürün oturumlarından ayrıdır. Admin production bundle da geçti. Kurs Yönetimi staff workspace/enrollment bileşenleri yalnız ilgili erişim veya katılım akışında `@defer` ile yüklenir. | Üretim ortamına deploy/rollback, production backup/restore ve eski ürün regresyon matrisi açık. |
| P2 — katılım | Kurum/bağımsız öğretmen çalışma alanı, profil, üyelik, hedef kullanıcı kimliğiyle davet ve kabul/üyelik iptali var. Öğrenci daveti oluşturulurken owner aynı çalışma alanındaki etkin ve hesaba bağlanmamış yerel StudentRecord kaydını açıkça seçebilir; e-posta otomatik eşleştirmesi yapılmaz. Seçim yokken aynı e-postalı yerel kayıt varsa kabul 409 ile durur; eşleşme yoksa yeni kayıt açılır. | E-posta/SMS davet teslimi, self-servis hesap kaydı, ilk sürüm veli portalı ve legacy hesap/üyelik/rol aktarımı yoktur. P7 öğrenci roster aktarımı yalnızca yerel öğrenci operasyon kaydı oluşturur; mevcut hesap veya üyelik kazandırmaz. |
| P3 — öğrenci ve kurs kayıtları | Hesaplı/hesapsız öğrenci, kurs/grup kaydı, arşiv ve yerel veli bağlantısı var. Satır önizlemeli/idempotent CSV yalnız öğrenci operasyon kaydı oluşturur; kurs/gruplar panel/API'den tanımlanır. Koçluk ve Hızlı Okuma kaynaklarından öğrenci roster'ı owner'ın seçtiği alanlarla önizlenip idempotent aktarılır; `UserId` boş kalır. PostgreSQL API kabul senaryoları geçti. | P7 kapsamında seçilmiş kaynak öğretmen daveti ile kurum adı/türü aktarımı vardır; doğrulanmış veli hesabı/portal erişimi yoktur. Kaynak öğretmen ataması veya eski ürün yetkileri aktarılmaz. |
| P4 — takvim | Tekil/haftalık plan, taşıma, katılımcı görüntüsü, çakışma/kapasite kontrolleri, seri düzenleme/iptal, yoklama/tamamlama, uygunluk/izin, temel şube/derslik ve telafi kredisi uygulandı. PostgreSQL API ve Chrome personel/öğrenci panel kabul senaryoları geçti. | Kurumlar/kişisel alanlar arası takvim çakışması MVP dışıdır. Gelişmiş şube/derslik yönetimi MVP sonrası kapsamdır. |
| P5 — finans | Owner yetkili dört model, TRY/Nakit/EFT manuel tahsilat, kısmi dağıtım, borç/tahsilat/paket ters kayıtları, öğrenci ekstresi ve tarih/yöntem bazlı workspace özeti var. Tamamlanmış ders `Absent` olsa da ders/saat ücreti ve paket hakkı tüketilir. PostgreSQL API ve Chrome owner paneli geçti; özet ve detay raporu 400 TRY taksit + 450 TRY saatlik + 350 TRY ders başı ücretini 1.200 TRY gecikmiş bakiye olarak aynı hesaplıyor ve ters kaydı alınmış borcu dışlıyor. Öğrenci yalnız kendi bakiye ve kalan paket hakkı özetini salt okunur görür. Ekstre ve çalışma alanı finans raporları P7'de CSV/yazdırma çıktısı olarak eklendi. | Online ödeme, e-fatura/e-belge, vergi, otomatik prorata ve muhasebe/kâr raporu yoktur. Aylık tutar ile vade kullanıcı tarafından belirlenir. Öğrenci ödeme/taksit hareketlerini görmez. |
| P6 — ödev ve portal | Öğretmen/grup ödevi, teslim/puan/geri bildirim, PDF/JPEG/PNG ekleri ve son teslim tarihinden 180 gün sonra dosya/metadata temizleme (süre ve başlangıç olayı kullanıcı tarafından onaylandı), öğrenci devamsızlık ve yalnız kendi bakiyesi/kalan paket hakkı, ürün-scope'lu notification endpoint'i ve in-app hatırlatma worker'ı uygulandı. Worker `FOR UPDATE SKIP LOCKED` ile çoklu örnekte kayıtları tek kez alır; hatırlatma durumu ve event aynı transaction/outbox'ta yazılır. Ek dosya temizlik worker'ı için kabul testi süresi dolmuş dosya ve metadata'yı sildiğini, süresi geçmemiş eki koruduğunu doğrular; domain testi son teslim tarihine 180 gün eklenmesini doğrular. Ders/ödev zamanları, taksit vade hatırlatması, kullanıcı hesabı bağlanması, öğrenci/öğretmen iptali, reschedule, tam ödeme ve ters kayıt senaryoları PostgreSQL E2E'de geçti. P6 kapanışında o tarihteki 18 migration ikinci kez no-op uygulandı. Kullanıcı onaylı varsayılanlar: ders ve ödev teslimi 24 saat önce; taksit 3 gün önce ve vade günü 09.00 Europe/Istanbul. | E-posta/SMS ve tercih ekranı MVP'de yoktur; kanalı genişletme ayrıca karara bağlıdır. KVKK saklama/export matrisi P0 hukuk onayı bekler. Veli portalı yoktur. 10 MiB ve dosya türleri uygulama varsayılanıdır, ürün sahibi kararı değildir. |
| P7–P8 — yönetim ve pilot | P7: SystemAdmin abonelik yönetimi/audit, Owner plan özeti, eşzamanlı öğrenci kotası, abonelik salt-okunur kuralı, raporlar ve owner-seçimli aktarım uygulandı. P8: ayrı servis/uygulama, migrate-only ve production overlay mevcut; Course Management'e özel sabit ClamAV 1.5.4 ve PostgreSQL smoke'u EICAR/temiz dosya, 26 migration/no-op ve backup/restore sayımlarını doğruladı. Identity değerlendirme ekranıyla aynı readiness aggregate entegrasyonu uygulandı. | Hosted CI/artifact, genel retention/purge ve KVKK dayanağı, production rollback/backup/SLO/RPO/RTO, tarayıcı kaynak yükü, eski ürün regresyon matrisi, pilot ve yayın kararı açıktır. Ortak admin-panel audit seçilen kapsam dışında kalır.

P8 Release yeniden doğrulaması: yerel önbellekle, NuGet güvenlik denetimi kapalı restore sonrasında CI'daki varsayılan paralel `dotnet build ... --configuration Release --no-restore --warnaserror` komutu yükseltilmiş yerel çalıştırmada 0 uyarı/0 hata geçti. Release `dotnet ef migrations has-pending-model-changes` komutu build ederek “No changes have been made to the model since the last migration” sonucunu verdi. Production Compose birleşimi `config --quiet` ile yerel placeholder secret/host değerleri kullanılarak geçti; bu statik yapılandırma kontrolüdür, deployment kanıtı değildir. Bu restore'da NuGet audit kapalıydı; sonrasında Course Management API'nin transitif .NET paketleri için `dotnet list ... package --vulnerable --include-transitive` taraması açık paket bulmadı. Uzaktaki GitHub Actions sonucu hâlâ doğrulanmış sayılmaz. Yerel YARP smoke, untrusted inbound Host ile Course Management rotasının hedef Host'unu kullandığını doğruladı; production hedef ve hostname hâlâ deployment kapısıdır.

Önceki panel checkpoint'i (13 Ekim): öğrenci uygulaması production build'i 214.04 kB ilk paketle geçti; personel portalının 46 dosyadaki 272 testi ve admin panelinin 84 dosyadaki 434 testi geçti. Personel portalı production build'i geçti, ancak o checkpoint'te ilk paket 736.83 kB ile 500 kB bütçesini aşıyordu ve ortak `app.component.scss` 14.38 kB ile 14 kB bütçesini 377 bayt aşıyordu. Admin panelinin o turdaki production build'i Google Fonts için `fonts.googleapis.com` DNS erişimi bulunmadığından tamamlanmadı; development build'i geçti. `--optimization=false` tanılama derlemesi Angular bundle bütçelerini aştığı için başarısız döndü ve production doğrulaması yerine sayılmadı. Daha önceki başarılı production build kaydı bu yeniden denemeden ayrı bir doğrulamadır. O checkpoint'teki foundation sözleşmesi 7/9 geçti; korunan baseline yenileme anındaki HEAD `4f81d70`'e ve 1.544 izlenen dosyaya sabitlendi; release-build snapshot klasörleri geçici envanter dışında bırakıldı. Odaklı fixture testi 1/1 geçti. Branch HEAD'i daha sonra `e00902c7`'ye ilerledi; aradaki commit'ler Coaching personel arayüzü/servisi ve testlerinde korunan kaynak değişiklikleri içeriyor ve baseline'a alınmadı. Tam yerel test, o tarihte workspace'teki dört untracked `clients/speed-reading/shared` yolu ve değişmiş `coaching-institution-workspace.component.html` nedeniyle 7/9 idi; güncel çalışma ağacı sonucu üstteki P8 kaydındadır.

13 Ekim güncel paylaşılan panel doğrulaması: Angular testleri admin panelinde 89 dosya/479 test, personel portalında 47 dosya/290 test ile geçti. `ng build --project=staff-portal --configuration production` 808.88 kB ilk paketle ve `ng build --project=admin-panel --configuration production` 807.44 kB ilk paketle başarılı oldu. Staff portalının Course Management workspace (191.09 kB) ve enrollment (2.38 kB) bölümleri lazy chunk olarak üretildi. Staff paket bütçesi 500 kB'ı, mevcut admin Speed Reading katalog SCSS bileşen bütçesi 4 kB'ı aşıyor; bunlar build uyarısıdır ve bu doğrulamada korunan ürün kaynakları değiştirilmedi.

Kurs öğrenci uygulamasındaki giriş ve MFA, yeni hesap kaydı anlamına gelmez. Uygulama öğrencinin kendi profilini, davetlerini, çalışma alanlarını/kurslarını, yaklaşan ders ve devamsızlık geçmişini, ödev/teslim akışını ve kendi bakiye/kalan paket hakkı özetini sunar. Başka üründeki kayıtların içeriği otomatik kopyalanmaz; ortak hesapla oturum açma tek başına kurs verisi veya yetki aktarmaz.

## 1. Kabul edilen kapsam ve mimari kararlar

- Kurs yönetimi, koçluk ve hızlı okumadan bağımsız üçüncü üründür.
- Öğrenci paneli sıfırdan, ayrı Angular uygulaması olarak geliştirilir: `clients/course-management`.
- Öğretmen ve kurum alanları mevcut `staff-portal` uygulamasına yeni ürün modülü olarak eklenir.
- Merkezi yönetim mevcut `admin-panel` uygulamasına ayrı modül olarak eklenir.
- Backend: `services/course-management-service`; kendi veritabanı, migration, depolama ve mesajları bulunur.
- Identity hesabı ortak kalır; kurs yönetimi profilleri ve çalışma alanı ilişkileri yeni servise aittir.
- Başka üründeki kişi/kurum, yetkili katılım veya aktarım gerçekleştiğinde burada yerel kayıt kazanır.
- Mevcut ürünlerin özellikleri, iş kuralları, kayıtları, API sözleşmeleri ve ekranları değiştirilmez.
- Mevcut bileşenler ortaklaştırılmaz; yeni modül kendi bileşenlerini kullanır. Görsel yaklaşım referans alınabilir.

Varsayımlar: İlk sürüm Türkçe ve TRY odaklıdır; öğretmen ve kurum modları birlikte desteklenir. Öğrencinin giriş hesabı olmadan operasyon kaydı açılabilir. Online tahsilat, resmi e-belge entegrasyonu ve genel muhasebe ilk sürüme dahil değildir. Öğrenci uygulaması mobil uyumlu hazırlanır; native mobil uygulama planlanmaz. Bunlar ürün varsayımlarıdır, uygulanmış kararlar değildir.

## 2. Mevcut ürünleri koruma sözleşmesi

### 2.1 Korunan alanlar

- `services/coaching-service/**`, `services/speed-reading-service/**` ve mevcut migration'ları.
- `clients/speed-reading/**`.
- Personel portalındaki `features/coaching/**` ve `features/speed-reading/**`.
- Admin panelindeki mevcut koçluk, hızlı okuma ve koçluk öğrenci portalı ekranları.
- Mevcut ürünlerin veri aktarımı, üyelikleri, abonelikleri, rol izinleri ve bildirim akışları.
- Ortak CSS, mevcut tasarım değişkenleri, paket sürümleri ve mevcut ekran metinleri; yeni ürünle ilgili ekleme gereken ürün seçici metinleri hariç.

Bu alanlarda yeni ürün gerekçesiyle refactor, yeniden adlandırma veya işlev değişikliği yapılmaz. Yeni geliştirme için mevcut ürün tablolarına sütun eklenmez; eski veriler üzerinde toplu backfill/migration çalıştırılmaz.

### 2.2 Gerekli ortak kayıt noktaları

Tamamen sıfır ortak dosya değişikliği, mevcut panellerde üçüncü ürünün görünmesiyle bağdaşmaz. Kullanıcının kabul ettiği panel genişlemesi kapsamında aşağıdaki noktalara yalnızca yeni ürünü tanıtan eklemeler yapılır:

| Nokta | İzin verilen ekleme | Koruma şartı |
|---|---|---|
| Identity ürün enum/route değerleri | `CourseManagement = 3`, `course-management` | Mevcut enum değerleri, ürün URL'leri korunur |
| Identity giriş, kayıt, erişim, MFA/oturum geçişi | Üçüncü ürüne ait yol ve sözleşmeler | Koçluk/hızlı okuma taleplerinin davranışı değişmez |
| Identity izin kapsamı | Yeni ürün izinlerinin diğer ürünlere sızmasını önleyen sınırlama | Eski ürünlerin mevcut izin kararları korunur |
| Staff portal ürün tipi, seçici ve kök bileşen | Üçüncü ürün ve yeni bileşenin bağlanması | Eski çalışma alanı bileşenleri değiştirilmez |
| Staff davet/bildirim yönlendirme noktaları | Yeni ürün için açık API seçimi | Eski ürünlerin URL ve HTTP metotları korunur |
| Admin rota/menü/izin kayıtları | Yeni kurs yönetimi rotaları ve izinleri | Eski rotalar ve ekranlar korunur |
| Gateway | Yeni `/api/course-management/**` yolu ve cluster | Mevcut route/transform/policy değerleri korunur |
| Solution, Compose, CI ve yayın ayarları | Yeni projeler ve ayrı build/test/deploy hedefleri | Mevcut servis ayarları değiştirilmez |
| Ortak event sözleşmeleri | Ayrı `CourseManagement` namespace'i | Mevcut event tipleri ve tüketicileri değiştirilmez |
| Notification | Yeni ürüne özel tüketici ve şablon kaydı | Mevcut bildirim tipleri/şablonları korunur |
| Identity salt okunur aktarım sözleşmesi | Gerekirse yeni ürüne ait ayrı internal endpoint/DTO | Eski endpoint, profil ve üyelikler değiştirilmez; yetkisiz toplu dizin erişimi verilmez |

Yeni ürüne ait izinlerin eski ürün token'larına eklenmesi engellenir. Mevcut `PlatformPermissionScope` diğer ürünün izinlerini dışlayarak çalıştığından üçüncü üründe bu alan özellikle sözleşme testleriyle korunur. Genel izin sistemini yeniden tasarlamak bu kapsamda değildir.

Yeni modül devre dışıyken eski kullanıcılar için yeni menü, gereksiz istek veya hata oluşmaması hedeflenir. Kurs kullanıcıları dışında yeni ürün erişimi otomatik verilmez.

Kök bileşenin CSS'i yeni modülün stilleriyle değişmez; yeni stiller modül kapsamındadır. Öğrenci uygulamasının session/cookie davranışı mevcut ürünleri oturumdan çıkarmamalıdır; aynı tarayıcıda ayrı sekmelerle eski/yeni ürün kullanımı regresyon senaryosudur. Bağımlılık sürümlerini yükseltmek veya mevcut paket kilidini yeniden çözmek bu geliştirme kapsamına dahil değildir; zorunlu yeni paket ihtiyacı P0'da ayrıca listelenir.

### 2.3 Her teslimin kanıtı

- Korunan dosyalarda diff bulunmaması; ortak kayıt dosyalarının izin verilen eklemelerle sınırlı olması.
- Eski ürünlerin giriş, MFA, ürün geçişi, öğretmen/kurum görünümü ve kritik öğrenci akışlarının geçmesi.
- Eski ürün API'lerinin route, DTO ve izin kararlarının korunması.
- Yeni ürün kapalı ve açık durumlarında personel/admin panellerinin kontrol edilmesi.
- Yeni ürün migration'larının yalnızca yeni veritabanı ile gerekli Identity ürün/izin kayıtlarına etkisi.
- Test öncesi var olan arızaların baseline olarak ayrılması; yeni arızaların teslim edilmemesi.

## 3. Uygulama sınırları ve dosya yerleşimi

```text
services/course-management-service/
  CourseManagement.Domain/
  CourseManagement.Application/
  CourseManagement.Infrastructure/
  CourseManagement.API/

clients/course-management/                         # yeni öğrenci uygulaması
clients/admin-panel/projects/staff-portal/src/app/features/course-management/
clients/admin-panel/src/app/features/course-management/ # admin toplu operasyon özeti
shared/EduPlatform.Shared.Contracts/Events/CourseManagement/
services/notification-service/                     # kurs event consumer ve notification API
services/identity-service/Identity.Application/Commands/JoinCourseManagement/

# Mevcut test yolları
tests/Unit/CourseManagement.Application.UnitTests/
tests/Integration/Identity.API.IntegrationTests/       # Identity katılım/auth regresyonları
tests/E2E/course-management-*.test.mjs
tests/E2E/course-management-migration.smoke.ps1
```

Yukarıdaki yollar kodda kullanılan mevcut yerleşimi gösterir. Canlı PostgreSQL'e bağlanan HTTP API kabul senaryoları `tests/E2E/course-management-auth.integration.test.mjs` içinde çalışır; `tests/Integration/CourseManagement.IntegrationTests` ise gerçek PostgreSQL/RabbitMQ ile erasure, consumer/outbox, bildirim ve metrik servis entegrasyonlarını kapsar. Sürümlü event kontratları, EF bus outbox, reminder worker ve Notification tüketicileri/endpoint'i uygulanmıştır. P7 import ekranı, mevcut Coaching ve Speed Reading erişim tokenlarıyla seçilen kaynağın yetkili roster uçlarını okur: öğrenci roster'ı, Coaching `GET /api/coaching-admin/institutions/{institutionId}/teachers` veya Speed Reading'in `role=Teacher` üyelik filtresi ve Identity `GET /api/institutions` kurum listesi. Genel profil exportu yoktur ve kullanılmaz. P7 dışında generic profil/kayıt aktarımı yoktur; yeni API/DB'nin iki eski servisle doğrudan backend bağımlılığı kurulmaz, kaynak okuma owner tarafından başlatılan istemci akışıdır.

Kendi `course_management_db` veritabanı kullanılır. Aynı PostgreSQL sunucusunda ayrı veritabanı olabilir; uygulama kullanıcısının diğer ürün veritabanlarına yetkisi olmaz. Dosyalar ayrı storage alanında tutulur. Önbellek anahtarları, mesaj kuyrukları ve metrikler ürün adıyla ayrılır.

## 4. Kimlik, profil, çalışma alanı ve roller

### 4.1 Veri sahipliği

| Veri | Sahibi |
|---|---|
| Kullanıcı hesabı, parola, MFA, doğrulanmış giriş adresi, oturum | Identity |
| Ürün erişimi ve ürün kapsamlı personel rolü | Identity |
| Kurs öğrenci/öğretmen/veli profili | CourseManagement |
| Kurumun kurs yönetimi profili, şube ve derslikleri | CourseManagement |
| Ortak kurum kimliği mevcutsa referansı | Identity referansı; yerel operasyon profili CourseManagement |
| Çalışma alanı üyeliği, yerel rol ve kayıt erişimi | CourseManagement |
| Kurs/grup kaydı, ders, yoklama, ödev ve finans | CourseManagement |

Ortak hesap ayarları ile ürün profili ayrı ekranlardır. Yeni ürün profili güncellemesi Identity'deki hesap veya diğer ürün profillerini güncellemez. Doğrulanmış hesap e-postası ile kurumun kullandığı operasyon iletişim adresi ayrı anlamlar taşır.

### 4.2 Çalışma alanı

`Workspace` türleri: `Institution`, `IndependentTeacher`.

Kullanıcı birden fazla çalışma alanına katılabilir. Kurum çalışanı olan öğretmen ayrıca kişisel alan açabilir. Her istek ürün erişimini, çalışma alanı üyeliğini, işlemin iznini ve hedef kaydın kapsamını sunucuda doğrular. İstemcinin gönderdiği `workspaceId` tek başına yetki sağlamaz.

Mevcut MVP çalışma alanı rolleri `Owner`, `Teacher` ve `Student`'tır. Bağımsız öğretmen, `IndependentTeacher` tipindeki kendi çalışma alanının `Owner`'ıdır. `Guardian` Identity rolü veya portal erişimi değildir; yalnızca yerel iletişim kaydı olabilir. Ayrı kurum yöneticisi/sekreter/finans görevlisi rolleri tanımlanmamıştır. Platform `SystemAdmin` toplu operasyon sayılarını ve sayfalı plan listesini görür, çalışma alanı planını atayıp güncelleyebilir ve yalnız abonelik atama/güncelleme audit'ini okur; daha geniş kullanıcı/üyelik desteği yetkisi verilmez.

| Rol | Temel yetki |
|---|---|
| Owner (kurum/bağımsız öğretmen) | Çalışma alanı, üyelik ve kendi alanının operasyonları |
| Öğretmen | Atandığı öğrenciler/dersler, yoklama ve ödev |
| Öğrenci | Kendi dersleri, ödevleri, paketleri ve izin verilen bilgiler |
| Veli | Portal/hesap erişimi yok; yalnız yerel iletişim kaydı |
| Platform yöneticisi | SystemAdmin toplu sayılar, abonelik planı yönetimi ve salt okunur abonelik atama/güncelleme audit'i; kullanıcı kimlikleri, kişi/ödev/finans ayrıntısı, impersonation veya abonelik dışı destek mutasyonu yok |

Sekreter ve finans görevlisi sonraki fazda yerel izinlerle eklenebilir. MVP'de sırf bu roller için eski personel erişim politikasının kapsamı genişletilmez. Admin'in kişi/ödev/finans kaydına erişmesi veya destek amaçlı işlem yapması ayrı ürün kararı ve audit tasarımı gerektirir; mevcut admin endpoint'i bu yetkileri vermez.

Yeni kurum ve bağımsız öğretmen için self-servis katılım akışları ayrıca bulunur; yalnızca davetli kullanıcı desteklenmez. Mevcut hesap yeni ürüne katılır, tamamen yeni kişi Identity hesabı açar; her iki durumda yerel ürün profili ve çalışma alanı oluşur. Ürün rolü portala giriş sağlar; çalışma alanındaki izinler yerel üyelikten çözülür. Son üyelik kaldırıldığında yerel erişim kapanır; diğer ürün erişimleri iptal edilmez. Identity hesabı pasifleştirilirse yeni ürün erişimi de geçersizleşir; bu yaşam döngüsü mevcut ortak güvenlik sözleşmesiyle ve ayrı yeni ürün testleriyle doğrulanır.

### 4.3 Hesapsız öğrenci

Yerel öğrenci kaydı `StudentRecordId` ile oluşturulur; `IdentityUserId` başlangıçta boş olabilir. Kurs kaydı, ödeme ve yoklama bu yerel kayıtla çalışır. Portal daveti kabul edilince doğrulanmış hesap bağlanır. Ödev öğrenciye atanabilir; portal üzerinden teslim için hesap bağlantısı gerekir.

Bir veli birden fazla öğrenciye bağlanabilir. Veli olmak ve ödeme sorumlusu olmak ayrıdır. Aynı kişinin farklı çalışma alanlarındaki borçları, notları ve ilişkileri birbirine açılmaz.

## 5. Ürünler arası katılım ve aktarım

1. Ortak hesaba giriş veya doğrulanmış davet kabulü.
2. Kurs yönetimi ürün erişiminin oluşturulması/doğrulanması.
3. Yeni üründe yerel profil oluşturulması.
4. Davetteki çalışma alanı ve rol için yetkili üyelik oluşturulması.
5. Yalnızca ihtiyaç duyulan ve aktarımı yetkilendirilmiş alanların alınması.
6. Kaynak, aktör, zaman ve eşleştirme sonucunun kaydedilmesi.

Başka üründe kayıtlı olmak otomatik katılım veya yetki vermez. Ad-soyad veya benzer e-posta üzerinden hesap birleştirilmez. Öğrenciler ve öğretmenler ortak `UserId` ile doğrulanır; kurum eşleştirmesi ortak kurum kimliği ve yetkili yönetici doğrulamasıyla yapılır.

İlk sürüm tek kişinin katılımını ve mevcut hesapla davet kabulünü destekler. P3 CSV içe aktarma yalnızca yeni üründeki yerel öğrenci operasyon kayıtları içindir; kurs/gruplar panel/API'den tanımlanır ve CSV Identity hesabı veya ürün yetkisi oluşturmaz. P7'de owner'ın kaynak üründeki yetkisiyle öğrenci roster'ı ayrı akışta seçilir, önizlenir ve aktarılır; bu işlem mevcut hesap veya üyeliği bağlamaz. P0 alan matrisi öğrenci roster'ı, mevcut Identity userId'siyle Teacher davetini ve kurum ad/türü aktarımını ayrı sözleşmeler olarak tanımlar; diğer kaynak alanlar açık kalır. Mevcut Coaching/Speed Reading servislerine export endpoint'i eklenmez; aktarım mevcut yetkili roster API'lerini çağırır. Yalnızca kaynak profilde olup bu API'lerden verilmeyen alanlar aktarılmaz; isteğe bağlı operasyon alanları P3 CSV veya doğrulanmış manuel girişle tamamlanır. Temel ortak kimliğin bağlanması CSV'ye bırakılmaz.

Aktarım alan matrisi P0 teslimidir: kaynak alan, veri sahibi, kullanılacak endpoint, yetki kapsamı, hedef alan, kullanıcıya gösterilen aktarım seçimi ve eksik veri davranışı. P2 kabul koşulu mevcut kurum, öğretmen ve öğrencinin ortak kimliğiyle yeni ürüne katılabilmesidir. P7 öğrenci roster kabulü, owner'ın öğrenci kayıtlarını seçip önizlemesi, seçilen alanlarla idempotent yerel kayıt oluşturması ve `UserId` alanını boş bırakmasıdır; öğretmen üyeliği/rol ve kurum metadata aktarımı ayrı kapsam ve onay gerektirir. Mevcut kurum üyelikleri diğer ürünlerden taşınmaz veya silinmez; burada yeni yerel üyelikler ancak açık davet/kabul ile oluşturulur.

Kod incelemesinden çıkarılan ilk alan eşleme taslağı (ürün sahibi onayı değildir):

| Kaynak ve yetki | Mevcut kaynak alanları | Kurs hedefi için aday eşleme | Sınır / açık karar |
|---|---|---|---|
| Identity [`InstitutionsController`](../services/identity-service/Identity.API/Controllers/InstitutionsController.cs), `GET /api/institutions/{id}` (`Institutions.View`, MFA; SystemAdmin genel, diğer yöneticiler etkin ürün kapsamındaki kendi kurumuyla sınırlı) | [`InstitutionDto`](../services/identity-service/Identity.Application/DTOs/InstitutionDtos.cs): `Id`, `Name`, adres/iletişim, lisans/kota, aktiflik ve sayımlar. | `Name` → `Workspace.Name`; kaynak `Id` ayrı aktarım eşlemesinde kaynak anahtarı olabilir. | `Workspace` kaynak kurum kimliği veya adres/iletişim alanı tutmaz. `OwnerUserId` kurum DTO'sundan çıkarılamaz; oturum sahibinin kurum sahipliği ayrıca doğrulanıp açıkça seçilmelidir. Lisans, kota, sayım ve iletişim alanları aktarılmaz. |
| Identity [`InstitutionController`](../services/identity-service/Identity.API/Controllers/InstitutionController.cs), `GET /api/institution/students` (`InstitutionAdmin`/`InstitutionOwner`, MFA; handler mevcut Coaching kurumunu çözer) | [`InstitutionStudentRosterItem`](../services/identity-service/Identity.Application/Interfaces/Repositories.cs): `UserId`, `FirstName`, `LastName`, `Email`, `GradeLevel`, `IsActive`, `LastLoginAt`, `CreatedAt`, `TeacherUserId`, `TeacherName` | `FirstName` + `LastName` → `StudentRecord.DisplayName`; `Email` → `StudentRecord.Email`; `IsActive` → `StudentRecord.IsActive` yalnız seçim verilirse. | Kaynak endpoint telefon döndürmez. Öğretmen ataması kurs `CourseGroup` üyeliğiyle aynı sözleşme değildir. `UserId` seçimi öğrenci erişimini etkiler ve aktarımın kendisiyle doğrudan bağlanmamalı; davet/katılım kararı ayrıca uygulanmalıdır. Bu liste öğretmen roster'ı değildir; `TeacherUserId` yalnız öğrenci atamasını gösterir. |
| Coaching [`CoachingAdminController`](../services/coaching-service/Coaching.API/Controllers/CoachingAdminController.cs), `GET /api/coaching-admin/institutions/{institutionId}/teachers` (`Coaching.View`, MFA; kurum admini yalnız kendi `RequireReadScope` kurumunu okuyabilir) | [`CoachingTeacherReportPage`](../services/coaching-service/Coaching.Application/Interfaces/ICoachingIdentityReportClient.cs): `UserId`, `FirstName`, `LastName`, `Email`; Identity internal report client yalnız etkin öğretmenleri döndürür. | Owner tek tek seçtiği `UserId` için yeni workspace'te `Teacher` daveti açar; ad/e-posta yalnız seçim önizlemesinde gösterilir, kaynak profil/rol/atamalar kopyalanmaz. Davet kabulü üyeliği etkinleştirir. | Roster sayfalıdır; P7 sayfa boyutu 100 kullanır. Route ve Identity rapor kontratı eski ürünlere bu Course Management kapsamı için eklenmemiştir; kaynak read path'i bu çalışma alanında mevcut olan uçtur. |
| Speed Reading [`SpeedReadingInstitutionMembersController`](../services/speed-reading-service/SpeedReading.API/Controllers/SpeedReadingInstitutionMembersController.cs), `GET /api/speed-reading/institutions/{institutionId}/members` (`SystemAdmin`/kurum admini-owner, `Institutions.Manage`, MFA; sayfa başına en çok 100) | [`SpeedReadingInstitutionMemberView`](../services/speed-reading-service/SpeedReading.Application/Institutions/SpeedReadingInstitutionMembershipContracts.cs): `UserId`, ad/soyad/e-posta, rol ve aktiflik, `GradeLevel`, `TeacherUserId`/`TeacherName`, `CurrentLevel`, `TargetWpm`, `TargetComprehension`, `DailyGoalMinutes`, `LearningStyle`, `StudentCount` | Ad + soyad → `DisplayName`; e-posta → `Email`; seçilirse üyelik aktifliği → `IsActive`. | Okuma seviyesi, hedefler, öğrenme stili, kaynak öğretmen ve öğrenci sayısı mevcut `StudentRecord` hedefinde karşılık bulmaz; aktarım için ayrıca tasarlanıp onaylanmadıkça kapsam dışıdır. |
| Speed Reading [`SpeedReadingInstitutionProfileController`](../services/identity-service/Identity.API/Controllers/SpeedReadingInstitutionProfileController.cs), `GET /api/institution/speed-reading/me` (`InstitutionAdmin`/`InstitutionOwner`, `Institutions.Manage`, MFA) | `CurrentSpeedReadingInstitutionResponse`: `InstitutionId`, `InstitutionName`. | Kurum yöneticisinin kaynak kurum seçimini doğrulamak için aday; `InstitutionName` → `Workspace.Name` yalnız Identity kurum DTO'suyla çelişmiyorsa ve kullanıcı onaylarsa. | Yalnız Speed Reading kurumunu döndürür; genel kurum aktarım API'si değildir. Kullanıcının mevcut `course-management` tokenı bu kaynak yetkisinin yerine geçmez. |
| Speed Reading [`SpeedReadingInstitutionMembersController`](../services/speed-reading-service/SpeedReading.API/Controllers/SpeedReadingInstitutionMembersController.cs), `GET /api/speed-reading/institutions/{institutionId}/members` (`SystemAdmin`/kurum admini-owner, `Institutions.Manage`, MFA; sayfa başına en çok 100; `role=Teacher` filtresi desteklenir) | [`SpeedReadingInstitutionMemberView`](../services/speed-reading-service/SpeedReading.API/Controllers/SpeedReadingInstitutionMembersController.cs): `UserId`, ad/soyad/e-posta, `Role`, üyelik/aktiflik, `GradeLevel`, `TeacherUserId`/`TeacherName`, okuma hedefleri ve öğrenci sayısı. | Ad + soyad → `CourseUserProfile.DisplayName`; açık davet kabulinden sonra `UserId` üyelik hedefi, `Role=Teacher` → `WorkspaceMembership.Role=Teacher`. `Role=Student` üyelikleri öğrenci kartı aktarımından ayrı tutulur. | Course erişimi, profil veya üyelik otomatik oluşturulmaz; kaynak `IsActive` tek başına kurs erişimi vermez. Okuma seviyesi/hedef/stil ve kaynak öğrenci-öğretmen ataması kurs grup kaydı değildir. |
| Course Management [`StudentRecord`](../services/course-management-service/CourseManagement.Domain/StudentRecord.cs) ve [`LearningEndpoints` P3 CSV](../services/course-management-service/CourseManagement.API/LearningEndpoints.cs) | Hedefte `UserId?`, `DisplayName`, `Email?`, `PhoneNumber?`, `IsActive`, `WorkspaceId`; P3 import yalnız `DisplayName`/`Email`/`PhoneNumber` oluşturur ve `UserId` boş bırakır. | Mevcut hedefe yalnız seçilip önizlenen alanlar yazılabilir; kaynağı ve kaynak kaydını gösteren `ImportMapping` ayrı idempotency/eşleme anahtarıdır. | P3 CSV, P7 kaynak aktarımı değildir. E-posta/ad benzerliğiyle hesap eşleme yapılmaz; P2 davet kabulinde owner açık `StudentRecordId` seçer; e-posta/ad otomatik eşleştirmesi yapılmaz. |
| Identity `GET /api/users/bulk/export` | SystemAdmin + MFA + `Users.View` gerektiren genel kullanıcı dışa aktarımı. | Kurum owner aktarımına doğrudan kaynak olarak önerilmez. | Kurum üyeliği bağlamı ve alan seçimi bu route'un yetki sözleşmesinde yoktur; kullanılacaksa ayrı bir admin yetki kararı gerekir. |

P7 öğrenci roster aktarımının alan sözleşmesi uygulanmıştır: displayName zorunlu; email ve isActive owner tarafından seçilir; seçilmeyen e-posta hedefe gönderilmez, seçilmeyen aktiflik varsayılanı etkin kayıttır. phoneNumber, sınıf, kaynak öğretmen ataması, öğrenme/ilerleme metrikleri ve Identity UserId öğrenci profiline aktarılmaz. Kaynak API'den alınan SourceRecordId yalnız workspace+ürün kapsamlı idempotency eşlemesine yazılır; hedef StudentRecord.UserId null kalır. Öğretmen aktarımında owner mevcut Coaching/Speed Reading roster'ından kişileri seçer, Identity userId ile pending Teacher daveti oluşturur; kabul öncesi üyelik açılmaz. Kurum aktarımı owner'ın seçtiği kurumun yalnız `name`/`type` alanlarını önizleyip mevcut workspace'e yazabilir; yeni workspace, kaynak üyelik veya diğer profil alanları taşınmaz. Kaynak API kullanımı mevcut ürünlerin route/DTO/izin kurallarını değiştirmez.

P3 CSV önizlemesi satır bazlı hata ve yinelenen operasyon kaydı kontrolünü, güvenli tekrar çalıştırmayı sunar. P7 legacy aktarım önizlemesi ayrıca seçilen kaynak alanları/kişileri gösterir; `ImportMapping` kaynak sistem/kayıt/çalışma alanı eşleştirmesini saklar. Ders geçmişi, borç, koçluk özel notları ve hızlı okuma sonuçları varsayılan olarak aktarılmaz. Yerel profil sonraki düzenlemelerde kaynak ürüne yazılmaz; sürekli çift yönlü senkronizasyon yoktur.

Mevcut P3 öğrenci CSV'si sabit `displayName`, isteğe bağlı `email` ve `phoneNumber` başlıklarıyla yalnız yerel `StudentRecord` oluşturur; Identity hesabı veya rol bağlamaz. P7 öğrenci/öğretmen/kurum akışları staff portalın seçilen Coaching veya Speed Reading ürün tokenıyla mevcut yetkili roster/kurum uçlarını okur; kaynak servislerin route/DTO/izin kapsamını değiştirmez. Öğrenci `SourceRecordId` idempotency eşlemesinde tutulur, fakat `UserId` hedef öğrenci kaydına yazılmaz. Öğretmen için yalnız mevcut Identity `UserId` ile pending davet oluşturulur; kurumda seçili ad/tür mevcut workspace'e aktarılır. Otomatik ad/e-posta hesap eşlemesi veya kaynak ürünlere yazma yoktur.

Identity ve yeni servis arasında ortak veritabanı transaction'ı kurulmaz. Katılım işlemi durum kaydı ve idempotency ile tekrar denenebilir. Identity erişimi oluşup yerel profil oluşturulamazsa kullanıcıya katılımın tamamlanmadığı gösterilir; retry eksik adımı tamamlar, eski üyelikleri silmez.

## 6. MVP fonksiyonları ve veri modeli

### 6.1 Öğrenci ve kurs kaydı

- Öğrenci kartı, veli bağlantısı, aktif/pasif operasyon durumu.
- Kurs/program ve grup tanımı; özel ders gruba ihtiyaç duymadan açılır.
- Bir öğrencinin birden fazla kaydı ve öğretmenle ilişkisi.
- Kayıt tarihleri, dönem, ücret anlaşması ve ödeme planı.
- Kayıt durumları: taslak, aktif, dondurulmuş, tamamlanmış ve iptal. Grup kapasitesi ve kayıt tarihleri doğrulanır; kurs kaydı sona erse de geçmiş korunur.
- MVP'de temel şube/derslik tanımı, isteğe bağlı derslik kapasitesi ve dersin şube/derslik bilgisini takvimde gösterme bulunur. Ders planında derslik kapasitesi ve oda saat çakışması denetlenir. Şube bazlı filtreler/yetkiler, franchise hiyerarşisi ve şubeler arası finans konsolidasyonu sonraki fazdadır.
- Kurum içi notlar ile öğrenci/veli görünür geri bildirim ayrı tutulur.

Varlıklar: `Workspace`, `Membership`, `StudentProfile`, `TeacherProfile`, `StudentRecord`, `GuardianLink`, `Course`, `Group`, `Enrollment`, `Branch`, `Room`.

### 6.2 Takvim ve yoklama

- Tek ders ve tekrarlayan ders serisi; kurum, öğretmen ve öğrenci görünümü.
- Öğretmen/oda/grup/öğrenci için tanımlanan çakışma kuralları.
- Serinin tek oturumunu ayrı iptal etme ve kalan gelecek oturumları topluca iptal etme uygulanmıştır. Haftalık serinin kalan planını düzenleme de uygulanmıştır. İleri tarihli, yoklamasız, seri dışı tek ders sahip veya atanmış öğretmen tarafından taşınabilir; sunucu yeni zamanı, katılımcı/öğretmen/izin/çalışma saati/derslik çakışmasını ve kapasiteyi tekrar denetler. Seri oturumu seri düzenleyicisinden değiştirilir. Telafi politikası seçilip uygulanmıştır: kurum/öğretmen iptali etkin öğrenciye 30 günlük kredi verir; öğrencinin 24+ saat önce iptali onay bekleyen kredi üretir; geç iptal/no-show üretmez.
- Ders durumları: planlandı, tamamlandı, iptal edildi; erteleme eski/yeni oturum bağlantısıyla izlenir.
- Öğrenci yoklaması: katıldı, katılmadı, mazeretli, geç katıldı.
- Öğretmen için haftalık çalışma aralıkları ve tekil izin istisnaları; çalışma aralığı tanımlanmamış öğretmenlerde kısıt uygulanmaz. Aralık değişikliği mevcut dersleri iptal etmez. Resmi tatil takvimi ve dönemsel geçerlilik sonraki adımdır.
- İptal ve telafi kayıtları; geçmiş tamamlanmış dersler yeniden yazılmaz.

Varlıklar: `LessonSeries`, `LessonSession`, `LessonParticipant`, `Attendance`, `Availability`, `MakeupCredit`.

Zamanlar UTC olarak saklanır. Haftalık seri yerel başlangıç saatiyle ve açık IANA saat dilimiyle tanımlanır; çalışma alanı için varsayılan saat dilimi ayarı henüz yoktur. Yaz saati geçişinde var olmayan veya iki kez oluşan yerel başlangıç saatleri seri oluşturmayı durdurur. Eşzamanlı ders ve izin kayıtlarında çakışma yalnızca ekrandan kontrol edilmez; sunucuda serializable transaction ile yarış koşulu engellenir.

İlk P4 diliminde tekil oturum, haftanın seçilen günlerinde tekrarlayan grup/bire bir seri, grup dersinde o anki etkin kayıtların katılımcı anlık görüntüsü ve bire bir öğrenci oturumu desteklenir. Seri en fazla 180 gün sürer ve oluşturma isteğinde IANA saat dilimi belirtilir; takvim seri saatlerini aynı bölgede gösterir. Üretilen oturumlar ayrı iptal edilebilir; gelecek planı bitiş tarihi, günler, başlangıç saati, süre ve derslik yönünden düzenlenebilir. Başlamamış planlı oturumlar yeniden hesaplanır; eşleşen başlangıçlar mevcut kaydı korur, değişen başlangıçlar eski oturumu iptal edip yeni oturum ekler. Geçmiş, tamamlanmış ve başlamış oturumlar değişmez. Başlamamış ve yoklaması olmayan seri dışı ders taşınabilir; seri oturumu seri düzenleyicisinden değiştirilir. Öğretmen/öğrenci çakışmaları yalnızca seçili çalışma alanının takvimi içinde denetlenir; kurumsal ve kişisel takvimler birbirine açılmaz. Zamanlar seri hale getirilebilir işlem içinde doğrulanır. Personel dersi başlamadan iptal edebilir, öğrenci kendi yaklaşan planlı derslerini görür. Ders başladıktan sonra her katılımcı için katıldı/katılmadı/mazeretli/geç kaldı yoklaması girilir; bitiş saati geçmiş ve tüm katılımcıları işaretlenmiş ders tamamlanabilir. Tamamlanan dersin yoklaması değiştirilemez. Öğretmen haftalık çalışma saatleri ve izin aralıklarıyla, oda ise şube içindeki planlı derslerle çakışma denetimindedir; seçili odanın kapasitesi de planlanan katılımcı anlık görüntüsünü karşılamalıdır. Temel şube/derslik tanımı, seçim ve takvimde gösterim MVP kapsamındadır. Şube bazlı yetki/filtre, şube-derslik düzenleme/silme ve franchise kapsamı MVP sonrasına bırakılmıştır. İptal/telafi kredisi etkin katılımcı anlık görüntüsüne göre üretilir; öğrenci erken iptalinde sadece kendisine ait telafi talebi oluşur. Kredinin kullanılabilmesi için 30 gün içinde öğretmen onayıyla telafi dersi planlanmalıdır.

Kişisel ve kurumsal takvimler MVP'de ayrı kalır. Ürün genelinde veya harici takvimler arasında müsaitlik paylaşımı daha sonra, ders ayrıntısını açmadan eklenir. MVP kurum dışındaki takvimleri kapsayan çakışmasızlık garantisi vermez.

### 6.3 Paket ve finans

- Dört temel ücret modeli: dönemlik/taksitli anlaşma, aylık sabit ücret, ders/saat başına ücret ve adet bazlı özel ders paketi.
- Aylık modelde otomatik prorata yoktur; owner aylık tutarı ve her vade tarihini tanımlar. Aylık vadeler tam aylık tutardır. Ders/saat modelinde tamamlanmış ders ücretlendirilir; ders süresi ve oran anlaşmada saklanır. Tamamlanmış derste `Absent` yoklaması da ders/saat ücreti doğurur. Tutarlar TRY ve iki ondalık basamakla tutulur. Geçmiş borçlar güncel fiyat değişikliğiyle yeniden hesaplanmaz.
- Paket geçerlilik süresi ve kalan hak, para bakiyesinden ayrı hareket defterinde tutulur. Tamamlanan ders, yoklama `Absent` olsa dahi bir hak tüketir; hatalı tüketim silinmez, tekil ters hareketle geri alınır.
- Nakit ve EFT/havale manuel tahsilat kaydı; ilk sürümde online ödeme veya fatura/e-belge yoktur.
- Kısmi tahsilat, bir tahsilatın birden fazla taksite dağıtılması.
- Düzeltme, ödeme iptali ve iade hareketleri kaydı silmeden ters kayıtla yapılır; indirim otomasyonu ayrı kapsamdır.
- Öğrenci hesap ekstresi ve owner finans özeti: toplam borç/tahsilat/bakiye, vadesi geçen, gelecek 30 gündeki borç ve tarih aralığında ödeme yöntemi bazlı net tahsilat.
- Öğrencinin ders hakkı ile para bakiyesi ayrı tutulur.

Uygulanan MVP varlıkları: `FeeAgreement`, `Installment`, `Payment`, `PaymentAllocation`, `PackageMovement`. Ayrı ödeme sağlayıcısı, tahakkuk kural motoru, vergi/fatura, indirim ve muhasebe defteri bu kapsamda yoktur.

Tutarlar decimal ve TRY olarak saklanır. Finans endpoint'leri yalnızca çalışma alanı owner'ına açıktır; kurum öğretmeni ve öğrenci erişimi 404 ile reddedilir. Finansal hareketler silinerek düzeltilmez; yetkili ters hareket oluşur. Toplam tahsilat dağılımı tahsilatı ve taksit kalanını aşamaz. Paket kullanımı ve ders tahakkuku ders tamamlama akışında idempotenttir. Tahsilat, borç ve paket ters kayıtları asıl hareketi korur. Raporlarda borç, tahsilat, kalan alacak ve paket hakkı ayrı gösterilir; tahsilat muhasebesel gelir/kâr olarak sunulmaz.

İlk sürümde genel kural motoru yerine aşağıdaki sabit politika uygulanır: kurum/öğretmen iptalinde etkin her öğrenciye 30 gün geçerli telafi kredisi; öğrencinin ders başlangıcından en az 24 saat önce yalnızca kendi katılımını iptali halinde öğretmen onayı bekleyen kredi; 24 saatten az süre kala iptal veya gelmeme halinde kredi yoktur. Öğretmen krediyi onaylayıp telafi oturumunu planlar. Bir telafi kredisi yalnızca bir kez kullanılabilir; tamamlanmış derslerin geçmişi değişmez. Ders takviminde planlama tek başına borç doğurmaz; finansal sonuç ücret modeli ve gerçekleşen iş olayıyla belirlenir. Aylık sabit ücrette kısmi ay için sistem otomatik prorata yapmaz; tutar/vadeler kullanıcı tarafından belirlenir.

### 6.4 Ödev ve öğrenci portalı

- Öğrenci/gruba ödev, açıklama, son tarih ve öğretmen materyali.
- Öğrenci metin veya dosya teslimi; mevcut dosya filtresi PDF/JPEG/PNG ve en çok 10 MiB'tır. Bu filtre ürün kararı değil geçici uygulama varsayılanıdır.
- Teslim durumu, geç teslim, puan ve geri bildirim.
- Yetkili dosya indirme, tarama/karantina ve saklama süresi.
- Dersler, devamsızlık, kendi toplam bakiyesi ve kalan paket hakkı. Öğrenci ödeme/taksit hareketini görmez.
- Öğrenci için yeni ürünün kendi onboarding, profil ve in-app bildirim ekranları. MVP'de veli portalı yoktur.
- Kullanıcı kararı: ödev ek dosyaları son teslim tarihinden (`dueAt`) 180 gün sonra silinir; üretimde ClamAV taraması uygulanır. Yedek ve log saklaması bu ürün dosyası politikasından ayrıdır.

Varlıklar: `Assignment`, `AssignmentRecipient`, `Submission`, `Attachment`, `NotificationPreference` (tercih ekranı/şeması henüz yok).

Koçluk dosya servisinin uygulaması çağrılmaz; yeni storage namespace'i ve yeni endpoint'ler kullanılır. Fotoğraf/PDF desteği yeni ürünün kendi kontratıdır. MVP'de açık genel sohbet, video konferans veya otomatik ödev değerlendirme yoktur.

## 7. Ekran planı

### 7.1 Yeni öğrenci uygulaması

Önerilen adres: ayrı host veya `/course/` altında ayrı statik uygulama; yayın ortamı kararı hazırlık fazında verilir. Personel ve admin uygulamalarına öğrenci ekranı gömülmez.

- Giriş/mevcut Identity hesabıyla davet kabulü; ürün erişimi ve yerel profil tamamlama. Öğrenci için self-service kayıt yok.
- Ana sayfa: sıradaki dersler, bekleyen ödevler, kalan haklar.
- Ders takvimi ve ders detayı.
- Ödev listesi, detay, teslim ve geri bildirim.
- Devamsızlık ve ders geçmişi.
- Finans özeti: yalnız kendi bakiyesi ve kalan paket hakkı; payment/installment kaydı yok.
- Profil, in-app bildirimler ve hesap ayarlarına geçiş.
- Veli görünümü ilk MVP'de yok.

### 7.2 Personel portalı — yeni üçüncü ürün

Yeni modülde route tabanlı gezinme kullanılır; mevcut ürünlerin sekmeleri/router yapısı değiştirilmez. Boş mevcut router'a yalnızca kurs yönetimi rotaları eklenir; kök ürün seçicisinin yeni modülü göstermesi için küçük kayıt eklenir.

Öğretmen: bugünkü dersler, öğrenciler, takvim, yoklama, ödevler. Kişisel çalışma alanında ayrıca paketler, alacaklar ve tahsilatlar.

Kurum: genel bakış, öğrenci kayıtları, öğretmenler, kurslar/gruplar, şube/derslik, kurum takvimi, yoklamalar, ödevler, ödeme planları/tahsilat, raporlar ve çalışma alanı ayarları.

MVP raporları açıkça şunlardır: aktif kayıt/öğrenci sayısı; gün/hafta öğretmen ve derslik programı; öğrenci/grup devamsızlığı; kullanılmamış ve süresi yaklaşan paket hakları; vadesi gelen/geciken taksitler; tarih/ödeme yöntemi bazlı tahsilat ve iade; öğrenci hesap ekstresi; ödev teslim oranı. P7'de yetkili öğrenci, ders/yoklama, ödev, ekstre ve finans raporları CSV/yazdırılabilir PDF olarak eklendi; doğrudan sunucu PDF üretimi yoktur, tarayıcı yazdırma akışından PDF üretimi doğrulanmıştır. Öğrenci sayısı ve ders yükü raporları öğretmen hakedişi veya kâr hesabı olarak sunulmaz.

Platform aboneliği ayrı bir yönetim ekranıdır ve öğrenci finansından bağımsızdır. SystemAdmin sayfalı çalışma alanı listesinden kurum/bağımsız öğretmen alanını, etkin öğrenci sayısını ve plan alanlarını görüp plan, öğrenci limiti, başlangıç-bitiş ve yönetim durumunu atayabilir. Owner yalnız kendi çalışma alanının bu özetini salt okunur görür. Admin listesi owner kimliği/iletişimi, öğrenci kayıtları ve tahsilat hareketi göstermez; her değişiklik workspace audit'ine yalnız eylem ve zaman olarak yazılır. Atanmış bir planın öğrenci sınırı öğrenci oluşturma, CSV commit, öğrenci davetiyle yeni kayıt açma ve arşivden geri yüklemede eşzamanlı istekleri de kapsayacak biçimde uygulanır; mevcut etkin sayı yeni sınırın üstündeyse plan değişikliği reddedilir. Plan atanmayan alanlar şu an kota ve abonelik erişim filtresi dışındadır. Atanmış plan yalnız `Active` ve `[StartsAt, EndsAt)` aralığında yazmaya izin verir; başlangıç öncesi, bitiş anı/sonrası veya askıya alınmış durumda workspace iş verisi salt-okunur olur. Owner, öğretmen ve öğrenci mevcut bilgileri okuyup dışa aktarabilir; kişisel profil güncellemesi açık kalır; salt-okunur import önizlemesi owner için kullanılabilir. Yalnız SystemAdmin planı yeniden etkinleştirebilir. Bu davranış aktif/askıda/bitmiş plan, kurs ve öğrenci oluşturma, davet, tahsilat, okuma ve profil API testleriyle doğrulandı. Kayıtlar silinmez. Otomatik abonelik satışı, kart tahsilatı ve tahsilat komisyonu MVP dışındadır. Kurumda çalışan öğretmen varsayılan olarak kurum tahsilatlarını göremez. Öğretmen kendi kişisel alanında owner ise o alanın finans yetkisine sahiptir.

Platform aboneliğinin ilk sürümü: admin tarafından öğretmen/kurum planı, aktif öğrenci limiti, başlangıç-bitiş ve erişim durumu yönetimi; kullanıcının kendi planını görmesi. Otomatik abonelik satışı, kart tahsilatı ve tahsilat komisyonu MVP dışındadır. Abonelik bittiğinde kayıtlar silinmez; yetkili geçmiş görüntüleme ve veri dışa aktarma korunur, yeni operasyon oluşturma politikası P0'da sabitlenir.

### 7.3 Admin modülü

Hedef kapsam aşağıdaki yönetim ve destek akışlarıdır. Mevcut uygulama toplu operasyon sayılarına ek olarak sınırlı abonelik yönetimi ve workspace plan listesi sunar; kullanıcı/üyelik erişim desteği, impersonation ve kayıt düzeyinde akademik/finansal destek görünümü yoktur.

- Kurs yönetimine katılan kurumlar ve bağımsız öğretmen çalışma alanları.
- Ürün erişimi/davet destek işlemleri; yerel profil ve üyelik durumu.
- Ürün aboneliği ve limit yönetimi; kurum öğrenci borçlarından ayrı.
- İşletim özeti, başarısız aktarım ve mesaj işlemleri.
- Yeni ürüne ait audit ve destek görünümü.

Abonelik yöneticisi `/admin/subscriptions` ve `/admin/workspaces/{workspaceId}/subscription` uçlarını kullanır. Admin kapsamı çalışma alanı ID/adı/türü, etkin öğrenci sayısı ve abonelik alanlarıyla sınırlıdır; owner/user kimliği, öğrenci listesi, iletişim ve kurum tahsilat kayıtları dönmez. `/admin/subscription-audit` aynı SystemAdmin + `coaching` veya `course-management` token politikasını uygular; yalnız `Subscription.Assigned`/`Subscription.Updated` olaylarını çalışma alanı adı/türü, eylem ve zaman olarak döndürür. Aktör/hedef/workspace kimliği, öğrenci/ödev/finans veya ödeme ayrıntısı içermez; sayfa boyutu en çok 100'dür ve yalnız bu iki olay için kısmi indeks kullanır. Owner görünümü `/me/subscriptions` ile yalnız `OwnerUserId` eşleşen alanlara döner. Tenant API'sinin sınırı devre dışı bırakılmaz. Ürün erişimi/davet müdahalesi ve hassas kayıt erişimi için daha geniş admin destek yetkisi verilmemiştir; böyle bir kapsam ayrı P0 kararı ve gerekçeli denetim tasarımı ister.

## 8. API ve mesajlaşma yaklaşımı

Yeni domain API'leri `/api/course-management/v1` altında toplanır. Mevcut `/api/students`, `/api/teachers`, `/api/sessions`, `/api/assignments` yolları yeniden kullanılmaz.

Örnek kaynaklar:

```text
/me/profile
/me/workspaces
/me/subscriptions
/workspaces/{workspaceId}/members
/workspaces/{workspaceId}/students
/workspaces/{workspaceId}/courses
/workspaces/{workspaceId}/groups
/workspaces/{workspaceId}/enrollments
/workspaces/{workspaceId}/lessons
/workspaces/{workspaceId}/lesson-series
/workspaces/{workspaceId}/payments
/workspaces/{workspaceId}/packages
/workspaces/{workspaceId}/assignments
/workspaces/{workspaceId}/imports
/admin/workspaces
/admin/subscriptions
/admin/workspaces/{workspaceId}/subscription
```

Öğrenci self-service endpoint'leri hesabından öğrenciyi çözer; URL'deki öğrenci kimliğine güvenmez. Listeler sayfalı ve scope filtrelidir. Beklenen iş kuralı hataları belirgin kodlarla döner; eşzamanlı düzenleme için sürüm/optimistic concurrency kullanılır. Öğrencinin finans endpoint'i yalnız kendi toplam bakiyesini ve kalan paket hakkını verir; ödeme/taksit kaydı ve veli görünümü sunmaz.

Kurs bildirimleri için sürümlü event'ler: `CourseManagementLessonScheduledV1`, `CourseManagementLessonRescheduledV1`, `CourseManagementLessonCancelledV1`, `CourseManagementAssignmentPublishedV1`, `CourseManagementAssignmentSubmittedV1`, `CourseManagementAssignmentReviewedV1`, `CourseManagementPaymentRecordedV1` ve worker'ın yayımladığı `CourseManagementReminderDueV1`. Hatırlatma kayıtları ders/ödev/taksit değişiklikleriyle birlikte saklanır; iptal, reschedule, öğrenci katılımını bırakma ve tam tahsilat bekleyen ilgili kayıtları iptal eder. Ödeme ters kaydı açık taksidin gelecekteki hatırlatmalarını yeniden kurar. Worker PostgreSQL satır kilidiyle birden çok örnekte çift kuyruğa almayı önler ve durum/event'i aynı EF bus outbox transaction'ında yazar. Başlangıçta geçmişe düşmüş ders/ödev veya günü geçmiş yaklaşan-vade hatırlatmaları atlanır. Taksit bildirimi tutar, taksit adı/sırası, ödeme hareketi, veli bilgisi veya ilişki kimliği taşımaz; öğrenci sadece kendi bakiye/kalan paket özetini salt okunur görür.

Kullanıcı onaylı varsayılan zamanlama: ders ve ödev teslimi hedefinden 24 saat önce; tarih-only taksit için 3 gün önce ve vade günü saat 09.00 Europe/Istanbul. İlk sürüm yalnız uygulama içi bildirim üretir; e-posta/SMS ve tercih ayarları kapsam dışıdır. Alıcı ve çalışma alanı zaman dilimi politikası ile kanal genişletme ayrı P0 ürün kararıdır. Mevcut Notification kuyruk/inbox yapısı ve CourseManagement.* filtreli API korunur; diğer ürün bildirim akışları değişmez.

Mevcut kodda kurs bildirim kanalı yalnız uygulama içidir. E-posta/SMS/WhatsApp/push kurs hatırlatmalarında bağlı değildir; e-posta altyapısının başka ürünlerde bulunması bu akışın hazır olduğu anlamına gelmez. Alıcı tercihleri ve kanal genişletme ayrı ürün kararıdır.

## 9. Aşamalar, bağımlılıklar ve çıkış koşulları

Tahminler 1 deneyimli geliştirici, mevcut altyapının kullanılabilir olması, kararlı kapsam ve düzenli geri bildirim varsayımıyladır. Takvim taahhüdü değildir. İlk iki faz sonunda tahmin yeniden yapılır. Test, inceleme ve temel dokümantasyon faz sürelerine dahildir.

| Faz | İlk tahmin | Bağımlılık | Kod durumu (13 Ekim 2026) | Çıkıştan önce kalan kapsam |
|---|---:|---|---|---|
| P0 — Hazırlık ve kontratlar | 3–5 iş günü | Yok | Identity Account kapsamı CourseManagement için zorunludur; assessment/execution tüketicileri, idempotency, öğrenci/öğretmen kimlik bağını kaldırma, teslim metni/eklerini silme, not/tarih ve finans geçmişini anonim koruma, Owner devri ve sole-owner workspace için anonim Owner + salt-okunur durum uygulanmıştır. TeacherMaterial içeriği/metadata saklanır ve uploader GUID anonimleştirilir. Identity değerlendirme ekranıyla aynı aggregate'i kullanan iç endpoint bağlandı. Course Management kendi assessment verisini raporlar; birleşik Identity değerlendirmesi bütün zorunlu servis yanıtları geldikten sonra tamamlanır ve execution öncesi yeniden kontrol edilir. Identity sonucu hazır değilse, hold içeriyorsa veya endpoint doğrulanamıyorsa destructive execution fail-closed olur. Unit, integration ve endpoint/provider testleri geçti. | KVKK/işleme dayanağı ve genel saklama matrisi; kalan serbest metin, outbox/inbox ve yedeklerdeki kişisel veri için purge/retention; hosted CI/artefakt (disposable Docker privacy E2E 6/6 geçti), production host, erişilebilirlik, SLO/RPO/RTO, eski ürün regresyonu ve pilot onayı release kapısıdır.
| P1 — Servis ve ayrı uygulama temeli | 4–6 iş günü | P0 | Temel uygulama, izole PostgreSQL migration/tekrar uygulama, API hazır olma/entegrasyon doğrulamaları ve admin production bundle geçti. Oturum ayrıştırması için Identity seçkisi 105/105, staff portal 243/243 ve Edge panel smoke'u 2/2 geçti. Korunan ürün/Gateway/ayrı uygulama contract testi 5/5 geçti. | Üretim yayın/geri alma, backup/restore ve geniş eski ürün regresyon matrisi doğrulanmalı. |
| P2 — Identity ve çalışma alanları | 6–9 iş günü | P1 | Profil, kurum/öğretmen alanı açma, üyelik, davet/kabul ve temel izolasyon var. Öğrenci yalnız mevcut Identity hesabıyla daveti kabul eder; self-servis hesap ve veli portalı yoktur. Öğrenci davetinde owner mevcut yerel öğrenci kaydını açıkça seçebilir; seçmeden aynı e-postalı kayıt varsa kabul reddedilir, otomatik eşleştirme yapılmaz. | E-posta/SMS teslimi ve yaşam döngüsü uçtan uca doğrulama açık. Legacy hesap/rol/öğretmen üyeliği aktarımı yoktur. Öğrenci kaydı seçimi ve davet idempotency API/UI kabul testleriyle doğrulanmıştır. |
| P3 — Öğrenci/kurs/grup kayıtları | 5–8 iş günü | P2 | Öğrenci/kurs/grup kayıtları, arşiv ve veli bağlantısı; öğrenci operasyon kaydı için CSV önizleme/idempotent aktarım uygulandı. P7 öğrenci roster aktarımı Koçluk/Hızlı Okuma kaynaklarından owner seçimi ve önizlemesiyle eklendi; Identity bağlantısı üretmez. Kurslar ve gruplar panel/API'den tanımlanır; PostgreSQL kabul senaryoları geçti. | Onaylanan dar P7 öğretmen daveti ve kurum adı/türü aktarımı uygulanmıştır; kaynak ataması/rol kopyası ile doğrulanmış veli hesabı/portal erişimi yoktur. |
| P4 — Takvim, tekrar ve yoklama | 8–12 iş günü | P3 | Tekil/haftalık plan, tekil dersi taşıma, seri düzenleme/iptal, yoklama/tamamlama, izin/uygunluk, temel oda/şube ve 30 günlük telafi kredisi uygulandı; PostgreSQL API ve Chrome öğrenci/personel panel kabul senaryoları geçti. | İleri şube/derslik yönetimi MVP kapsamı dışıdır. Seri değiştirme sırasında bire bir eşleşen yeni oturumlar telafi kredisi üretmez; açıkça iptal edilen/toplu iptal edilen oturumlar üretir. |
| P5 — Finans ve ders paketleri | 10–15 iş günü | P3, P4 | Tamamlandı: dört ücret modeli; kullanıcı tanımlı vade/taksit; TRY manuel nakit/EFT tahsilatı ve dağıtımı; ödeme/borç/paket ters kaydı; öğrenci ekstresi ve owner finans özeti. PostgreSQL migration/API ve Chrome personel kabul testleri geçti. P7'de ekstre/borç-paket CSV ve yazdırılabilir PDF akışları eklendi. | Faz çıkış ölçütleri geçti. Online ödeme, e-belge/vergi, otomatik prorata ve muhasebe/kâr hesabı bu MVP kapsamı dışındadır. |
| P6 — Ödev, portal ve bildirimler | 6–9 iş günü | P3; ders ekranları için P4, ödeme ekranları için P5 | Ödev/teslim/değerlendirme, öğrenci paneli, sınırlı finans özeti, taranan ekler ve dueAt + 180 gün temizleme, course-scoped inbox ve zamanlanmış in-app worker uygulandı. Ders ve ödev teslimi (24 saat önce), taksit (3 gün önce ve vade günü 09.00 Europe/Istanbul) varsayılanları kullanıcı tarafından onaylandı. | Genel profil/akademik/finans saklama ve export/silme matrisi ile dosya tipi/boyut limitleri için hukuk/ürün onayı gerekir. E-posta/SMS/tercihler ve veli portalı MVP dışında.
| P7 — Admin, aktarım ve pilot hazırlığı | 5–8 iş günü | P2–P6 | SystemAdmin abonelik yönetimi/audit, Owner plan özeti, eşzamanlı öğrenci kotası, abonelikte salt-okunur kısıt, raporlar ve owner-seçimli öğrenci/öğretmen/kurum aktarımı uygulanmıştır. PostgreSQL/API aktarım, abonelik ve auth kabul testleri geçti; source-import client testleri 6/6; staff panel smoke 4/4. Güncel doğrulamada Course Management Release unit 67/67 ve integration 15/15 geçti. İzole restore 26 migration, 6 workspace, 14 öğrenci, 32 reminder ve 1 ek dosya metadata kaydını, dosya SHA-256 manifestiyle doğruladı. | Genel admin kullanıcı/üyelik ve impersonation desteği MVP dışı; production backup/restore ve RPO/RTO kanıtı, Gateway production host, eski ürün regresyon matrisi, pilot ve yayın kararı tamamlanmalıdır.
| P8 — Pilot ve sürüm kararı | 5–10 iş günü | P7 | Migrate-only, production overlay, CI idempotent SQL artifact'ı ve gerçek ClamAV 1.5.4/PostgreSQL smoke'u var. Course Management öğrenci paketi build/audit ve API transitif paket taraması geçti. Güncel izole dal foundation contract 12/12 geçti; korunan Coaching/Hızlı Okuma uygulama dosyalarında fark yoktur. Önceki 10/12 sonucu eski ana checkout durumuna aittir ve izole dal için geçerli değildir. Identity readiness/erasure disposable Docker E2E 6/6; Coaching backup ve Gateway route dahil P0 kümesi 8/8; öğrenci export, broker recovery, attachment ownership ve admin/teacher tenant isolation 4/4 geçti. | Hosted CI/artifact, gerçek deploy/rollback, KVKK/retention ve eski outbox/yedek purge, performans/SLO/RPO/RTO, pilot ve açık release kararı gerekir. Ortak admin-panel audit kapsam dışıdır.

Kapsam ayrımı: P3 CSV işi, yeni kurs alanına öğrenci operasyon kayıtlarını yüklemek içindir; kurs ve grup tanımları panel/API üzerinden yapılır. P7 legacy aktarımı kaynakta yetkili Owner'ın seçip önizlediği öğrenci roster, mevcut Identity userId'siyle Teacher daveti ve kurum adı/türü akışlarından oluşur. Aynı iş olarak sayılmazlar. Mevcut davet API'sinin hedefi `TargetUserId` olduğu için e-posta adresinden otomatik hesap eşleştirmesi veya toplu hesap aktarımı yapılmaz.

### 9.1 Faz çıkış kabul ölçütleri

Bir fazın kodunun bulunması fazı kapatmaz. İlgili ölçütler test sonucu veya onaylı karar kaydıyla kanıtlanır; korunan eski ürünlere ait kapı her fazda ayrıca geçerlidir.

| Faz | Ölçülebilir kabul ölçütleri |
|---|---|
| P0 | Korunan dosya/rol matrisi ve kullanıcı kararları kayıtlı; Identity Account kapsamı CourseManagement servisini zorunlu sayar. StudentSubmission metni/ekleri silinir; not, tarih ve bunlardan türeyen akademik durum ile gerekli finans geçmişi anonim kalır. TeacherMaterial içerik/metadata korunur ve uploader GUID anonimleştirilir. Başka aktif Owner varsa devir, tek Owner varsa anonim Owner ve salt-okunur workspace uygulanır. Identity değerlendirme ekranıyla aynı aggregate'i kullanan iç endpoint bağlandı; Course Management execution öncesi hazır sonucu yeniden doğrular ve Unknown/ActiveHold durumlarında değişiklik yapmaz. Eski pending assessment kayıtları migration ile geçersizleştirilir. Gerçek PostgreSQL/RabbitMQ retry/outbox testi, unit 67/67, integration 15/15, disposable Docker privacy E2E 6/6 ve endpoint/provider testleri geçti. | KVKK/retention, kalan serbest metin, outbox/inbox ve yedek purge, Identity tam regresyonu, hosted CI, production host/operasyon ve pilot onayı P0/P8 yayın kapısıdır.
| P1 | Kurs API, öğrenci uygulaması ve paneller için tanımlı ayrı build'ler geçer; migration zinciri boş izole PostgreSQL'e uygulanır ve ikinci uygulama değişiklik üretmez; `/health/live` ve DB erişimini sınayan `/health/ready` beklenen sonucu verir. Ana `/api/course-management/v1` rotaları yalnız geçerli kurs ürünü tokenı kabul eder; tek istisna, yalnız kendisine hedeflenmiş daveti listeleyip kabul etmeye yarayan subject-scope `/participation/invitations` uçlarıdır. `coaching` veya `course-management` ürün tokenlı SystemAdmin'in ayrı `/admin/overview` sayısal özeti de açıkça tanımlanmış kapsamdır. Personel için kurs cookie'si `/api/auth/course-management` path'ine sınırlıdır; login/MFA/refresh/revoke bu cookie'yi kullanır, ürün değişimi Coaching/Speed Reading refresh tokenını rotasyona sokmaz ve kurs logout'u eski ürün cookie'sini silmez. `CourseStudent` oturumu yalnız aktif kurs erişimi ve ürün rolüyle verilir; privileged kullanıcı MFA politikası uygulanır. Eski ürün tokenları diğer kurs verisi rotalarını açamaz.
| P2 | Öğretmen/kurum katılımı, öğrenci davet kabulü, profil oluşturma ve aynı işlemin tekrar gönderilmesi senaryoları geçer; çalışma alanı A'daki kullanıcı B verisini okuyamaz; üyelik iptalinden sonraki istek reddedilir; Identity ve yerel servis kısmi hata/retry senaryosu eski üyeliği silmez veya yinelenen üyelik üretmez. |
| P3 | Hesaplı ve hesapsız öğrenci kurs/gruba kaydedilir; veli bağlantısı çalışma alanı sınırını aşmaz; arşivlenen öğrenci aktif listeden/portal kurslarından çıkar ama geçmiş kayıtları korunur; CSV önizlemesi hatalı ve yinelenen satırları commit öncesi gösterir, tekrar commit ikinci kayıt üretmez. |
| P4 | Tek ders ve haftalık seri grup/bire bir akışları; taşıma; saat dilimi/DST; öğretmen, öğrenci, izin ve oda çakışması; kapasite; seri değişikliği/iptali; yoklama/tamamlama için PostgreSQL API ve Chrome senaryoları geçer. Kurum/öğretmen tekil/toplu iptali etkin katılımcılara 30 günlük kredi verir; bireysel erken iptal yalnız o öğrenci için onay bekleyen kredi oluşturur; geç iptal/no-show kredi üretmez; süresi geçen kredi onaylanamaz/planlanamaz; telafi planı ve tamamlanması krediyi tek sefer tüketir. Aynı seri saatinin değiştirilmesiyle eşleşen yeni oturumlar ikinci hak yaratmaz. |
| P5 | Dönemlik, aylık sabit, ders başı/saat başı ve paket akışları PostgreSQL testinde çalışır; aylık vadeler tam tutarlı ve elle tarihlidir, prorata yoktur; 2 ondalık TRY, taksit aşımı ve fazla dağıtım reddi doğrulanır; kısmi/çok taksitli tahsilat ve idempotent tekrar geçer; ödeme, borç ve paket hakkı düzeltmesi ters kayıtla yapılır; paket kalan hakkı parasal bakiyeden ayrıdır; completed + `Absent` ders tahakkuk ve paket tüketimini sürdürür; owner ekstresi/özetinde bakiye, gecikmiş/yaklaşan borç ve tarih/yöntem bazlı net tahsilat doğru, owner dışı erişim reddedilmiştir; migration boş DB'ye uygulanıp tekrarlandığında değişiklik üretmez. |
| P6 | Öğretmen ödevi öğrenci/gruba atar; mevcut Identity öğrencisi yalnız kendi ödevini/ders geçmişini ve kendi bakiye/kalan paket özetini görür; aynı workspace'teki başka öğrenci hesapları finans yanıtına girmez; ödeme/taksit hareketi ve veli portalı yoktur. İzinli PDF/JPEG/PNG yüklemesi (uygulama varsayılanı: en çok 10 MiB) içerik imzası ve üretimde ClamAV taramasından geçer; kullanıcı kararıyla ekler `dueAt` son teslim tarihinden 180 gün sonra dosya ve metadata olarak temizlenir; domain testi bu bitiş tarihini doğrular, temizlik testi süresi dolan dosya ve metadata'yı silip geçerli eki korur. Yetkisiz öğrenci/öğretmen dosyaya erişemez. Domain değişikliği ve hatırlatma aynı PostgreSQL transaction'ında, dispatch edilecek olay EF bus outbox'ında saklanır. Reminder worker çoklu örnekte `SKIP LOCKED` ile tek claim yapar; ders ve ödev teslimi için kullanıcı onaylı 24 saat, taksit için 3 gün önce ve vade günü 09.00 Europe/Istanbul zamanlamasını uygular. İptal/reschedule, öğrenci katılım iptali ve tam ödeme bekleyen hatırlatmayı durdurur; ödeme ters kaydı yalnız açık gelecekteki hatırlatmaları yeniden kurar. Finans hatırlatma bildirimi tutar/taksit/veli/ilişki ayrıntısı içermez. Eski hedefe ait ve zamanı geçmiş hatırlatma gönderilmez. MVP kanalı in-app'tir; e-posta/SMS tercihi yoktur. |
| P7 | Admin toplu özet ve abonelik listesi/ataması yalnız geçerli `sub` ve SystemAdmin rolüyle, `coaching` veya `course-management` ürün tokenında çalışır; anonim, farklı ürün ve admin olmayan erişimleri reddedilir. Admin yanıtında workspace adı/türü, öğrenci sayısı ve plan alanları bulunabilir; owner/user kimliği, iletişim, öğrenci kayıtları ve tahsilat hareketi bulunmaz. Owner yalnız sahibi olduğu planı görür; öğretmen ve öğrenci başka workspace planını alamaz. Değişiklik owner audit'inde yalnız eylem/zaman olarak görünür. Kullanıcı onaylı `/admin/subscription-audit` yalnız `Subscription.Assigned` ve `Subscription.Updated` olaylarını verir; yanıt alanları `workspaceName`, `workspaceType`, `action`, `occurredAt` ile sınırlıdır, en çok 100 kayıt sayfalar ve en yeni olay önce gelir; kimlik/öğrenci/ödev/finans verisi içermez. Atanmış aktif öğrenci limiti manuel kayıt, CSV commit, Identity davet kabulinde yeni StudentRecord yaratılması ve arşivden geri yüklemede uygulanır; eşzamanlı öğrenci ekleme limiti aşamaz; mevcut öğrenci sayısını aşan yeni plan 409 alır. Atanmış plan `[StartsAt, EndsAt)` aralığında ve `Active` durumunda yazmaya izin verir; başlangıç öncesi, bitiş anı/sonrası veya `Suspended` durumda workspace iş verisi yazmaları 409 `Subscription.ReadOnly` alır. Owner/öğretmen/öğrenci okuma ve dışa aktarma yapabilir; kişisel profil güncellemesi açık, owner import önizlemesi salt-okunur olarak açık kalır; sadece SystemAdmin planı yeniden etkinleştirir. Plansız workspace abonelik kapısı dışında kalır. PostgreSQL API kabulü askıya alma/bitiş altında kurs, öğrenci, tahsilat ve davet oluşturma/kabul yazmalarını; okuma/profil erişimi, read-only durum bildirimi ve yeniden etkinleştirmeyi doğrular. Yalnız abonelik olayları SystemAdmin audit kapsamındadır; daha geniş admin kullanıcı/üyelik desteği, impersonation ve hassas kayıt erişimi MVP dışıdır. P7 aktarımı kaynak kurumu açıkça seçen Owner için uygulanmıştır: öğrenci roster alan seçimi/önizleme/commit ile aktarılır ve UserId boş kalır; öğretmenler seçilip mevcut Identity userId ile Teacher daveti açılır; kurum adı/türü seçilip yalnız mevcut Institution workspace'ine yazılır. İlk kaynak kurumu otomatik seçilmez; isim/e-posta ile eşleştirme, eski rol/profil/atama ve yeni workspace aktarımı yoktur. Yetkili finans, öğrenci, ders/yoklama ve ödev raporları CSV/yazdırılabilir PDF üretir; iletişim/ödeme notunu dışarıda bırakır, CSV formül enjeksiyonunu güvenli metne çevirir, API'nin sayfa üst sınırını aşmadan tüm sayfaları toplar ve rapor kapsamı/rol filtresini açıklar. Workspace borç/paket raporu yalnız owner'a açıktır; yazdırma sayfalarından PDF dosyası üretimi doğrulanmıştır. Yedekleme/restore runbook'u, kritik E2E ve eski ürün regresyonları tamamlanmalıdır. |
| P8 | Kurs Yönetimi paketi, API, öğrenci ve panel build/test kapılarından geçer; migrate-only ve CI idempotent SQL artefaktı doğrulanır; production Compose API/migration/Gateway hedefini tanımlar. Course Management npm audit yalnız kendi öğrenci paketinde çalışır. Erasure assessment/execution bütün zorunlu servisleri bekler; Course Management iş kuralları, idempotency, anonimleştirme ve TeacherMaterial saklama test edilir. Destructive execution, Identity değerlendirme ekranıyla aynı readiness aggregate'ini doğrular ve belirsiz sonuçta fail-closed kalır. OTel düşük kardinaliteli hata/teslim ölçümleri ve attachment cleanup kuyruğunun kalıcılaştırılamadığı birleşik storage/DB arızası metriği test edilir; alarm, metriğin ilk artışında çalışmalıdır. | Hosted CI/artifact, gerçek ortam backup/restore/rollback, genel retention/purge ve KVKK dayanağı, production SLO/RPO/RTO, pilot ve açık yayın onayı beklenir. Ortak admin-panel audit kapsam dışı olduğundan monorepo CI genel sonucu ayrı kapı olarak raporlanır.

P6 ek dosya alt kabulü: scanner stream'i tarama sonrası kapanmadan dosya silinmez. EICAR içeren PDF production-pinned ClamAV ile 422 alır ve diskte dosya bırakmaz; clean PDF 201 alır. Deterministik scanner E2E'si ayrıca beklenmeyen scanner yanıtında 503, istemci iptalinde dosyanın temizlenmesini doğrular. Silme tekrar başarısız olduğunda metadata içermeyen storage-key cleanup isteği DB'ye yazılır; worker başarısız retry sonrasında isteği korur ve başarılı silmeden sonra kaldırır. Release unit 67/67, PostgreSQL/RabbitMQ integration 15/15, iki kez uygulanan 26 migration ve restore/sayaç kontrolü geçti. Cleanup isteğinin DB'ye yazılamadığı eşzamanlı depolama+DB arızası kritik log üretir; bu olağan dışı birleşik arıza için ayrı storage reconciliation/operasyon alarmı P8 operasyon kapısında izlenmelidir.

Eski tahminler toplam 52–82 iş günlük ilk planlama aralığıdır; gerçekleşen eforu, kalan eforu veya takvim taahhüdünü göstermez. P1–P5 uygulanmış olsa da kalan efor, açık P0 kararları ve sonraki fazların gerçek kabul kriterleri netleştikten sonra yeniden tahmin edilmelidir.

Revizyon sırasında oluşan ilk toplam mühendislik tahmini 52–82 iş günü, yaklaşık 11–17 çalışma haftasıdır. Bu aralık doğrulanmış görev eforlarının toplamı değildir; aylık/ders bazlı modeller eklendiği için P5 için ek pay ayrılmış ön tahmindir. Artık gerçekleşmiş veya kalan efor tahmini olarak kullanılmamalıdır. Pilotun gerçek gözlem süresi ayrıca 2–4 takvim haftası olabilir; P8 eforu bu gözlem süresine yayılır. Tatiller, entegrasyon beklemeleri ve kapsam değişiklikleri takvim süresini uzatabilir.

Kritik yol: P0 → P1 → P2 → P3 → P4 → P5 → P6'nın finans/ders ekranları → P7 → P8. Ödev geliştirmesi P3 sonrasında başlayabilir; aynı geliştirici için bu durum toplam eforu azaltmaz. Admin iskeleti P1, erişim yönetimi P2, final işletim ekranları P7'de tamamlanır. Öğrenci uygulaması P1'den itibaren vardır; son fazda açılmaz.

## 10. İlk dört somut teslim

### Teslim A — Ürün iskeleti ve koruma

- Yeni servis ve öğrenci uygulaması ayrı derlenir.
- Yeni domain yolu Gateway'den doğru servise gider.
- Staff/admin modülleri erişimsiz kullanıcılara görünmez.
- Yeni ürün kapalıyken eski ürünlerin baseline akışları geçer.

### Teslim B — Gerçek katılım

- Mevcut Identity hesabı yeni ürün davetini kabul eder; ikinci hesap oluşmaz.
- Yerel profil ve kişisel/kurum çalışma alanı oluşur.
- Aynı kullanıcı iki alan arasında geçer; veriler karışmaz.
- Tekrarlanan kabul/aktarım mükerrer profil veya üyelik üretmez.

### Teslim C — Dersin günlük yönetimi

- Kurum hesapsız öğrenci kaydeder, kurs/grup kaydı ve ders planı açar.
- Öğretmen aynı öğrenciye atanmış dersi görür ve yoklama alır.
- Öğrenci davet kabulünden sonra kendi takvimini görür.
- Çakışma, iptal, tekil dersi taşıma, seri planı değişikliği, telafi kredisi üretme/onaylama/planlama/tüketme ve öğrencinin kendi grup katılımını iptal etme senaryoları geçer.

### Teslim D — Kullanılabilir MVP

- Taksitli kurs ve özel ders paketi uçtan uca çalışır.
- Aylık ücret bir dönemde bir kez borçlandırılır; ders/saat bazlı özel ders doğru süre ve anlaşma oranıyla borçlandırılır.
- Kısmi ödeme/dağılım/iade ve paket tüketimi doğru hesaplanır.
- Ödev verilir, öğrenci teslim eder, öğretmen değerlendirir.
- Kurum, bağımsız öğretmen, öğrenci/veli ve admin senaryoları birlikte geçer.
- Veri aktarımı, audit, yedek/restore ve ürünler arası izolasyon doğrulanır.

## 11. Test ve sürüm kapıları

### Domain testleri

- İptal politikası × katılım × ücret modeli sonucunun doğrulanması.
- Paket tüketimi, tekrar işleme, telafi ve geçerlilik süresi.
- Taksit yuvarlama, kısmi tahsilat, dağılım limiti, iade/ters işlem.
- Tekrarlayan planın tek/gelecek oturum değişikliği.

### Integration/contract testleri

- Eski ürün token'ıyla yeni ürüne ve yeni ürün token'ıyla eski ürüne erişim reddi.
- Yetkisiz workspace/student/lesson/payment/attachment kimliğiyle erişim reddi.
- Kurumun öğretmenin kişisel öğrencilerini ve finansını görememesi.
- Öğretmenin kurum finansını varsayılan olarak görememesi.
- Veli ilişkisinin ve öğrencinin self-service kapsamının doğrulanması.
- Aynı anda çakışan ders, çift tahsilat ve çift paket kullanımının engellenmesi.
- Katılımın kısmi başarısızlığında güvenli tekrar deneme.
- Yeni kurum/öğretmen self-servis katılımı ile seçili aktarım sınırları: öğrenci roster alan seçimi, Identity userId'siyle Teacher daveti ve kurum adı/türü; otomatik kimlik eşleştirmesi yok.
- Aylık borcun çift oluşmaması; iptal/dondurma ve fiyat değişikliğinin geçmiş borcu değiştirmemesi.
- Grup kapasitesi, üyelik iptali, öğretmen ayrılması ve abonelik sonlanmasının kayıt geçmişini koruması.
- Aynı tarayıcıda eski ve yeni ürün sekmeleri arasında session/cookie izolasyonu.
- Broker kesintisi, event yeniden teslimi ve hatırlatma iptali.
- Dosya erişimi, karantina, limit ve yetki kaldırıldığında erişim reddi.

### E2E

1. Kurum → öğrenci kaydı → kurs kaydı → taksit → ders → yoklama → tahsilat.
2. Bağımsız öğretmen → paket → tekrarlayan ders → iptal/telafi → kalan hak.
3. Öğretmen → ödev → öğrenci teslimi → geri bildirim.
4. Veli portalı → çocuk seçimi → ders/ödeme görünürlüğü (**ilk sürüm/MVP dışında; bu yol haritasının kabul testi ve yayın kapısı değildir**).
5. Aynı kişi → ürün geçişi → kurum/kişisel alan geçişi → eski ürüne dönüş.
6. Admin → yeni ürün yönetimi; eski admin ekranlarının erişim ve davranış kontrolü.

Her fazda ilgili testler çalışır. Ortak auth/gateway/panel kökü değiştiğinde ilgili eski ürün regresyonları da çalışır. Son pilot kapısında tam kritik akış paketi, mobil görünüm ve erişilebilirlik kontrolleri yapılır. Mevcut testlerin varlığı başarı kanıtı sayılmaz; çalıştırılmış sonuç kaydedilir.

## 12. Yayın ve işletim

- Yeni servis, worker, öğrenci uygulaması, staff modülü ve admin modülü için ayrı sürüm kayıtları.
- Yeni servis/öğrenci uygulaması ayrı deploy edilir; staff/admin modülleri host uygulamalarıyla birlikte derlenir. Bu iki modül için fiziksel olarak ayrı deploy garanti edilmez.
- Önce test ortamında Identity/Gateway eklemeleri, yeni servis ve frontend'ler doğrulanır; üretimde eksik backend varken menü açılmaz.
- Yeni ürün yayın etkinleştirmesi ortak servislerde geri uyumlu eklemelerden sonra yapılır.
- Kurs DB/storage backup ve restore senaryosu pilot öncesi denenir.
- Ders planlama/seri yollarındaki 409 reddi `course_management.lesson.conflict.rejections`; ödeme yazma yollarındaki 4xx/5xx `course_management.payment.errors`; `/imports/` yazma yollarındaki 4xx/5xx `course_management.import.errors` sayaçlarıdır. MassTransit gönderim zamanı ile bildirim tüketicisinin başarılı dispatch anı arasındaki süre `course_management.event.delivery.delay` histogramında milisaniye olarak tutulur.
- Ölçümler `EduPlatform.CourseManagement` meter'ında toplanır ve ortak OpenTelemetry altyapısında `OTEL_EXPORTER_OTLP_ENDPOINT` ayarlandığında dışarı aktarılır. Etiketler yalnız HTTP durum kodunu içerir; kullanıcı, çalışma alanı, öğrenci, tutar veya serbest metin taşımaz. Üretimde alarm eşiği, hedef gecikme ve hata bütçesi tanımlanmamıştır; pilot verisi ve işletim sahibinin onayı gerekir.
- Rollback: yeni ürün giriş/menüsünü kapatma ve yeni uygulama/servisi geri alma. Eski ürün tabloları değiştirilmediği için onlara veri rollback'i gerekmez.
- Ortak ürün kayıtları geri alınırken yeni ürünle üretilmiş Identity satırları nedeniyle enum tanımsızlığı yaratılmaz; çoğunlukla kayıtları koruyup erişim/yayın kapatılır.
- Profil silme, hesap kapatma, veri export'u ve finans kayıtlarının saklama kuralları ayrıdır. Hesap kapatma, gerekli finansal geçmişi otomatik silmez; yasal saklama ve anonimleştirme kararları yayın öncesi netleştirilir.

## 13. MVP sonrası geliştirme sırası

1. Öğretmen hakedişi: ücret anlaşması, tamamlanan derslerden dönemlik hesap, onay ve ödeme; geçmiş oran snapshot'ı.
2. Online ödeme: kurum/öğretmenin kendi tahsilat hesabı, webhook doğrulama, idempotency ve mutabakat. Platformun parayı toplaması/dağıtması ayrı ürün ve hukuki karardır.
3. Sekreter/finans rolleri, şube bazlı yetkiler, franchise/şube konsolidasyonu ve ayrıntılı yetkiler. Temel şube/derslik tanımı MVP'dedir.
4. Öğrenci rezervasyonu, kontenjan/bekleme listesi ve telafi talebi.
5. Aday öğrenci CRM ve kayıt yenileme.
6. Harici takvim ve gizlilik koruyan alanlar arası müsaitlik.
7. Resmi e-belge/muhasebe entegrasyonları ve gelişmiş yönetim raporları.
8. SMS/WhatsApp/push kanalları, isteğe bağlı kurum markası ve online başvuru formları.

Video altyapısı, soru bankası, yapay zekâ değerlendirmesi, diğer ürünlerin özel raporlarını birleştirme ve tam bordro bu yol haritasının MVP kapsamı dışındadır.

## 14. Açık kararlar ve varsayımlar

Her kararın uygulama durumu ayrı belirtilir. Seçilmiş kararlar ilgili kabul testleri geçmeden uygulanmış sayılmaz; açık yasal, erişim ve işletim kararları üretim sürümünü engeller.

### 14.1 Kodun bugün uyguladığı erişim ve veri kaynağı sınırları

Aşağıdaki tablolar kullanıcı tarafından seçilmiş ve API/UI testleriyle doğrulanmış erişim kurallarını, ayrıca henüz uygulanmamış aktarımı ve açık P0 kararlarını ayırır.

| Rol | Bugünkü çalışma alanı erişimi | Bugün açık olmayan yetki |
|---|---|---|
| Owner (kurum/bağımsız öğretmen) | Kendi çalışma alanında öğretmen, öğrenci, kurs/grup, veli iletişimi, takvim, yoklama, üyelik ve finans kayıtlarını yönetir; öğrenci ekstresi ve workspace finans özetini görür; kendi plan özetini salt okunur görüntüler. | Abonelik ataması/güncellemesi ve global admin yönetimi yok. |
| Teacher | Yalnız kendisine atanmış grup ve derslerde öğretim, ödev, teslim değerlendirme, yoklama ve uygunluk işlemlerini yapar; mevcut etkin öğrencileri kendi grubuna kaydedebilir; kendi grubundaki öğrencilerin kart ve bağlı veli iletişimini okuyabilir; kendine atanmış grup oluşturabilir. | Öğrenci kartı oluşturma/düzenleme/arşiv/geri açma, CSV önizleme/commit, başka öğretmene grup atama, veli kaydı/bağlantısı değiştirme, üyelik yönetimi ve finans owner yetkisindedir. Bağımsız öğretmenin kendi çalışma alanında Owner rolü varsa tam yönetici yetkisine sahiptir. |
| Student | Daveti kabul ettiği üyelikten kendi kurs ve yaklaşan derslerini görür; kendi ürün profilini düzenler; kendi toplam bakiyesini ve kalan paket haklarını salt okunur görür. | Grup/öğrenci yönetimi, veli görünümü, ödeme/taksit hareketi, ödev atama ve değerlendirme yönetimi yok. |
| Guardian | Henüz portal hesabı/üyeliği yok. Yerel iletişim bağlantısı doğrulanmış ilişki veya erişim sağlamaz. | Öğrenci, ders veya finans ekranı yok. |
| SystemAdmin | `GET /api/course-management/v1/admin/overview` toplu operasyon sayılarını; `GET /api/course-management/v1/admin/subscriptions` sayfalı çalışma alanı planlarını; `GET /api/course-management/v1/admin/subscription-audit` yalnız abonelik atanma/güncellenme eylemi, zaman ve workspace adı/türünü gösterir. `PUT /api/course-management/v1/admin/workspaces/{workspaceId}/subscription` plan atama/güncelleme yapar; `SystemAdmin` ve `coaching` veya `course-management` ürün tokenı gerekir. | Audit yanıtında aktör/hedef/workspace kimliği, öğrenci/ödev/finans ayrıntısı veya ödeme hareketi yoktur. Impersonation ve abonelik dışı workspace mutasyonu/genel destek erişimi yok. |

#### Rol × kaynak × işlem matrisi — seçilmiş hedef politika ve doğrulanmış uygulama

“Öğretmen” yetkisi etkin çalışma alanı üyeliği ve hedef kaydın atanmış grup/öğretmen bağlamıyla sınırlandırılır. Öğrenci kartı/CSV ve veli yönetimi için owner sınırı kullanıcı kararıdır; kurum öğretmeninin dar rolü ve bağımsız öğretmenin workspace owner olarak tam yetkisi API ve panelde uygulanmıştır. Gösterilen API yollarının ayrıntısı `services/course-management-service/CourseManagement.API` altındaki endpoint'lerde bulunur.

| Kaynak / işlem | Owner | Teacher | Student | Guardian | SystemAdmin | Kod karşılığı ve açık sınır |
|---|---|---|---|---|---|---|
| Ürün API erişimi | Ana `/api/course-management/v1` rotalarına geçerli `platform_product=course-management` tokenı ve GUID `sub` gerekir; `/me/subscriptions` yalnız kendi OwnerUserId eşleşmelerini döndürür. | Aynı. | Aynı. | Kurs ürünü üyeliği/portal hesabı yok. | `/admin/overview`, `/admin/subscriptions` ve workspace abonelik atama rotaları için SystemAdmin rolü ve `coaching` veya `course-management` ürün tokenı gerekir. | Tek istisna `/api/course-management/v1/participation/invitations` GET ve hedefli davet kabul POST rotalarıdır: `coaching`, `speed-reading` veya `course-management` tokenı yalnız kendi `sub` değerine gönderilmiş daveti okuyup kabul edebilir. Başka kurs verisi açılmaz. `Program.cs`, `WorkspaceEndpoints.cs`, `CourseManagementAdminEndpoints.cs`. |
| Profil ve çalışma alanı | Kendi profilini düzenler; kendi çalışma alanını görür. Kurum/bağımsız öğretmen workspace'i oluştururken başlangıçta Owner olur. | Kendi profilini düzenler; etkin üyesi olduğu alanı görür. | Kendi profilini düzenler; etkin üyesi olduğu alanı görür. | Portal yok. | Profil veya workspace ayrıntısına global erişim yok. | Workspace oluşturma, Identity'deki ürün rolüne ve workspace türüne bağlıdır; üyelikte `UserId` token `sub` değerinden alınır. `Program.cs`, `WorkspaceEndpoints.cs`. |
| Üyelik, davet, üyeleri iptal ve workspace audit | Owner listeler, davet eder, daveti iptal eder, Owner olmayan üyeliği kapatır ve audit kayıtlarını okur; Owner üyeliği korunur. Personel paneli son 100 kaydı eylem/zaman olarak gösterir. | Owner yönetim rotalarına ve audit akışına erişemez. | Kendi üyeliklerini listeler; yalnız kendisine yöneltilen daveti kabul eder. | Yok. | Üye listesi ve genel audit yok; abonelik değişikliği owner'ın workspace audit akışında eylem/zaman olarak görünür. | Davet rolü yalnız Teacher veya Student olabilir; Owner daveti oluşturulamaz. Audit endpoint'i owner olmayan öğretmene 404 döner; panel actor/target kimliklerini göstermez. Student daveti hedef Identity sub GUID'siyle verilir; owner davet oluştururken bağlanacak yerel kaydı açıkça seçer. Aynı e-posta otomatik bağlantı kurmaz; seçim yoksa aynı e-postalı kayıt durumunda kabul 409 ile durur. WorkspaceEndpoints.cs. |
| Öğrenci kaydı ve CSV | Çalışma alanındaki öğrencileri listeler, oluşturur, CSV önizler/commit eder, düzenler, arşivler ve geri açar. | Yalnız aktif olarak kendi grubuna kayıtlı öğrenci kartlarını okur; öğrenci kaydı oluşturamaz/düzenleyemez. CSV önizleme ve commit owner'a aittir. | Başkasının öğrenci kaydını yönetemez. | Yok. | Yalnız toplu aktif öğrenci sayısı. | CSV yerel `StudentRecord` üretir; Identity hesabı/rolü açmaz. Öğretmen yalnız kendi atanmış gruplarındaki aktif öğrencileri listeler; owner workspace'teki tüm kayıtları yönetir. `LearningEndpoints.cs`. |
| Öğrenci arşivleme ve yeniden açma | Öğrenciyi arşivler/geri açar; aktif grup kayıtları kapanır ve öğrenci üyeliği iptal edilir. | Arşivleyemez veya geri açamaz. | Kendi kaydını arşivleyemez. | Yok. | Mutasyon yok. | Öğretmen arşiv isteği API'de 404 döner; çoklu grup/üyelik etkisi yalnız owner başlatabilir ve audit edilir. `LearningEndpoints.cs`. |
| Veli iletişim kaydı | Tüm yerel veli kayıtlarını okur, oluşturur/düzenler; öğrenci bağlantısı ekleyip kaldırır. | Yalnız kendi atanmış gruplarındaki öğrencilere bağlı veli iletişimini okur; kayıt/bağlantı oluşturamaz, düzenleyemez veya kaldıramaz. | Veli kaydını göremez. | Kendi iletişim verisini portalda göremez. | Kişi/veli ayrıntısı yok. | Öğretmenin veli listesi yalnız kendi aktif öğrenci bağlantılarından türetilir; başka öğretmenin öğrencisine bağlı iletişim görünmez. Yerel kayıt kimlik doğrulaması, portal veya ödeme sorumluluğu oluşturmaz. `LearningEndpoints.cs`. |
| Kurs, şube ve derslik | Kurs/şube/derslik oluşturur; tümünü okur. | İlgili atanmış kursları okur; çalışma alanındaki şube/derslik listesini okuyabilir; oluşturma Owner'a aittir. | Kendi aktif kayıtlarından kursları görür. | Yok. | Yalnız toplu kurs sayısı. | Kaynaklar çalışma alanına göre scope edilir. `LearningEndpoints.cs`. |
| Grup ve grup kaydı | Grup oluşturur, öğretmen atar ve her gruba öğrenci kaydeder. | Yalnız kendine atanmış grupları görür; kendine grup oluşturabilir ve mevcut etkin öğrenciyi kendi grubuna kaydeder. Başka öğretmene grup atayamaz. | Grup oluşturamaz/kayıt yönetemez; kendi aktif grup kurslarını görür. | Yok. | Mutasyon/detay yok. | Owner herhangi bir etkin öğretmene atama yapabilir. Öğretmenin başka öğretmene atama isteği 404 döner; grup kaydı yalnız atanmış öğretmen veya owner tarafından yapılır. `LearningEndpoints.cs`. |
| Takvim, yoklama, telafi ve uygunluk | Alandaki tüm dersleri ve telafileri yönetir; çalışma alanındaki öğretmenler için zaman/uygunluk tanımlar. | Kendi atanmış dersini ve öğrencisini planlar/değiştirir/iptal eder, yoklama ve tamamlama yapar; kaynak öğretmen olduğu telafiyi onaylar; kendi izin ve uygunluk penceresini yönetir. | Yalnız kendi yaklaşan dersini, yoklama geçmişini ve telafi kredisini görür; kendi katılımını kurala göre iptal eder. | Yok. | Kişi/ders ayrıntısı yok. | Öğretmen, kendi aktif üyeliği dışındaki çalışma alanı ve başka öğretmene atanmış ders üzerinde işlem yapamaz. Finans etkisi Owner sınırında kalır. `LearningEndpoints.cs`. |
| Ödev, teslim ve ek dosya | Çalışma alanındaki ödevleri ve yetkili teslimleri yönetir. | Kendi atandığı grup/öğrenci için ödev verir; yalnız yetkili olduğu ödevin teslimini okur/değerlendirir. | Yalnız kendi ödevini/teslimini görür, teslim gönderir ve kendi izinli eklerini kullanır. | Yok. | Ödev/teslim/ek dosya ayrıntısı yok. | Ödev ve ek dosya rotaları workspace, recipient, assignment teacher/group ve öğrenci `sub` kontrolü yapar. `AssignmentEndpoints.cs`, `AssignmentAttachmentEndpoints.cs`. |
| Finans ve tahsilat | Ücret, vade, tahsilat, ters kayıt, ekstre ve raporları yönetir/okur. | Erişemez; bağımsız öğretmenin kişisel workspace'inde Owner rolündeyse erişir. | Yalnız kendisine ait toplam bakiye ve kalan paket sayısını salt okunur görür. | Portal ve finans görünümü yok. | Finans hareketi veya kişi bazlı finans verisi yok. | Öğrenci DTO'sunda ödeme/taksit hareketi yoktur. `FinanceEndpoints.cs`; karar için PostgreSQL API testi ve öğrenci portalı kabulü kaydedilmiştir. |
| Eski ürünlerden aktarım | Kaynak ürününde kurum yöneticisi yetkisi olan workspace Owner, yalnız mevcut workspace'e öğrenci kaydı aktarır. | Aktarım ve davet oluşturma ayrı iş akışıdır; otomatik davet/üyelik verilmez. | Yok. | Yok. | Global aktarım yok. | P7 API/UI Koçluk ve Hızlı Okuma öğrenci sayfalarını okur; ad soyad zorunlu, e-posta ve aktiflik seçilebilir. Varsayılan yalnız ad soyaddır. Öğrenci kimliği (WorkspaceId, SourceSystem, SourceRecordId) ile idempotent saklanır; UserId boş kalır. E-posta çakışması önizlemede durdurulur, otomatik bağlama yapılmaz. Öğretmen ve kurum alanlarının onaylanan dar aktarım kapsamı P7'de uygulandı. |

#### P0 için doğrulama ve karar kapısı

`tests/E2E/course-management-auth.integration.test.mjs` imzalı token/ürün kapsamı, farklı ürün tokenının reddi, tenant sınırı, davet ve SystemAdmin rol sınırını kapsar. Öğrenci davetinde owner seçtiği `StudentRecordId` yalnız aynı workspace'te etkin ve hesaba bağlanmamış olmalıdır; aynı e-posta eşleşmesi seçim yerine geçmez. Seçim yapılmadan aynı e-postalı yerel öğrenci varsa kabul 409 `Student.RecordSelectionRequired` ile hiçbir bağlantı yan etkisi oluşturmadan durur; owner daveti iptal edip doğru yerel kaydı seçerek yeniden yollar. Seçilen kaydın yanlış workspace'te, arşivli veya zaten bağlı olması reddedilir; kayıt eşleşmesi yoksa yeni yerel öğrenci açılır. Eşzamanlı kabul ve idempotent tekrar da sınanır. Öğretmen rolü, kendi grubu/öğrenci/veli okuma kapsamı, owner izinleri ve workspace izolasyonu aynı PostgreSQL E2E'de doğrulanır. Foundation contract testi ürün uygulaması ve korunan ürün ayrımını denetler.

| Kaynak | Bugün alınan alan | Kurs yönetimi hedefi | Durum ve sınır |
|---|---|---|---|
| İmzalı Identity tokenı | `sub` kullanıcı GUID'si ve `platform_product=course-management` | Workspace üyeliği ve davet kabulünde `UserId` | Oturum sırasında doğrulanır; eski ürün verisini taşımaz. |
| Kullanıcının kurs profil formu | Kullanıcının girdiği görünen ad | Yerel `UserProfile.DisplayName` | Identity profiline veya başka ürüne yazılmaz. |
| Davet edilen Identity hesabı | Davet hedefi `UserId` GUID'si ve varsa token e-posta değeri; kabulde görünen ad alınır. | Student rolüyse owner'ın seçtiği yerel `StudentRecordId` bağlantısı veya eşleşme yoksa yeni yerel kayıt. | Yerel kayıt e-posta ile otomatik bulunup bağlanmaz. Owner seçimi aynı workspace'te etkin ve hesaba bağlanmamış kayıtla sınırlıdır; aynı e-posta kaydı seçilmemişse kabul 409 ister. Yeni kayıt kota sayacına eklenir; seçilmiş mevcut kayıt yeni öğrenci kotası tüketmez. |
| Elle giriş veya P3 CSV | `displayName`, isteğe bağlı `email`, `phoneNumber` | Yerel `StudentRecord` | CSV önizleme, satır doğrulama, mükerrer kontrolü ve güvenli tekrar çalıştırma var; Identity hesabı/rol üretmez. |
| Koçluk ve hızlı okuma ürün kayıtları | Koçluk GET /api/institution/students ve Hızlı Okuma GET /api/speed-reading/institutions/{institutionId}/members?role=Student kaynak ürün yetkisiyle sayfalı roster verir; Hızlı Okuma kurum kimliği /api/institution/speed-reading/me üzerinden alınır. | Seçilen öğrenci satırları için StudentRecord.DisplayName, isteğe bağlı Email ve IsActive; kaynak ID ayrı eşlemede | P7 owner aktarım API/UI ve alan sözleşmesi uygulandı. Geçici kaynak tokenı bellekte kullanılır; Course Management oturumu değişmez. Eski servis DB'sine yazma ve çift yönlü eşitleme yoktur. Öğretmen roster'ı ayrı okunur ve tek tek seçilen mevcut Identity userId'si Teacher davetine gider; üyelik davet kabulünde etkinleşir. Kurum adı/türü seçilip önizlenerek mevcut Institution workspace'ine yazılır; yeni workspace veya eski rol/profil/atama taşınmaz. |

Kaynak şeması incelemesi (13 Ekim 2026; bu envanter aktarım izni değildir):

| Kaynak kayıt | Kodda bulunan alan grupları | Kurs ürününe olası sınır / dışlama |
|---|---|---|
| Identity `User` ve `StudentProfile` | Sabit `User.Id`; e-posta, ad/soyad, telefon; öğrenci kurum bağlantısı, doğum tarihi, cinsiyet, sınıf, okul, veli bağlantısı, öğrenme stili ve günlük hedef | `User.Id` yalnızca aynı hesabın açıkça bağlanması için adaydır; parola/credential taşınmaz. Öğrenci adı ve sınıf gibi alanlar owner seçimi/önizlemesine tabi adaydır. Doğum, cinsiyet, okul ve veli ilişkisi ayrı seçim ve hukuki dayanak olmadan aktarılmaz. |
| Identity `TeacherProfile` ve `Institution` | Öğretmen `UserId`, ad/soyad, unvan, branş, deneyim, sertifika ve kurum bağlantısı; kurum adı/türü, adres/şehir/ilçe, telefon/e-posta/site, vergi numarası, lisans ve limitler | Onaylanan dar kapsam: kurum adı ve türü ayrı seçilip önizlenerek mevcut Institution workspace'ine yazılır; yeni workspace açılmaz. Öğretmen için yalnız owner'ın seçtiği mevcut UserId Teacher daveti oluşturur; profil alanları, eski rol ve kaynak ataması taşınmaz. Vergi/lisans/limit ve hassas iletişim bilgileri varsayılan dışarıda; kullanıcı/kurum otomatik eşleştirmesi yok. |
| Speed Reading `SpeedReadingInstitutionMembership` | `InstitutionId`, `UserId`, Student/Teacher rolü, aktiflik | Owner seçtiği Teacher üyelerini mevcut kaynak UserId ile davet eder; kaynak aktifliği tek başına erişim vermez ve kurs üyeliği yalnız davet kabulünde etkinleşir. Öğrenci ataması, profil/okuma hedefleri ve eski izinler aktarılmaz. |
| Speed Reading `SpeedReadingUserProfile` | Seviye, WPM/anlama hedefi, günlük hedef, yaş grubu, doğum tarihi, öğrenme stili, tarihsel ad/e-posta ve kurum | Koşullu kurs aktarımının dışında tutulur; bunlar hızlı okuma ürünü ilerleme/öğrenme verisidir. |
| Coaching `CoachingSession`, `Assignment`, `AcademicGoal` | Öğretmen/öğrenci/kurum referansları, oturum, ödev ve hedef kayıtları | Ayrı ürünün akademik/koçluk geçmişidir; kullanıcı kaynak bazında farklı bir karar vermedikçe kurs ürününe taşınmaz. Eski servis verisi değiştirilmeyecek. |

Bu tablo kaynak kod alanlarını ve koruyucu sınırları gösterir. 13 Ekim 2026 kararı yalnız seçilen öğrencileri, seçilen mevcut Identity userId'lerine gönderilen Teacher davetlerini ve kurum adı/türünü mevcut Institution workspace'ine almayı onaylar. Otomatik eşleştirme, eski rol/profil/grup ataması ve yeni workspace oluşturma kapsam dışıdır; diğer alanlar için ayrıca ürün/hukuk kararı gerekir.

#### Course Management verisi ve mevcut yaşam döngüsü (kod envanteri; hukuk politikası değildir)

| Veri grubu | Kimlikle/kişiye bağlanan alanlar | Bugünkü kod davranışı ve saklama kanıtı |
|---|---|---|
| Profil, çalışma alanı, üyelik ve davet | UserId/OwnerUserId/TargetUserId, yerel görünen ad, workspace adı, rol ve davet durumu | Account erasure kimlik bağını ve yerel profili kaldırır, hedef davet/reminder kayıtlarını siler; ortak iş kayıtlarındaki kullanıcı GUID’lerini anonim GUID ile değiştirir. Silinen kullanıcı tek Owner ise workspace anonim Owner kimliğiyle salt okunur tutulur; başka etkin Owner varsa sahiplik devredilir. Mevcut kod workspace adını silme/anonimleştirme sırasında değiştirmez; kişiyi tanımlayan ad/başlık içerip içermediği saklama incelemesinde doğrulanmalıdır. Davet süresinin dolması tek başına fiziksel temizleme yapmaz.
| Öğrenci ve veli rehberi | Öğrenci `UserId`, görünen ad, e-posta, telefon; veli görünen ad, e-posta, telefon ve öğrenciyle ilişki | Öğrenci arşivi `IsActive`/üyelik/kayıt durumunu kapatır; öğrenci ve bağlı geçmiş kayıtlar kalır. Veli bağlantısı pasifleştirilebilir; veli iletişim kartı için otomatik saklama/temizleme bulunmaz. |
| Ders ve akademik geçmiş | Öğretmen/öğrenci GUID’leri, ders/grup/kurs adları, takvim/yoklama/telafi; ödev başlık/açıklaması, teslim metni, puan ve geri bildirim; öğretmen izin gerekçesi | Silinen öğrencinin profil/iletişim alanları, başka öğrenciyle paylaşılmayan guardian kayıtları, teslim yanıtı/serbest geri bildirim ve teslim dosyaları silinir. Not ile SubmittedAt/ReviewedAt anonim akademik geçmişte kalır. Ayrı Status alanı yoktur; API teslim durumunu gönderim/değerlendirme zamanı ve DueAt üzerinden türetir. Silinen öğretmenin atama/grup/ders referansları anonimleştirilir; atama başlık/açıklaması, workspace/kurs/grup adları ve izin gerekçesi mevcut kodda kalır. TeacherMaterial dosyaları saklanır; yalnız yükleyen kullanıcı kimliği anonimleştirilir. Bu serbest metin/ad alanlarının saklanması veya anonimleştirilmesi hukuk/ürün kararına açıktır.
| Finans defteri | Öğrenci/işlem GUID’leri; ücret, taksit, tahsilat tutarı, tarih, yöntem, serbest not ve paket hareketleri | Gerekli iş/finans geçmişi korunur; öğrenci/aktör bağlantıları anonim GUID’e çevrilir, serbest ödeme notu temizlenir ve taksit etiketi genelleştirilir. Tutar/tarih/yöntem ve ters kayıtlar korunur. Yasal saklama süresi ve anonimleştirmenin yeterliliği hukuk/ürün onayı bekler.
| Ödev eki dosyaları | Dosya adı, türü, SHA-256, storage anahtarı, yükleyen/öğrenci/ödev kimliği ve yükleme/son tarih | Kullanıcı kararı: son teslim tarihinden 180 gün sonra dosya ve metadata silinir. Erasure teslim dosyalarını siler; silinen öğretmenin yüklediği TeacherMaterial dosya ve metadata kayıtları korunur, yükleyen kullanıcı GUID'i anonimleştirilir. Worker saatlik en çok 100 kayıt işler. Yedek kopyasının ve logların saklama süresi bu kurala dahil değildir ve ayrıca kararlaştırılmalıdır. |
| Audit, hatırlatma ve mesaj outbox | Aktör/hedef/alıcı GUID’leri, eylem, entity, mesaj tipi ve zaman damgaları | Erasure workspace audit aktör/hedef bağlantılarını anonimleştirir ve silinen alıcının Course Management reminder kayıtlarını kaldırır. Eski outbox/inbox payload ve yedek snapshot purge/retention politikası henüz yoktur; P0/P8 release kapısı açık kalır.
| Notification servisi kayıtları | UserId, başlık, mesaj, ürün tipi ve ilgili kayıt kimliği | Notification kendi Account erasure tüketicisiyle kayıtları temizler; Course Management aynı Account isteğinde ayrı assessment/execution tüketicisiyle değerlendirilir. Her iki servis sonucu tamamlanmadan Identity hesabı silinmez. Notification erasure kursun finans/akademik geçmişini değiştirmez.

Kod izleri: `CourseManagement.Domain` entity'leri; `CourseAssignmentAttachmentCleanupService`; Identity `DataErasureAssessmentScopePolicy`; Notification `NotificationErasureAssessmentService` ve `NotificationErasureExecutionService`. Bu tablo kişisel veri envanteridir; hukuki saklama süresi, işleme dayanağı veya silme yetkisi kararı değildir.

Rol sınırı, aktarım ve teknik Account erasure davranışı kod ve test kapsamıyla belgelenmiştir; Identity assessment endpointi ve Course Management execution-time recheck yerel Docker E2E ile de doğrulanmıştır. P0/P8 kapanışında hukukî retention/işleme dayanağı, outbox/inbox/yedek purge, hosted CI, production işletim eşikleri, korunan ürün regresyonu ve pilot kanıtları kalır.

| Açık karar | Bugünkü varsayım/kısıt | Gerekli olduğu faz | Karar kanıtı |
|---|---|---|---|
| Öğrenci hesap kaydı ve katılım | Karar verildi: öğrenci mevcut Identity hesabıyla daveti kabul eder; yeni self-servis öğrenci hesabı yoktur. Bir Identity hesabı için çalışma alanı başına en fazla bir öğrenci kaydı korunur. | P2 | Mevcut davet/kabul ve tekil `UserId` sınırının PostgreSQL testleri; yeni hesap açılmadığına dair öğrenci uygulaması kontrolü. |
| Davet kabulinde yerel öğrenci bağlantısı | Kullanıcı kararı: owner Student daveti oluştururken bağlanacak yerel `StudentRecordId` kaydını açıkça seçer; sistem e-posta/ad ile otomatik eşleştirme yapmaz. Seçim aynı workspace içindeki etkin ve hesaba bağlanmamış kayıtla sınırlıdır. Seçim yokken aynı e-postalı yerel kayıt varsa kabul 409 `Student.RecordSelectionRequired` döndürür ve yan etki üretmez; owner iptal edip doğru kayıtla yeni davet yollar. Eşleşme yoksa yeni yerel kayıt oluşturulur. | P2 — uygulandı | Migration `20261013062059_P2ExplicitStudentInvitationLink`, API/UI ve PostgreSQL E2E; aynı-workspace/etkin/bağlantısız doğrulaması, seçimsiz aynı-e-posta 409'u, no-match yeni kayıt, rol/workspace/arşiv reddi ve concurrent/idempotent kabul senaryoları geçti. |
| Ürünler arası öğrenci, öğretmen ve kurum aktarımı | Owner kaynak kurum listesinden kaydı açıkça seçip önizler; ilk kayıt otomatik seçilmez. Öğrenci aktarımı seçilen ad (zorunlu) ve isteğe bağlı e-posta/aktiflik alanlarını yazar; UserId boş kalır ve kaynak kimliği idempotent eşlemede tutulur. Öğretmenler tek tek seçilir ve yalnız mevcut Identity userId ile Teacher daveti oluşturulur; üyelik kabulden önce etkinleşmez. Kurum adı/türü ayrı seçilip önizlenerek yalnız mevcut Institution workspace'ine aktarılır. E-posta/ad eşleşmesi, eski rol/profil/grup ataması ve otomatik yeni workspace yoktur. | P7 — onaylanan aktarım kapsamı uygulandı | PostgreSQL E2E owner/kaynak yetkisini, yanlış kaynak reddini, preview/commit/replay, seçilen alanları, stale preview reddini, IndependentTeacher reddini ve teacher pending/acceptance akışını doğrular; staff source import unit testleri 6/6, production build geçti. Chrome panel smoke 4/4 içinde owner akışı seçilmiş öğretmeni `targetUserId` ile yalnızca Pending davete dönüştürür; seçili kurum alanlarını mevcut workspace'e yazar ve workspace oluşturulmadığını doğrular. |
| Kurum/öğretmen/öğrenci davet iletişimi | Davet API'si mevcut Identity kullanıcı GUID'sine yöneliktir; e-posta/SMS gönderimi ve bilinmeyen hesaba davet yoktur. | P2/P7 | Davet kanalı, doğrulama, süre, yeniden gönderim, iptal ve yanlış alıcı senaryoları. |
| Ders iptali, gelmeme ve telafi | Karar verildi ve uygulandı: kurum/öğretmen tekil/toplu iptali etkin öğrencilere 30 günlük kullanılabilir kredi verir; öğrenci 24+ saat önce yalnız kendi katılımını iptal ettiğinde onay bekleyen kredi alır; geç iptal/no-show kredi vermez; öğretmen onaylar ve telafiyi planlar. Tamamlanma krediyi tüketir, iptal edilmiş telafi dersi süresi içindeyse krediyi geri açar. | P4 tamamlandı | Grup/bire bir iptal, erken/geç katılım iptali, yetki, süre sonu, kredi planlama/yeniden açma/tüketim PostgreSQL ve Chrome kabul testleri. |
| Ücret, taksit ve ödeme kaydı | Karar verildi ve P5'te uygulandı: TRY manuel nakit/EFT; kullanıcı tanımlı tutar/vade ve taksit; kısmi dağıtım; online ödeme/e-belge/vergi yok; ödeme, borç ve paket hareketleri ters kayıtla düzeltilir; aylık sabit ücrette prorata yok; tamamlanan ders `Absent` olsa da ücret/paket hakkı tüketilir. | P5 tamamlandı | Dönem/aylık/ders/saat/paket API akışları, kısmi tahsilat, tekrar/idempotency, ters kayıt ve özet mutabakatı `course-management-auth.integration.test.mjs` içinde doğrulandı; owner finans ekranı Chrome smoke'ta geçti. |
| Öğrenci finans görünürlüğü | Kullanıcı 13 Ekim 2026'da onayladı: öğrenci yalnız kendi toplam bakiyesini ve kalan paket hakkını salt okunur görür; ödeme/taksit hareketi ve veli görünümü yoktur. | P5/P6 doğrulandı | PostgreSQL API kabul testi ikinci öğrenciyi dışlar ve yanıt alanlarını yalnız `workspaceId`, `workspaceName`, `balance`, `packageBalances` ile; paket alanlarını yalnız `agreementId`, `grantedSessions`, `remainingSessions`, `expiresAt` ile sınırlar. Edge Playwright akışında bakiye ve kalan hak özeti görüldü. |
| Öğretmen öğrenci/grup/veli sınırı | Kullanıcı kararı: öğrenci kartı/CSV ve arşiv owner'da; öğretmen yalnız kendi grubuna atanmış aktif öğrencilerle öğrenme akışını yürütür, kayıtlı öğrenciyi kendi grubuna ekleyebilir ve o grubun veli bilgisini okuyabilir; başka öğretmene atama ile veli bağlantısı/kartı değişikliği owner'dadır. Bağımsız öğretmen kendi alanında Owner ise tam yetkilidir. | P0/P3 | Uygulandı ve PostgreSQL E2E ile doğrulandı: owner pozitifleri, öğretmen create/update/archive/CSV/link/edit/unlink ve cross-assignment negatifleri, kendi grup/öğrenci/veli okuma kapsamı ve başka öğretmen kaydının görünmemesi. |
| Öğrenci/veli erişimi ve küçükler | Karar verildi: ilk sürüm veli portalı yoktur. Yerel veli iletişim kaydı doğrulanmış kimlik veya erişim sağlamaz; öğrenci kendi izinli akademik verisini görür. Dosya/rıza/kanuni saklama kuralları hukuk onayı ister. | P2/P6 | Öğrenci ve veli token'ı için negatif erişim testleri; hukuk onaylı rıza, veri saklama ve görünürlük matrisi. |
| Veri koruma ve dosya saklama | Kullanıcı kararı: ekler ödev dueAt tarihinden 180 gün sonra silinir; account silme sürecinde student submission metni ve dosyaları silinir; not/durum/tarih anonim kalır. TeacherMaterial içeriği/metadata saklanır ve yükleyen kimliği anonimleştirilir. PDF/JPEG/PNG ve 10 MiB teknik varsayılandır, karar değildir. Workspace/kurs/grup adları ve öğretmenin ödev açıklaması/izin gerekçesi gibi kalan serbest metinler henüz scrub edilmez. | P0/P6/P7/P8 | Hukuk/ürün onaylı genel saklama, KVKK dayanağı; Identity readiness endpoint'i bağlandı. Kalan serbest metin ve ad alanlarının saklanma/anonimleştirme kuralı; export/erasure ve dosya kısıt matrisi; yedek/outbox purge planı ve testleri. Identity sonucu hazır değilse veya hold varsa execution kapalıdır.
| Identity account erasure ve Course Management | Identity Account RequiredServices Coaching, Notification, SpeedReading ve CourseManagement servislerini zorunlu sayar; dört execution tamamlanmadan Identity hesabı silinmez. StudentSubmission metni/dosyaları silinir; not/durum/tarih ve gerekli finans geçmişi anonim kalır. TeacherMaterial dosyası/metadata saklanır, yükleyen hesap GUID’i anonimleştirilir. Course Management, Identity yönetim ekranındaki aynı istek aggregate'ini kullanan iç readiness endpoint'inden okur. İlk Course Management assessment'i kendi servis verisini raporlar; execution öncesi tamamlanmış aggregate yeniden doğrulanır. ActiveHold, Unknown, incomplete/mismatched result veya provider hatasında kayıt değişmez. Başka etkin Owner varsa devir; tek Owner ise anonim Owner ve salt-okunur workspace uygulanır. | Identity assessment entegrasyonu ve teknik fail-closed kapsamı uygulandı; P0/P8 hukuk, retention ve operasyon kapıları açık | Identity readiness handler, service-key endpoint ve Course Management provider testleri; Course Management unit/integration ve RabbitMQ/PostgreSQL erasure testleri. Yasal saklama/KVKK, retention/outbox/inbox/backup purge ve hosted CI kanıtı gerekir; disposable Docker privacy E2E 6/6 geçti.
| Takvim varsayılanları | Her seri açık IANA saat dilimi ister; çalışma alanı varsayılanı, tatiller, yaz saati sonrasındaki değişiklik ve kişisel/kurumsal takvim çakışma politikası yok. | P0/P4 | Saat dilimi kaynağı ve DST/holiday örnekleri; kurum ve kişisel alanların izole kalacağına dair kabul testi. |
| Ödev teslim hatırlatması | Kullanıcı 14 Ekim 2026'da in-app bildirimin `dueAt` öncesi 24 saatte gönderilmesini onayladı; kod ve `CourseManagementReminderTests` zamanlama testi bu kuralı doğrular. | P6 kararı verildi ve uygulandı | `LessonAndAssignmentRemindersAreScheduledTwentyFourHoursBeforeTarget` hedefli tekrar 14 Ekim'de 1/1 geçti; hem ders hem ödev hedefinden 24 saat öncesi doğrulanır. |
| Platform aboneliği ve kısıt | Kullanıcı kararı: atanmış plan başlangıç zamanından önce, bitiş zamanında/sonrasında ve manuel askıya alındığında çalışma alanı salt okunurdur. Owner/öğretmen/öğrenci mevcut kayıtları okuyup dışa aktarabilir; iş verisi yazmaları engellenir; kişisel profil ve owner'ın salt-okunur import önizlemesi açık kalır; yalnız SystemAdmin yeniden etkinleştirir. Owner salt okunur plan özeti ve atanmış plan öğrenci limiti uygulanır; plansız workspace kota ve abonelik kapısı dışında kalır. | P7 doğrulandı | PostgreSQL API testi aktif/askıda/süresi dolmuş plan, öğrenci ve kurs yazmaları, davet kabulü, geçmiş okuma, profil güncellemesi ve SystemAdmin yeniden açmasını doğrular. Kullanıcı onaylı abonelik-only audit endpoint'i yetki/alan/sayfalama kabul testleriyle doğrulanmıştır; öğrenci importu yalnız Owner ve kaynak ürün yetkisiyle sınırlıdır. |
| Admin destek erişimi | SystemAdmin toplu özet ve abonelik planı yanında abonelik atama/güncelleme audit'ini çalışma alanı adı/türü, eylem ve zaman olarak salt okunur görür. Aktör/hedef/workspace kimliği, iletişim, öğrenci/ödev/finans ayrıntısı, impersonation ve abonelik dışı destek mutasyonu yoktur. | Dar abonelik audit'i P7'de onaylandı ve uygulandı; daha geniş kullanıcı/üyelik erişimi, impersonation ve hassas kayıt desteği MVP kapsamına alınmadı | API anonim/yanlış ürün/admin olmayan tokenları reddeder; sayfayı en çok 100 kayıtla sınırlar, yalnız iki abonelik eylemini döndürür ve response alan kümesini doğrular. Daha geniş destek gerekirse izin listesi, gerekçe, audit ve tenant sınırları ayrıca onaylanmalıdır. |
| Yayın, ölçek ve işletim | Yerel Compose overlay'i var; üretim topolojisi, SLO, hedef veri hacmi, eşzamanlılık, RPO/RTO ve destek hedefleri yok. | P0/P1/P7/P8 | Ortam/secret/backup/restore/rollback runbook'u ve pilotta ölçülüp onaylanan eşik tablosu. |
| Öğrenci uygulaması adresi ve erişilebilirlik | Ayrı Angular uygulaması var; kurs cookie path'i ve personel ürün seçiminin sekme başına saklanması uygulandı. Production host/path, desteklenen ekranlar ve WCAG hedefi karara bağlanmadı. | P0/P1/P8 | Production host/domain ve cookie/CORS threat check; eski ürünle çok sekmeli oturum regresyonu; mobil ve erişilebilirlik kontrol listesi. |
| Frontend bağımlılık güvenliği | Kullanıcının kararıyla yalnız Course Management öğrenci paketi kapsamda. @angular/build ve @angular/cli 21.2.26’ya yükseltildi; ağ erişimli npm audit 0 bulgu verdi, npm production build geçti. Ortak admin-panel kilidi ve Coaching/Speed Reading paketleri değiştirilmedi; daha önce görülen ortak admin audit bulguları bu kararın dışında kalır. | Kurs Yönetimi paketi yerel olarak temiz; hosted CI sonucu bekliyor | clients/course-management kilitli bağımlılıkları için npm audit --audit-level=high ve production build geçmelidir. Ortak admin-panel değişiklikleri ayrıca ürün regresyonu ve ayrı yetkilendirme gerektirir; tam monorepo CI bu dış kapsam audit nedeniyle yine kırmızı olabilir.

Bu kararların çıktısı ekran taslakları, örnek iş senaryoları ve test edilebilir kabul koşulları olur. Ürünler arası ortaklaştırma veya eski özellikleri yeniden yazma gerektirmez; yeni ürün sınırındaki kontratlar üzerinden çözülür.

## 15. Araştırma ile özellik eşleştirmesi

12 Ekim 2026 kontrolünde ürünlerin resmi sayfaları yeniden okunmuştur. Buradaki bilgiler üretici tarafından açıklanan özelliklerdir; bağımsız kullanım testi, pazar payı, fiyat veya entegrasyon uygunluğu doğrulaması değildir. Özelliklerin MVP/sonraki faz seçimi bizim kapsam kararımızdır.

| Araştırma kaynağı | Doğrulanan özellik grubu | Bu plandaki karşılığı |
|---|---|---|
| [AKINSOFT resmi yardım dosyası](https://bilgibankasi.akinsoft.net/tr/home/makale/1622-akinsoft-kurs-otomasyon-yardim-dosyasi) | Öğrenci/grup/sınıf, ders programı, bakiye/taksit ve öğretmen ödemeleri | Öğrenci, takvim, taksit MVP; öğretmen hakedişi sonraki faz |
| [ArtechSchool özellikleri](https://www.artechschool.com/ozellikler) | Öğrenci, ders, ödeme, CRM ve kurum operasyonları | Öğrenci/ders/ödeme MVP; CRM ve daha geniş entegrasyonlar sonraki faz |
| [EduSMS resmi sayfası](https://edusms.com.tr/) | Öğrenci/veli kaydı, ders programı, yoklama, ödeme planı ve hatırlatmalar | Veli, kurs kaydı, takvim, yoklama ve tahsilat MVP; SMS kanalı sonraki faz |
| [TutorCruncher özellikleri](https://tutorcruncher.com/features) | Müşteri yönetimi, takvim, faturalama/öğretmen ödemeleri, iletişim ve raporlar | Çalışma alanı/takvim/finans/rapor MVP; hakediş, online ödeme, CRM ve marka özelleştirme sonraki faz |
| [Teachworks resmi sayfası](https://www.teachworks.com/) | Bireysel/grup dersleri, öğretmen müsaitliği, oda planlama, öğrenci yönetimi ve öğretmen saat/ücret takibi | Takvim/oda/öğrenci MVP; hakediş ve ileri entegrasyonlar sonraki faz |
| [TutorBird öğrenci portalı](https://www.tutorbird.com/student-portal/) | Öğrenci/veli portalı, rezervasyon, telafi, bakiye ve çalışma dosyaları | Portal/telafi/ödev teslimi MVP; self-servis rezervasyon sonraki faz; çalışma süresi günlüğü kapsam dışında |
| [Classcard resmi sayfası](https://www.classcardapp.com/) | Takvim/yoklama, rezervasyon, üyelik/ücret, materyal ve raporlar | Takvim/yoklama/paket/materyal/rapor MVP; rezervasyon/online ödeme/markalı uygulama sonraki faz |

Araştırmada görülen her özellik ilk sürüm gereksinimi değildir. Çalışma süresi günlüğü, deneme sınavı/optik analizi, kartlı geçiş ve video ders altyapısı talep edilen günlük kurs operasyonlarının dışında tutulur. Sektörel ihtiyaç pilotta doğrulanırsa ayrı kapsam olarak değerlendirilir; koçluk/hızlı okuma özelliği buraya taşınmaz.

## 16. Kullanıcı ihtiyaçları için izlenebilirlik ve kontrol sonucu

| İnsan tarafından istenen ihtiyaç | Plan karşılığı | Faz | Kabul kanıtı |
|---|---|---|---|
| Mikroservis projesine yeni servis | Ayrı servis, DB ve API yolu | P1 | Yeni API bağımsız çalışır; eski servis diff'i yok |
| Kursların öğrencilerini takip etmesi | Öğrenci kartı, kurs/grup kaydı, veli ve geçmiş | P3 | Hesaplı/hesapsız öğrenci kursa kaydedilir ve geçmiş izlenir |
| Ders saatlerini planlama | Tekrarlayan/tek ders, oda ve öğretmen müsaitliği | P4 | Çakışma ve seri değişikliği senaryoları geçer |
| Ödemeleri takip etme | Dört ücret modeli, taksit, tahsilat, bakiye ve iade | P5 | Anlaşma → borç → kısmi ödeme → kalan tutar doğru hesaplanır |
| Öğretmenin özel derslerini takip etmesi | Bağımsız öğretmen çalışma alanı ve finansı | P2–P5 | Kurumda çalışan öğretmenin kişisel alanı ayrı çalışır |
| Öğrenciye ödev verme | Yeni ürün ödev/teslim/değerlendirme modeli | P6 | Öğretmen verir, öğrenci teslim eder, geri bildirim görünür |
| Koçluk ve hızlı okumadan ayrı olması | Korunan alanlar, yeni modeller ve runtime bağımsızlık | Her faz | Veri/API/yetki ve eski akış regresyonları geçer |
| Kullanıcı Identity, profil burada | Ortak hesap; yerel ürün profili/üyelik | P2 | Profil güncellemesi başka ürün veya ortak hesaba yazmaz |
| Mevcut kurum/öğrenci/öğretmenin aktarımı | Owner-seçimli öğrenci roster aktarımı, Identity userId'siyle Teacher daveti ve kurum adı/türünün mevcut workspace'e aktarımı; otomatik hesap eşleştirmesi yok | P0, P2, P7 | Seçilen alan, davet kabulü, idempotency ve mevcut workspace sınırı doğrulanır |
| Öğrenci panelinin sıfırdan olması | Ayrı `clients/course-management` | P1, P4–P6 | Ayrı build/yayın; eski öğrenci ekranlarında değişiklik yok |
| Kurum/öğretmen portalına üçüncü ürün | Yeni staff feature modülü | P1–P6 | Eski ürünler korunarak yeni alan açılır |
| Admin genişletmesi | Ayrı admin feature modülü | P1, P2, P7 | Yeni ürüne özel yönetim ve audit |
| Diğer kısımların değişmemesi | Korunan dosyalar ve sınırlı ortak kayıt listesi | Her faz | İzin verilen kayıt noktaları dışında eski kod diff'i yok |
| Türkiye ve dünya araştırmasına uygunluk | Kaynak/özellik/faz matrisi | Plan ve pilot | Temel operasyonlar MVP; ertelenen farklar açık |

Kontrol sonucu: İnsan tarafından belirtilen ana ihtiyaçların tamamı revize planda karşılık ve kabul ölçütüne sahiptir. Bu sonuç teknik uygulanabilirliğin veya testlerin tamamlandığı anlamına gelmez. Özellikle kaynak verinin erişilebilirliği, ortak session davranışı, ücret/iptal politikaları ve gerçek kapasite hedefleri P0'da doğrulanacaktır.

Bu denetimde düzeltilen eksikler: araştırma kaynaklarının izlenebilirliği; aylık/ders-saat ücretlendirmesi; yeni kurum/öğretmen self-servis katılımı; owner-seçimli öğrenci roster aktarımı, Identity userId Teacher daveti ve kurum adı/türü aktarımının açık sınırları; temel/ileri şube desteği ayrımı; rapor ve bildirim kanal kapsamı; platform aboneliği yaşam döngüsü; hesap/üyelik iptali ve çok sekmeli oturum koruması. Veli, hakediş, online ödeme, CRM ve diğer büyüme özellikleri ilk kullanıcı talebiyle araştırma önerilerinden ayrı olarak işaretlenmiştir; ilk sürüme otomatik geniş kapsam eklenmez.

Pilot başlangıcından önce hedef veri hacmi, eşzamanlı kullanıcı sayısı, öğrenci kaydı/ders planlama işlemlerinin kabul edilen süreleri ve performans sınırları yazılı olarak belirlenir. Bu ölçümler henüz verilmediğinden plan belirli bir ölçek veya gecikme garantisi iddia etmez.
