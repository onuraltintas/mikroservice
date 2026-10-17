# Haftalık taslaktan kazanım planı — canlı yayın ve doğrulama

Tarih: 17 Ekim 2026. Son uygulama kaynak kimliği: `5411fc01fe62ddc40231ca941d3b76e395527d8b`.
Son yayın: `coaching-weekly-outcomes-20261017-r2`, 15.33.13 Türkiye saati.
Kapsam: Coaching API ve admin-panel içindeki öğrenci arayüzü.

## Canlı sonuç

- Haftalık kayıtlı ders saatlerinden kazanımlı önizleme hesaplanıyor; önizleme veritabanına plan/görev yazmıyor.
- Varsayılan hedef yaklaşan 1 Haziran; kullanıcı tarihi değiştirebiliyor. Sistem tarihi sessizce uzatmıyor.
- Süre yetersizliğinde dersin tahmini bitişi, eksik dakika ve yaklaşık ek haftalık süre gösteriliyor.
- Kullanıcının seçimiyle önerilen tarihe uzatma veya erken biten derslerin saatlerini kullanma yeniden hesaplanıyor.
- Onaydan sonra ayrı bir **taslak** oluşuyor. Mevcut aktif planın değiştirilmesi ayrı yayımlama adımı.
- Takvim boş sabah saatleri yerine ilk dersin yarım saat öncesine açılıyor. Mobilde ilk açılış günü boşsa sıradaki dolu gün seçiliyor. Açıkça basılan Bugün düğmesi bugünü gösteriyor.
- Konu sıralamasında ünite → üst konu → alt konu → kazanım sırası kullanılıyor. Canlı kontrolde bulunan üst konu sırası hatası düzeltildi; Matematik artık Dönüşüm Geometrisi yerine Çarpanlar ve Katlar ile başlıyor.

## Verilerin korunması

İlk yayından önce: 4 plan, 5 görev; 4 tamamlanmış kayıt, toplam 86 gerçek çalışma dakikası.

Üretim kopyasında geçiş iki kez çalıştırıldı. İlk geçişten önce/sonra Coaching iş tabloları
ve Identity hesap/rol/ürün erişimi özetleri aynı kaldı. İlk karşılaştırmada yalnız yeni
dört nullable sütun JSON satır özetinden çıkarıldı; diğer eski alanların tamamı karşılaştırıldı.
Mevcut haftalık taslak da geçiş karşılaştırmasına dâhil edildi.

İkinci yayında bu dört alan artık mevcut veri olduğundan **onlar da özetlere dâhil edildi**.
Tarayıcı testinden sonra yeni satırlar eklenmesine izin verilerek önceki bütün iş satırlarının
özetlerinin hâlâ bulunduğu doğrulandı. Test öğrencisinin haftalık şablonunda aşağıda
belirtilen kontrollü düzenleme yapıldığı için bu satır tarayıcı sonrası genel satır karşılaştırmasından
ayrı değerlendirildi. Identity oturum giriş zamanı gibi oynak alanlar hesap karşılaştırmasına alınmadı.

Son toplam: 6 plan, 338 görev. Eski 4 plan ve 5 görev aynı kaldı; artış doğrulama planlarından
geliyor. Tamamlanmış kayıt sayısı 4, gerçek çalışma toplamı yine 86 dakika. İlk deneme
taslağı arşivlendi; doğru sıralı son taslak yayımlanmadı. Eski aktif plan hâlâ aktif.

Identity ve Coaching yedekleri sunucudaki özel sürüm dizinlerinde tutuldu. Diğer servislerin
container kimlikleri değişmedi. Otomatik veritabanı Down/geri yükleme uygulanmadı.

## Canlı tarayıcı senaryoları

| Senaryo | Gözlenen sonuç |
|---|---|
| Eski haftayı açma | Matematik ve Türkçe yarım saatlik kartlar korundu; mobil ilk açılış Salı 18.00 kartlarına odaklandı. |
| Eksik veri | Türkçenin onaylı kazanım eşleştirmeleri bulunmadığı açıkça listelendi, oluşturma engellendi. Eksik ders bitmiş kabul edilmedi. |
| Test haftasını kaydetme/yeniden açma | Yalnız test öğrencisinde Matematik: Pzt/Çrş/Cuma 18–19, Salı 18–19 ve 19–20. Toplam 300 dk/hafta; sürüm 1. |
| Varsayılan 1 Haziran | 17.10.2026 başlangıcı için 01.06.2027 geldi. 52 kazanım, 7.215 dk; bitiş 05.04.2027. |
| Kısa hedef | 01.11.2026 hedefinde 600 dk yerleşti, 6.615 dk açık gösterildi. Önerilen 05.04.2027 seçilince açık sıfırlandı. |
| 1 Haziran sonrasına uzatma | 17.05.2027 başlangıcında varsayılan hedef 01.06.2027; 6.435 dk açık. Sistem 01.11.2027 önerdi; buton seçilince tarih uzadı ve açık sıfırlandı. Bu önizleme kaydedilmedi. |
| Boşalan saat seçeneği | Canlı seçildi, yeniden hesaplandı, kayıtlı kaynakta `ReuseFreedSlots=true` doğrulandı. Tek onaylı ders olduğu için başka derse fiilî devir oluşmadı. |
| Son taslağı oluşturma | 167 görev, 52 farklı kazanım, 7.215 dk. İlk görev 19.10.2026 18–19, Çarpanlar ve Katlar / M.8.1.1.1. Son tarih 05.04.2027. |
| Taslağı yeniden açma | Aynı plan kimliğiyle açıldı; ilk sayfada 50 görev, kazanım metni ve süre sürümü 2 göründü. |
| Plan seçme | Taslak/Aktif/Arşiv filtreleri ve plan başlıklarından seçim çalıştı. Eski aktif planda 12 dk/0 soru ve 17 dk/8 soru; eski arşivde 40 dk/24 soru görüldü. |
| Dolu saat odağı | İç takvim kaydırması 1260 px = 17.30. Dersler 18.00/19.00'da görünür. Hafta ve ay geçişi çalıştı; düzenleme sırasında konum sıfırlanmadı. |
| Boş alanlara dağıtma | SQL ile her yeni görevin haftalık şablondaki aynı gün/saat aralığının içinde kaldığı doğrulandı; gece/gündüz boş aralıklara görev yazılmadı. |

Son taslak: `2f7e2d07-e044-471d-aedc-dfe004df7e2e`.
Başlık: **Canlı doğrulama · Yıllık kazanım · 17.10.2026 r2**.
İlk arşivlenen test: `5a2d2096-257b-448b-86d9-91c6c426d50f`.
Bu kayıtlar sentetik doğrulama planlarıdır; gerçek tamamlanmış çalışma eklenmedi.

## Veritabanına ne kaydediliyor?

| Yer | Bilgi |
|---|---|
| `study_weekly_drafts` | Öğrencinin son haftalık şablonu, gün/ders/başlangıç/30–60 dakika kartları, saat dilimi ve iyimser kilit sürümü. |
| `study_plan_revisions` | Yeni bağımsız plan kimliği, `PlanId`, revizyon numarası, başlık, öğrenci UserId, durum, eşzamanlılık sürümü. |
| `study_plan_revisions.WeeklySource` | JSONB: kaynak haftalık sürüm, eğitim bağlamı sürümü, başlangıç/bitiş, boşalan saat tercihi, kaynak aktif plan kimliği/sürümü, saat dilimi ve önizleme parmak izi. |
| `study_plan_tasks` | Tarih, tam dakika süresi, konu kimliği; yeni alanlar `PlannedStartMinute`, `WeeklyLessonId`, `LessonNameSnapshot`. Tamamlanma/gerçek süre boş başlar. |
| `study_plan_task_outcomes` | Görev → kazanım → **belirli süre tahmini kimliği**; ayrılan dakika, kazanım kod/metin anlık görüntüsü. |

Son taslağın kaynakları: haftalık sürüm **1**, eğitim bağlamı sürümü **2**, aktif kaynak
plan sürümü **3**, `Europe/Istanbul`, 17.10.2026–05.04.2027. Her görev kazanım bağlantısındaki
ayrılan dakika, görev süresine eşit. Çakışma, yetim bağlantı veya tarih dışına taşma bulunmadı.

Görev parçaları kazanım sürelerine göre örneğin 15 veya 45 dakika olabilir; haftalık
takvim kartları 30/60 dakikalık kapasiteyi tanımlar. Kazanım bir karta sığmazsa devamı
sonraki uygun ders saatine yerleşir.

## Hangi sürüm nasıl seçiliyor?

1. **Haftalık şablon:** son kaydedilen sürüm alınır. Önizleme isteği beklenen sürümü gönderir;
   uyuşmazlıkta 409 döner. Kullanıcı eski haftalık sürümler arasından seçim yapmaz;
   haftalık şablonun ayrı bir geçmiş sürüm listesi bu aşamada yoktur.
   Kaynak JSON'u haftanın bütün eski kartlarını kopyalamaz; sürüm numarasını tutar.
   Üretilmiş planın kesin tarih/saat/kazanım kayıtları görevlerde sabit kalır, ancak eski
   haftalık kart düzenini aynı şekilde geri açma bu kayıttan sağlanmaz.
2. **Ders/program kapsamı:** öğrencinin eğitim bağlamının sunduğu dersler, sınav türü,
   sınıf ve akademik yıl dikkate alınır. Canlıda uygun resmî paket
   `meb-2018-lgs-matematik-2026-pilot-v1`.
3. **Kazanım ve süre:** yalnız onaylı eşleştirme, arşivlenmemiş kazanım ve yayımlanmış süre
   kullanılır. Bir kazanımda yayımlanmış en yüksek `Number` seçilir; canlıda hepsi **2**.
   Taslak bir süre varsa yayımlanmış sürümün yerine geçmez. Seçilen tahmin `EstimateId`
   ile sabitlenir; eski kayıt sonraki süre değişikliğinde kendiliğinden güncellenmez.
4. **Plan seçimi:** kullanıcı Plan durumu filtresi ve başlık satırından belirli planın kimliğini
   açar. Yeni üretim ayrı `PlanId` ve revizyon 1 oluşturur; bir önceki planın üstüne yazmaz.
   Arayüzde süre sürümünü elle değiştiren veya haftalık sürüm seçen bir menü yoktur.
5. **Önizlemeden kayıt:** sunucu dağılımı yeniden hesaplar. Haftalık şablon, eğitim bağlamı,
   kazanım/süre veya kaynak aktif çalışmalar değişmişse parmak izi uyuşmaz ve kayıt reddedilir.
6. **Taslağı yayımlama:** ayrıca onay gerekir. Haftalık/eğitim bağlamı veya kaynak aktif plan
   sürümü değişmişse yeniden üretmek gerekir. Yayımlama yeni planı aktifleştirip önceki aktif
   planı arşivler; canlı testinde bu adım uygulanmadı.

## Sınırlar ve takip işleri

- Canlıda onaylı kapsam yalnız Matematik. Diğer LGS derslerinin kazanım/süreleri tamamlanmadan
  bütün dersleri içeren yıllık plan oluşturulamaz; bu, eksik veriyi gizleyen bir otomasyonla aşılmadı.
- Başlangıç incelemesinde eşleştirme satırları üzerinden toplanan 8.415 dakika yinelenen konu
  bağlantılarını da içeriyordu. **Doğru tekilleştirilmiş 52 kazanım toplamı 7.215 dakikadır.**
- İki farklı ders arasında fiilî boş saat devri canlıda gözlenemedi. İki dersli gerçek yerel
  PostgreSQL testinde seçilmeden 30 dk açık kaldı; seçimle 1 Haziran'daki boş Türkçe saati
  Matematiğe geçti, 120 dk eksiksiz kaydedildi ve eski gerçek çalışma korundu. Yerel senaryo
  canlı testi olarak sunulmuyor.
- Takvim haftalık tekrar şablonudur. Üretilen yıllık görevler önizlemede tarih/saat tablosunda,
  kayıt sonrasında sayfalı plan listesinde gösterilir; yıllık görevlerin gün/hafta/ay takvimine
  işlenmiş görünümü henüz yoktur.
- Gün görünümünde kullanıcının sonradan seçtiği boş gün boş gösterilir; dolu olmayan saatler
  takvimden kaldırılmaz. İlk açılış odağı dolu saatlere taşınır.
- Saat aktarımının işareti önizlemede çalışma parçasında görünür. Kayıtta fiilen çalışılacak
  ders ve plan düzeyindeki aktarım tercihi tutulur; saati bağışlayan eski haftalık kartın
  kimliği ayrıca saklanmaz. Kart geçmişi ve ayrıntılı aktarım izi ileride ayrıca eklenebilir.
- Yayımlanmamış test taslağı ve 300 dk/haftalık test şablonu test hesabında bırakıldı. Yeni bir
  plan üretmek için mevcut taslağın yayımlanması veya arşivlenmesi gerekir.

## Testler ve kanıtlar

- Önceki geliştirmede 196 arayüz testi; son düzeltmeden sonra yerelde çalışabilen 694 koçluk regresyonu tekrar başarılı (0 atlanan).
- Bu yayında: 12 takvim testi; bileşen satır kapsamı %95,5.
- Gerçek PostgreSQL dâhil 71 müfredat/planlama testi; dağıtım servisinin satır kapsamı %100.
- Ek iki dersli PostgreSQL aktarım/kayıt testi: 1 başarılı.
- Canlıya giden yalıtılmış arayüz paketinde 18 masaüstü/mobil tarayıcı testi başarılı.
- Canlı kopya geçişi iki kez uygulandı; SQL kaynak/süre/çakışma/saat aralığı koruma kontrolleri geçti.
- Son sağlık gözlemi 15.48.52 Türkiye saatinde tamamlandı: 15 dakika boyunca 31 örneğin
  tamamı başarılı. İki servis de çalışır durumda, yeniden başlama sayısı 0; giriş,
  haftalık takvim, önizleme ve plan sayfaları HTTP 200. Son hata taramasında uygulama
  hata satırı 0; korumalı anonim API istekleri 401. Gözlem sonrası eski veri özetleri
  yeniden karşılaştırıldı ve korundu.

Ortak Identity test projesine ilk verilen geniş `FullyQualifiedName~Coaching` filtresi
Docker gerektiren koleksiyonları ve adında coaching geçen ilgisiz Identity testini de
seçti: 136 Docker bağlantı/fixture hatası, 1 mevcut Identity ürün varsayılanı beklentisi
hatası, 15 çevre nedeniyle atlanan test oluştu. Bu deneme başarılı olarak sayılmadı.
Ardından kaynak dosyalardaki `[Collection]` tanımlarından Docker koleksiyonları ayrılıp
yalnız `Identity.API.IntegrationTests.Coaching*` sınıfları çalıştırıldı: **694 başarılı,
0 başarısız, 0 atlanan**. Yeni müfredat testleri ayrıca gerçek yerel PostgreSQL'de çalıştı.
İlgisiz çalışma klasörü/Identity değişiklikleri canlı paketine alınmadı.

Günlükler: `artifacts/coaching-weekly-outcomes-live-20261017-r2/` ve ilk yayın için
`artifacts/coaching-weekly-outcomes-live-20261017/`. Ek test günlükleri:
`weekly-outcome-focus-green.log`, `weekly-outcome-order-green.log`, `weekly-outcome-reuse-postgres-final.log`.
Regresyon: `weekly-outcome-deploy-coaching-regression-local.log`; geniş denemenin
günlüğü `weekly-outcome-deploy-coaching-regression.log`.

Ekran kanıtları: son sürüm dizinindeki `proof/generated-draft.jpg`, `preview-shortfall.jpg`,
`extended-beyond-june.jpg`, `occupied-calendar.jpg`.

İlk paket SHA-256: `ddc2b03adbbd11c1d340f6fd8516a5c8cf8b7c10caa2d647c7043b5d8bd7509a`.
Son paket SHA-256: `0d5957750cb01941126aabe9cfc85cf833f72b7818e9f8fad1eb06ef812fdca4`.
