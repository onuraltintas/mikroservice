# Admin yeni kullanıcı: iki servisi birlikte seçme

15 Ekim 2026, Türkiye saati 14:44'te canlıya alındı (`2026-10-15T11:44:07Z`). Uygulama kaynak commit'i `4554e579`; outbox test kanıtını güçlendiren ek commit `6e62e6c4`.

## Kullanım ve kapsam

[Admin kullanıcıları](https://eduivme.com/dashboard/identity/users) → **Kullanıcı Ekle** → bilgileri ve rolü doldur → **Servisler** altında Koçluk, Hızlı Okuma veya ikisini birlikte işaretle → **Kullanıcı Oluştur**.

Öğrenci, öğretmen, kurum yöneticisi ve kurum sahibi test edildi. En az bir servis seçimi zorunlu; ilk açılışta seçim yok. Başlangıçta seçilen rol her seçili servise ayrı atanır. Örneğin çift seçimli öğretmen tek kullanıcı hesabında `Teacher · Koçluk` ve `Teacher · Hızlı Okuma` kayıtlarına sahip olur. Sonradan mevcut servis bazlı rol yönetiminden bağımsız değiştirilebilir/silinebilir.

Tek hesap, ortak parola, tek parola kurulum daveti oluşturulur. İki servis birlikte seçilince davetin ana servisi Koçluk olur; ortak parola iki serviste de kullanılır. Yalnız Hızlı Okuma seçilirse önceki Hızlı Okuma daveti korunur.

Abonelik oluşturulmaz, uzatılmaz veya birleştirilmez. İki servisin mevcut abonelik zorunluluğu/erişim kuralları ayrı çalışmayı sürdürür. Kurum hesabına bu formdan servis rolü ve erişim açılır; kurum kaydı/üyeliği ve abonelikleri ayrıca yönetilir. Mevcut hesap/kurum bağlantıları değiştirilmez. Aynı e-posta zaten varsa yeni hesap isteği reddedilir; hesap üzerine yazılmaz.

SystemAdmin genel kapsamlı kalır; servis seçimi gösterilmez ve ek ürün erişimi oluşturulmaz. Hata halinde seçimler korunur; istek sürerken yinelenen kayıt engellenir. Başarı ekranı her iki servis kapsamını gösterir.

## Teknik davranış

- `CreateUserCommand.Products` opsiyonel init dizisidir; mevcut beş parametreli constructor ve `Product` alanı korunmuştur. Açık liste varsa önceliklidir. Boş liste ve desteklenmeyen ürünler kayıttan önce reddedilir. Tekrarlanan ürünler ayıklanır, sıralama sabitlenir.
- Eski çağrılarda `Products` yoksa önceki `Product` davranışı sürer. Eski CSV içe aktarma da gerçek API'de test edildi.
- Yeni arayüz tekli seçimde hem `product: 1/2` hem listeyi gönderir. Çift seçimde `product: null, products: [1,2]` gönderir. Eski sunucuya dönülürse eski backend çift seçimi açık null nedeniyle reddeder; sessizce yalnız Koçluk hesabı oluşturmaz. Yeni backend eski arayüzü destekler.
- İlk rol/hesap kaydından sonra ikinci servisin rolü aynı RoleId ile ayrı UserRole satırına eklenir; her ürünün erişimi ayrıdır. Koçluk öğrenci/öğretmen profili yalnız Koçluk seçiliyse bir kez açılır. Olay sözleşmesi/tüketiciler değiştirilmedi; tek `UserCreatedEvent` yayımlanır.
- Mevcut iki kayıt ve hata halinde telafi silmesi düzeni korunur. Hata temizliği öncesi EF tracker sıfırlanır; temizlik iptal edilmiş istekten bağımsız, 10 saniye sınırlı token kullanır. Temizlik hataları loglanır. Akış tek transaction olarak sunulmaz.

## Test kanıtları

| Kontrol | Sonuç |
|---|---|
| Backend kullanıcı oluşturma, servis rolü ve abonelik regresyonları | 79 geçti, atlanan yok |
| Admin bileşen/servis ve rol yönetimi birim testleri | 39 geçti |
| Gerçek aday API + PostgreSQL kopyası + ayrı RabbitMQ/Redis | 25 senaryo geçti |
| Yerel üretim derlemesi, masaüstü/mobil yeni kullanıcı | 26 geçti |
| Canlıdan sunulan arayüz, masaüstü/mobil yeni kullanıcı | 26 geçti |
| Canlıdan sunulan arayüzde önceki servis bazlı rol yönetimi | 20 kontrol geçti |

API provası dört rol × üç servis seçimini sınadı: tek UserId, doğru sayıda kalıcı rol/erişim kaydı, ayrı rol satırı kimlikleri, profil kapsamı, gerçek broker olayı, tek kullanımlık parola kurulumu ve ürün kapsamlı JWT ile giriş. İki servisli hesaplarda Koçluk rolü API'den silindi; Hızlı Okuma rolü/erişimi ve girişi korundu. Geçersiz listeler, liste önceliği/tekrar ayıklama, anonim/MFA koruması ve eski CSV yolu ayrıca doğrulandı.

Yalnız kopyadaki ikinci rol insert'ine geçici hata trigger'ı eklendi. Başarısız istek sonrasında hesabın silindiği, e-postasına ait bekleyen `OutboxMessage.Body` olmadığı ve test sonuna kadar broker'da davet olayı görünmediği doğrulandı. Trigger yalnız geçici veritabanındaydı.

Prova canlı yedeğinden alınmış adı doğrulanan geçici veritabanını ve ayrı broker/cache'i kullandı; API dışarı port yayımlamadı. Prova tamamlanınca geçici konteynerler ve veritabanı kaldırıldı. Gerçek kullanıcılara davet gönderilmedi. CAPTCHA yalnız yalıtılmış prova API'sinde kapalıydı; canlı CAPTCHA/MFA ayarları korundu. Tarayıcı testleri bütün API/oturum çağrılarını fixture ile karşıladı: bunlar canlı gerçek hesap girişi veya gerçek kullanıcı oluşturma testi değildir.

Kanıtlar: `artifacts/admin-dual-service-tests/admin-dual-service-regression.trx`, `artifacts/admin-user-dual-service-relational-results.json`, `artifacts/admin-user-dual-service-local/`, `artifacts/admin-user-dual-service-live/`, `artifacts/admin-role-services-live/`.

## Yayın

Migration yok. Backend gerçek canlı temelinden (`838fc1b0`), yalnız CreateUser üç dosyası ve önceki sürümün mevcut olay sözleşmesiyle derlendi. Mevcut canlı Identity imajına yalnız `Identity.Application.dll/pdb` katmanlandı; Shared.Contracts/API/Infrastructure/dependency DLL'leri değiştirilmedi. Admin gerçek canlı temelinden (`98438809`) yalnız IdentityService ve CreateUser bileşen değişiklikleriyle derlendi. Çalışma alanındaki diğer geliştirmeler yayına dahil edilmedi.

Sıra **Identity → admin**. Mevcut imajlar/ortam değerleri sabitlendi; aday hash'leri, ağsız admin önizlemesi ve gerçek prova sonucu yayının önkoşuluydu. Geri dönüşte **admin → Identity** önceki imajlarına döner. Diğer servisler ve mevcut uyumlu olay tüketicileri değiştirilmedi.

- Identity: `sha256:fe8149016f0c7a4c4c0769f6c430d7ebfaf8ff5921f7598cafe83926c7bbf137`
- Admin: `sha256:8c792d77c16456f4b39b23a1c4bf7c6d5c4cbee82e8fbd14851d24c99b51c159`

Yayın sonunda iki hedef running/restart 0; diğer servis konteyner kimlikleri aynı. Betikler `tools/releases/admin-user-dual-service-20261015/`, root erişimli sunucu kanıtları `/var/lib/eduivme/releases/admin-user-dual-service-20261015`.
