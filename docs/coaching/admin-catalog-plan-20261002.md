# Koçluk admin katalog ve öğrenci inceleme planı

Bu çalışma yereldir. Kullanıcı canlı yayın öncesinde sonuçları görmek istedi;
bu planın migration'ı canlıya uygulanmadı ve GitHub'a gönderilmedi.

## Sıra ve tamamlanma kapıları

1. Veri/yetki envanteri: ilk kontrol tamamlandı.
2. Ortak şehir–ilçe entegrasyonu: eşleştirici, API istemcisi, nullable kimlik alanları
   ve yerel migration doğrulandı. Admin formu ve kayıt bazında onaylı eşleştirme işlemleri bekliyor.
3. Ders/ünite/konu: salt okunur listeleme API'si ve admin ekranı hazır;
   oluşturma/düzenleme, kullanım bilgisi ve yayın işlemleri bekliyor.
4. Okul: salt okunur listeleme API'si ve admin ekranı hazır;
   ortak konum seçimi, onaylı eşleştirme ve yazma işlemleri bekliyor.
5. Üniversite programı: listeleme, ad/üniversite/kod araması ve puan türü/yıl
   filtreleri API'de hazır; admin liste ekranı hazır, yazma işlemleri bekliyor.
6. Kontrollü aktarım önizleme/onay/yayın ekranı: bekliyor.
7. Öğrenci detayında müsaitlik/plan/görev/geçmiş incelemesi: bekliyor.
8. Öğrenci detayında hedef/sınav/rapor incelemesi: bekliyor.
9. Yetkili gerekçeli düzeltme, arşivleme ve işlem geçmişi: bekliyor.
10. Tam admin E2E, regresyon, yayın raporu ve ayrı canlı onayı: bekliyor.

Bir adımın altyapı testinin geçmesi, kullanıcı ekranının bitmiş olduğu anlamına gelmez.
Yeni katalog işlemleri global SystemAdmin kapsamıyla sınırlandırılmalı; kurum yöneticisi
ortak kataloğu değiştirmemelidir. Mevcut Coaching.View/ContentManage izinleri ve Coaching
MFA kategorisi kullanılmalı; koşulsuz yeni MFA zorunluluğu getirilmemelidir.
Öğrenci inceleme/düzeltme yetkileri katalog düzenleme yetkisinden ayrı tutulmalıdır.

## İlk veri envanteri

Altı onaylı dosyanın salt okunur ön kontrolü geçti: 169 ders, 768 ünite,
1.998 ana konu, 3.222 alt konu, 21.602 üniversite programı, 3.010 okul.
Toplam 30.769 katalog kaydı; bu kontrolde DB bağlantısı veya yazısı yapılmadı.

Yerel Identity referans JSON'u ile 3.010 kaynak okulun Türkçe büyük/küçük harf ve
NFC-normalizasyonu sonrası kesin şehir–ilçe karşılaştırması:

- 276 eşleşti.
- 1.179 okulda şehir birebir bulunamadı.
- 1.555 okulda şehir bulundu ancak ilçe birebir bulunamadı.
- Belirsiz çoklu eşleşme sayısı 0.

Bu sayılar canlı DB'den yeniden çıkarılmış eşleştirme raporu değil, sağlanan okul dosyası
ile yerel referansın karşılaştırmasıdır. Bazı kaynaklarda `(MERKEZ)` eki, farklı şehir
yazımı ve referansta birleşik Unicode nokta farklılıkları vardır. Aksan silme, ek silme,
benzerlik tahmini ve başka şehirdeki ilçeyi seçme uygulanmadı. 2.734 kaydın yanlış olduğu
sonucu çıkarılamaz; yalnız katı otomatik eşleştirmeyle doğrulanamadıkları anlamına gelir.
Onaylı kayıt bazlı eşleştirme ekranı tamamlanmadan bunların kimlikleri doldurulmamalıdır.

## Yerel konum altyapısı

- `SchoolLocationMatcher`: yalnız tekil ve il kapsamındaki kesin ad eşleşmesi.
- `ICoachingLocationDirectory` / `IdentityLocationDirectoryClient`: ortak konum API'sini okur;
  Identity DB'ye doğrudan bağlantı yoktur. İlçe–il ilişkisi kontrol edilir, iptal ve timeout vardır.
- `TargetSchool.ProvinceId` / `DistrictId`: birlikte atanır; kaynak City/District adları korunur.
  Eski kayıtların kimliği otomatik tahmin edilmez.
- `20261002151257_LinkTargetSchoolAdministrativeLocations`: nullable kolonlar, çift-alan
  constraint'i ve filtre indeksi. Identity tablosuna cross-database foreign key yoktur.
  Kaydedilmiş konum eşleşmeleri varsa Down bunları sessizce silmez.
- `CoachingDesignTimeDbContextFactory`: migration scaffolding için sabit erişilemeyen
  tasarım bağlantısı; canlı kimlik bilgisi gerekmez. Yayında hâlâ API --migrate-only kullanılır.

## Doğrulama

- Yeni/ilgili konum-katalog regresyonu: **34 geçti, 0 başarısız, 0 atlanan**.
- Geniş plan/hedef/rapor/katalog/dışa aktarma regresyonu: **179 geçti, 0 başarısız, 0 atlanan**.
  Bu iki seçim örtüşür; toplam olarak toplanmamalıdır.
- Gerçek, disposable PostgreSQL: eski adların korunması, yarım kimlik çiftinin reddi,
  konum saklama, dolu eşleşmelerle Down reddi ve açık fixture temizliği sonrası Down başarısı.
- C# incelemesindeki geri alma veri kaybı bulgusu düzeltildi ve yeniden incelendi.
- Matcher satır/dal kapsamı %100; bütün ürünün test kapsamı olarak yorumlanmamalıdır.
- Admin arayüzü ve E2E bu aşamada tamamlanmadı; test edilmiş sayılmaz.

RED/GREEN checkpointleri aktif `codex/platform-hardening` dalında ayrı commitlerle korunur.

## Katalog listeleme ve düzenleme temeli — yerel ikinci kontrol

- Global kapsam kontrolü, `Coaching.View` ve mevcut Coaching MFA kategorisi korunur.
  Sayısal enum yolları kabul edilmez; türler yalnız isimleriyle seçilir.
- API beş katalog türünü SQL tarafında arar, filtreler ve sayfalar. Sıralama ad + kimliktir.
  Literal `%`, `_` ve ters eğik çizgi araması kaçışlanır. Başka katalog türüne ait
  filtreler sessizce yok sayılmaz, doğrulama hatası döner.
- Ders/ünite/konu ve hedef domain düzenlemeleri tüm doğrulamayı atamalardan önce yapar;
  kaynak/kimlik ve tarihsel ilişkiler değişmez. Henüz yazma API'si olarak sunulmadılar.
- Yeni admin adresi: `/dashboard/coaching/catalog`, menü: Koçluk → Ders ve hedef katalogları.
  Yalnız SystemAdmin ve Coaching.View ile görünür; SSR'de API çağrısı yapılmaz.
  Eski filtre isteği iptal edilir; yükleme/boş sonuç/hata ayrı gösterilir.
  Bu ekran salt okunurdur. Ünite/konu ilişki seçicileri ve okul konum filtreleri
  henüz ekrana bağlanmadı; API filtreleri vardır.
- Geniş backend regresyonu: **195 geçti, 0 başarısız, 0 atlanan**.
- İlave API/konum hata senaryoları: **25 geçti, 0 başarısız, 0 atlanan**;
  önceki seçimle örtüştükleri için sonuçlar toplanmamalı.
- Arayüz bileşeni, menü/rota ve HTTP servis testleri: **10 geçti**.
- Admin production build başarılı. Mevcut Speed Reading katalog stil bütçesi uyarısı
  devam ediyor; bu çalışmanın kapsamı dışında değiştirilmedi.
- Controller ve konum istemcisinin async dalları dahil satır kapsamı %100;
  katalog reader async satır kapsamı %98. Bunlar ürün genelinin kapsamı değildir.
- C# kapsam incelemesinde blocker yok. Yazma endpointleri, tam tarayıcı E2E ve
  kullanım/işlem geçmişi güvenlik kontrolleri henüz tamamlanmadı.

Bu kontroller **planın tümünün tamamlandığı anlamına gelmez**. Canlıya veya GitHub'a
bu yeni admin geliştirmesi aktarılmadı. Bir sonraki çalışma güvenli yazma API'leri,
kullanım kontrolü, gerekçeli denetim kaydı ve admin düzenleme formlarıdır.
