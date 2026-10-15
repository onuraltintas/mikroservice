# Hızlı Okuma test, belge ve kaynak temizliği — 15.10.2026

Bu çalışma, aynı günkü [denetimden](../../artifacts/speed-reading-audit-20261015/README.md) sonra kullanıcının test düzeltme, belge güncelleme ve yalnız doğrulanan kullanılmayan dosyaları kaldırma isteğini uygular. PostgreSQL doğrulamasında bulunan kelime sınavı kayıt hatası, canlı testte bulunan öğretmen öğrenci-listesi hatası ve CAPTCHA hata mesajı ayrımı düzeltildi. **15.10.2026 18:54 Türkiye saati itibarıyla son backend ve frontend düzeltmeleri canlıdadır.** Migration ve abonelik ayarı değişikliği gerekmedi. Çalışma alanında önceden bulunan diğer geliştirmeler korundu ve yayın paketine alınmadı.

## Test düzeltmeleri

- Fixation ve görsel genişleme player testlerinde constructor/DI atlanarak oluşturulan nesnelere eksik öğrenci `AuthService` mock'u eklendi. Gerçek `goBack`/`cancelExit` davranışı korunarak duraklama, devam etme ve çıkış onayı durumları da kontrol edildi.
- Yedi eski kaynak sözleşmesi beklentisi güncel davranışla eşleştirildi: eksik çalışma/tekrar/ödev yolu ayrımı, fixation'a özel istatistik şeridi, saccade geçiş sayısı, assessment guard zinciri, gösterim temposu ve takistoskopun metin gerektirmemesi. Gereksinim kontrolleri silinmedi.
- PostgreSQL görsel genişleme testi normal eğitim için `10 → 11 → 12 → 13` beklentisine geçirildi; doğru cevap bilgisi ve son yanlışta 13'te kalma kontrolü eklendi. Seviye tespiti protokolüne dokunulmadı.
- Ortak staff test paketindeki üç eski fixture hatası için yalnız `course-management-product.spec.ts` dosyasına `ChangeDetectorRef` provider'ı eklendi; mevcut ürün izolasyonu beklentileri korundu.
- Ödeme E2E testindeki eski giriş başlığı seçicisi gerçek e-posta alanı ve giriş düğmesiyle değiştirildi. Girişe yönlendirme, seçilen planın `returnUrl` içinde korunması, anonim kullanıcıya banka/ödeme alanlarının açılmaması ve yazma isteği yapılmaması kontrolleri korundu.
- [Tek sözleşme çalıştırıcısı](../../tools/test-speed-reading-contracts.mjs), `npm run test:contracts --prefix clients/speed-reading` ile frontend/backend altındaki 24 dosyayı çalıştırır. Başarısız runner sonucunu dışarı aktarır. Speed Reading frontend CI işine eklendi.

## Belge güncellemeleri

[İstemci README](../../clients/speed-reading/README.md) mevcut dosya yapısı, Node/Angular sürümü, Gateway adresi, test komutları, gerçek build yapılandırmaları/bütçeleri, önizleme ve veri sınırlarıyla yenilendi. Yanlış Cypress, staging, eski öğretmen ağacı ve örnek destek/lisans bağlantıları kaldırıldı.

[Sitemap rehberi](../../clients/speed-reading/sitemap-README.md) canlı backend sitemap'i ile elle üretilen `public/sitemap.xml` dosyasını ayırıyor; statik betiğin dinamik CMS/blog verisi çekmediği açıklandı. [E2E rehberi](../E2E_TESTING.md), [test README](../../tests/README.md) ve ana README test bağlantısı güncellendi. Güncellenen dört alt rehberin yerel Markdown bağlantı kontrolü geçti; önceki denetimdeki ana README kırık hedefi de mevcut test rehberine bağlandı. Tarihli raporlar geçmiş kanıtı olarak korundu.

## Kaldırılan dosyalar ve kanıt

**43 dosya / 244.632 byte kaynak** kaldırıldı:

- Dört boş public sayfa iskeleti: contact, payment, blog-list ve blog-detail. Çalışan route'lar `.component.ts` sürümlerini kullanır; kaldırılan kısa adlar `... works!` iskeletleriydi.
- Eski Hızlı Okuma içi koçluk route/layout, öğretmen ve öğrenci koçluk ekranları, yalnız bu ekranların kullandığı üç servis ve base layout.
- Bu kapalı, kullanılmayan uygulamayı test eden üç spec dosyası.

Toplam: **19 üretim TS, 21 HTML/SCSS, 3 spec**. [Dosya listesi, boyutlar ve SHA-256 değerleri](../../artifacts/speed-reading-cleanup-20261015/deletion-manifest.json) inceleme ve Git geçmişinden kurtarma için kayıtlıdır. Dosyalar silinmeden önce takipli/değiştirilmemiş oldukları doğrulandı. Öğrenci/admin/staff ve diğer istemci kaynakları dahil, kalan üretim veya test dosyalarından silme kümesine çözümlenen import ve yerel şablon/stil referansı bulunmadı.

Çalışan `CoachingRedirectComponent`, `LegacyStaffRedirectComponent`, merkezi admin yönlendirmeleri, backend `Legacy` adaptörleri, migration'lar, içerik paketleri ve tarihsel release/rollback çıktıları korundu. Statik analizde kalan **77 aday** bu değişiklikte kesin kullanılmayan olarak onaylanmadı ve kaldırılmadı: [liste](../../artifacts/speed-reading-cleanup-20261015/remaining-candidates.json).

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| Temizlik öncesi düzeltilmiş öğrenci paketi | **1.006 / 1.006 geçti** |
| Temizlik sonrası öğrenci paketi | **998 / 998 geçti** |
| Backend yerel testleri | **1.294 / 1.294 geçti** |
| Merkezi admin paketi | **95 dosya, 567 / 567 geçti** |
| Staff öğretmen/kurum paketi | **47 dosya, 291 / 291 geçti** |
| Frontend/backend kaynak sözleşmeleri | **24 dosya; runner 44 / 44 geçti** |
| Görsel genişleme Playwright | **24 / 24 geçti**; masaüstü + mobil, cevap/yanlış, özel ayarlar, mola ve sonuç |
| Takistoskop Playwright | **8 / 8 geçti**; masaüstü + mobil, 75 ms, sabit basamak ve geri bildirim |
| Fixation seçili Playwright | **2 / 2 geçti**; dört harf, duraklatma, sonuç, kayıt yapılmaması |
| Ödeme erişimi Playwright | **4 / 4 geçti**; anonim girişe yönlendirme ve oturumlu banka/form görüntüleme |
| Öğrenci, admin, staff production build | **Üçü de başarılı**; staff `/staff/` base-href ile doğrulandı; mevcut bütçe uyarıları devam ediyor |
| Öğrenci TypeScript `--noEmit` | **Geçti** |
| Değiştirilen kaynaklarda whitespace kontrolü | **Geçti** |
| PostgreSQL test projesi derlemesi | **Geçti**; hedef testler listelendi |
| Hızlı Okuma entegrasyon sınıfları, son geniş koşu | **189 / 189 geçti; 0 atlanan**; öğretmen roster regresyonları dahil, PostgreSQL kullanan senaryolar gerçek geçici veritabanında çalıştı |
| Canlı kaynak tabanından izole frontend paketi | **991 / 991 geçti; 0 atlanan**; mevcut başka giriş/Google çalışmalarına ait 9 test ve kod değişikliği yayın kapsamı dışında |
| Görsel genişleme PostgreSQL | **8 / 8 geçti**; Docker hazır olduktan sonra gerçek geçici veritabanında doğrulandı |
| Kelime egzersizi PostgreSQL | **4 / 4 geçti**; öğrenme, tekrar, sınav ve öğrenme→sınav |
| Kelime regresyon birim testleri | **16 / 16 geçti** |
| Backend Release build | **0 hata, 0 uyarı** |

1.006'dan 998'e düşüş, kaldırılan üç eski spec dosyasındaki 8 testten kaynaklanır. Başarısız test atlanarak başarı üretilmedi: temizlikten önce tüm 1.006 testin geçtiği ayrıca kaydedildi.

Temizlik öncesi ve sonrası frontend production çıktısındaki **119 JavaScript/CSS/HTML dosyası birebir aynı**; eksik, eklenen veya değişen dosya yok: [karşılaştırma](../../artifacts/speed-reading-cleanup-20261015/bundle-comparison.json). Bu kontrol frontend çalışma zamanı paketinin temizliğe rağmen aynı kaldığını doğrular; canlı ortamın test edildiği anlamına gelmez. Aşağıdaki PostgreSQL takibinde backend kalıcılık koduna ayrıca dar bir hata düzeltmesi yapıldı.

Öğrenci test kapsamı: statements **%64,41**, branches **%58,12**, functions **%56,45**, lines **%65,38**. Bu değerler yalnız ilgili tarayıcı test çalıştırmasının kapsamıdır; backend veya bütün platform kapsamı değildir.

Toplam **38** tarayıcı doğrulamasının sonuçları ve son durum [kanıt klasöründe](../../artifacts/speed-reading-cleanup-20261015) tutulur. Bunlar yerel uygulama + fixture API kullanır; gerçek ödeme/kullanıcı hesabı veya PostgreSQL kalıcılık testi değildir. Bütün egzersiz E2E config'lerinin yeniden çalıştırıldığı iddia edilmez.

## PostgreSQL takip doğrulaması ve kelime sınavı düzeltmesi

İlk koşuda Docker Desktop `Secrets Engine / engine.sock` başlatma hatası nedeniyle kapalıydı. Kullanıcı Docker'ı açtıktan sonra motor erişimi doğrulandı ve görsel genişleme PostgreSQL testlerinin sekizi de geçti. Geçici Testcontainers PostgreSQL kullanıldı; Docker verileri sıfırlanmadı ve socket/secret dosyaları silinmedi.

Geniş ilk koşuda **186 testin 184'ü geçti; iki kelime sınavı senaryosu** `IX_user_gamification_UserId` benzersizlik hatası verdi. Aynı tamamlama işleminde kelimeler kaydedilirken henüz `SaveChanges` yapılmamış puan profilleri veritabanı sorgusuyla bulunamıyor, dört kelime ve tamamlama için beş profil oluşturuluyordu. InMemory testleri benzersizlik kısıtını uygulamadığı için önceki testler bunu yakalamamıştı. Birim teste tek profil/soru sayısı/tek rozet/XP ve tekrar tamamlama kontrolleri eklenince hata önce yeniden üretildi: [başarısız regresyon kanıtı](../../artifacts/speed-reading-cleanup-20261015/vocabulary-red.log).

Dar düzeltme üç kalıcılık dosyasında yapıldı:

- `OwnedVocabularyProgressRecorder` ve oturum tamamlama profil araması önce aynı EF context'inin izlediği aktif profile bakıyor; her kelimede yeni profil yaratılmıyor.
- `OwnedGamificationAchievementEvaluator`, aynı işlemde henüz kaydedilmemiş rozetleri de dikkate alıyor; aynı rozet ve ödülü tekrar verilmiyor.
- Puan formülü, rozet kriterleri ve veri şeması değiştirilmedi. İki mevcut test dosyası dört soru, tek profil/rozet, bir kez verilen rozet ödülü + egzersiz XP'si, tek tamamlama ve tekrar isteğinin sonucu değiştirmemesini doğruluyor. Öğrenme/tekrar modlarının puan profili oluşturmaması da kontrol ediliyor.

Düzeltme sonrası **16 kelime birim testi, 4 gerçek PostgreSQL senaryosu, 1.294 backend testi ve 44 kaynak sözleşmesi geçti**. Backend Release build sıfır hata/uyarıyla tamamlandı. İlk geniş koşunun başarısız sonucu [ayrı saklandı](../../artifacts/speed-reading-cleanup-20261015/test-results/speed-reading-integration-initial.trx).

**Son geniş koşu: 186 başarılı, 0 başarısız, 0 atlanan**; 1 dakika 36 saniye. [Sonuç TRX](../../artifacts/speed-reading-cleanup-20261015/test-results/speed-reading-integration.trx), [koşu günlüğü](../../artifacts/speed-reading-cleanup-20261015/postgres-integration.log). Önceki Docker ortam engeli ve iki kelime sınavı hatası bu koşuyla kapandı. Filtre yalnız `Identity.API.IntegrationTests.SpeedReading` sınıflarını seçer; ortak projedeki başka isimli bütün testlerin veya depo genelinin tamamını kapsamaz.

Geniş koşu komutu:

```powershell
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --filter "FullyQualifiedName~Identity.API.IntegrationTests.SpeedReading" --logger "trx;LogFileName=speed-reading-integration.trx"
```

## Depo geneli sınırlar

Kullanıcının sonraki açık isteğiyle eski ürün koruma snapshot'ı uzlaştırıldı. Güncel envanter **1.533 dosya** içeriyor: ilk uzlaştırmada 32 tarihsel dosya eklemesi ve 104 hash farkı kaydedildi; ardından öğretmen roster düzeltmesinin bir ek dosya hash'i güncellendi. Yalnız temizlik manifestindeki 43 silme kabul edildi. Snapshot, `c62957ae` kaynak tabanını ve çalışma alanı kapsamını açıklar; başka geliştirmelerin yayına onayı anlamına gelmez. Önceden mevcut giriş/Google/onay çalışmalarının hash'leri yalnız kaynak koruması için donduruldu; canlı pakete alınmadı. Dört yerel build kopyası, gerçek `clients/shared` kaynaklarından ayrı oldukları doğrulanarak dosya bazında ignore edildi.

Envanter fonksiyonu silinmiş takipli dosyaları artık mevcut saymıyor; Git yollarını NUL ayracıyla okuyarak Türkçe karakterli dosyaları da koruyor. Silinen takipli dosya ve yeni `özel.ts` için regresyon kontrolleri var. **12 / 12 koruma sözleşmesi testi geçti**, hiçbir assertion atlanmadı. [Koşu kaydı](../../artifacts/speed-reading-cleanup-20261015/snapshot-contract.log). Bu sonuç, depo genelindeki bütün CI işlerinin yeniden çalıştırıldığı anlamına gelmez.

TypeScript/JavaScript değişiklikleri ve silme kümesi bağımsız incelemeden geçti. PostgreSQL testleri ve son kelime kalıcılık düzeltmesi C# incelemesinden geçti. Frontend test/build sonuçları önceki temizlik koşusundan, backend ve PostgreSQL sonuçları son düzeltme sonrası koşulardandır.

## Canlı yayın ve doğrulama

İlk backend yayını `speed-reading-maintenance-20261015` etiketiyle 18:32'de kelime sınavı düzeltmesini taşıdı. Canlı panel testleri iki ek sorunu ortaya çıkardı; bunlar da dar kapsamla düzeltildi:

- Öğretmen öğrenci listesinde `Nullable<Guid>.GetValueOrDefault()` Npgsql tarafından SQL'e çevrilemiyor ve endpoint 500 dönüyordu. Aynı null davranışını koruyan `?? Guid.Empty` ifadesi beş yerde kullanıldı. Kurum/öğretmen/aktif öğrenci kapsam koşulları korunuyor. Üç yeni gerçek PostgreSQL testi önce hatayı üretti, sonra geçti; kurum dışı ve pasif öğrencilerin dışlanması, bağımsız öğrenci, sayfalama ve filtreler de kontrol edildi. Son geniş koşu **189/189**, son backend birim koşusu **1.294/1.294**: [TRX](../../artifacts/speed-reading-cleanup-20261015/test-results/speed-reading-integration-after-roster.trx).
- Öğrenci girişinde CAPTCHA reddinin Türkçe açıklamasındaki “doğrula” kelimesi yanlış e-posta uyarısına yol açıyordu. Ayrım artık `Auth.EmailNotConfirmed` kodunu veya e-postaya özgü açıklamayı kontrol ediyor. İki yeni regresyon başarısızlıkla başlayıp düzeltmeyle geçti. CAPTCHA açık, eşik 0,5 olarak korundu. Kullanıcının normal tarayıcı girişleriyle öğrenci, öğretmen ve kurum oturumları açıldı.

Son yayın kimlikleri:

| Bileşen | Kaynak / canlı imaj |
|---|---|
| Backend, 18:54:25 Türkiye | `d1b74c30d473fe4c03658ad67467a3d97fa51c14` · `speed-reading-roster-20261015` · `sha256:cb8392f347f054c0b2feb985f8f3ce3a852979d928bc95c0ec0859fd577cb74b` |
| Öğrenci frontend, 18:51:23 Türkiye | `d7f743ab15c3f5d26a38c6b17dfe150237573917` · `speed-reading-login-message-20261015` · `sha256:db91e15a852b1bc1f9da27f4dde71a2b4d0f4f6ed1f7ef25e6645756346ce64c` |

Backend yalnız Infrastructure DLL/PDB değiştirir; canlı DLL SHA-256 `ac517618e0487bae4a0ae3d9ed9329f550a4e40d6f28a422d4cb720bbcdaed42`. Application ve Domain DLL'leri aynı kaldı. Frontend canlı kaynak tabanından izole üretildi: onaylı 43 silme, iki test fixture düzeltmesi ve giriş hata sınıflandırması içerir. **991/991 test geçti**, üretim build başarılı. Çalışma alanındaki dokuz ek giriş/Google testinin ve ilgili geliştirmelerin kapsam dışında kalma gerekçesi [kaydedildi](../../artifacts/speed-reading-cleanup-20261015/isolated-test-scope.json). Önceki frontend imajındaki **112 hash'li JavaScript dosyası korundu**; açık eski sekmelerin dinamik importları kaybolmuyor. Admin, staff, Identity ve diğer servis konteynerleri değişmedi.

Sunucuda erişimi kısıtlı yedek ve eski imaj/ortamdan üretilmiş geri dönüş yapılandırması hazırlandı. Son aday imaj önce yedekten oluşturulan **ayrı `speed_maintenance_drill_20261015` veritabanında** gerçek DLL bağımlılıklarıyla çalıştırıldı. Araç üretim veritabanı adını reddeder; prova sonunda yalnız bu adlı kopya silinir. Kelime senaryoları ve boş öğretmen listesi SQL sorguları için **59/59 kontrol geçti**: [son klon prova sonucu](../../artifacts/speed-reading-cleanup-20261015/roster-production-clone-results.json). Şema/migration gerekmedi. Klon provası ile aşağıdaki gerçek oturum kontrolleri ayrı doğrulamalardır.

| Panel | Canlı sonuç |
|---|---|
| Admin | Gerçek SystemAdmin oturumunda kullanıcıların servis rolleri, Hızlı Okuma servis özeti ve abonelik ayarı görüntülendi. Servis özeti 21 tür, 407 egzersiz, 831 metin ve 24 program getirdi. Abonelik zorunluluğu kapalı göründü; ayar değiştirilmedi. |
| Öğrenci | Normal giriş, test profili kurulumu, uygulamadaki “Şimdilik Atla” seçeneğiyle seviye 1 günlük program, ilk göz takibi egzersizini başlatma/duraklatma/devam etme/tamamlama doğrulandı. 75 saniyelik sonuç kaydedildi; günlük ilerleme 1/6 oldu ve sıradaki takistoskop açıldı. Raporlarda toplam aktivite 1 göründü. Tam seviye tespiti uygulanmadı; başlangıç seviyesi ölçülmüş sonuç olarak sunulmuyor. |
| Öğretmen | Normal giriş sonrası öğrenci listesi, sınıf özeti ve ödev yönetimi doğrulandı. Hatalı SQL düzeltildikten sonra endpoint 200 dönüyor. Bu öğretmene bağlı Hızlı Okuma öğrencisi olmadığından normal boş liste görüldü. |
| Kurum | Normal kurum oturumunda öğrenci/öğretmen listeleri, son 30 gün özeti, içerik analizi ve ödev yönetimi açıldı; ilgili API'ler 200 döndü. Test kurumunda Hızlı Okuma üyesi/etkinliği olmadığından normal boş sonuçlar görüldü. |

CAPTCHA kapatılmadı ve eşiği düşürülmedi. Gerçek ödeme/tahsilat veya davet e-postası gönderimi yapılmadı. Bu ilk kontrol sırasında kurum ve öğretmen panellerinde üyeli öğrenci üzerinden ödev atama/tamamlama zinciri henüz tekrarlanmamıştı; boş liste kontrolleri bunun yerine sayılmaz. Sonraki kullanıcı isteğiyle test üyelikleri ve öğretmen bağlantısı kuruldu; 19:17'de öğrenci ödevi tamamladı, öğretmen ayrıntısı ve kurum raporunda tamamlanma doğrulandı. Bu ek canlı koşunun kanıtları ve açık üç bulgusu [ayrı rapordadır](speed-reading-assignment-live-20261015.md). Tüm egzersizler ve depo genelindeki bütün CI işleri çalıştırılmış değildir.

Son sağlık örneği **19:01:18 Türkiye**: backend/frontend çalışıyor, ikisinde de yeniden başlama sayısı 0, frontend giriş sayfası 200. Son backend başlangıcından bu örneğe kadar **66 HTTP isteğinde 0 sunucu 5xx**, ortalama 133,81 ms, en yüksek 1.177,97 ms görüldü. Öğretmen listesi ve gerçek öğrenci tamamlama endpoint'i 200 döndü. Bu yaklaşık yedi dakikalık örneklem yük testi veya kalıcı performans garantisi değildir. [Sağlık ve HTTP özeti](../../artifacts/speed-reading-cleanup-20261015/final-production-health.log). İlk kelime düzeltmesi yayını için önceki 15 sağlık örneği [ayrı kayıttadır](../../artifacts/speed-reading-cleanup-20261015/initial-production-health-observation.log); son backend yayınının 15 dakikalık izlemi olarak sunulmaz.

Ekran kanıtları: [öğrenci sonuç](../../artifacts/speed-reading-cleanup-20261015/student-live.jpg), [öğrenci günlük ilerleme](../../artifacts/speed-reading-cleanup-20261015/student-program-live.jpg), [admin](../../artifacts/speed-reading-cleanup-20261015/admin-live.jpg), [öğretmen](../../artifacts/speed-reading-cleanup-20261015/teacher-live.jpg), [kurum](../../artifacts/speed-reading-cleanup-20261015/institution-live.jpg).

## Ödev döngüsünün sonraki düzeltmesi

Ödev döngüsünde bulunan tarih kayması, ölçülmeyen egzersizin sıfır puan olarak ortalamaya girmesi ve öğrenci listesinin etkileşim beklemesi düzeltildi. **19:39:43 Türkiye** yayını, veri onarım migration'ı ve yeni gerçek öğrenci–öğretmen–kurum döngüsü [ayrı güncel raporda](speed-reading-assignment-fixes-20261015.md) kayıtlıdır. Yeni geniş koşular backend 1.294, entegrasyon 190, öğrenci 994 ve staff 291 başarılı test içerir. Önceki bölümlerin zamanları ve kapsamları tarihsel olarak korunur.
