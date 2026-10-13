# Koçluk tamamlama çalışması — 13 Ekim 2026

## Tamamlanan ve doğrulanan bölümler

- Abonelik kontrolü gerçek `/api/assignments`, `/api/exams`, `/api/goals`, `/api/sessions`, `/api/reports`, `/api/calendar` yollarını kapsıyor. Ücretsiz abonelik/ödeme, sözleşme ve gizlilik işlemleri erişilebilir kalıyor. Süresi dolan öğrenci isteği middleware üzerinden 403 ile duruyor.
- Eski hızlı okuma öğretmen ve öğrenci koçluk adresleri güncel personel/öğrenci panellerine yönleniyor. Eski uygulamanın oturum ve Coach rolü bu yönlendirmeyi engellemiyor.
- Öğretmen kendi aktif öğrencisinin haftalık planını hazırlıyor, sürüm kontrolüyle kaydediyor ve yayımlıyor. Öğrenci kendi hesabıyla gerçekleşen süreyi kaydediyor. Öğretmen ve aktif kurum aynı planı ve çalışma raporunu okuyabiliyor.
- Kurum plan değiştiremiyor; öğretmen öğrenci adına çalışma tamamlayamıyor. Öğrenci kapsamı her istekte sunucuda doğrulanıyor. Birleşik kurum/öğretmen rolü, kaldırılmış öğretmen atamasını yazma yetkisine dönüştürmüyor.
- Yeni plan yayımlanınca önceki tamamlanmış işler ve süreleri arşivde korunuyor. Eski sürüm ve tamamlanmış işi taşıma istekleri reddediliyor.
- Kaydedilmemiş taslak ve bekleyen kayıt; öğrenci raporundan çıkış, öğretmen sekmesi, ürün/rol değişimi ve çıkış işlemlerinde korunuyor.
- Kurum geçmiş filtreleri öğretmenle aynı yerel takvim günü sınırlarını kullanıyor; İstanbul saat dilimindeki üç saatlik sapma giderildi.
- Öğretmen sınav sonucu formunda ders adı/puan satırları var. Sunucuda da oluşturma ve güncelleme sırasında ders adları, tekrarlar ve sınavın maksimum puanı doğrulanıyor.
- Gerçek API tarayıcı testiyle bulunan öğretmen hedef/sınav enum biçimi uyumsuzluğu düzeltildi: sayısal form seçenekleri HTTP sınırında sunucunun beklediği enum adına çevriliyor; sunucunun katı sözleşmesi korunuyor.
- Öğretmen hedeflerini mevcut aktif okul/üniversite kataloğuna ve sınav türü/puan ölçeğine bağlayabiliyor. Hedef sahibi öğretmen ve aktif atama birlikte doğrulanıyor. Öğrenci ve kurum öğretmen hedefini değiştiremiyor.
- Hedef kaydı sonrası kart ve düzenleme formu güncelleniyor; sonraki başlık değişikliği yeni puan ölçeğini sıfırlamıyor. Öğrenci/hedef kapsamı değişince eski istekler iptal ediliyor.
- Öğretmen ve admin hedef düzenleme formları okudukları sürümü gönderiyor. Eski sürümdeki form 409 alıyor; güncel puan/ölçek değiştirilmiyor. Kullanıcı güncel kaydı yüklemeyi seçene kadar form korunuyor; yenileme başarısız olsa da tekrar deneme eylemi erişilebilir kalıyor. Eski istemciler için sürüm alanı isteğe bağlıdır.
- Kurumun “Analiz ve uyarılar” sekmesi mevcut karşılaştırma/erken uyarı API'lerine bağlandı. Tarih/sınıf filtreleri, 25 kişilik sayfalama, yetki kaybında veri temizleme ve uyarıdan öğrenci raporuna geçiş var. Geri dönüş filtre/sayfa bağlamını koruyor. Filtre değişiminde eski istek ve sonuçlar temizleniyor.
- Kurum öğrenci sınıfını ve öğretmen atamasını düzenleyebiliyor, öğrenci/öğretmeni onayla kurumdan çıkarabiliyor. Identity kurum kapsamındaki mevcut uçlar kullanılıyor. Bekleyen kayıt filtre, gezinme ve çıkışı engelliyor; çıkarılan öğretmen liste yenilemesi başarısız olsa da seçim önbelleğinden hemen kaldırılıyor.
- Kurum abonelik/kontenjan sekmesi etkin lisansı gösteriyor; mevcut kurum öğrencisine koltuk atama, askıya alma ve ayrılan öğrencinin rezerve koltuğunu serbest bırakma sunucuda kurum kapsamıyla doğrulanıyor. Son koltuk eşzamanlı atamalarda aşılmıyor. Kurumdan ayrılan öğrencinin geçmiş koltuğu aktif erişim vermiyor.
- Eski hedef düzenleme/silme uçları da aktif öğretmen atamasını doğruluyor; kaldırılmış atama veya birleşik kurum/öğretmen rolü geçmiş hedefi değiştiremiyor.
- Koltuk kayıtlarında işlem tekrarının hem commit öncesi geri alma hem commit sonrası yanıt kaybı senaryosu doğrulandı; tekrar önce gerçek veritabanı durumunu yüklüyor. Kurum kapsamı değişince eski okuma/yazma istekleri ve bildirimler temizleniyor.
- Öğretmen/öğrenci planında isteğe bağlı kaynak/sayfa ve planlanan soru sayısı var. Öğrenci gerçekleşen soru sayısını süreyle birlikte kaydediyor. Tamamlanmış sayı değiştirilemiyor; açık sıfır ile eksik kayıt ayrılıyor. Sabit iş otomatik taslağa taşınırken kaynak ve planlanan sayı korunuyor. Kurum/admin geçmişi ve gizlilik dışa aktarımı aynı alanları içeriyor.
- Çalışma raporu planlanan/kaydedilen soru toplamını ve kaç işte sayı girildiğini gösteriyor. Eksik sayılar sıfır kabul edilmiyor. Uzun boşluksuz kaynak metni öğrenci/kurum mobil görünümünde taşmıyor.
- Manuel seans oluşturma/yeniden planlama aynı öğretmen veya katılımcının çakışan saatini veritabanında atomik reddediyor. Bitişte başlayan seans serbest; iptal edilen aralık açılıyor. Eski çakışmalı kayıtlar ve metadata düzenlemeleri korunuyor. Öğretmen/admin409 yanıtında formu koruyup başka saat seçilmesini istiyor. Katılım silme ile seans güncellemesinin kilit sırası da doğrulandı.

## Test kanıtları

Haftalık rapor artık önceki yedi gün ile karşılaştırılabiliyor; bilinmeyen gerçekleşen süre sıfıra çevrilmiyor. Ders/konu öncelikleri yanlış ve boş cevap oranından üretiliyor, sınav türü ve veri kaynağı korunuyor. Öğretmen ve kurumun seans hazırlık özeti güncel genel ilerleme ile iki haftayı birlikte getiriyor; yeniden açılış da raporu yeniliyor. Kurum görünümü salt okunur, kirli öğretmen taslağından geçiş onayla korunuyor. İstanbul DateOnly başlıkları ve 390 piksel rapor genişliği doğrulandı.

Yerel sonuçlar `artifacts/coaching-completion-20261013/` altında tutuluyor.

- `coaching-regression-stage10.trx`: 724 başarılı, 0 başarısız, 0 atlanan; `e2e-stage10-sessions.log` 8 başarılı tarayıcı senaryosu. Yeni seans senaryosu öğretmen/admin409, korunmuş form, bitişik görüşme ve gerçek ICS başlangıcını doğrular. Identity kapsamı hâlâ yalnız yerel sentetik fixture'dır.
- `session-conflicts-red.trx` öğretmen/öğrenci eşzamanlı çift rezervasyonu3 testte yeniden üretir. `session-lock-order-red.trx`40P01 kilit sırası hatasını gösterir; BEFORE STATEMENT kilit düzeltmesinden sonra `session-conflicts-migration-green.trx`7 seçili PostgreSQL/API/gizlilik testi geçti. Down/Up eski seansları silmez. `session-conflicts-teacher-ui-green.log`7, `session-conflicts-admin-ui-green.log`4 seçili UI testi başarılı.

- `coaching-regression-stage9-final.trx`: 718 başarılı, 0 başarısız, 0 atlanan. `task-questions-green.trx` 34; yeni gizlilik alanlarını içeren `task-questions-privacy-green.trx` 1; yeni migration Down/Up ve DB sınırlarını içeren `task-questions-migration-green.trx` 2 seçili test başarılı. İlk tam stage9 sonucu eski tabloyu yeniden oluşturan testte yeni sütun bulunamadığını gösterdi; test güncel migration'ı tekrar uygular ve tüm718 yeniden geçti.
- `task-questions-ui-green-final.log`: kaynak/soru formu, ortak rapor, HTTP sözleşmesi ve admin geçmişinde 33 başarılı test. `staff-stage9.log` 175 başarılı personel testi. `admin-coaching-regression-stage8.log` kaynak/soru aşamasından önce 270 başarılı koçluk testi.
- `e2e-stage9-questions-final.log`: 7 başarılı senaryo; gerçek API ile kaynak/planlanan30/gerçekleşen24 soru döngüsü ve 390 piksel öğrenci/kurum görünümü dahil. `task-source-mobile-red.log` uzun kaynakla390→1719 piksel taşmayı yeniden üretir. `task-questions-report-red.log` eşzamanlı derlemedeki eski form imzası nedeniyle derleme RED'idir; ilgili checkpoint başlığındaki runtime test sayısı kanıt olarak kullanılmamalıdır.

- `coaching-regression-stage7.trx`: 716 başarılı, 0 başarısız, 0 atlanan.
- `staff-stage8.log`: 175 başarılı personel testi, 24 dosya. `session-brief-refresh-red.log` tekrar açılıştaki eski raporu iki panelde yeniden üretir.
- `e2e-stage8.log`: 7 başarılı senaryo; öğretmen ve kurum seans özeti, iki haftalık karşılaştırma ve kurum raporunun 390 piksel görünümü dahil. Yerel Identity fixture sınırı devam eder.

- `coaching-regression-stage2-final.trx`: 696 başarılı, 0 başarısız, 0 atlanan; gerçek Testcontainers PostgreSQL dahil.
- `staff-regression-stage2.log`: personel panelinde 149 başarılı test, 22 dosya.
- `staff-regression-stage3.log`: kurum analizi dahil personel panelinde 155 başarılı test, 23 dosya.
- `staff-regression-stage4-final.log`: kurum üyelik yönetimi dahil 163 başarılı test, 23 dosya. `membership-review-red.log` bekleyen öğretmen araması ve çıkarma sonrası başarısız yenileme hatalarını yeniden üretiyor.
- `coaching-regression-stage5-final.trx`: 715 başarılı, 0 başarısız, 0 atlanan. Üyelik değişimi gerçek PostgreSQL üzerinde kurum yöneticisi/sahibi, dış kurum öğretmeni reddi, mükerrer atama engeli ve geçmişin korunmasıyla doğrulandı.
- `staff-regression-stage5.log`: 170 başarılı test, 24 dosya. `institution-seat-retry-red.trx` geri alınmış koltuk aktivasyonunun yanlış başarı bildirmesini; `institution-seat-scope-red.log` eski kapsam yazma yanıtını yeniden üretiyor.
- `staff-target-ui-final.log`: ortak hedef düzenleyicisi ve kapsamlı HTTP yollarında 14 başarılı test.
- `institution-dates-red.log` / `institution-dates-green.log`: tarih sapması yeniden üretildi ve 6 test geçti.
- `exam-rows-red.log` / `exam-rows-green.log`: yapısal ders puanı formunda 12 test geçti.
- Abonelik/middleware seçili testleri 67; eski yönlendirme Edge/Karma testleri 3 başarılı.
- `e2e-results.json`: dört Playwright senaryosu başarılı; plan yayımlama/tamamlama/kurum raporu, hedefin 450/500 LGS ölçeğinin başlık düzenlemesinden sonra korunması, yetkisiz erişimin reddi ve kurum analizinden rapora geçiş/geri dönüş. Mobil analiz ekranı 390 pikselde yatay taşma olmadan doğrulandı. Gerçek yerel Coaching API ve PostgreSQL kullanılıyor; Identity oturumu ve ilişkiler yalnız sentetik yerel fixture ile sağlanıyor. Gerçek Identity giriş veya canlı kullanıcı testi olarak değerlendirilmemeli.
- `staff-enum-green.log`: katı sunucu enum sözleşmesiyle hedef/sınav servislerinde 9 test başarılı.
- Son `e2e-results.json` beş başarılı senaryo içeriyor. Ek üyelik senaryosu sınıf/öğretmen düzenleme, bekleyen kayıtta çıkış engeli, çıkarma onayı ve eski öğretmen seçeneğinin temizlenmesini sentetik Identity HTTP yanıtlarıyla doğruluyor; gerçek Identity üyelik entegrasyonu kanıtı değildir.
- Güncel E2E sonucu altı başarılı senaryoya ulaştı. Son senaryo gerçek yönetici plan/lisans oluşturma API'si, kurum koltuk atama/kaldırma API'si ve öğrenci abonelik erişimi API'sini birlikte doğruluyor; kurum üyeliği yine sentetik Identity fixture'dır. Lisans test sonunda iptal edilir; ücret tahsilatı yapılmaz. Kontenjan ekranı 390 pikselde taşmıyor.
- Hedef sürüm kontrolü sonrasında E2E yedi başarılı senaryo: ek admin formu eski 400 hedefini kaydetmeyi reddediyor; öğretmenin yeni 450/500 hedefini yükleyip yeni başlıkla kaydediyor. `admin-goal-version-green.trx` 8, `admin-goal-version-ui-green-final.log` 15, `goal-version-ui-green-final.log` 12 başarılı seçili test içeriyor. `*-goal-recovery-retry-red.log` iki panelde 409 sonrası başarısız yenileme sorununu yeniden üretiyor.

Frontend testlerinde bu Windows ortamındaki geçici modül çözümleme sorunu için `artifacts/coaching-completion-20261013/vitest-runner.mjs` kullanılıyor. Geçici TEMP/TMP dizini client `.tmp` altında. Backend testleri `-m:1 -p:UseSharedCompilation=false` ile seri derleniyor.

## Açık iş listesi

Bu çalışma henüz bütün ürün eksiklerinin tamamlandığı anlamına gelmiyor. Önceki karşılaştırmadan kalan işler:

1. Yaklaşan çalışma/seans hatırlatmaları.
2. Koçluk öğrenci ücret/borç/taksit/kasa akışının mevcut kurs finansından ayrı ürün kapsamıyla ele alınması.
3. Koçluk soru bankası, süreli sınav alma/puanlama ve kazanım takibi.
5. Tamamlanan bölümleri kapsayan mobil/masaüstü tarayıcı senaryoları ve son kapsam incelemesi.

Zoom ve SMS sağlayıcı/hesabı kullanıcıda şu an yok. Mevcut manuel toplantı bağlantısı, ICS ve uygulama içi bildirimler kullanılabilir. Gerçek sağlayıcı entegrasyonu veya teslimat testi yapılmış değildir. Canlıya dağıtım yapılmadı.

Dağıtım sırası: yeni `/api/internal/coaching/institution-student` Identity ucu, Koçluk sürümünden önce yayımlanmalı. Eski Identity ile kurum koltuk erişimi 404 nedeniyle erişim vermeyi reddeder. Son backend doğrulamasında başka çalışmanın açık Gateway sürecine dokunmadan önce Koçluk API'si derlendi, test projesi `-p:BuildProjectReferences=false` ile mevcut derlenmiş bağımlılıklara karşı çalıştırıldı. Frontend V8 kapsam ölçümü `@vitest/coverage-v8` kurulmadığı için başlamadı; kapsam yüzdesi iddia edilmiyor.
