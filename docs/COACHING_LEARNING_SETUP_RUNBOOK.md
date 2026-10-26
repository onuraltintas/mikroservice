# Koçluk eğitim bağlamı, katalog sürümü ve ilk kurulum

Tarih: 16 Ekim 2026. Bu paket, profil sahipliği/UserId temelinden sonraki eğitim bağlamı ve öğrenci kurulumunu uygular. Profil sahipliği geçişiyle birlikte 16 Ekim 2026 19:39:55 TSİ'de canlıya alınmıştır. Geçiş ve canlı doğrulama ayrıntıları: canlı dağıtım raporu.

## Öğrenci akışı

Ana sayfada öğrencinin kurulum durumunu gösteren kart ve `/coaching-portal/setup` sayfası vardır. Öğretmen paneline öğrenci kurulumu eklenmez. Kurulum boyunca mevcut planlara, ödevlere ve ana sayfaya bağlantılar görünürdür; uygulama genelinde zorunlu kurulum yönlendirmesi yoktur.

1. **Eğitim bilgilerim:** mevcut Coaching profili kullanılır. Öğrenci/mezun ve öğrenci için 1–12 sınıf yeterlidir. Hazır eğitim bilgileri yeniden sorulmaz. Kısa form okul, doğum tarihi ve diğer isteğe bağlı alanları açmaz; bunlar Koçluk profilim sayfasında düzenlenebilir. Profil henüz hazırlanmadıysa açık profil hazırlama işlemi kullanılabilir.
2. **Çalışma amacım:** eğitim yılı başlangıcı; okul dersleri, LGS, YKS, genel çalışma veya henüz karar vermedim. LGS/YKS için hedef sınav yılı; YKS için TYT/SAY/EA/SOZ/DIL birincil kapsam; DIL için yabancı dil alınır. Mezun kullanıcı okul dersleri yerine genel çalışma veya sınav hazırlığı seçer. Üniversite/lise hedefi zorunlu değildir.
3. **Müsaitliğim:** mevcut saat dilimi ve haftalık saat aralıkları kullanılır. Günlük kapasite, o günün aralıklarının toplamıdır. Yeni bir ikinci müsaitlik kaynağı oluşturulmaz. Saatleri kaydetmek aktif planı değiştirmez.
4. **Derslerim:** yayımlanmış, eğitim yılına ve kapsama uygun programdan dersler gösterilir. Okul amacı sınıfa; LGS, LGS derslerine; YKS seçilen kapsama göre TYT/AYT/YDT derslerine süzülür. Öğrenci ders seçimini ve 1–5 önceliğini belirler; 1 en yüksek önceliktir. Kendi değerlendirdiği 1–5 zorluk isteğe bağlıdır. AYT içinde ders seçimi öğrencinindir; puan türünden konu yeterliliği türetilmez.
5. **Başlangıç durumum:** seçili derslerin konuları sayfalı gösterilir. Öğrenci çalışmadım/çalıştım/tekrar istiyorum beyanını girebilir; konu beyanları ve deneme sonucu isteğe bağlıdır. Bilgileri gözden geçirdiğini onaylar. Beyanlar `StudentDeclaration` kaynağı ve aktör/tarih ile saklanır; öğrenme yeterliliği sayılmaz.
6. **Plan önizlemesi:** mevcut plan motoru kullanılır. Öğrenci, seçili derslerinden konuları öncelik sırasıyla ekler; süreleri kontrol eder ve tarihi seçer. Katalogda süresi olmayan konu için açık süre girilir. Bu paket yeni bir otomatik konu öneri algoritması içermez. Taslak kaydetme ve açık yayınlama mevcut mekanizmayla devam eder.

Kaydedilmiş bağlam başka oturumda okunur. Sonraki adım yalnız sayaçtan değil; gerçek profil, müsaitlik, yayımlanmış katalog, seçili dersler ve başlangıç onayından hesaplanır. Kaydetme hatası ve sürüm çakışması ekrandaki seçimleri korur. Kaydedilmemiş alanlarda sayfadan ayrılma koruması vardır.

Sınıf veya eğitim kapsamı değiştiğinde eski kapsamın dersleri yeniden onaylanır/seçilir. Mevcut aktif plan ve çalışma geçmişi bundan etkilenmez. Yayımlanmış uygun katalog bulunamazsa eksik açıkça gösterilir; mevcut plan erişimi devam eder.

## Veritabanı

Geçiş: `20261016144923_StudentLearningSetup`.

| Tablo/değişiklik | Alanlar ve sınırlar |
|---|---|
| `coaching.curriculum_versions` | Id, tekil Code, Name, VersionLabel, ValidFromYear/ValidToYear, Source, isteğe bağlı HTTPS SourceUrl, IsPublished, Version, oluşturma/güncelleme alanları. |
| `coaching.study_catalog_lessons.CurriculumVersionId` | Eski kayıtlar için nullable FK. Bir kez bağlanan ders başka sürüme taşınmaz; yeni sürüm için yeni ders kaydı kullanılır. |
| `coaching.student_learning_contexts` | Id, tekil StudentUserId, AcademicYearStart, Purpose, TargetExamYear, PrimaryScoreType, ForeignLanguage, CurriculumVersionId, EducationStatus/GradeLevel anlık görüntüsü, LastCompletedStep, SetupVersion=1, StartingStatusReviewed, Version, tarihler/aktörler. |
| `coaching.student_course_preferences` | Birleşik PK ContextId/LessonId; IsIncluded, Priority, SelfReportedDifficulty. |
| `coaching.student_topic_states` | Birleşik PK ContextId/TopicId; DeclaredStatus, Source, UpdatedBy, UpdatedAt. |

Bu sürümde kullanıcı başına bir güncel çalışma bağlamı vardır. Eğitim yılı/kapsam değişikliği bu güncel kurulumu değiştirir. Yıllara göre bağlam arşivi ve plan revizyonuna eğitim bağlamı anlık görüntüsü, sonraki hedef/plan geliştirmesinde ayrıca ele alınmalıdır.

StudentUserId, yerel Coaching öğrenci profilinin UserId alanına bağlıdır. Bağlamın silinmesi yalnız kendi tercih/beyan satırlarına cascade uygular. Katalog ve profil bağlantıları restrict kullanır. Mevcut çalışma planı, görev, sınav, Identity veya Hızlı Okuma tabloları yeniden numaralandırılmaz.

Katalog kullanım kontrolü artık ders tercihlerini ve konu beyanlarını da sayar. Kullanımdaki kayıt kalıcı silinebilir gösterilmez; mevcut pasife alma mekanizması kullanılabilir.

## API ve servisler

| İşlem | Yol |
|---|---|
| Kendi kurulumunu oku | GET `/api/coaching/study-planning/setup` |
| Kendi kurulumunu kaydet | PUT `/api/coaching/study-planning/setup` |
| Bağlama uygun ders/konu kataloğu | GET `/api/coaching/study-planning/setup/catalog?lessonId=...&pageNumber=1` |
| Program sürümlerini listele/oluştur | GET/POST `/api/coaching-admin/curricula` |
| Program yayın durumunu değiştir | PUT `/api/coaching-admin/curricula/{id}/publication` |

`CoachingLearningSetupService`, yerel aktif profili ve Identity'deki güncel Koçluk erişimi/öğrenci rolünü doğrular. Öğrenci endpointleri dışarıdan öğrenci kimliği almaz. GET kurulum kaydı açmaz. PUT, null ExpectedVersion ile ilk kaydı; güncel ExpectedVersion ile düzenlemeyi kabul eder. Tekil UserId ve concurrency token mükerrer/stale yazmaları korur.

Katalog konuları yalnız aktif ders, ünite, üst konu ve yaprak konu kapsamından gelir. Konu listesi 50'lik sayfalıdır; seçimler en fazla 100 ders ve 1000 konu beyanıyla sınırlıdır. Sıralama kayıt kimliğiyle kararlı hale getirilmiştir. Geçerlilik yılları 2000–2200; hedef sınav yılı eğitim yılı başlangıcından en fazla beş yıl sonrasıdır.

`CoachingCurriculumService`, global yönetici kapsamı, içerik yönetimi izni ve mevcut MFA politikasını kullanır. Oluşturma/yayın işlemlerinin gerekçesi audit'e yazılır. Kaynak doğruluğu yönetici tarafından kontrol edilir; kaynak adı kendi başına resmi doğrulama anlamına gelmez.

Kurulumdan yapılan önizleme ve taslak istekleri `expectedLearningContextVersion` taşır. Sunucu kurulumun güncel ve hazır olduğunu, konuların seçilmiş derslerden geldiğini tekrar doğrular. Çalışma saatleri ve aktif plan için mevcut sürüm kontrolleri de sürer. Mevcut bağımsız otomatik-plan ekranının açık konu seçme akışı geriye uyumlu kalır.

Kişisel dışa aktarma şeması 1.5'e çıktı ve yalnız kullanıcının eğitim bağlamını, tercihlerini, beyanlarını içerir. Yetkili silme değerlendirmesi ve uygulaması bu kayıtları kapsar; mevcut hukuki saklama kontrolü sürer.

## Katalog hazırlama ve dağıtım

1. Önceki profil sahipliği/UserId geçişinin tamamlandığını doğrulayın ve veritabanı yedeği alın.
2. Coaching'in `--migrate-only` işlemiyle yeni additive geçişi uygulayın. Bu geçiş veri temizliği veya tahmini müfredat/süre doldurması yapmaz.
3. Coaching API, öğrenci/yönetici istemcisi ve ortak profil bileşenini kullanan personel istemcisini dağıtın.
4. Global yönetici, **Koçluk → ortak katalog → Müfredat ve program sürümleri** bölümünü açar. Kaynağı, yılları, sürüm etiketini ve ilgili mevcut dersleri seçerek taslak program oluşturur. Ders/ünite/konu içeriklerini ortak katalogdan kontrol eder ve aktif hale getirir; programı gerekçeyle yayımlar.
5. Yeni öğrenci/mezun, mevcut planı olan öğrenci ve öğretmen ile rol/kurulum/plan erişimini doğrulayın. Süresi eksik konuların açıklamasını ve sürüm çakışmasını kontrol edin.

Eski katalogları otomatik olarak belirli bir resmi müfredata atayan veya süre uyduran seed yoktur. Canlıda doğru kaynakla program yayınlamak operasyonel hazırlık işidir. Henüz yayın yoksa mevcut işler kullanılabilir; yeni kişiselleştirilmiş kurulum kataloğu eksik gösterir.

Geri dönüşte önceki uygulama sürümüne dönülürken yeni tablolar korunabilir. Veri içeren tablolarda otomatik `Down` çalıştırmayın. Eski plan erişimi yeni tablolara bağlanmadığı için korunur.

## Doğrulama

| Son kontrol | Sonuç |
|---|---|
| Geniş backend regresyon kümesi | 760 geçti, 0 başarısız, 0 atlanan. |
| Yeni kurulum testleri | 21 geçti; bu sayı geniş backend kümesinin içindedir. Gerçek yerel PostgreSQL testi dahildir. |
| Öğrenci/yönetici Angular testleri | 59 geçti. |
| Öğretmen/kurum Angular regresyonu | 22 geçti. |
| Masaüstü ve mobil Chrome | 4 senaryo geçti. |
| Üretim derlemesi | Öğrenci/yönetici ve personel istemcileri başarılı. |
| Yeni backend sınıfları için kapsam | %95,80 satır, %80,97 dal; async durum makineleri dahil hedeflenen yeni sınıflar, uygulama geneli değil. |

Yeni Angular servisinin statement/branch/function kapsamı %100'dür. Kurulum bileşeninin satır kapsamı %93,93, ana sayfa kartının %100, program yönetimi bileşeninin %96,15'tir. Bunlar ilgili bileşen testlerinin kapsamıdır; uygulamanın tamamının kapsamı olarak yorumlanmamalıdır.

- PostgreSQL 18 üzerinde geçici, rastgele adlı test veritabanları: önceki şemadan yükseltme; aktif plan/tamamlanmış dakika koruma; yeni oturumda devam; tercih güncelleme; tekil UserId; konu beyanı ve katalog kullanım koruması; gerçek plan önizlemesi.
- Backend testleri: eğitim/sınav koşulları, kendi hesabına erişim, iptal edilen ürün erişimi, profil/sınıf değişimi, katalog kapsamı, mükerrer/sürüm çatışması, kişisel dışa aktarma ve yetkili silme.
- Angular: kısa profil, kurulum kayıt/hata/yeniden deneme, ders/konu seçimleri, ana sayfa kartı, program oluşturma/yayınlama, mevcut profil/saat/plan/katalog regresyonları.
- Üretim istemcisi ile masaüstü ve mobil Chrome: profil → amaç → müsaitlik → sayfa yenileyip devam → ders → başlangıç beyanı → önizleme; eksik kurulumla mevcut aktif plana erişim. Tarayıcı testlerinde API yanıtları izole fixture'dır; gerçek veritabanı ve servis davranışı ayrı integration testlerinde doğrulanır.
- Test çıktıları: `artifacts/coaching-learning-setup-tests/`; tarayıcı senaryoları `tests/E2E/specs/coaching-learning-setup.spec.mjs` ve ayrı Playwright config'i ile tekrar çalıştırılabilir.

Eski Testcontainers katalog testlerinin Docker gerektiren 10 senaryosu bu makinede Docker endpoint'i bulunmadığı için çalışamadı. Yeni geçiş ve kullanım koruması gerçek yerel PostgreSQL testinde doğrulandı. Geniş regresyon kümesinde Docker gerektiren bu sınıflar yer almaz.

Mevcut `System.Security.Cryptography.Pkcs` NU1902/NU1903 paket uyarıları ve bazı eski istemci stil/bundle bütçesi uyarıları devam ediyor. Bu paketin üretim derlemeleri bu uyarılarla başarılıdır.

## Sonraki paket

Ölçülebilir hedefler ve yönlendirmeli plan üretiminin geliştirilmesi: hedef türü/değer/dönem/ölçüm kaynağı; bağlam ve hedefin plan revizyonuna kaydı; ders önceliği ve başlangıç beyanlarından konu önerisi; 7–28 günlük yönlendirmeli taslak. Ayrıntılı kazanım/yeterlilik, aralıklı tekrar ve süre uyarlama sonraki aşamalardır.
