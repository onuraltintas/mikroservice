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
- Yeni servisin sahip olduğu tek ek tablo `SpeedReadingIdempotencyRecords`'tır.
  Bu tablo aynı `Idempotency-Key` ile gelen tekrarların ikinci sonuç kaydı
  üretmesini engeller; eski tabloları değiştirmez. `CreatedAt` index'i,
  operasyon ekibinin 7 günlük replay penceresinden eski kayıtları güvenle
  temizleyebilmesi için eklenir.

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
- `GET /api/speed-reading/progress/active-exercise-sessions` — aktif/paused oturumlar
- `GET /api/speed-reading/program-templates` — aktif program şablonları
- `GET /api/speed-reading/progress/programs` — oturum açmış kullanıcının programları
- `GET /api/speed-reading/progress/daily-exercise-logs` — günlük tamamlanma kayıtları
- `GET /api/speed-reading/learning-paths/templates` — aktif öğrenme yolu şablonları
- `GET /api/speed-reading/learning-paths/progress` — öğrencinin yolu ve düğüm durumları
- `GET /api/speed-reading/learning-paths/personalized` — kişiselleştirilmiş yol öğeleri

Bu yazma uç noktası yalnızca öğrenci sonucunu taşımak için açılmıştır. Eski
uygulamanın aynı sonucu yazan yolu, üretimde yeni servis tek veri sahibi olarak
doğrulanana kadar kapatılmamalıdır. İçerik yönetimi ve admin CRUD'u sonraki
dilimdir.

## Geçiş kontrol listesi

1. Üretim veritabanının geri yüklenebilir yedeğini ve tablo satır sayısı
   snapshot'ını al.
2. Yeni servisi aynı veritabanına salt-okunur kullanıcıyla bağla.
3. Eski API ile yeni API'nin aynı kimlikler için döndürdüğü katalog/soru
   sonuçlarını karşılaştır; farkları raporla.
4. En az bir gözlem periyodu boyunca hata, gecikme ve satır sayısı metriklerini
   izle.
5. Yeni servisi platform ve bağımsız frontend'lerde kademeli olarak aç.
6. `speed-reading-migrations` one-shot container'ı ile yalnızca
   `001_write_support.sql` scriptini çalıştır; tablo ve unique index'i doğrula.
7. Yazma yetkisini yalnızca yeni servisin veritabanı kullanıcısına ver; eski
   uygulamanın aynı sonuç write endpoint'ini kapat.
8. Sorun halinde Gateway rotasını eski uygulamaya geri al; veritabanına geri
   alma/migration çalıştırma.

## Sonraki taşıma dilimleri

Sıradaki dilimler, her biri test ve geri dönüş kontrolüyle ayrı ayrı taşınır:

1. Sonuç yazma uç noktasının entegrasyon/geri dönüş testleri ve audit event'i.
2. Analitik, gamification ve rapor snapshot'ları.
3. Admin içerik CRUD uçları ve audit event'leri.
4. Mevcut `speed-reading-frontend` uygulamasının bağımsız servis Gateway'ine
   geçirilmesi.

Her dilimde mevcut şema korunacak; yeni tablo veya kolon ihtiyacı varsa önce
ayrı bir versiyonlanmış migration ve geri dönüş planı hazırlanacaktır.
