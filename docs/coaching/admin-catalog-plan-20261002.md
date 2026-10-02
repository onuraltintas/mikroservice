# Koçluk admin katalog ve öğrenci inceleme planı

Bu çalışma yereldir. Kullanıcı canlı yayın öncesinde sonuçları görmek istedi;
bu planın migration'ı canlıya uygulanmadı ve GitHub'a gönderilmedi.

## Sıra ve tamamlanma kapıları

1. Veri/yetki envanteri: ilk kontrol tamamlandı.
2. Ortak şehir–ilçe entegrasyonu: eşleştirici, API istemcisi, nullable kimlik alanları
   ve yerel migration doğrulandı. Admin formu ve kayıt bazında onaylı eşleştirme işlemleri bekliyor.
3. Ders/ünite/konu admin ekranı ve API: bekliyor.
4. Okul admin ekranı ve API: bekliyor.
5. Üniversite programı admin ekranı ve API: bekliyor.
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
