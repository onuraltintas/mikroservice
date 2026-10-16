# LGS Matematik kazanım ve süre pilotu

Tarih: 16 Ekim 2026. Durum: geliştirme tamamlandı; aynı gün 21:34:33 Türkiye saatiyle canlıya alındı. Kullanıcının açık yayın talimatıyla 58 eşleştirme onaylandı ve 52 süre yayınlandı; katalogda taslak süre kalmadı. Aşağıdaki geliştirme notları ilk taslak paketini anlatır. Güncel canlı sonuçları ve yayın dayanağı için [canlı yayın raporuna](coaching-outcomes-live-release-20261016.md) bakın.

## Kapsam

İlk paket LGS Matematik içindir. Diğer LGS derslerine aynı veri modeliyle devam edilebilir; onların kazanımları bu pakete dahil değildir.

- 52 resmî kazanım metni ve kodu.
- 12 resmî konu grubu, toplam 180 ders saati.
- Mevcut katalogdaki 13 MEBİ konu kaynak kimliğine aday eşleştirme, toplam 58 konu–kazanım çifti.
- 52 düşük güvenli, yayınlanmamış çalışma süresi taslağı.
- Daha önce MEBİ'nin okuma uçlarından doğrulanmış iki kazanım UUID'si için kaynak eşleştirmesi. Diğer 50 kayda MEBİ kimliği uydurulmadı.

**Resmî ders saati ile bireysel çalışma dakikası ayrı saklanır.** Resmî program kazanım başına ayrı bireysel öğrenme süresi vermez. Örneğin Çarpanlar ve Katlar için üç kazanıma toplam 10 ders saati ayırır.

## Kaynaklar ve yıl kapsamı

1. [MEB Matematik Dersi Öğretim Programı 2018](https://mufredat.meb.gov.tr/Dosyalar/201813017165445-MATEMAT%C4%B0K%20%C3%96%C4%9ERET%C4%B0M%20PROGRAMI%202018v.pdf): zaman dağılımı PDF sayfası 27 / basılı sayfa 25; sekizinci sınıf kazanımları PDF sayfaları 73–78.
2. [MEB Temel Eğitim 2026–2027 çerçeve plan duyurusu](https://tegm.meb.gov.tr/www/2026-2027-egitim-ogretim-yili-taslak-cerceve-planlar-yayinlandi/icerik/1316): yeni modelin ortaokulda 5, 6 ve 7. sınıflardaki uygulamasını belirtir. Sekizinci sınıf için bu pilotun 2018 programını kullanması bu kademeli geçiş bilgisi ve mevcut LGS konu kapsamıyla ilişkilendirilmiştir.
3. MEBİ referansı: <https://ogmmateryal.eba.gov.tr/mebi>. Önceki incelemede korumalı genel kazanım listeleri öğrenci yetkisiyle 403 döndürdü. Bu paket tam MEBİ kazanım kataloğu dışa aktarımı değildir; resmî MEB programı esas alınmıştır.

Belge SHA256:

```text
ca94096da33478c822772425b1384bacdbe40548b0813302257fec7e8c9f9c8b
```

Program kodu: `meb-2018-lgs-matematik-2026-pilot-v1`. Geçerlilik **eğitim yılı başlangıcı 2026**, yani 2026–2027 ile sınırlıdır. Sonraki yıl otomatik devretmez. TYMM ve farklı program sürümleri ayrı kayıtlar olmalıdır.

| Resmî konu | Kazanım | Ders saati |
|---|---:|---:|
| Çarpanlar ve Katlar | 3 | 10 |
| Üslü İfadeler | 5 | 15 |
| Kareköklü İfadeler | 8 | 25 |
| Veri Analizi | 2 | 12 |
| Basit Olayların Olma Olasılığı | 5 | 12 |
| Cebirsel İfadeler ve Özdeşlikler | 4 | 15 |
| Doğrusal Denklemler | 6 | 30 |
| Eşitsizlikler | 3 | 10 |
| Üçgenler | 5 | 18 |
| Eşlik ve Benzerlik | 2 | 8 |
| Dönüşüm Geometrisi | 3 | 10 |
| Geometrik Cisimler | 6 | 15 |
| **Toplam** | **52** | **180** |

## Sürelerin anlamı

İlk süreler öğretmen onayı yapılmış tahminler değildir. İncelemeye başlanabilmesi için oluşturulmuş **düşük güvenli bütçe dağılımlarıdır**:

1. Resmî konu bütçesi, model varsayımı olan 40 dakika ile çarpılır.
2. Konunun tüm kazanımlarına eşit bölünür, beş dakikaya yuvarlanır.
3. Yaklaşık %40 öğrenme, %50 alıştırma ve %10 kısa kontrol olarak ayrılır.

40 dakika ve bu oranlar **bireysel öğrenme için resmî MEB ölçümü değildir**. Her kayıttaki `DurationEvidence` bu varsayımları açıklar. Yuvarlama nedeniyle taslakların toplamı 7.215 dakikadır; bu toplam resmî süre olarak sunulmaz. Video ve mola süreleri kullanılmaz.

`OfficialBudgetDraft` sürümü doğrudan yayınlanamaz. Yönetici, kapsamı ve süreyi inceleyip dayanağını yazarak yeni bir `ReviewedStudy` sürümü oluşturur. En az bir konu eşleştirmesi onaylanmadan süre yayınlanamaz. Öğrenci yalnız onaylı eşleştirme ve yayınlanmış en yeni süre sürümünü görebilir.

**Doğrusal Denklemler 1/2:** Her iki MEBİ konu kaydına altı kazanım aday olarak bağlandı. İçerik ayrımı doğrulanmadığı için tüm eşleştirmeler taslaktır. İnceleyen kişi her kaydın gerçek kapsamını onaylamalı; iki adayın tamamını otomatik onaylamamalıdır. Plan aynı kazanımı iki konuda seçerse aynı süreyle yalnız bir kez sayar; çelişen süre seçimlerini reddeder.

## Veritabanı

Yeni migration: `20261016180740_StudyCatalogOutcomes`. Yalnız Coaching şemasına yedi tablo ekler; Identity hesaplarını ve mevcut çalışma tablolarının verilerini dönüştürmez.

| Tablo | İçerik ve koruma |
|---|---|
| `official_curriculum_editions` | Sürüm kodu, LGS, sınıf, ders, geçerli eğitim yılları, kaynak adresleri, belge ve paket SHA256 |
| `official_curriculum_topics` | Resmî konu kodu/adı, kazanım sayısı ve **ders saati** bütçesi |
| `study_catalog_outcomes` | Kanonik kazanım kodu/metni, resmî konu ve program bağlantısı, kaynak PDF sayfası, sıra |
| `study_catalog_outcome_sources` | MEBİ gibi dış kaynakların kazanım kimlikleri; bir kaynak kimliği iki farklı kazanıma bağlanamaz |
| `study_catalog_topic_outcomes` | Yerel konu–kazanım bağlantısı, inceleme dayanağı, onay, inceleyen UserId ve tarih |
| `outcome_duration_estimates` | Öğrenme/alıştırma/kontrol dakikaları, yöntem, güven, dayanak, inceleyen, sürüm ve yayın durumu |
| `study_plan_task_outcomes` | Görev, kazanım, kullanılan süre sürümü, o göreve ayrılan dakika, katalog/öğrenci seçimi kaynağı |

Program ve kazanım kodları kendi kapsamlarında benzersizdir. Kazanımın resmî konu/program ilişkisi ve planın süre sürümü/kazanım ilişkisi birleşik foreign key'lerle korunur. Dakikalar pozitif toplam ve sınır kontrollerine tabidir. Kazanıma bağlı konular katalog kullanım denetiminde silinemeyen kayıt olarak gösterilir.

Süre düzeltmesi yeni sürüm oluşturur. Önceki sürümler ve plan bağlantıları korunur. Görev farklı günlere bölünürse yalnız gerçekten takvime yerleşen dakika görevlere dağıtılır; plana sığmayan süre kaydedilmiş çalışma gibi gösterilmez. Bekleyen sabit görevlerin kazanım bağlantıları yeni taslağa kopyalanır; tamamlanmış çalışmalar önceki planda kalır.

Plan görevi silinince kişisel kazanım bağlantıları cascade ile temizlenir. Resmî katalog silinmez. Kişisel veri dışa aktarımı yalnız ilgili öğrencinin görev bağlantılarını ve kullanılan sürümleri içerir.

## Yönetici akışı

`/dashboard/coaching/catalog` → **LGS kazanımları ve süre incelemesi**:

1. `data/coaching-outcomes/lgs-matematik-2026-pilot.json` dosyasını seç.
2. **Aktarımı önizle:** eşleşen/eşleşmeyen konuları ve uyarıları incele. Veri yazılmaz.
3. Gerekçe girip **Taslakları aktar** seç. Paket veya katalog değişirse eski önizleme parmak iziyle aktarım reddedilir.
4. Resmî programı ve kazanımı seç; ilgili konu eşleştirmelerini dayanak belirterek incele.
5. Öğrenme, alıştırma ve kısa kontrol dakikalarını gir; süre inceleme dayanağıyla yeni sürüm oluştur.
6. İncelenmiş sürümü yayınla. Bu işlem öğrencinin planını oluşturmaz veya değiştirmez.

API yetkileri: `Permissions.Coaching.ContentManage`, Coaching MFA kategorisi ve sunucuda doğrulanan global yönetici kapsamı. Kurum yöneticisi ortak kataloğu değiştiremez. Yazmalar transaction, PostgreSQL advisory lock ve kısa denetim kayıtları kullanır. Mevcut altı dosyalı katalog aktarım biçimi değişmedi; kazanımlar ayrı paketle yönetilir.

Yeni uçlar:

```text
GET  /api/coaching-admin/outcomes
GET  /api/coaching-admin/outcomes/{editionId}
POST /api/coaching-admin/outcomes/import-preview
POST /api/coaching-admin/outcomes/import
PUT  /api/coaching-admin/outcomes/mappings/{topicId}/{outcomeId}
POST /api/coaching-admin/outcomes/{outcomeId}/estimates
PUT  /api/coaching-admin/outcomes/estimates/{estimateId}/publication
GET  /api/coaching/study-planning/topics/{topicId}/outcomes
```

## Öğrenci ve personel akışı

Mevcut otomatik plan ekranında LGS konusunu ekledikten sonra **Kazanım önerilerini yükle** seçilir. Öğrenci kendi eğitim yılına uygun, incelenip yayınlanmış kazanımları görür; istediğini seçer, süreyi kendi planında değiştirebilir. Yayınlı öneri yoksa açık mesaj gösterilir ve mevcut elle konu süresi girme yolu kullanılabilir.

Önizleme ve taslak kaydında kazanım kimliği ve beklenen süre sürümü yeniden doğrulanır. Süre önerisi arada değişmişse kullanıcı kazanımları yeniden yüklemelidir. Öğrenci değişikliği genel kataloğa yazılmaz. Toplam konu süresi mevcut planlayıcının 1.440 dakika sınırını aşarsa seçim küçültülmesi istenir; süre sessizce kısaltılmaz.

Taslak oluşturmak mevcut aktif planı değiştirmez. Yayımlama ayrı adımdır. Kaydedilmiş görevde kazanım kodu, metni, o göreve ayrılan dakika ve süre sürümü görünür. Personel aynı plan bileşeninde bu bilgiyi okuyabilir. Eski konu+dakika API talepleri ve kazanımsız eski planlar çalışmaya devam eder.

## Tekrar üretme

Resmî PDF'yi kaynaktan indirip paket oluştur:

```powershell
python -X utf8 tools/coaching-outcomes/prepare_lgs_math.py artifacts/coaching-outcomes/mathematics-2018.pdf data/coaching-outcomes/lgs-matematik-2026-pilot.json
```

PyMuPDF gerekir. Kaynak dosya 80 sayfa ve çıkarılan kazanım sayısı 52 değilse araç durur. Matematiksel kök işareti konumlanmış PDF glifi olduğundan `M.8.1.3.3` için düz metin gösterimi `a√b` olarak düzeltilmiştir. Kapsam sınırlamalarının tamamını yeniden yazmak yerine her kazanım kaynak sayfaya bağlanır.

Yerel PostgreSQL testleri yalnız açıkça belirtilen localhost bağlantısını ve kendi oluşturduğu rastgele `test_coaching_*` veritabanlarını kullanır; bunları test bitince kaldırır. Üretim bağlantısını test değişkenine vermeyin.

```powershell
$env:COACHING_PROFILE_TEST_POSTGRES = '<yerel geçici PostgreSQL bağlantısı>'
dotnet test tests/Integration/Identity.API.IntegrationTests --filter 'FullyQualifiedName~CoachingOutcome' -m:1 -nr:false
```

Ekran ve tarayıcı testleri:

```powershell
cd clients/admin-panel
npm test -- admin-panel --watch=false --include='src/app/features/coaching-portal/pages/student-outcome-plan.component.spec.ts' --include='src/app/features/dashboard/pages/coaching-outcomes.component.spec.ts'
npm run build -- admin-panel --configuration development
cd ../../tests/E2E
npx playwright test --config playwright.coaching-outcomes.config.mjs
```

Tarayıcı senaryoları derlenmiş gerçek arayüzü kontrollü API yanıtlarıyla sınar; canlı API veya üretim hesabında işlem yapmaz. Gerçek kalıcılık ve migration ayrı PostgreSQL testlerinde doğrulanır.

## İlk geliştirme sonunda kalan işler ve sonraki yayın

İlk geliştirme aşaması canlı dağıtım içermiyordu. Sonraki açık kullanıcı talimatıyla migration, yedekten prova, hesap/plan/çalışma parmak izi karşılaştırması ve canlı yayın tamamlandı. Migration katalog verisi seed etmez; kontrollü yayın aracı paketi aktardı, açık kullanıcı onayını kaydetti ve süreleri yayınladı. İşlem ayrıntıları [canlı yayın raporundadır](coaching-outcomes-live-release-20261016.md).

Yayın, bireysel sürelerin öğretmen tarafından ölçüldüğü anlamına gelmez: başlangıç tahminleri düşük güvenli olarak tutuldu. Doğrusal Denklemler 1/2 için onay ortak resmî konu kapsamınadır; video bölüm kapsamı doğrulanmış sayılmaz. Öğretmen gözlemiyle sürelerin iyileştirilmesi ve diğer beş LGS dersinin resmî programlarının eşleştirilmesi sonraki içerik işleridir.

## Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Pilot backend testleri | 13 geçti; ikisi gerçek, geçici PostgreSQL üzerinde |
| Tam paket aktarımı | 52 kazanım / 12 resmî konu / 13 yerel konu / 58 aday ilişki / 2 MEBİ kaynak kimliği; ikinci aktarımda çoğalma yok |
| Geçiş ve geçmiş | Önceki migration'dan yükseltme ve ikinci migration çağrısı geçti; önceki aktif plan, 25 dakika ve 8 soru korundu |
| Kişisel bağlantılar | Dışa aktarımda kullanılan sürüm mevcut; görev silinmesinde bağlantı cascade ile temizlendi, ortak kazanımlar korundu |
| Yeni backend davranışlarının satır kapsamı | 318 / 348 = %91,4; kazanım modeli, dağıtım, servis, okuyucu ve yeni controller dosyaları birlikte ölçüldü |
| Docker gerektirmeyen mevcut Coaching testleri | 693 geçti; yeni pilotun önceki 12 testi bu koşunun içindedir |
| İlgili ekran testleri | Yönetici/öğrenci kazanım ve ortak plan görünümü seçili koşusunda 24 geçti; otomatik planın mevcut testlerini de içeren ayrı koşuda 10 geçti; bunlar örtüşen kümelerdir |
| Geniş ekran regresyonu | 208 geçti, 1 mevcut menü sayısı beklentisi başarısız: test 12 bağlantı bekliyor, mevcut kurulum bağlantısıyla 13 var |
| Masaüstü ve mobil tarayıcı | 4 geçti: öğrenci kazanım seçimi → süre değiştirme → önizleme → taslak → yayın → çalışma kaydı; yönetici paket önizleme/gerekçe/aktarım |
| Uygulama derlemesi | `admin-panel` development derlemesi geçti |
| Kod incelemesi | Kazanıma bağlı konu silme koruması ve ortak plan görünümündeki eksikler testle yeniden üretildi, düzeltildi; diff boşluk kontrolü geçti |

Geniş backend denemesi tamamen yeşil değildir: eski testlerin Docker PostgreSQL fixture'ı, Docker Desktop Linux motoruna erişemediği için 136 testte başlayamadı. Aynı geniş filtre Coaching dışındaki test adlarını/parametrelerini de eşleştirdi ve değiştirilmeyen `AdminUserProductProvisioningTests.Legacy_constructor_and_json_without_product_keep_coaching_default` testinde bir hata gösterdi. Bu Identity davranışı LGS pilotunda değiştirilmedi. Yukarıdaki 693 testlik sınıf adıyla sınırlandırılmış koşu bu dış kapsamı ve Docker fixture sınıflarını içermez; 13 pilot testi ayrıca gerçek yerel PostgreSQL ile doğrulandı.

Mevcut menü testindeki 12/13 beklentisi ve Identity testi bu görevden önceki kaynaklarla aynıdır; ilişkili dosyalara LGS çalışmasında değişiklik yapılmadı. Bu iki bağımsız hata pilot kapsamında gizlenmedi veya testler devre dışı bırakılmadı.

Kanıtlar çalışma klasöründe `artifacts/coaching-outcome-*.log`, `artifacts/coaching-outcomes/browser.json`, `student-desktop.png`, `student-mobile.png` ve `final-coverage/*/coverage.cobertura.xml` altındadır. Tarayıcı kanıtları kontrollü API yanıtları kullanır; canlı doğrulama kanıtı olarak değerlendirilmemelidir.
