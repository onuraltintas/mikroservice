# Koçluk gelişim ekranları ve bildirim bağlantıları

Tarih: 20 Ekim 2026. Durum: canlıya dağıtıldı; öğrenci ve öğretmen oturumlarında doğrulandı. Son veri karşılaştırması: 15:00 Türkiye saati.

## Yapılan değişiklikler

- Öğrenci gelişim ekranı Hedefler, Denemeler, Konu analizi ve Çalışma raporu sekmelerine ayrıldı.
- Öğretmenin seçili öğrenci için açtığı gelişim/plan alanı aynı sekmeleri kullanır. Plan editörü ayrıca Çalışma planı sekmesindedir. Seans hazırlık özeti Çalışma raporu sekmesinden açılır.
- Sekmeler görünürlük değiştirir; form ve editörler sekme değişiminde yeniden oluşturulmaz. Hedef formu, tarih aralığı, analiz girişleri, plan taslağı ve onay bilgileri korunur.
- Sekmeler klavyeden sağ/sol ok, Home ve End ile kullanılabilir. Seçili sekme ve panel ilişkisi ARIA öznitelikleriyle tanımlandı. Dar ekranlarda sekmeler satırlara bölünür.
- Konu analizi ilk açılışta yüklenir. Daha sonra sekmeler arasında geçerken aynı bileşen korunur. Dönem filtresi, kapsamına girmeyen konu analizi ve plan düzenleme sekmelerinde gösterilmez.
- Sınav kapsamları kullanıcıya AYT Sayısal, AYT Eşit Ağırlık, AYT Sözel ve YDT İngilizce adlarıyla gösterilir. Analiz satırlarında ders adı kaydedilmiş ad veya sunucunun dinamik ders adından alınır.
- Öğrenci ana sayfasındaki otomatik plan kısayolu güncel çalışma planı sihirbazına bağlandı.

## Bildirimden ilgili kaydı açma

Mevcut `relatedEntityId` kullanılır. Yeni sütun veya bildirim verisi dönüşümü gerekmez.

| Alıcı / olay | Açılan yer |
|---|---|
| Öğrenci / StudyPlanPublished, StudyPlanUpdated | `/coaching-portal/study-plans?plan=<revisionId>` |
| Öğrenci / AssignmentCreated, AssignmentUpdated, AssignmentGraded | `/coaching-portal/assignments/<assignmentId>` |
| Öğretmen / AssignmentSubmitted | Personel portalında ilgili ödevin teslim/değerlendirme ayrıntısı |

Öğretmen bildirimi, ödev ilk liste sayfasında bulunmasa bile doğrudan kimliğiyle açılır. Farklı bir ödev açıldığında ayrıntı bileşeni yeni kimlikle yeniden yüklenir. Normal liste gezinmesine dönüldüğünde eski bildirim hedefi temizlenir.

Bildirimden geçiş açık ödev formunu değiştirmez; önce formun kaydedilmesi/kapatılması gerekir. Kaydedilmemiş puan/geri bildirim için ayrılma onayı istenir, değerlendirme kaydı sürerken geçiş engellenir. Diğer çalışma alanlarının mevcut ayrılma kontrolleri korunur.

Kimliği eksik, sıfır veya geçersiz eski bildirimlere tahminî bağlantı eklenmez. Bilinmeyen bildirim türleri ve diğer ürünlerin bildirimleri Koçluk kaydına yönlendirilmez. `actionUrl` veya bildirim metni yönlendirme adresi olarak kullanılmaz. Kaynak erişim yetkisi mevcut sunucu uçlarında kontrol edilmeye devam eder.

Bildirim türleri anlaşılır adlarla gösterilir. Öğrenci ekranındaki ilk 100 kayıt sayısı artık toplam bildirim sayısı diye sunulmaz. Ölçülmeyen canlı bağlantı için sabit “Anlık güncelleniyor” ibaresi kaldırıldı. Personel bildirim merkezi yalnız açıldığında yüklenir.

## Veri koruma

- Üretim veritabanına yazma, silme veya geçiş yapılmadı.
- Hesap, hedef, plan, çalışma ve kazanım ilişkilerinin şemaları değiştirilmedi.
- Kayıt koruma regresyonları localhost üzerindeki ayrı PostgreSQL test kümesinde, testlerin oluşturup kaldırdığı veritabanlarında çalıştırıldı. Küme test bitiminde durduruldu.
- Yerel görsel kontrolde gerçek Angular bileşenleri ve örnek veriler kullanıldı. Ardından gerçek öğrenci ve öğretmen oturumlarıyla canlı doğrulama yapıldı.
- Dağıtımdan önce Coaching, Identity ve Notification veritabanları yedeklendi. Dağıtım ve canlı testlerden sonra satır içeriklerinin özetleri karşılaştırıldı: 60 Coaching, 26 Identity ve 9 Notification tablosunda korunan kayıtlar birebir aynı. Taşıma amaçlı Inbox/Outbox tabloları ve oturum açıldığında değişen Identity yenileme belirteçleri bu karşılaştırmaya dahil değildir.
- 44 plan, 62.016 çalışma ve 61.979 kazanım bağlantısı korundu. Aktif LGS planı aynı kimlik ve sürümle kaldı; mevcut tamamlanan çalışma, ödev teslimi, 95 puan ve geri bildirim değişmedi.

## Canlı doğrulama ve düzeltilen hata

| Akış | Sonuç |
|---|---|
| Öğrenci: Hedefler, Denemeler, Konu analizi, Çalışma raporu | Dört sekme açıldı; analiz ve rapor verileri yüklendi. |
| Öğrenci: kaydedilmemiş hedef, dönem filtresi ve konu analizi girişi | Sekmeler arasında değerler korundu; test değerleri kaydedilmedi. |
| Öğretmen: seçili öğrencinin beş gelişim/plan sekmesi | Sekmeler açıldı; kaydedilmemiş plan başlığı ve çalışma alanları korundu. |
| Öğretmen: açık taslaktan ödev bildirimine geçiş | Kaydedilmemiş değişiklik uyarısı çıktı. Kullanıcı yalnız test taslağından ayrılmayı onayladı; kayıtlı plan değişmedi. |
| Öğrenci: plan bildiriminden geçiş | Bildirimin gösterdiği aktif plan doğrudan kimliğiyle açıldı; yıllık takvim yüklendi. |
| Öğrenci: değerlendirme bildiriminden geçiş | İlgili ödev açıldı; mevcut 95/100 ve koç geri bildirimi görüldü. |
| Öğretmen: teslim bildiriminden geçiş | İlgili ödevin teslim/değerlendirme ekranı açıldı; 1/1 teslim, öğrenci notu, puan ve geri bildirim görüldü. |

Canlı testte ödev ayrıntısı ilk açılışta sunucu hatası verdi. `assignment_instruction_attachments` tablosunda mevcut olan `created_by` ve `updated_by` sütunları EF modelinde doğru adlara bağlanmamıştı. Yapılandırma ve model snapshot eşleştirmeleri düzeltildi; yeni sütun veya veritabanı geçişi eklenmedi.

Hata önce gerçek PostgreSQL üzerinde yeni bir regresyon testiyle üretildi. Aynı hata canlı yedeğinin ayrı bir veritabanına geri yüklenmiş kopyasında, mevcut canlı servis modülüyle de görüldü. Düzeltilmiş modül aynı ödevi salt okunur sorguyla, 95 puan ve değerlendirmesi korunarak açtı. Sonrasında yalnız ilgili servis modülü canlıya alındı ve iki oturumda bildirimden geçiş yeniden doğrulandı. Geçici prova veritabanı kaldırıldı. Düzeltmeden sonraki son sunucu günlüklerinde iki portal ve Coaching servisi için hata satırı görülmedi.

## Otomatik regresyon paketi

`clients/admin-panel` dizininden:

```sh
npm run test:coaching-development
```

Paket 13 test dosyasını çalıştırır: öğrenci için 60, öğretmen için 57 test; toplam 117 başarılı test.

Önemli kapsama:

- Sekme seçimi, klavye odağı, tarih/form/analiz/plan taslağı korunması ve rapor hatasında hedef formuna erişim.
- LGS, TYT, AYT Sayısal, AYT Eşit Ağırlık, AYT Sözel, YDT İngilizce × öğrenci/öğretmen: analiz, üç denemeden öneri, takvim önizlemesi, açık onay ve doğru kapsamla plan güncelleme istekleri.
- Önizlemesiz/onaysız uygulamama; değişmiş veride kör tekrar yapmama; salt okunur kullanım.
- Plan/ödev bağlantılarının tam kayıt kimliğiyle üretilmesi, eksik/eski/geçersiz kimliklerde bağlantı üretmeme.
- Öğretmen bildiriminden ödev açma, ikinci ödevde ayrıntı yenileme, açık formu ve kaydedilmemiş değerlendirmeyi koruma.
- Paylaşılan bildirim servisinin mevcut okundu/silme/oturum izolasyonu testleri.

PostgreSQL deneme regresyonları ve yeni ödev kalıcılık regresyonu birlikte çalıştırıldı: **31 başarılı, 0 atlanan**. Kapsam, net kuralları, üç deneme, dinamik konu kataloğu, önizleme/onay, tamamlanan görevler ve kazanım bağlantılarının korunması, çakışma ve eski önizlemenin reddini içerir. Yeni test tüm geçişlerle oluşturulmuş şemada değerlendirilen ödevi okur, öğretmen dosya bilgisini kaydedip yeniden okur; teslim tarihi, öğrenci notu, 95 puan ve geri bildirimin değişmediğini doğrular.

İlgili ödev, dosya erişimi, yapılandırma, öğretmen filtreleri ve bildirim sunucu testleri de çalıştırıldı: **25 başarılı, 0 atlanan**. Bu paketin içindeki 6 bildirim testi, ilgili olayın doğru alıcıya ve kayıt kimliğine gitmesi ile bildirim sayfalama davranışını doğrular.

Son incelemede eski bildirim hedefinin normal gezinmede temizlenmesi ek bir doğrulamayla sınandı: öğretmen çalışma alanının 3 testi başarılı.

GitHub Actions'a `coaching-development-regression` işi eklendi. Arayüz paketi ve gerçek PostgreSQL deneme/ödev kalıcılık testleri çalıştırılır; mevcut dağıtım işi bu sonuca da bağımlıdır. Workflow YAML yerelde ayrıştırılarak doğrulandı; GitHub üzerinde bir CI çalıştırması bu oturumda başlatılmadı.

## Derleme ve kanıtlar

- Öğrenci/admin üretim derlemesi başarılı: başlangıç paketi yaklaşık 813 kB.
- Personel üretim derlemesi başarılı: başlangıç paketi yaklaşık 988 kB; mevcut 1 MB hata sınırı artırılmadı.
- Depoda önceden bulunan Angular bütçe/şablon uyarıları ve .NET paket uyarıları bu paketin kapsamında değiştirilmedi.
- Canlı dağıtımda yalnız bu paket kapsamındaki 20 arayüz dosyası mevcut canlı kaynakların üzerine uygulandı; çalışma alanındaki ilgisiz değişiklikler pakete alınmadı. İki portalın HTML, JavaScript ve CSS yanıtları başarılı ve uygun içerik türünde doğrulandı.
- Canlı sürümler: `admin-panel:coaching-development-20261020`, `staff-portal:coaching-development-20261020`, `coaching-service:coaching-development-20261020-attachment-fix`. Önceki imajlar ve sunucu yedekleri geri dönüş için korundu.

Canlı kanıtlar `artifacts/coaching-development-live-20261020-r1/` dizinindedir: `final-verification.log`, `assignment-green.log`, `assignment-related-tests.log` ve kimlik bilgisi/yedek içermeyen `public-verification.tar.gz`. Arşivde önce/sonra tablo özetleri, son kayıt sayıları, derleme günlüğü ve yedek kopyası üzerinde salt okunur doğrulama sonucu bulunur.

Gerçek canlı ekran görüntüleri aynı dizinde: `teacher-draft-fields.png`, `teacher-draft-preserved.png`, `student-form-preserved.png`, `student-plan-notification.png`, `teacher-assignment-notification.png` ve `student-grade-preserved.png`.

Çalışma alanı içindeki test günlükleri:

- `artifacts/coaching-development-regression.log`
- `artifacts/coaching-development-postgres.log`
- `artifacts/coaching-development-notification-backend.log`
- `artifacts/coaching-development-navigation-final.log`
- `artifacts/coaching-development-build.log`

Örnek verili yerel görseller:

- `artifacts/coaching-development-student.png`
- `artifacts/coaching-development-teacher.png`
- `artifacts/coaching-development-notifications.png`
- `artifacts/coaching-development-mobile.png`
