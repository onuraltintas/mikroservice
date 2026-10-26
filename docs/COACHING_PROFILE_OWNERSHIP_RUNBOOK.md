# Koçluk profil sahipliği — ilk aşama

Tarih: 16 Ekim 2026

Canlı durum: 16 Ekim 2026 19:39:55 TSİ'de eğitim bağlamı paketiyle birlikte uygulandı. Dağıtım ve veri koruma kanıtları.

Bu aşama Koçluk öğrencisi ve öğretmeninin profilini, kurum üyeliğini, öğretmen–öğrenci atamasını ve davetini Coaching servisine taşır. Ortak hesap Identity'dedir. Hızlı Okuma'nın profil ve ilişki tabloları değiştirilmez.

## Sahiplik ve kimlik standardı

| Veri | Sahibi | Anahtar |
| --- | --- | --- |
| Hesap, ad-soyad, e-posta, telefon, parola, MFA, oturum, ürün erişimi ve ürün rolleri | Identity | `User.Id` |
| Ortak kurum kaydı, kurum yöneticisinin yetkisi, doğrulanmış veli ilişkisi | Identity | Kullanıcı ve kurum kimlikleri |
| Öğrencinin eğitim ve paylaşım bilgileri | Coaching | Benzersiz `UserId` |
| Koçun unvan, branş, deneyim ve biyografisi | Coaching | Benzersiz `UserId` |
| Koçluk kurum üyeliği | Coaching | `InstitutionId + UserId + Role` |
| Koçluk öğretmen–öğrenci ataması | Coaching | `TeacherUserId + StudentUserId` ve kurum/ders kapsamı |
| Koçluk daveti | Coaching | Gönderen ve öğretmen için kullanıcı kimlikleri |
| Hızlı Okuma profilleri ve ilişkileri | Speed Reading | Mevcut kullanıcı kimliği standardı |

Profil tablolarının kendi teknik `Id` alanı vardır. İşlemler ve atamalar bu teknik profil kimliğini kabul etmez. Hesap `UserId`'si kullanılır. Eski Identity atamalarındaki profil kimlikleri aktarım sırasında kullanıcı kimliklerine çözülür.

Çalışma tablolarındaki mevcut `StudentId`, `TeacherId` ve benzeri hesap kimlikleri yeniden numaralandırılmaz. Çalışma planı, görev, tamamlanma, süre, soru sayısı ve geçmiş kayıtları profil aktarımı sırasında değiştirilmez. Yeni profil tablolarından çalışma geçmişine silme zinciri eklenmez.

## Veritabanı

Yeni tablolar Coaching veritabanının `coaching` şemasındadır:

- `student_profiles`: `UserId`, eğitim durumu, sınıf, okul, şehir, ilçe, okul türü, şube, mezuniyet yılı, isteğe bağlı doğum tarihi, biyografi, görsel, paylaşım tercihi ve aktiflik.
- `teacher_profiles`: `UserId`, unvan, JSON branşlar, deneyim yılı, biyografi, görsel, kurum geneli görünürlük izni ve aktiflik. Öğretmen kendi profilinden kurum geneli yetki veremez.
- `institution_memberships`: kurum, kullanıcı, öğrenci/öğretmen rolü ve aktiflik. Bir kullanıcı/rol için aynı anda bir aktif kurum vardır.
- `teacher_student_assignments`: öğretmen ve öğrenci UserId'leri, isteğe bağlı kurum ve ders, başlangıç/bitiş ve aktiflik. UserId üzerinden yerel profil yabancı anahtarları vardır.
- `invitations`: gönderen UserId, alıcı e-postası, hedef ürün rolü, kurum/öğretmen kapsamı, durum, son tarih ve kabul eden UserId.

Profil ve ilişkilerde `Version` eşzamanlılık belirtecidir. Profil güncellemesi `ExpectedVersion` alır; eski sürümle yazma 409 döndürür. Atama aynı öğretmen/öğrenci/kurum/ders için tekildir; PostgreSQL'de boş kurum/ders değerleri de tekillik kapsamındadır.

Migration'lar:

1. `20261016133831_OwnedCoachingProfiles`
2. `20261016134530_OwnedCoachingInvitations`

Bu migration'ların `Up` işlemleri yeni tablolar ve kısıtlar ekler. Identity hesabı veya mevcut çalışma tablosu silmez.

## Ekranlar ve uçlar

Öğrenci: `/coaching-portal/profile` → **Koçluk profilim**. Öğretmen: personel portalı, Koçluk öğretmen çalışma alanı → **Koçluk profilim ve davetlerim**.

Ortak hesap düzenleyicisindeki eğitim/kurum alanları kaldırıldı. Ad, soyad, telefon, parola ve ürün erişimi ortak hesap ekranında kalır. Eğitim ve uzmanlık alanları Koçluk ekranına yazılır. Kaydetme hatası veya sürüm çatışması formu temizlemez; kaydedilmemiş değişiklikler için sayfadan çıkış koruması vardır.

| İşlem | Uç |
| --- | --- |
| Kendi Koçluk profili | `GET /api/coaching/profiles/me` |
| Eksik ürün profilini açıkça oluşturma | `POST /api/coaching/profiles/me/provision` |
| Öğrenci/öğretmen profilini güncelleme | `PUT /api/coaching/profiles/me/student` veya `/teacher` |
| Öğretmenin kendi öğrenci listesi | `GET /api/coaching/teachers/me/students` |
| Öğretmenin öğrenci daveti | `POST /api/coaching/teachers/invite-student` |
| Öğretmenin bağlantı sonlandırması | `DELETE /api/coaching/teachers/me/students/{studentUserId}` |
| Öğrencinin koç bağlantısını sonlandırması | `DELETE /api/coaching/students/me/teachers/{teacherUserId}` |
| Kurum işlemleri | `/api/coaching/institution/...` |
| Sistem yöneticisinin seçili kurum işlemleri | `/api/coaching/institutions/{institutionId}/...` |
| Gelen/giden davetler | `/api/coaching/invitations/my-invitations` ve `/sent-pending` |
| Daveti kabul, reddet veya iptal | `/api/coaching/invitations/{id}/accept`, `/reject`, `/cancel` |

Okuma isteği profil oluşturmaz. Şifreyle kayıt, diğer üründen Koçluğa katılım, yönetici hesabı oluşturma ve Google ile yeni kayıt Koçluk profilini ürün bilgisi taşıyan `CoachingProfileProvisioningRequested` olayıyla ister. Consumer ürün, şema sürümü, rol ve güncel ortak hesap erişimini doğrular; tekrar gelen olay mevcut profili yeniden oluşturmaz veya pasif profili açmaz. Eksik profil, profil ekranındaki açık oluşturma işlemiyle de tamamlanabilir.

Eğitim durumunda öğrenci seçilmişse sınıf 1–12 gerekir. Mezunda sınıf boştur; mezuniyet yılı isteğe bağlıdır. Okul, şehir, doğum tarihi ve biyografi planlama için zorunlu yapılmamıştır. Bu aşamada profil eksikliğini bütün uygulamada zorunlu bir ilk giriş sihirbazına bağlayan yeni akış eklenmemiştir; hedef ve plan sihirbazları sonraki aşamadır.

## Yetkilendirme ve veri koruma

- Ürün oturumu doğrulaması korunur. Hızlı Okuma oturumu Koçluk profil uçlarına erişemez.
- Coaching, Identity'den yalnızca ortak hesap/ürün rolü/ürün erişimi ile ortak kurum-yönetici-veli doğrulamalarını alır. Eğitim, sınıf ve öğretmen–öğrenci ataması için Identity'ye danışmaz.
- Öğretmen erişimi aktif yerel profil, doğru kurum ve aktif atamaya bağlıdır. Kurum üyeliğini sonlandırmak ilgili atamaları bitirir; çalışma geçmişini silmez.
- Kurum raporlarında öğrencinin paylaşım tercihi korunur. Genel sistem yöneticisi istisnası mevcut davranıştır.
- Davet kabulü doğru e-posta ve ürün rolünü, gönderenin güncel yetkisini, kurumun aktifliğini ve öğretmenin kapsamını tekrar doğrular. Davet, ortak hesap rolünü veya diğer ürün erişimini kendiliğinden vermez.
- Profil güncelleme gövdesindeki başka bir UserId üzerinden hesap değiştirme desteklenmez; kullanıcı oturumdan alınır.
- Yeni profil ve ilişkiler öğrenci kişisel veri dışa aktarımına eklendi; dışa aktarım sürümü `1.4` oldu. Yerel profiller ve kullanıcı kimliğiyle bağlı ilişkiler mevcut yetkili veri silme sürecinin kapsamına alındı. Normal kurum/koç değişikliği bu silme sürecini başlatmaz.

## Eski kayıtların durumu

Eski profil kimlikleri yeni sistemde kullanılmaz. Eski Identity profil/atama tabloları ilk geçişte aktarım kaynağı ve geri dönüş arşivi olarak bırakılmıştır; fiziksel tablo silme migration'ı yoktur. Eski öğretmen, öğrenci bağlantısı, kurum üyeliği ve davet web uçları `410 Profiles.OwnedByCoaching` döndürür. Identity profil güncellemesinde eğitim alanına yazma açıkça reddedilir. Aktif panelde çift yazım yoktur.

Eski Identity davetleri bu geçişte taşınmaz; test davetleri yeni Koçluk panelinden yeniden gönderilir. Eski tabloların ve artık kullanılmayan handler/repository kodlarının fiziksel temizliği, tüm ortamlar yeni sürüme geçtikten sonraki ayrı bir temizlik işidir. Arşiv tabloları yeni Koçluk verilerinin güncel kopyası değildir.

## Ortama geçiş sırası

Yerel geliştirme ve izole test doğrulamasına ek olarak canlı dağıtım ve veri aktarımı tamamlandı. Üretim kopyası provası, canlı veri karşılaştırması ve öğrenci tarayıcı sonuçları yayın raporundadır.

1. Koçluk profil, üyelik, atama, davet ve kayıt işlemlerini geçiş boyunca bakım kapsamına alın. İki servisin farklı sahiplik sürümlerini kullanıcı trafiğine birlikte açmayın.
2. Identity ve Coaching veritabanlarını yedekleyin. Ortak hesap sayısı/kimlikleri ile çalışma planı ve tamamlanmış görev sayıları/sürelerini karşılaştırma için kaydedin.
3. Yeni Coaching sürümünde migration'ları tek seferlik modla çalıştırın:

   ```text
   dotnet Coaching.API.dll --migrate-only
   ```

4. Yeni Identity hesap dizini ve servis anahtarı korumalı aktarım okuyucusunu, ardından yeni Coaching consumer/API sürümünü bakım altında başlatın. `Services:IdentityService` ve mevcut dahili servis anahtarı doğru olmalıdır.
5. Aktarımı önce önizleyin:

   ```text
   dotnet Coaching.API.dll --import-identity-profiles
   ```

   Bu komut `legacy-profile-snapshot` dahili ucundan okur. Ortak hesapta aktif Koçluk rolü/erişimi bulunan eski profiller aktarım kaynağına alınır. Geçersiz eğitim/görsel alanı, aktif olmayan kurum veya tutarsız atama varsa yazmadan hata verir; sessizce ilişki atlamaz.

6. Sayımları doğruladıktan sonra aynı komutu `--apply` ile çalıştırın. Tek `SaveChanges` işlemi profil ve ilişkileri birlikte yazar. Var olan yeni Koçluk kaydı üzerine yazılmaz. Tekrar çalıştırmak kopya profil/atama üretmez. Identity'de hesap, parola, MFA veya ürün erişimi değiştirilmez.
7. Öğrenci ve personel arayüzlerini birlikte yayınlayın. `Coaching:PublicBaseUrl` davet bağlantılarının ortak site adresidir; öğretmen daveti personel portalına, öğrenci daveti Koçluk profil sayfasına gider.
8. Hesap ve çalışma sayımlarını tekrar karşılaştırın. Öğrenci profil kaydı, öğretmen listesi, kurumdan çıkarma, davet kabulü ve paylaşım kapalı öğrenci raporu örneklerini doğrulayın. Ardından bakım durumunu kaldırın.

Yeni sisteme veri yazıldıktan sonra migration `Down` ile geri dönmek yeni profilleri/davetleri kaybettirir. Geri dönüşte trafiği durdurup iki servis ve arayüz sürümünü birlikte değerlendirin; çalışma tablolarını sıfırlamayın. Eski arşivi güncel veri gibi kullanmayın.

## Doğrulama

Son test çalıştırması `artifacts/coaching-profile-ownership-tests/final-backend.trx` içindedir. Docker gerektiren eski veritabanı testleri yerine yeni koruma testi, ayrı yerel PostgreSQL örneğinde gerçek Identity ve Coaching şemalarıyla çalıştırıldı.

Sonuçlar:

- Seçilen 739 backend regresyon testi geçti; bunların içinde gerçek HTTP güvenlik ve PostgreSQL aktarım/koruma testleri var.
- Ana panelde 54, personel panelinde 56 arayüz testi geçti.
- İki panelin üretim derlemesi başarılı.
- Yeni profil, ilişki, aktarım ve ortak hesap dizini kodu için ölçülen çekirdek backend satır kapsaması `%91,51`; dal kapsaması `%67,60`. Bu ölçüm tüm ürünün kapsaması değildir.
- Yeni profil arayüzünün satır kapsaması `%97,89`, ifade kapsaması `%84,12`, dal kapsaması `%83,14`.
- Mevcut PKCS paket güvenlik uyarıları (`NU1902/NU1903`) ve arayüz paket/stil boyutu uyarıları görülmeye devam ediyor; bu aşamadaki değişikliklerin kapsamına alınmadı.

Doğrulanan kritik durumlar:

- Eski şemadan yeni şemaya migration; yeni UserId yabancı anahtarları ve boş kapsamlı atama tekilliği.
- Gerçek Identity hesabının kimliği, parola hash'i, e-posta doğrulaması, adı ve Hızlı Okuma ürün erişiminin korunması.
- Eski profil kimliklerinden UserId ilişkilerine aktarım; önizleme, tekrar çalıştırma ve atama başlangıç tarihinin korunması.
- Tamamlanmış görevin kimliği, öğrencisi, tamamlanması, gerçek çalışma süresi ve soru sayısının korunması.
- Ürün/kurum sınırı, erişim iptali, paylaşım tercihi, aktiflik, davet alıcısı ve eski profil uçlarının kapanması.
- Profil arayüzü, kurum/davet servisleri, öğrenci menüsü, ortak hesap ekranı ve iki panelin üretim derlemesi.

Test PostgreSQL bağlantısı yalnızca `COACHING_PROFILE_TEST_POSTGRES` ile açıkça seçilir. Koruma testi localhost ve rastgele `test_coaching_ownership_...` veritabanıyla sınırlıdır; sonunda yalnızca kendi oluşturduğu test veritabanını kaldırır.

```powershell
$env:COACHING_PROFILE_TEST_POSTGRES = '<izole yerel PostgreSQL bağlantısı>'
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --filter 'FullyQualifiedName~CoachingOwnedProfile|FullyQualifiedName~CoachingProfileCutoverTests|FullyQualifiedName~CoachingOwnershipHttpTests|FullyQualifiedName~CoachingOwnedInvitationTests'

# clients/admin-panel dizininde:
node node_modules/@angular/cli/bin/ng.js test admin-panel --watch=false --ts-config=tsconfig.coaching-profile-tests.json
node node_modules/@angular/cli/bin/ng.js build admin-panel --configuration=production
node node_modules/@angular/cli/bin/ng.js build staff-portal --configuration=production
```

Bu testler yük testi veya canlı dağıtım doğrulaması değildir. Hedef oluşturma, otomatik planlama algoritması ve müfredat/kazanım veri kataloğunun genişletilmesi bu aşamanın kapsamı dışındadır.
