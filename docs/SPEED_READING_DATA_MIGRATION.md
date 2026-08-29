# Hızlı Okuma Veri Taşıma ve Uyumluluk Planı

## Mevcut durum ve final bağımsız ownership hedefi

Owned veri modeli, runtime cutover, gerçek production backfill/parity ve aktif
servis izolasyonu tamamlandı. 2026-08-29 operasyon kanıtları aşağıda kayıtlıdır.
Authenticated E2E için gerçek bir test hesabı ile login ve kullanıcı akışlarının
ayrıca doğrulanması gerekir; legacy veritabanı ve önceki image'lar rollback için
bilinçli olarak korunmaktadır.
Kapanış kapıları:

1. Speed Reading kendi PostgreSQL veritabanını ve kendi migration geçmişini
   kullanır; eski Hızlı Okuma veritabanına production runtime erişimi yoktur.
2. Tüm Speed Reading yazma kuralları yeni domain/use-case/repository
   katmanında çalışır; `LegacySpeedReading*` sınıfları runtime dependency
   olmaktan çıkar.
3. Identity, Coaching ve Notification verileri doğrudan SQL ile okunmaz;
   servisler arası versioned contract/event kullanılır.
4. Eski veriden yeni şemaya backfill, checksum/row-count karşılaştırması,
   shadow read ve write cutover kanıtları saklanır.
5. Eski uygulama ve eski veritabanı kapalıyken login, katalog, egzersiz,
   sonuç, ilerleme, rapor ve yönetim akışları çalışır.
6. Bağımsız backup/restore, health/metrics/logging, rollback ve production
   E2E sonuçları geçerlidir.

### Geçiş fazları

| Faz | Durum | Kapanış ölçütü |
| --- | --- | --- |
| Baseline ve auth/session hizalama | Tamamlandı | 48 frontend test GREEN; canonical auth ve HttpOnly session akışı |
| Yeni owned şema ve migration altyapısı | Tamamlandı | `speedreading_owned_db` ve bağımsız EF migration geçmişi production'da doğrulandı |
| Core exercise/session/progress iş mantığı | Tamamlandı / aktif | Owned runtime, backfill ve 60 tablo parity kontrolü GREEN |
| Content/program/report/gamification slice’ları | Tamamlandı / aktif | Owned store ve canlı endpoint smoke doğrulandı; Iyzico bilinçli olarak ertelendi |
| Frontend/gateway legacy endpoint temizliği | Tamamlandı / uyumluluk alias'ları korunuyor | Aktif standalone endpoint'ler 404 üretmiyor; geriye dönük `/api/v1` alias'ları kontrollü biçimde duruyor |
| Shadow read ve write cutover | Tamamlandı / aktif servis | Servis dedicated owned role kullanıyor; legacy egzersiz tablolarında bu role `INSERT=false` |
| Eski runtime/DB erişiminin kaldırılması | Kontrollü tamamlandı; cleanup bekliyor | Aktif servis legacy DB'ye erişmiyor; eski DB/image'lar rollback için tutuluyor |

### Production operasyon kanıtı (2026-08-29)

- Backup: `/opt/eduivme/backups/speed-reading/20260829T043855Z`; legacy ve
  owned dump'ları alındı, SHA-256 değerleri backup dizininde doğrulandı.
- Restore drill: geçici PostgreSQL veritabanlarına iki dump geri yüklendi,
  tablo/katalog sayımları doğrulandı ve yalnızca drill veritabanları silindi.
- Migration: `b0d3810` image'ı ile owned migration çalıştı; `No migrations
  were applied. The database is already up to date.` sonucu alındı.
- Parity: `--verify-owned-parity` 60 tabloyu karşılaştırdı; `IsMatch=true`,
  mismatch sayısı `0`.
- Runtime isolation: canlı servis `speedreading_owned_runtime` kullanıyor;
  `speedreading_owned_db` ve `speed_reading` sahipliği bu role ait, legacy
  egzersiz tablolarında yazma yetkisi yok.
- Live smoke: health/live, health/ready, capabilities, CMS ve subscription
  endpoint'leri 200; oturum gerektiren phase-plan ve reading-texts endpoint'leri
  oturumsuz istekte beklenen 401 döndürüyor.

## Uygulanan owned runtime slice’ları

Bağımsız veri sınırı kod tabanına eklendi. `SpeedReading__OwnedDataEnabled=true`
ile normal web runtime yalnızca owned bağlantıyı kullanır; canlı trafik cutover'ı
ve production backfill operasyonu tamamlanmış, kanıtları üstte kaydedilmiştir:

- `SpeedReading.Domain` altında egzersiz, okuma metni/sorusu ve egzersiz
  oturumu/sonucu aggregate'leri bulunur. Oturum yaşam döngüsü, pause süresi,
  timeout, cevap tekilleştirme ve sonuç tekilliği kuralları burada korunur.
- `OwnedSpeedReadingDbContext`, legacy `SpeedReadingDbContext`'ten tamamen
  ayrıdır ve yalnızca `speed_reading` şemasındaki yeni tabloları modeller:
  `exercises`, `reading_texts`, `reading_questions`, `exercise_sessions`,
  `exercise_session_answers`, `exercise_session_results`, `reading_sessions`.
- `CreateOwnedSpeedReadingCore` migration'ı ayrı migration history tablosu
  (`speed_reading.__ef_migrations_history`) kullanır. Legacy tabloya
  `ALTER`, `DROP` veya business migration uygulanmaz.
- Yeni bağlantı `ConnectionStrings:SpeedReadingOwned` veya
  `SPEED_READING_OWNED_CONNECTION_STRING` ile verilir. Bağlantı boşsa servis
  eski çalışma düzenini korur; bağlantı verilirse migration-only container
  owned migration'ı da uygular ve readiness health check'i ekler.
- İlk katalog backfill'i servis image'ı ile açıkça
  `--backfill-owned-catalog` argümanıyla çalışır. Komut legacy'den kategori,
  tür, egzersiz, metin ve soruları aynı UUID'lerle tek transaction içinde
  kopyalar; duplicate/orphan kayıt varsa durur, ikinci çalışmada mevcut ID'leri
  yeniden eklemez. Bu komut cutover yapmaz ve runtime endpoint'lerini owned
  store'a çevirmediği için production'da ancak backup/parity onayından sonra
  çalıştırılmalıdır.
- Katalogdan sonra session/progress geçmişi servis image'ı ile açıkça
  `--backfill-owned-sessions` argümanıyla çalıştırılır. Komut egzersiz
  oturumlarını, JSON içindeki cevapları, egzersiz sonuçlarını ve okuma
  geçmişini aynı UUID'lerle taşır; artık kaynağı bulunmayan sonuçlarda
  `legacy_session_id` korunur. İşlem tek transaction ve insert-only'dir;
  hatalı referans, bilinmeyen durum veya bozuk/çakışan cevap verisi varsa
  sessizce atlamak yerine durur. Önce katalog backfill'i yapılmalıdır.
- Assignment geçişi `--backfill-owned-assignments` komutuyla katalogdan sonra
  çalıştırılır. Assignment ve öğrenci-atama kayıtları aynı UUID'lerle taşınır;
  aktif öğrenci üyeliği için `(assignment_id, student_id)` filtresiyle unique
  indeks kullanılır. Identity kullanıcı profilleri bu servise kopyalanmaz;
  yalnızca stable user ID tutulur.
- Program geçişi `--backfill-owned-programs` komutuyla assignment/catalog
  backfill'lerinden sonra çalıştırılır. Program şablonları, öğrenci ilerlemeleri
  ve günlük egzersiz logları aynı UUID'lerle owned store'a insert-only olarak
  taşınır; her foreign-key referansı önceden doğrulanır ve eksik referans varsa
  işlem durur. Bu adım mevcut veriyi taşır; program okuma/reset, admin CRUD ve
  günlük ilerleme tamamlama runtime'ı da owned store'a bağlanmıştır.
- Owned assignment runtime'ı öğrenci varlığı ve adını eski `Users` tablosundan
  okumaz; Identity'nin internal `POST /api/internal/reporting/speed-reading/users`
  sözleşmesini servis anahtarıyla çağırır. Identity erişilemezse assignment
  yazma/ayrıntı işlemi fail-closed davranır.
- `SpeedReading__OwnedDataEnabled=true` olduğunda egzersiz oturumu, assignment,
  program, günlük ilerleme, gamification, katalog, CMS, abonelik/ödeme,
  bildirim, RSVP, review, feedback, adaptive learning/text, rapor, öğrenci
  analitikleri, öğretmen raporları ve admin analitikleri owned store üzerinden
  çalışır. Identity kullanıcı/rol/kurum bilgisi için yalnızca internal,
  versioned HTTP sözleşmeleri kullanılır.
- Katalog sorguları `OwnedSpeedReadingCatalog`, okuma/egzersiz geçmişi ve
  sonuç yazma akışları `OwnedSpeedReadingProgress`/
  `OwnedSpeedReadingProgressWriter` üzerinden owned DB'yi kullanır. Sonuç
  idempotency kayıtları da aynı owned ledger'a yazılır; bu çekirdek öğrenci
  akışlarında eski `StudentExerciseResults`, `ReadingSessions` veya katalog
  tablolarına SQL erişimi kalmaz.
- Aynı flag açıkken program listesi, öğrenci programları, günlük loglar ve admin
  öğrenci ilerleme/reset uçları `OwnedSpeedReadingPrograms` üzerinden owned
  store'u kullanır. Admin araması kullanıcı adı/e-postası için Identity'nin
  internal user-directory sözleşmesini çağırır; legacy `Users` tablosuna SQL
  erişimi yapılmaz. Program şablonu admin yazma owned idempotency ledger'ı ile,
  günlük egzersiz tamamlama ise owned progress/log tabloları ve domain kuralları
  ile yürür; bu akışlarda legacy program/log writer'ı kullanılmaz.
- Günlük egzersiz akışı da aynı flag ile `OwnedSpeedReadingDailyProgress`
  kullanır. Egzersiz seçimi, fallback zorluk seviyesi, tekrar numarası, puan,
  streak, gün/hafta ilerlemesi ve program tamamlanması owned domain kurallarıyla
  yürür; hız/anlama özeti owned session-result geçmişinden hesaplanır. Böylece
  günlük tamamlama sırasında legacy program/log tablolarına SQL yazılmaz.
- Web runtime owned modda `SpeedReadingDbContext` kaydetmez ve eski bağlantıyı
  istemez. `--migrate-only` de owned modda yalnızca owned EF migration geçmişini
  uygular; legacy migration/backfill komutları ise geçiş süresince iki bağlantıyı
  açıkça kullanır.
- `SPEED_READING_CONNECTION_STRING` yalnızca migration/backfill ve legacy
  fallback modu için gereklidir. Kod cutover'ı, backfill, checksum/row-count
  parity, aktif runtime'ın legacy yazma yollarından ayrılması ve backup/restore
  kanıtları tamamlanmıştır; authenticated production E2E son kullanıcı kapanış
  kanıtı olarak ayrıca tutulur.

Yeni DB'yi mevcut PostgreSQL volume'ünde kullanmadan önce production backup
alınmalı ve `speedreading_owned_db` veritabanı operasyonel olarak oluşturulup
erişim doğrulanmalıdır. Compose ilk kez oluşturulan PostgreSQL volume'lerinde
bu veritabanını `POSTGRES_DB_SPEED_READING_OWNED` ile otomatik oluşturacak
şekilde hazırlanmıştır; mevcut volume'lerde init script geriye dönük çalışmaz.

## Kaynak uygulama kararı

Taşınacak kaynak `onuraltintas/HizliOkuma` reposudur. `HizliOkuma_DDD`
repo'su 23 Kasım 2024 tarihli tek uygulamalı Clean Architecture sürümüdür;
`HizliOkuma` ise güncel mikroservis yapısını, Gateway'i, Content servisini ve
2026 tarihli migration/operasyon dokümanlarını içerir. Bu nedenle yeni servis
eski DDD reposundan yeniden üretilmeyecek; güncel Content veritabanı sözleşmesi
esas alınacaktır.

## Veri kaybını önleyen ilk aşama (geçiş tarihçesi)

İlk aşamada hızlı okuma servisi mevcut PostgreSQL veritabanına bağlanan salt-
okunur bir uyumluluk katmanıydı. Bu bölüm, mevcut owned runtime'ın neden
insert-only backfill ve ayrı migration geçmişi kullandığını açıklar:

- Mevcut business tabloları için EF Core migration dosyası eklenmez ve
  `EnsureCreated` çağrılmaz.
- Bağlantı `ConnectionStrings:SpeedReading` veya
  `SPEED_READING_CONNECTION_STRING` ile dışarıdan verilir.
- Tablo ve kolon adları mevcut Content şemasındaki PascalCase adlarıyla birebir
  eşleştirilir.
- Tüm sorgular `AsNoTracking` ve `IsDeleted = false` filtresiyle çalışır.
- Mevcut uygulama yazmaya devam ederken yeni servis yalnızca doğrulanmış
  okuma trafiği alır.
- Yeni servisin sahip olduğu ek tablolar `SpeedReadingIdempotencyRecords` ve
  `SpeedReadingAdminAuditRecords`'tır.
  Bu tablo aynı `Idempotency-Key` ile gelen tekrarların ikinci sonuç kaydı
  üretmesini engeller; eski tabloları değiştirmez. `CreatedAt` index'i,
  operasyon ekibinin 7 günlük replay penceresinden eski kayıtları güvenle
  temizleyebilmesi için eklenir.
- Admin mutation istekleri `AdminAuditMiddleware` ile append-only audit
  kaydına yazılır; request body içindeki hassas alan adları audit'e alınmaz.

İlk dikey dilimde taşınan tablolar:

| Tablo | Kullanım |
| --- | --- |
| `ExerciseTypeCategories` | Egzersiz türü kategori referansı |
| `ExerciseTypes` | Egzersiz motoru ve görünen katalog bilgileri |
| `Exercises` | Zorluk, tür ve JSON konfigürasyonu |
| `ReadingTexts` | Okuma metni ve seviye bilgileri |
| `ReadingQuestions` | Metne bağlı anlama soruları |
| `ExerciseSessions` | Devam eden/bitmiş egzersiz oturumları |
| `StudentExerciseResults` | Öğrenci egzersiz performans sonuçları |
| `ReadingSessions` | Okuma hızı ve anlama geçmişi |
| `ExerciseProgramTemplates` | Günlük egzersiz program şablonları |
| `StudentProgramProgresses` | Öğrencinin program ilerleme durumu |
| `DailyExerciseLogs` | Günlük egzersiz tamamlanma kayıtları |
| `LearningPathTemplates` | Öğrenme yolu şablonları |
| `LearningPathNodes` | Şablon düğümleri |
| `NodeContents` / `NodePrerequisites` | Düğüm içeriği ve ön koşulları |
| `StudentPathProgresses` / `StudentNodeProgresses` | Öğrenci yolu ve düğüm ilerlemesi |
| `PersonalizedLearningPaths` | Kişiselleştirilmiş öğrenci yolu öğeleri |
| `ReportTemplates` | Rapor şablonları ve merkezi yönetim yazma uçları |
| `ReportSnapshots` | Kullanıcıya ait oluşturulmuş rapor snapshot'ları |
| `ScheduledReports` | Kullanıcıya ait zamanlanmış rapor ayarları |

## Yeni servis uçları

Frontend, `speed-reading-frontend` image'ı olarak API'den bağımsız dağıtılır.
Base Compose'ta `speed-reading` profiliyle isteğe bağlıdır; staging ve
production overlay'leri servisi etkinleştirir. Public domain/edge yönlendirmesi
ayrı bir yayın adımıdır; bu sayede mevcut VPS sitelerine dokunulmaz.

Gateway üzerinden aşağıdaki sözleşmeler sunulur:

- `GET /api/speed-reading/capabilities` — servis modu ve entegrasyon durumu
- `GET /api/speed-reading/exercise-types` — sayfalı egzersiz türü kataloğu
- `GET /api/speed-reading/exercises` — kimlik doğrulamalı, filtrelenebilir egzersiz listesi
- `GET /api/speed-reading/reading-texts` — kimlik doğrulamalı metin listesi
- `GET /api/speed-reading/reading-texts/{id}` — metin ve isteğe bağlı soruları
- `GET /api/speed-reading/progress/reading-history` — oturum açmış kullanıcının okuma geçmişi
- `GET /api/speed-reading/progress/reading-statistics` — oturum açmış kullanıcının özet istatistikleri
- `GET /api/speed-reading/progress/exercise-results` — sayfalı egzersiz sonuçları
- `POST /api/speed-reading/progress/exercise-results` — öğrencinin egzersiz
  sonucunu mevcut `StudentExerciseResults` tablosuna yazar; `Idempotency-Key`
  başlığı zorunludur. Aynı anahtar aynı payload ile tekrarlandığında mevcut
  sonuç döner, farklı payload ile kullanıldığında `409` döner.
- `POST /api/speed-reading/exercise-types` / `PUT /api/speed-reading/exercise-types/{id}` /
  `DELETE /api/speed-reading/exercise-types/{id}` — `ContentManage` yetkili
  admin içerik yönetimi.
- `POST /api/speed-reading/exercises` / `PUT /api/speed-reading/exercises/{id}` /
  `DELETE /api/speed-reading/exercises/{id}` — `ContentManage` yetkili
  egzersiz yönetimi. Bağlı okuma metni olan egzersizler silinemez.
- `POST /api/speed-reading/reading-texts` / `PUT /api/speed-reading/reading-texts/{id}` /
  `DELETE /api/speed-reading/reading-texts/{id}` — `ContentManage` yetkili
  okuma metni yönetimi. Sorusu bulunan metinler silinemez; metin ayrıntısı
  güncellemesinde frontend mevcut merkezi ayrıntıyı okuyup içerik, hedef yaş,
  etiket, önerilen seviye ve egzersiz bağını koruyan tam gövdeyi gönderir.
- `POST /api/speed-reading/reading-questions` / `PUT /api/speed-reading/reading-questions/{id}` /
  `DELETE /api/speed-reading/reading-questions/{id}` — `ContentManage` yetkili
  soru yönetimi. Sorular metin kimliğine bağlıdır; dört farklı seçenek, A-D
  doğru cevap, soru türü ve Bloom seviyesi backend'de doğrulanır.
- `GET /api/speed-reading/program-templates/admin` — `ProgramManage` yetkili
  tüm (aktif/pasif) program şablonlarını yönetim alanlarıyla döndürür.
- `POST /api/speed-reading/program-templates` / `PUT /api/speed-reading/program-templates/{id}` /
  `DELETE /api/speed-reading/program-templates/{id}` — `ProgramManage` yetkili
  program şablonu yönetimi. Öğrenci ilerlemesi bulunan şablonlar silinemez.
- `GET /api/speed-reading/learning-paths/templates/admin` — `ProgramManage`
  yetkili aktif/pasif öğrenme yolu şablonlarını döndürür.
- `POST|PUT|DELETE /api/speed-reading/learning-paths/templates` — `ProgramManage`
  yetkili öğrenme yolu şablonu yönetimi. Bağlı düğüm veya öğrenci ilerlemesi
  olan şablonlar silinemez.
- `GET /api/speed-reading/learning-paths/templates/{id}/admin` ve
  `POST|PUT|DELETE /api/speed-reading/learning-paths/nodes` — `ProgramManage`
  yetkili düğüm yönetimi. Ebeveyn farklı şablondan seçilemez, hiyerarşi döngüsü
  engellenir; bağlı öğeleri bulunan düğümler silinemez.
- `POST|PUT|DELETE /api/speed-reading/learning-paths/node-contents` ve
  `POST|DELETE /api/speed-reading/learning-paths/prerequisites` — `ProgramManage`
  yetkili düğüm içerik/önkoşul yönetimi. İçerik tek bir aktif egzersiz veya
  okuma metnine bağlanır; önkoşul grafiğinde şablon ve döngü doğrulaması yapılır.
- `GET /api/speed-reading/progress/active-exercise-sessions` — aktif/paused oturumlar
- `GET /api/speed-reading/program-templates` — aktif program şablonları
- `GET /api/speed-reading/progress/programs` — oturum açmış kullanıcının programları
- `GET /api/speed-reading/progress/daily-exercise-logs` — günlük tamamlanma kayıtları
- `GET /api/speed-reading/analytics/student/summary` — öğrencinin en fazla 366
  günlük okuma/egzersiz analitik özeti ve gün serisi; son okuma metrikleri ile
  gamification özeti, günlük çalışma hedefi/hedef tamamlama oranı ve son beş
  başarım da aynı token kapsamındaki cevapta yer alır
- `GET /api/speed-reading/analytics/student/reading-speed` ve
  `GET /api/speed-reading/analytics/student/comprehension` artık öğrenci
  istemcisinin ayrıntılı hız/anlama ekranları için token kapsamlı merkezi
  okumalardır. Soru türü ayrıntısı legacy oturumlarda bulunmadığından cevapta
  boş tutulur ve veri uydurulmaz.
- `GET /api/speed-reading/analytics/student/series` ve
  `GET /api/speed-reading/analytics/student/activity` öğrenci seri/aktivite
  ekranlarının merkezi, token kapsamlı okumalarıdır. Seri raporu mevcut
  `StudentProgramProgresses`, program şablonları ve `DailyExerciseLogs` içindeki
  gerçek günlük ilerlemelerden hesaplanır; legacy şemada seri-özel milestone
  bağı bulunmadığı için bu alan boş kalır. Aktivite
  raporu `ReadingSessions` ile `DailyExerciseLogs` kayıtlarını birleştirir ve
  veri yokluğunu `DataAvailable=false` ile açıkça bildirir. Her iki uçta da
  `studentId` parametresi yoktur ve tarih aralığı 366 günle sınırlıdır.
  Öğrenci kapsamlı sorgular için `004_student_analytics_indexes.sql` idempotent
  bileşik indexleri sağlar.
- `GET /api/speed-reading/analytics/teacher/students/{studentId}/reading-speed`
  ve `/comprehension` öğretmen rapor ekranlarının ilk merkezi dilimidir. Öğrenci
  kimliği route'ta bulunsa da yetki token sahibinden alınır ve Identity'nin
  `authorize-student-read` çağrısı tarafından doğrulanır; yetkisiz öğrenci için
  okuma sorgusu çalıştırılmaz. `INTERNAL_SERVICE_API_KEY` ile servisler arası
  çağrı yapılır ve tarih aralığı 366 günle sınırlıdır.
- `GET /api/speed-reading/analytics/teacher/students/{studentId}/activity`
  aynı Identity öğrenci kapsamıyla öğretmen aktivite ekranını merkezi okur;
  okuma oturumları ve günlük egzersiz kayıtları birleştirilir.
- `GET /api/speed-reading/analytics/teacher/class-overview` öğretmenin
  Identity'den çözülen kurum/atama öğrenci kapsamındaki sınıf özetini merkezi
  okur. `teacherId` istemciden kabul edilmez; tarih aralığı 366 günle sınırlıdır.
  Legacy `Users` rol taşımadığı için aktif öğrenci metriği
  `ActiveStudentsDataAvailable=false` olarak işaretlenir. Okuma/anlama oturumu
  olmayan dönemlerde WPM/anlama ortalamaları da
  `ClassAverageWpmDataAvailable` ve
  `ClassAverageComprehensionDataAvailable` bayraklarıyla veri yokluğu olarak
  gösterilir.
- `GET /api/speed-reading/analytics/teacher/assignments` merkezi atama raporu
  sözleşmesidir. Atama CRUD tabloları artık merkezi serviste bulunmasına rağmen
  kaynak `ReportsController` içinde bu ayrıntılı raporun karşılığı yoktur;
  endpoint bu nedenle `DataAvailable=false` döner ve boş sonuç gerçek sıfır
  gibi yorumlanmamalıdır.
- `GET /api/speed-reading/analytics/teacher/content-analysis` ve
  `/time-progress` Identity öğrenci kapsamı içinde gerçek içerik ve zaman
  ilerlemesi metriklerini döndürür. Identity kapsamı iç ağdaki
  `POST /api/internal/reporting/speed-reading/teacher-students` sözleşmesinden
  servis anahtarıyla alınır; istemciden `teacherId` veya öğrenci listesi alınmaz.
- `GET /api/speed-reading/analytics/admin/teachers/{teacherId}/class-overview`,
  `/assignments`, `/content-analysis` ve `/time-progress` admin ekranında seçilen
  öğretmeni hedefler. Endpoint'ler `ReportView` izniyle korunur; Identity, hedef
  öğretmen ve kurum yöneticisi arasındaki aktif kurum kapsamını doğrulamadan
  hızlı okuma sorgusunu çalıştırmaz.
- `GET /api/speed-reading/analytics/admin/platform-usage` `PlatformAnalyticsView`
  yetkili SystemAdmin kullanıcıların hızlı okuma veritabanından platform
  kullanımını okur. Bu endpoint `PlatformAnalyticsView` ile korunur; kurum
  yöneticilerinin kurum kapsamı olmayan global metriklere erişimi yoktur.
  `Users` tablosunda kayıt tarihi ve kurum adı bulunmadığı için yeni kullanıcı
  metrikleri veri yokluğu bayrağıyla açıkça belirtilir; kayıt tarihi tahmin
  edilmez. Kurum karşılaştırması ayrı kurum kapsam sözleşmesiyle çözülür.
- `GET /api/speed-reading/analytics/admin/content-analysis`
  `PlatformAnalyticsView` ile içerik katalog ve kullanım metriklerini merkezi
  okur. Okuma analizi `ReadingSessions` + `ReadingTexts`, egzersiz analizi
  `DailyExerciseLogs` + `ExerciseTypes` üzerinden hesaplanır; atama ve eski
  eğitim serisi tabloları kaynakta olmadığı için `AssignmentDataAvailable=false`
  olarak açıkça işaretlenir.
- `GET /api/speed-reading/analytics/admin/system-health` gerçek hızlı okuma
  öğrenme metriklerini (WPM, anlama, egzersiz tamamlanması, cevap ve başarı)
  merkezi okur. Legacy şemada operasyonel hata/satisfaction/telemetri kaynağı
  bulunmadığından health score, error rate ve system alerts veri yokluğu
  bayraklarıyla boş tutulur; operasyonel görünürlük OpenTelemetry/monitoring
  katmanından sağlanmalıdır.
- `GET /api/speed-reading/analytics/admin/institutions` gerçek kurum
  karşılaştırmasını döndürür. Identity kurum dizini
  `/api/internal/reporting/speed-reading/institutions` üzerinden servis anahtarı
  ile çağrılır; kurum adları, aktiflik ve rol sayıları Identity'ye, aktivite ve
  performans metrikleri hızlı okuma veritabanına aittir. İstemciden kurum kimliği
  alınmadığı için global rapor yalnızca `PlatformAnalyticsView` yetkili
  SystemAdmin kullanıcılarına açıktır. Legacy `Users` tablosunda rol bilgisi
  bulunmadığından aktif öğrenci metriği veri yokluğu bayrağıyla gösterilir.
- `GET /api/speed-reading/analytics/admin/programs` kaynak projedeki program
  analitiği dashboard'unu merkezi servise taşır. Aktif program ilerlemeleri,
  program dağılımı, haftalık ilerleme ve son öğrenci hareketleri aynı legacy
  tablolar üzerinden hesaplanır; eski frontend yolu için Caddy uyumluluk
  alias'ı bırakılmıştır.
- `GET /api/speed-reading/student-progress`, `GET /{id}` ve `POST /{id}/reset`
  kaynak admin öğrenci ilerleme operasyonlarını merkezi servise taşır. Liste
  araması kullanıcı adı/e-postası ve program adına göre yapılır; sıfırlama
  `ProgramManage` yetkisi ve audit actor bilgisiyle kaydedilir.
- `POST /api/speed-reading/assignments`, `GET /assignments/my-assignments`,
  `GET /assignments/teacher-assignments`, detay, silme ve öğrenci ilişki
  uçları merkezi servise taşındı. Oluşturma sırasında frontend'in gönderdiği
  `studentIds` artık gerçekten `StudentAssignments` kayıtlarına yazılır;
  egzersiz türü filtresi de uygulanır.
- `GET /api/speed-reading/reports/templates` ve
  `GET /api/speed-reading/reports/templates/{id}` — mevcut
  `ReportTemplates` tablosundan `ReportView` yetkili salt-okunur şablon okuma;
  SystemAdmin dışındaki roller Admin türü şablonları göremez ve özel şablonlar
  yalnızca oluşturucusuna görünür
- `GET /api/speed-reading/reports/snapshots` ve
  `GET /api/speed-reading/reports/snapshots/{id}` — `ReportView` yetkisi ile
  yalnızca token sahibinin `ReportSnapshots` kayıtları; 1 MB üzeri JSON gövdesi
  cevapta açıkça kırpılmış olarak işaretlenir. İstemcinin snapshot okuma
  çağrıları bu merkezi uçları kullanır. `POST|DELETE
  /api/speed-reading/reports/snapshots` kullanıcının kendi snapshot kaydını
  idempotency ve sahiplik kontrolüyle yönetir. Rapor PDF/Excel dışa aktarımı
  owned rapor verisini kullanan merkezi export servisi üzerinden çalışır; okuma
  metni PDF/DOCX dışa aktarımı da owned içerik sorgularını kullanan merkezi
  içerik export servisi üzerinden çalışır.
- `GET /api/speed-reading/reports/scheduled` — `ReportView` yetkisi ile
  token sahibinin `ScheduledReports` kayıtları
- `GET /api/speed-reading/reports/scheduled/{id}` — yalnızca token sahibinin
  tek zamanlama kaydı
- `POST|PUT|DELETE /api/speed-reading/reports/templates` — `ReportManage`
  yetkisiyle idempotency ve sahiplik kuralları uygulanarak şablon yönetimi
- `POST|PUT|DELETE /api/speed-reading/reports/scheduled` ve
  `PATCH /api/speed-reading/reports/scheduled/{id}/status` — `ReportManage`
  yetkisiyle kullanıcı sahipliği, aktif şablon doğrulaması, UTC saklama ve
  idempotent zamanlama yönetimi
- `GET /api/speed-reading/learning-paths/templates` — aktif öğrenme yolu şablonları
- `GET /api/speed-reading/learning-paths/progress` — öğrencinin yolu ve düğüm durumları
- `GET /api/speed-reading/learning-paths/personalized` — kişiselleştirilmiş yol öğeleri
- `GET /api/speed-reading/learning-paths/personalized/next` — tamamlanmamış ve kilidi
  açık ilk öneriyi döndürür; uygun öğe yoksa `204`
- `GET /api/speed-reading/gamification/user` — XP, seviye ve streak özeti
- `GET /api/speed-reading/gamification/achievements` — aktif kazanım kataloğu
- `GET /api/speed-reading/gamification/achievements/user` — kullanıcının açtığı kazanımlar
- `GET /api/speed-reading/gamification/leaderboard` — `LeaderboardView` yetkili;
  SystemAdmin global, diğer kullanıcılar kurum kapsamlı liderlik tablosu
- `GET /api/speed-reading/achievements/admin` — `GamificationManage` yetkili sayfalı yönetim listesi
- `POST /api/speed-reading/achievements`, `PUT|DELETE /api/speed-reading/achievements/{id}` —
  `GamificationManage` yetkili kazanım CRUD'u

Yazma uç noktaları idempotency ve audit ile korunur. Öğrenci istemcisindeki
`awardXP`, `checkAchievements`, streak ve showcase yazıları artık merkezi
`/api/speed-reading/gamification` uçlarını kullanır. Eski istemciler için
`/v1/gamification` Caddy alias'ı geçiş uyumluluğu sağlar; yeni frontend bu alias'a
bağlanmaz.

## Geçiş kontrol listesi

1. Üretim veritabanının geri yüklenebilir yedeğini ve tablo satır sayısı
   snapshot'ını al.
2. Yeni servisi aynı veritabanına salt-okunur kullanıcıyla bağla.
3. Eski API ile yeni API'nin aynı kimlikler için döndürdüğü katalog/soru
   sonuçlarını karşılaştır; farkları raporla.
4. En az bir gözlem periyodu boyunca hata, gecikme ve satır sayısı metriklerini
   izle.
5. Yeni servisi platform ve bağımsız frontend'lerde kademeli olarak aç.
6. Owned migration'ı çalıştır; ardından image'ın backfill komutlarını şu sırayla
   yürüt: `catalog`, `sessions`, `assignments`, `programs`, `age-groups`,
   `user-profiles`, `learning-paths`, `admin-audit`, `gamification`, `questions`,
   `visualization`, `vocabulary`, `subscriptions`, `cms`, `notifications`,
   `rsvp`, `review`, `content-feedback`, `adaptive-learning`, `adaptive-text`,
   `reports`. Her komut insert-only, referans kontrollü ve tekrar çalıştırılabilir.
7. Her backfill sonrası source/owned row-count ve checksum parity raporunu
   `--verify-owned-parity` ile üretip sakla; komut fark bulursa exit code 2
   döndürür ve eksik/bozuk referans varsa cutover yapılmaz.
8. Owned modda shadow read ve gerçek kullanıcı smoke testlerini tamamla;
   login, katalog, egzersiz, sonuç, ilerleme, rapor, öğretmen/admin analitikleri,
   CMS ve abonelik akışlarını kontrol et.
9. Yazma yetkisini yalnızca yeni servisin owned veritabanı kullanıcısına ver;
   eski uygulamanın aynı business write yollarını kapat.
10. Bağımsız backup/restore provası ve rollback rotasını doğrula; sorun halinde
    Gateway rotasını eski uygulamaya geri al.

## Kalan final operasyonel kapılar

Kod ve aktif runtime taşınması tamamlandı. Kalan doğrulama yalnızca gerçek bir
test hesabıyla authenticated E2E akışlarının kanıtlanmasıdır:

1. Login ve session yenileme.
2. Katalog/egzersiz başlatma, cevap kaydı, sonuç ve ilerleme kaydı.
3. Rapor/analytics ve yetkili admin akışları.

Aktif servis için legacy DB yazma yetkisi dedicated role ile kaldırılmıştır.
Eski release/image ve legacy DB, rollback süresi ve veri saklama politikası
belirlenene kadar silinmemelidir; bu varlıkların tutulması aktif servis
bağımsızlığını bozmaz.

Rapor `export/pdf` ve `export/excel` artık merkezi serviste gerçek PDF/XLSX
üretir. Newsletter abonelikten çıkış da merkezi CMS altında
`POST /api/speed-reading/cms/newsletter/unsubscribe` ile tamamlanmıştır.
Adaptive-performance yardımcı fonksiyonu hedef frontend'de kullanılmayan eski
bir servis metodudur; merkezi analytics ekranlarında aktif bir çağrısı yoktur.

Her dilimde mevcut şema korunacak; yeni tablo veya kolon ihtiyacı varsa önce
ayrı bir versiyonlanmış migration ve geri dönüş planı hazırlanacaktır.
