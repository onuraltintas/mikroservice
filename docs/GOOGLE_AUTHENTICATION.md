# Google ile Kimlik Doğrulama

Platformdaki Google girişi, Angular istemcisinin Google Identity Services ile aldığı
ID token'ı `POST /api/auth/google-login` üzerinden Identity Service'e göndermesiyle
çalışır. Backend token'ı Google imzası, yapılandırılmış `GOOGLE_CLIENT_ID` audience'ı,
Google issuer'ı, `sub` değeri ve `email_verified` claim'i ile doğrular.

## Yapılandırma

`.env` dosyasında web client ID tanımlı olmalıdır:

```dotenv
GOOGLE_CLIENT_ID=your-web-client-id.apps.googleusercontent.com
```

Bu değer gizli credential değildir; yine de Google Cloud Console'da yalnızca
uygulamanın gerçek origin'leri ve redirect/origin kısıtları tanımlanmalıdır. Identity
Service `GOOGLE_CLIENT_ID` yoksa token doğrulamasını kabul etmez.

Admin paneli Angular derlemesinde development ve production ortamları ayrı
environment dosyaları kullanır. Mevcut production dosyası çalışır durumdaki
public client ID'yi içerir; yayına almadan önce Google Cloud Console'da yalnızca
production origin'lerine izin veren ayrı bir web client oluşturulup
`clients/admin-panel/src/environments/environment.production.ts` ve backend
`.env` içindeki ID birlikte değiştirilmelidir. Bu değer gizli değildir; ancak
development client'ı production origin'lerinde kullanmayın.

## Hesap eşleme politikası

Google kullanıcısı `email` ile değil, değişmez Google `sub` değeriyle
`UserLogins(LoginProvider = "Google", ProviderKey = sub)` kaydına bağlanır. İlk başarılı
ve doğrulanmış Google girişinde aynı e-posta ile var olan düşük ayrıcalıklı aktif yerel
hesap tek seferlik olarak bağlanır; sonraki girişlerde e-posta değişikliklerinden
etkilenmeden `sub` kullanılır. SystemAdmin, kurum yöneticisi ve öğretmen hesaplarında
otomatik bağlama kapalıdır; kullanıcı mevcut oturumuyla `POST /api/auth/google-link`
endpoint'ini çağırarak hesabı açıkça bağlamalıdır. Google hesabı bulunmayan yeni
kullanıcı, `auth.allowregistration` açıkken öğrenci hesabı olarak oluşturulur.

ID token, access token veya Google refresh token'ı uygulama veritabanında saklanmaz.
SystemAdmin kullanıcıları Google ilk faktöründen sonra da MFA challenge'ına tabiidir.
Auth gateway `/api/auth/*` trafiğini IP başına rate limit eder.

## Takvim entegrasyonundan ayrım

Google ile platforma giriş ve Google Calendar senkronizasyonu aynı izin değildir.
Takvim entegrasyonu ayrıca authorization-code + state/PKCE akışı, açıkça seçilmiş
Calendar scope'ları ve şifrelenmiş refresh token saklama gerektirir. Kullanıcı girişinde
takvim scope'u istenmemelidir; bu entegrasyon ayrı bir bağlantı ekranı ve ayrı bir
entegrasyon sınırı olarak uygulanacaktır.

Google'ın resmi doğrulama kuralları için:

- [OpenID Connect ID token doğrulama](https://developers.google.com/identity/openid-connect/openid-connect)
- [OpenID Connect API referansı](https://developers.google.com/identity/openid-connect/reference)
- [Web server OAuth akışı](https://developers.google.com/identity/protocols/oauth2/web-server)
- [Calendar API scope'ları](https://developers.google.com/workspace/calendar/api/auth)
