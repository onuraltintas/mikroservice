# Müfredat yönetimi ve sınav tarihi — canlı sürüm, 16 Ekim 2026

Sekmeli ders ve müfredat yönetimi ile sınav tarihi alanı canlıya alındı. Sadece `coaching-service` ve `admin-panel` değiştirildi. Identity, hızlı okuma, personel paneli ve diğer çalışan servislerin konteyner kimlikleri korundu.

## Sürümler

| Dağıtım | Uygulama kontrol noktası | Canlıya geçiş (UTC) | SHA256 |
|---|---|---|---|
| `coaching-curriculum-20261016` | `ddc089ef53bf081efc1b8a86122f19eac4205f3a` | 16.10.2026 20:37:01 | `cce88e94af08c8797f69470a18cf1fde6e62c19e674a239301f23e5019720765` |
| `coaching-exam-date-20261016` | `dfbb4335e885328df8b43f85c1100f99c1d19c8e` | 16.10.2026 20:53:42 | `c207e754d5f48d55dc8d63a46870aad08eea9c6dcd14e31cd806759283d37f4d` |

Türkiye saati UTC+3'tür. Her sürüm, önceki canlı sürümün kaynaklarına yalnızca ilgili değişiklikler uygulanarak hazırlandı. Çalışma klasöründeki ilgisiz değişiklikler pakete alınmadı. Dört Coaching derlemesi önceki canlı imajın üzerine yerleştirildi; ortak altyapı derlemeleri değiştirilmedi. Paket ve dosya özetleri sunucuda doğrulandı.

## Sınav tarihi nasıl kullanılır?

1. Yönetici panelinden **Koçluk → Ders ve Müfredat Yönetimi → Sınav türleri** bölümünü açın.
2. Bir sınav türünü seçin veya **Sınav türü ekle** düğmesine basın.
3. **Sınav tarihi** alanından sınavın yapılacağı günü seçin. Tarih henüz belli değilse boş bırakabilirsiniz.
4. Ad ve kod gibi gerekli alanları doldurun. Mevcut sınavın kodu ilişkileri korumak için değişmez.
5. **İşlem gerekçesi** alanını doldurup kaydedin.

Liste, tarihi `gg.aa.yyyy` biçiminde gösterir. Boş kayıtlarda **Tarih belirtilmedi** yazar. Tarih daha sonra değiştirilebilir veya alan temizlenerek kaldırılabilir. Güncelleme diğer yönetici tarafından değiştirilmiş eski bir form üzerinden yapılırsa sürüm denetimi işlemi engeller.

Alan isteğe bağlıdır. LGS/TYT/AYT gibi mevcut sınavların tarihleri tahmin edilerek doldurulmadı.

## Veri modeli ve servisler

| Katman | Değişiklik |
|---|---|
| Coaching veritabanı | `coaching.study_catalog_exams.ExamDate`: nullable PostgreSQL `date` |
| Domain | `StudyCatalogExam.ExamDate`: `DateOnly?`; oluşturma/düzenleme işlemleri alanı kaydeder |
| Yönetici istek/yanıtı | `CurriculumSaveRequest.ExamDate` ve `CurriculumNode.ExamDate` |
| Yönetici liste/detay | `/api/coaching-admin/curriculum-management/children?kind=exams`, `/exams/{id}` tarihi döndürür |
| Yönetici kayıt | `/api/coaching-admin/curriculum-management/exams` POST, `/exams/{id}` PUT |
| Kimliği doğrulanmış katalog | `/api/coaching/catalog/exams` yanıtı `examDate` içerir |
| Arayüz | Yerel tarih giriş alanı; boş değer API'ye `null`, dolu değer `yyyy-MM-dd` gider |

Saat veya UTC dönüşümü yapılmaz. Tarih alanı Identity'ye taşınmadı; Coaching kataloğunda tutulur. Geçiş yalnızca nullable sütun ekler; kayıt silmez, kimlik değiştirmez, gerçek sınav tarihi üretmez.

Bu sürüm sınav türü başına tek tarih saklar. Henüz sınav yılı/oturumu tablosu, öğrenci hedefinde seçilmiş sınav tarihi, geri sayım veya tarihe göre otomatik plan üretimi eklenmedi. İleride aynı türün farklı yıllarını/oturumlarını eşzamanlı yönetmek gerekirse tarihleri ayrı sınav oturumlarıyla ilişkilendirmek gerekir. Şu an istenen tarih bilgisinin kaydı ve yönetimi sağlandı.

## Uygulanan geçişler

İlk dağıtım:

- `20261016185534_CurriculumManagementHierarchy`
- `20261016195911_DynamicCurriculumExams`
- `20261016201006_GradeIndependentCurriculum`

Tarih alanı:

- `20261016204727_ExamScheduledDate`

Her iki dağıtımda gerçek Coaching yedeği ayrı bir prova veritabanına geri yüklendi. Geçişler burada iki kez çalıştırıldı; önce/sonra kayıt içerik özetleri karşılaştırıldı. Ardından canlıya geçildi. Yeni tarih geçişinde mevcut sınavlar dahil bütün iş tabloları karşılaştırıldı; yalnız yeni `ExamDate` sütunu eski şemayla karşılaştırma için normalleştirildi. Tarayıcı testi sonunda bütün tarihler tekrar boş olduğundan geçiş sonrası durum ayrıca doğrulandı.

## Veri koruma sonucu

| Veri | Dağıtım öncesi | Son doğrulama |
|---|---:|---:|
| Ortak kullanıcı hesabı | 8 | 8 |
| Öğrenci planı | 4 | 4 |
| Plan görevi | 5 | 5 |
| Tamamlanmış görev | 4 | 4 |
| Gerçekleşen çalışma dakikası | 86 | 86 |
| Kazanım | 52 | 52 |
| Yayınlanmış kazanım süresi | 52 | 52 |
| Taslak kazanım süresi | 0 | 0 |
| Plan–kazanım bağlantısı | 1 | 1 |

Hesapların temel içerikleri, parola bilgileri, roller ve ürün erişimleri karşılaştırıldı. Normal girişin değiştirdiği son giriş ve oturum güvenliği sayaçları karşılaştırma dışında tutuldu. Coaching tarafında denetim ve mesajlaşma altyapısı tabloları dışında mevcut iş kayıtları içerik özetleriyle doğrulandı.

İlk canlı tarayıcı testinde `LIVE261016` kodlu bir sınav ve buna bağlı tek bir ders oluşturuldu. Bu nedenle sınav sayısı 5'ten 6'ya, ders sayısı 169'dan 170'e çıktı. İkisi de **pasif** bırakıldı. Öğrenci planı, görevi veya gerçekleşen çalışma oluşturulmadı. Tarih sürümü sırasında yeni sınav veya ders eklenmedi; aynı pasif test sınavı kullanıldı.

## Canlı tarayıcı doğrulamaları

Gerçek SystemAdmin ve Student oturumları kullanıldı:

- Yeni sınav türü ve ona bağlı ders ekleme başarılı.
- Mevcut LGS Matematik konu sırası değiştirildi, kayıt doğrulandı ve ilk değerine döndürüldü.
- `M.8.1.1.1` kazanım metni geçici değiştirildi. Eski öğrenci planının kendi özgün kazanım metnini, 30 dakikalık seçimini ve kullandığı süre sürümünü koruduğu görüldü; katalog metni geri alındı.
- Dört mevcut plana erişildi. Aktif planda 12 dakika/0 soru ve 17 dakika/8 soru kayıtları; eski arşiv planda 40 dakika/24 soru kaydı görüldü.
- Tarih sürümünde pasif test sınavına `20.06.2027` kaydedildi, sayfa yenilendi ve değer geri geldi. Tarih `27.06.2027` olarak değiştirildi ve tekrar kaydedildi.
- Örnek tarih temizlendi; listede **Tarih belirtilmedi** ve veritabanında `NULL` doğrulandı. Bu tarihler resmî sınav tarihi olarak kullanılmadı.
- Tarih sürümünden sonra aktif öğrenci planı ve kazanımlı arşiv planı tekrar açıldı.
- Anonim katalog/yönetici API çağrıları HTTP 401 döndürdü.

## Test kanıtları

| Kontrol | Sonuç / yerel kayıt |
|---|---|
| Eksik tarih alanı için RED | Backend alan eksikliği ve arayüzde giriş alanı eksikliği gözlendi; `a52176c0` kontrol noktası |
| Müfredat servis ve gerçek PostgreSQL testleri | 39 geçti, 0 atlandı; `artifacts/coaching-exam-date-green.log` |
| Tarih formu dahil yönetici arayüzü | 42 geçti; `artifacts/coaching-exam-date-ui-regression.log` |
| Diğer Coaching profil/plan/kurulum regresyonları | 694 geçti; `artifacts/coaching-exam-date-coaching-regression.log` |
| Masaüstü/mobil müfredat tarayıcı testleri | 14 geçti; `artifacts/coaching-exam-date-e2e.log` |
| Masaüstü/mobil öğrenci kazanım ve plan akışı | 4 geçti; `artifacts/coaching-exam-date-student-e2e.log` |
| EF model/geçiş eşleşmesi | Bekleyen model değişikliği yok; `artifacts/coaching-exam-date-model.log` |
| İzole üretim derlemesi | Coaching ve Angular başarılı; `artifacts/coaching-exam-date-live-20261016/*-build.log` |
| Canlı yedek geri yükleme/geçiş provası | Başarılı; `artifacts/coaching-exam-date-live-20261016/drill-result.log` |
| Canlı kayıtların içerik karşılaştırması | Başarılı; `artifacts/coaching-exam-date-live-20261016/preservation-result.log` |
| Canlı hata kaydı kontrolü | İki serviste de 0 hata satırı; `artifacts/coaching-exam-date-live-20261016/diagnostics.log` |

Bu test kümeleri kısmen aynı davranışları kapsar; sayılar toplanarak tekil test sayısı olarak sunulmaz. Yerel tarayıcı testleri üretim arayüz paketini kontrollü HTTP yanıtlarıyla çalıştırır; gerçek servis/veritabanı testleri ve canlı oturum kontrolleri ayrı kanıtlardır.

Kapsam ölçümünde değişen sınıfların satır kapsamı: `StudyCatalogExam` %95,65, `CoachingCurriculumManagementService` %97,45, `CoachingCatalogLookupsController` %100. Ölçüm dosyası `artifacts/coaching-exam-date-coverage/09e56a8d-edd4-4d56-aafa-4a2c1f176b23/coverage.cobertura.xml`.

Elle yapılan kapsam incelemesinde tarih alanının oluşturma, listeleme, detay, düzenleme ve temizleme yolları; mevcut yetkilendirme, değişiklik gerekçesi ve eşzamanlı düzenleme koruması; nullable geçiş ve önceki uygulamayla şema uyumu kontrol edildi. Kapsam dışındaki uygulama dosyaları değiştirilmedi.

Canlı ekran kanıtları `artifacts/coaching-exam-date-live-20261016/proof/` altındadır: `exam-date-saved.png`, `exam-date-final.png`, `student-archived-plan.png`. İlk dağıtımdaki katalog metni değişirken eski planın özgün metnini koruduğunu gösteren kanıt `artifacts/coaching-curriculum-live-20261016/proof/student-snapshot-preserved.png` dosyasıdır.

İlk müfredat sürümü 20:37:19–20:52:29 UTC arasında 31 sağlık örneğiyle gözlemlendi: iki servis çalışır durumda, yeniden başlama 0; giriş, yönetici müfredat ve öğrenci plan sayfaları HTTP 200. Tarih sürümünün 15 dakikalık gözlemi sürüyor; tamamlanma kaydı ayrıca bu rapora eklenecek.

## Geri dönüş ve yedekler

Her sürümün `/var/lib/eduivme/releases/<sürüm>/` dizininde:

- Geçiş öncesi Coaching ve Identity yedekleri ile SHA256 özetleri.
- Önceki canlı imaj kimlikleri ve özel `rollback.compose.json`.
- Prova geri yükleme, geçiş ve tekrar çalıştırma günlükleri.
- Canlı geçiş ve kayıt karşılaştırma kanıtları.

Geri dönüş uygulama imajlarını önceki sürüme alır. Nullable tarih sütunu önceki uygulamayla uyumludur. Canlı veritabanında otomatik `Down` veya yedekten geri yükleme çalıştırılmaz; sonradan oluşmuş gerçek çalışmaları silmemek için veritabanı geri dönüşü ayrı değerlendirilir. Yedekler ve özel bağlantı bilgileri sunucuda tutuldu; rapora veya Git'e alınmadı.

Geçiş/koruma kontrolünün başarısız olması, servisin hazır olmaması, yeniden başlama veya temel yönetici/öğrenci akışının bozulması geri dönüş nedenidir. Bu kontrollerde hata görülmedi.
