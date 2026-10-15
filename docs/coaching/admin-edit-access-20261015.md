# Mevcut kullanıcıda servis erişimi yönetimi

15 Ekim 2026 Türkiye saati 16:03'te canlıya alındı (`2026-10-15T13:03:49Z`). Kaynak: `437f3208`; son kilit/test düzeltmesi ve imaj etiketi: `c82a73b330c63d7ce36d21fc7f59619e0747624e`. Yayın paketi: `admin-edit-access-20261015`.

## Kullanım

[Admin kullanıcıları](https://eduivme.com/dashboard/identity/users) → kullanıcı menüsü → **Düzenle** → **Servis erişimleri**.

Koçluk ve Hızlı Okuma ayrı kartlarda durumları ve servis rolleriyle gösterilir. **Erişim aç / Erişimi kapat** işlemleri hemen kaydedilir; profil alanlarının kaydından ayrıdır. Değişiklikten sonra ekran kapatıldığında üst liste yenilenir. İstek sürerken yinelenen işlem, profil kaydı ve kapatma engellenir; hata halinde önceki durum korunur ve tekrar denenebilir.

Erişim değiştirmek rol atamaz/silmez, profil veya kurum üyeliğini değiştirmez, abonelik oluşturmaz/silmez/uzatmaz. İki servis de kapatılabilir. Rolü bulunmayan serviste rolün ayrıca **Roller** bölümünden atanması gerektiği belirtilir. SystemAdmin genel yetkili olduğu için bu ekranla kısıtlanamaz; arayüz açıklar, sunucu isteği reddeder.

Kapatma yeni girişi ve oturum yenilemeyi durdurur; yalnız seçili servisin aktif refresh token'ları iptal edilir. Diğer servisin oturumları korunur. Yeniden açmak aynı erişim satırını etkinleştirir ve eski iptal edilmiş oturumu diriltmez. Mevcut JWT, süresi dolana kadar kullanılabilir; bu sınır arayüzde açıklanır.

## Sunucu

`PUT /api/users/{id}/products/{coaching|speed-reading}/access`, gövde `{ "isActive": true|false }`. Açık bool zorunludur; eksik/null değer kapatma olarak yorumlanmaz. İşlem SystemAdmin, Users.Edit izni ve mevcut Users MFA politikasının doğrulamasını gerektirir. Diğer ürünler desteklenmez; bulunamayan hesap oluşturulmaz.

`IUserAccessManagementService.SetProductAccessAsync` mevcut domain grant/revoke işlemlerini kullanır. Yeni erişim EF'e açıkça Added olarak kaydedilir. Erişimin ilk kez yokken kapatılması da açık pasif kayıt bırakır. Tek transaction içinde kullanıcı satırı parametrik `FOR NO KEY UPDATE` ile kilitlenir; güncel erişim okunur, hedef erişim ve ilgili oturumlar değiştirilir ve kaydedilir. EF execution strategy transient hatalarda işlemi tekrarlar; tekrar öncesi tracker temizlenir. Bu kilit aynı kullanıcı güncellemelerini sıralarken refresh insert'in FK kilidine izin verir.

Mevcut admin rol atama yolu, açıkça pasif erişimi yeniden açmaz. İlk kez servis rolü atamasının önceki erişim oluşturma davranışı korunur; kayıt/Google katılım gibi diğer erişim kaynakları değiştirilmedi. Bu özellik kalıcı kayıt/katılım yasağı değildir; mevcut servis katılım kuralları ayrıca yürür.

## Test ve yayın kapsamı

- Backend erişim, scoped rol, kullanıcı oluşturma ve iki servisin abonelik kontrolleri: 105 test geçti; atlanan yok.
- Admin düzenleme, yeni kullanıcı ve erişim istemcisi: 47 test geçti.
- Yerel üretim arayüzü: masaüstü/mobil 20 düzenleme senaryosu geçti.
- Aday API + gerçek PostgreSQL kopyası + ayrı broker/cache: 35 senaryo geçti.
- Canlıdan sunulan düzenleme arayüzü: masaüstü/mobil 20 senaryo geçti.
- Canlıdan sunulan önceki yeni kullanıcı akışı: 26; servis bazlı rol yönetimi: 20 kontrol geçti.
- Kod incelemelerinde eşzamanlı ilk erişim oluşturma yarışı saptandı ve transaction/kilitleme ile giderildi; refresh FK kilidiyle gereksiz çekişmeyi önlemek için daha dar kilit kullanıldı.

Gerçek API provasında dört rol × iki servis için erişim kapatma, yeni girişin/refresh'in reddi, diğer servisin refresh'inin çalışması, rol atamasının kapalı erişimi açmaması, aynı erişim satırının tekrar açılması ve eski token'ın dirilmemesi sınandı. Profil/rol/kurum üyeliği satırları ve test kurumunun lisans/abonelik tarihleri JSON snapshot ile karşılaştırıldı. Sekiz eşzamanlı ilk grant ve sekiz karşıt toggle başarılı oldu; son durum ve tek kayıt doğrulandı. Eksik bool, eksik hesap, genel SystemAdmin, anonim/MFA'sız değişiklikler reddedildi. Önceki 25 kullanıcı oluşturma/davet/ürün kapsamı regresyonu da geçti.

Tarayıcı provalarında bütün API/oturum çağrıları fixture ile karşılandı; gerçek kullanıcı ve abonelikleri değiştirilmedi. Bu sonuçlar gerçek CAPTCHA/human oturum testi değildir. Gerçek HTTP/veritabanı provası canlı yedeğinin adı doğrulanan geçici kopyası, ayrı RabbitMQ/Redis ve port yayımlamayan API ile çalıştı. Prova bitince geçici veritabanı ve konteynerler kaldırıldı. Canlı CAPTCHA ve MFA ayarları korundu; CAPTCHA yalnız yalıtılmış prova API'sinde kapalıydı. Koçluk ve Hızlı Okuma servislerinin ayrı abonelik veritabanları bu görevde değiştirilmedi.

Migration yok. Backend `838fc1b0` tabanından önceki kullanıcı oluşturma/olay sözleşmesi dosyaları ve bu görevin dört backend dosyasıyla; admin `98438809` tabanından önceki üç dosya ve düzenleme/rol açıklaması dosyalarıyla yalıtılmış derlendi. `838fc1b0` kaynaklarında bulunan ancak gerçek canlı API assembly'sinde bulunmayan `CourseManagementAuthController` yalnız geçici derleme kopyasından çıkarıldı. Canlı ve aday controller/type envanteri karşılaştırıldı; başka controller kaldırılmadı. Çalışma alanındaki ders yönetimi geliştirmeleri yayına dahil edilmedi.

Mevcut Identity imajına yalnız API/Application/Infrastructure DLL/PDB dosyaları katmanlanır; Domain, Shared.Contracts ve diğer bağımlılıklar korunur. Sıra Identity → admin; geri dönüş admin → Identity. Ortam değerleri, diğer servis imajları ve aday imaj kimlikleri sabitlenir. Prova, payload hash'i ve ağsız admin önizlemesi başarılı olmadan canlı geçiş yapılmaz.

Yayınlanan imajlar:

- Identity: `sha256:14d22302e4bb579dcea73b3626457509a3b148105335b77ac79da1b3b126ce7b`
- Admin: `sha256:630a6a7493e0daac8950b2a50f5ff95ad7177ff97d8d0952e3d0e128d99b1368`

Yayın sonrası hedefler running/restart 0; diğer servis konteyner kimlikleri aynı. İki servisin genel giriş sayfaları HTTP başarılı. Kanıtlar: `artifacts/admin-edit-access-tests/admin-edit-access-confirmed.trx`, `artifacts/admin-edit-access-relational-results.json`, `artifacts/admin-edit-access-local/`, `artifacts/admin-edit-access-live/`, `artifacts/admin-user-dual-service-live/`, `artifacts/admin-role-services-live/`. Sunucu kanıtları root erişimli `/var/lib/eduivme/releases/admin-edit-access-20261015` dizininde.
