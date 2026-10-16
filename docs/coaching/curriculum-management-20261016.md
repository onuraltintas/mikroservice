# Ders ve Müfredat Yönetimi

Tarih: 16 Ekim 2026. Durum: geliştirme ve yerel doğrulama tamamlandı; bu geliştirme canlıya dağıtılmadı.

## Ekran ve kullanım

Yeni adres: `/dashboard/coaching/curriculum`. Yönetici menüsünde **Koçluk → Ders ve Müfredat Yönetimi**. Eski katalog ekranında da bağlantı bulunur. Sistem yöneticisi rolü ve `Permissions.Coaching.ContentManage` yetkisi gereklidir; kurum yöneticisi ortak müfredatı değiştiremez.

Solda açılır içerik ağacı, sağda seçilen kaydın formu bulunur:

```text
Ders grubu
└─ Ders (sınıf, sınav)
   └─ Ünite
      └─ Konu (isteğe bağlı çalışma dakikası)
         ├─ Kazanım
         └─ Alt konu
            └─ Kazanım
```

Kazanımlar en alt düzeydeki konuya bağlanır. Bir kazanım birden fazla konuda bulunabilir; ortak kazanım metni düzenlemesi tüm katalog bağlantılarına yansır. Kayıtlı öğrenci planları kendi metin kopyasını kullanır.

Mevcut dersler otomatik ve tahmine dayalı gruplandırılmaz. **Gruba bağlanmamış dersler** görünümünden seçilerek gruba taşınır. Konusuz kazanımlar için ayrı görünüm vardır. Listeler 25 kayıtla sayfalanır; ağaç dalları açıldığında yüklenir. Arama seçili görünüm/düzey içindedir. Teknik kimlikler yerine adlar, yollar, durum ve kazanım kodları gösterilir. Aynı adlı dersleri ayırmak için ağaç etiketlerinde ve taşıma yollarında sınıf/sınav bilgisi bulunur.

| Kayıt | İşlemler |
|---|---|
| Ders grubu | Ekleme, ad/sıra düzeltme, aktiflik, kullanılmıyorsa silme |
| Ders | Ekleme, bağlı kazanımların program kapsamını bozmayan ad/sınıf/sınav düzeltme, gruba taşıma, aktiflik, güvenli silme |
| Ünite | Ekleme, ad/sıra düzeltme, uyumlu başka ders kaydına taşıma, aktiflik, güvenli silme |
| Konu / alt konu | Ekleme, ad/sıra/dakika düzeltme, ünite veya konu altında uygun konuma taşıma, aktiflik, güvenli silme |
| Kazanım | Mevcut programın konu grubuna dayandırarak ekleme, kod/metin/not/sayfa/sıra düzeltme, konu bağlantısını taşıma, kapsam onayı, arşivleme, güvenli silme |
| Kazanım süresi | Öğrenme/alıştırma/kontrol dakikalarıyla yeni sürüm; taslak kaydetme veya açık onayla yayınlama; yayından kaldırma; kullanılmamış sürümü silme |

Yeni ders, ünite ve konu pasif oluşturulur; kayıttan sonra üst kayıtların aktifliği kontrol edilerek etkinleştirilir. Yeni ders grubu aktiftir. Yeni kazanımın konu bağlantısı onaysızdır ve henüz süre yayını yoktur. Gerekçe doldurularak konu kapsamı onaylanır, süre dayanağı yazılır ve süre yayınlanır.

Bir dersin adı, sınıfı veya sınavı değiştirilirken bağlı kazanım programlarının kapsamı doğrulanır. Kazanımları sessizce öğrenci listesinden düşürecek değişiklikler hem yeni yönetimde hem eski katalog servisinde reddedilir; farklı kapsam için yeni ders/program kaydı gerekir.

Kazanım eklemede mevcut kaynak program ve program konu grubu seçilir. Bu sürüm yeni bir resmî program belgesi veya sınav türü düzenleyicisi eklemez; mevcut kazanım programı LGS Matematik pilotudur. Diğer derslerin resmî kazanım paketleri ayrıca hazırlanıp aktarılmalıdır. Ders ve konu yönetimi mevcut bütün katalogda kullanılabilir.

## Güvenli taşıma

- **Ders → başka grup:** ders kimliği ve alt kayıtlar aynı kalır.
- **Ünite → başka ders kaydı:** yeni ünite ve konu ağacı oluşturulur; önceki ünite ve konular pasife alınır. Sınav, sınıf, ders adı ve müfredat sürümü aynı olmalıdır.
- **Konu → başka ünite / ana konu:** yeni konumda yeni konu kimliği oluşturulur; önceki konu ve alt konuları pasife alınır. Alt konuları olan ana konu, bir başka konunun altına taşınamaz; iki konu düzeyi korunur. Kendisine/altına taşıma ve program kapsamı dışına taşıma reddedilir. Aktif kayıt pasif üst kayda taşınamaz.
- **Kazanım bağlantısı → başka konu:** yalnız seçilen kaynak konu bağlantısı değiştirilir. Diğer bağlantılar, kazanım kimliği, süre sürümleri ve plan bağlantıları korunur. Yeni konu bağlantısı kapsam onayı bekler.

Kazanım bağlı bir konuya ilk alt konu eklenirken kazanımları bu yeni alt konuya aktarmak için açık onay istenir. Onay olmadan işlem reddedilir; onayla aktarım tek transaction içinde yapılır. Eski planların görevleri aynı kalır; yeni bağlantılar yeniden kapsam incelemesi bekler.

Taşıma, öğrencinin eski konu durumunu yeni konuya öğrenilmiş gibi kopyalamaz. Eski konu kimliklerine bağlı planlar ve öğrenci kayıtları geçmiş olarak korunur. Yeni konumda yeni kayıt oluştuğu ekranda açıkça belirtilir.

## Süreler ve geçmiş planlar

Konu dakikası ile kazanım çalışma süreleri ayrı alanlardır. Kazanım seçilmeden yapılan planlama konu önerisini kullanır. Kazanım seçildiğinde seçilen kazanımların süreleri kullanılır; konu dakikası otomatik değiştirilmez. Öğrencinin kendi planındaki süre değişikliği genel kataloğa yazılmaz.

Kazanım süresi 1–1.440 net dakikadır. Öğrenme, alıştırma, kısa kontrol, güven düzeyi ve dayanak ayrı saklanır. Süre değişikliği yeni sürüm oluşturur; önceki sürümün dakika değerleri güncellenmez. En az bir onaylı konu bağlantısı olmadan yayınlanamaz. Bütçe taslağı doğrudan yayınlanamaz; yeni incelenmiş sürüm gerekir.

Yayından kaldırılan süre sürümü geçmiş planlarda okunmaya devam eder. Gelecek seçimlerde varsa önceki yayınlanmış sürüm kullanılır. Hiçbir planda kullanılmamış sürüm gerekçe ve açık silme onayıyla kaldırılabilir. Kullanılmış sürümün kalıcı silinmesi sunucuda engellenir.

Plan görevlerinin başlığı, planlanan/gerçekleşen dakikası, soruları, tamamlanması, tarihi ve eski konu kimliği katalog taşıması sırasında değiştirilmez. Kazanım kodu ve metni görev–kazanım bağlantısında kayıt anındaki haliyle tutulur. Yeni otomatik plan ve sabitlenmiş görevin sonraki taslağa kopyalanması bu metni korur; kişisel veri dışa aktarımı da aynı metni okur.

## Güvenli silme ve eşzamanlılık

Silmeden önce kullanım ve parmak izi sunucuda yeniden denetlenir. Alt kayıt, konu–kazanım ilişkisi, öğrenci tercihi/durumu, plan görevi veya sınav referansı silmeyi engelleyebilir. Kazanım kullanılmışsa silinemez; kullanılmamış kazanım silinirken yalnız ona ait kullanılmayan süreler, kaynak eşlemeleri ve konu bağlantıları temizlenir. Genel kaynak program silinmez.

Ekranda engelleyen bağlantılar gösterilir. Kalıcı silme gerekçe, `SİL` metni ve kayıt kimliği doğrulaması gerektirir. Sunucuda transaction, katalog advisory lock ve ilişki tabloları üzerinde kilitler kullanılır. Kayıt veya kullanım bilgisi değişmişse işlem 409 ile reddedilir. Kaydedilmemiş metin ve süre girişleri korunur; başka kayda geçerken onay istenir. Süre oluşturma/yayınlama adımları boyunca işlem kilidi sürer; yayın adımı başarısız olursa oluşan sürümün tekrar oluşturulmaması açıkça bildirilir.

Değişiklikler aktör, işlem, kayıt kimliği, gerekçe ve önce/sonra parmak izleriyle append-only denetim kaydına yazılır. Uzun kazanım metninin tamamı kataloğa kaydedilir; denetim kaydında kısa ad önizlemesi ve tam ad SHA256 özeti kullanılarak 2.000 karakterlik denetim sınırı korunur. Tam gerekçe saklanır; aşırı kaçış karakterli bir gerekçe sınıra sığmazsa işlem açık hata ile reddedilir.

## Veritabanı değişikliği

Migration: `20261016185534_CurriculumManagementHierarchy`.

| Değişiklik | Koruma |
|---|---|
| `coaching.study_catalog_lesson_groups` | Yeni tablo: `Id`, `Name`, `DisplayOrder`, `IsActive` |
| `study_catalog_lessons.GroupId` | Nullable FK, Restrict; mevcut derslerin grup alanı boş kalır |
| `study_catalog_outcomes.IsArchived` | Mevcut kazanımlar için false |
| `study_plan_task_outcomes.CodeSnapshot`, `NameSnapshot` | Mevcut bağlantılar kazanım kodu/metniyle SQL üzerinden doldurulur |

Migration Identity şemasına dokunmaz; ortak hesap, şifre ve rol tablolarını değiştirmez. Önceki plan veya çalışma satırlarını silmez. Canlı dağıtımda önce yedek ve geri yükleme provası, sonra bu migration uygulanmalıdır. Bu görevde üretim veritabanına bağlantı veya migration işlemi yapılmadı.

API: `/api/coaching-admin/curriculum-management`. Düğümler için sayfalı alt kayıt okuma, detay, oluşturma, düzenleme, taşıma, aktiflik ve silme uçları; süre sürümü için yayından kaldırma ve güvenli silme uçları eklenmiştir. Süre oluşturma/yayınlama ve konu kapsamı onayı mevcut kazanım servislerini kullanır. Mevcut katalog, öğrenci planı ve aktarım uçları korunur.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| Yeni backend testleri | 27 geçti; altısı gerçek ve geçici PostgreSQL veritabanlarında |
| Önceki kazanım testleri | 13 geçti; geniş regresyon kümesiyle örtüşür |
| Mevcut Coaching regresyonları | Yerel Docker gerektirmeyen seçilmiş sınıflarda 694 geçti |
| İlgili ekran ve menü testleri | 39 geçti |
| Mevcut CI test projesinde keşif | 27 test bulundu; PostgreSQL değişkeni kapalı koşuda 21 geçti, 6 yerel PostgreSQL testi açık gerekçeyle atlandı. Bu altı test ayrı yerel koşuda geçti. |
| Yeni masaüstü/mobil tarayıcı akışları | 8 geçti: CRUD/taşıma/güvenli silme; kazanım/süre sürümleri; çakışma ve girdi koruma; ünite → konu → alt konu → kaynaklı kazanım ekleme |
| Eski öğrenci/yönetici tarayıcı akışları | 4 geçti; masaüstü/mobil öğrenci planı ve çalışma kaydı, yönetici paket aktarımı |
| Yeni yönetim servisi ve controller dosyaları | 361 / 379 satır, %95,3; bütün depo kapsamı değildir |
| Backend Release | Geçti, 0 uyarı / 0 hata |
| Angular production | Geçti; yeni bileşenin stili yaklaşık 6,9 kB ile 4 kB uyarı eşiğini aşar, 8 kB hata eşiğinin altındadır. Mevcut başka bileşenlerde de stil uyarıları vardır. |
| EF model/migration uyumu | Bekleyen model değişikliği yok |

Gerçek PostgreSQL testleri migration tekrarını ve metin geri doldurmasını, composite FK'lerle ünite/konu ağacı taşımayı, eşzamanlı düzenleme çakışmasını, uzun Türkçe metinleri ve taşınan konudaki eski bekleyen görevin hâlâ tamamlanabilmesini doğrular. Eski tamamlanmış çalışma 25 dakika / 8 soru olarak korunurken bekleyen görev için yalnız testin istediği yeni 18 dakika / 6 soru kaydedilir.

Tarayıcı senaryoları gerçek derlenmiş arayüzü kontrollü API yanıtlarıyla test eder; canlı doğrulama değildir. Kalıcılık ve migration ayrıca PostgreSQL üzerinde test edilmiştir. Bütün depo testlerinin veya canlı kullanıcı akışının bu görevde test edildiği iddia edilmez.

Hızlı yerel koşu için testler ayrı `Coaching.Curriculum.IntegrationTests` projesindedir. Aynı kaynak dosyaları mevcut `Identity.API.IntegrationTests` projesine linklendi; mevcut CI komutu yeni testleri de keşfeder. PostgreSQL'e özgü altı test için localhost bağlantı değişkeni ayrıca verilmelidir. Tekrarlanan koşu ve örtüşen kümelerin sayıları bağımsız test toplamı olarak toplanmamalıdır.

Kanıtlar: `artifacts/coaching-curriculum*.log`, `artifacts/coaching-curriculum/browser.json`, `manager-desktop.png`, `manager-mobile.png`, `ready-coverage/*/coverage.cobertura.xml`.

```powershell
$env:COACHING_CURRICULUM_TEST_POSTGRES = '<yalnız localhost geçici PostgreSQL bağlantısı>'
dotnet test tests/Integration/Coaching.Curriculum.IntegrationTests -m:1 -nr:false
# Arayüz: clients/admin-panel içinde
npm test -- admin-panel --watch=false --include=src/app/features/dashboard/pages/coaching-curriculum-manager.component.spec.ts
npm run build -- admin-panel --configuration production
# Tarayıcı: tests/E2E içinde
npx playwright test --config playwright.coaching-curriculum.config.mjs
```

Test veritabanları yalnız `test_coaching_curriculum_*` adıyla oluşturulur ve test bitince kaldırılır. Gerçek hesap veya üretim bağlantısını test değişkenine vermeyin.
