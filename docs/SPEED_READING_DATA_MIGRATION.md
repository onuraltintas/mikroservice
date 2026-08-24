# Hızlı Okuma Veri Taşıma ve Uyumluluk Planı

## Kaynak uygulama kararı

Taşınacak kaynak `onuraltintas/HizliOkuma` reposudur. `HizliOkuma_DDD`
repo'su 23 Kasım 2024 tarihli tek uygulamalı Clean Architecture sürümüdür;
`HizliOkuma` ise güncel mikroservis yapısını, Gateway'i, Content servisini ve
2026 tarihli migration/operasyon dokümanlarını içerir. Bu nedenle yeni servis
eski DDD reposundan yeniden üretilmeyecek; güncel Content veritabanı sözleşmesi
esas alınacaktır.

## Veri kaybını önleyen ilk aşama

İlk aşamada hızlı okuma servisi mevcut PostgreSQL veritabanına bağlanan salt-
okunur bir uyumluluk katmanıdır. Yazma geçişi yalnızca öğrenci egzersiz sonucu
uç noktası için additive bir ledger ile başlatılmıştır:

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
| `ReportTemplates` | Rapor şablonları (salt-okunur merkezi sınır) |
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
  güncellemede mevcut içerik ve etiketler korunarak açıkça gönderilmelidir.
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
  günlük okuma/egzersiz analitik özeti ve gün serisi
- `GET /api/speed-reading/reports/templates` ve
  `GET /api/speed-reading/reports/templates/{id}` — mevcut
  `ReportTemplates` tablosundan `ReportView` yetkili salt-okunur şablon okuma;
  SystemAdmin dışındaki roller Admin türü şablonları göremez ve özel şablonlar
  yalnızca oluşturucusuna görünür
- `GET /api/speed-reading/reports/snapshots` ve
  `GET /api/speed-reading/reports/snapshots/{id}` — `ReportView` yetkisi ile
  yalnızca token sahibinin `ReportSnapshots` kayıtları; 1 MB üzeri JSON gövdesi
  cevapta açıkça kırpılmış olarak işaretlenir
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
- `GET /api/speed-reading/gamification/user` — XP, seviye ve streak özeti
- `GET /api/speed-reading/gamification/achievements` — aktif kazanım kataloğu
- `GET /api/speed-reading/gamification/achievements/user` — kullanıcının açtığı kazanımlar
- `GET /api/speed-reading/gamification/leaderboard` — `LeaderboardView` yetkili;
  SystemAdmin global, diğer kullanıcılar kurum kapsamlı liderlik tablosu
- `GET /api/speed-reading/achievements/admin` — `GamificationManage` yetkili sayfalı yönetim listesi
- `POST /api/speed-reading/achievements`, `PUT|DELETE /api/speed-reading/achievements/{id}` —
  `GamificationManage` yetkili kazanım CRUD'u

Yazma uç noktaları idempotency ve audit ile korunur. Öğrenci istemcisindeki
`awardXP`, `checkAchievements`, streak ve showcase yazıları, merkezi gamification
yazma sözleşmesi tamamlanana kadar eski `/v1/gamification` uyumluluk köprüsünde
tutulur. Bu geçici köprü dokümante edilmiş bir geçiş kararıdır; merkezi yazma
uç noktaları ve rollback testleri geçmeden eski yollar kapatılmamalıdır. Tüm
gamification okuma ve admin kazanım çağrıları yeni `/api/speed-reading` yolundadır;
öğrenci yazma köprüsü dışında eski achievement endpoint'i kullanılmaz.

## Geçiş kontrol listesi

1. Üretim veritabanının geri yüklenebilir yedeğini ve tablo satır sayısı
   snapshot'ını al.
2. Yeni servisi aynı veritabanına salt-okunur kullanıcıyla bağla.
3. Eski API ile yeni API'nin aynı kimlikler için döndürdüğü katalog/soru
   sonuçlarını karşılaştır; farkları raporla.
4. En az bir gözlem periyodu boyunca hata, gecikme ve satır sayısı metriklerini
   izle.
5. Yeni servisi platform ve bağımsız frontend'lerde kademeli olarak aç.
6. `speed-reading-migrations` one-shot container'ı ile sıralı
   `Database/*.sql` scriptlerini çalıştır; ledger, audit tablosu ve unique
   index'leri doğrula.
7. Yazma yetkisini yalnızca yeni servisin veritabanı kullanıcısına ver; eski
   uygulamanın aynı sonuç write endpoint'ini kapat.
8. Sorun halinde Gateway rotasını eski uygulamaya geri al; veritabanına geri
   alma/migration çalıştırma.

## Sonraki taşıma dilimleri

Sıradaki dilimler, her biri test ve geri dönüş kontrolüyle ayrı ayrı taşınır:

1. Rapor template/snapshot/schedule yazma sözleşmelerinin ve admin ekranlarının taşınması.
2. Oyunlaştırma XP/streak yazma akışlarının event/idempotency geri dönüş testleri.
3. Mevcut `speed-reading-frontend` uygulamasının bağımsız servis Gateway'ine
   geçirilmesi ve gerçek veritabanıyla uçtan uca doğrulanması.

Her dilimde mevcut şema korunacak; yeni tablo veya kolon ihtiyacı varsa önce
ayrı bir versiyonlanmış migration ve geri dönüş planı hazırlanacaktır.
