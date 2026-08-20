# Admin Panel Yönetim Matrisi

Bu belge, Angular admin panelinin hangi platform yeteneklerini dinamik olarak
yönettiğini ve hangi alanların özellikle panel dışında bırakıldığını tanımlar.
Sunucu tarafındaki permission policy her zaman son otoritedir; paneldeki menü ve
route guard'ları yalnızca kullanıcı deneyimi ve erken yönlendirme sağlar.

## Yönetim kapsamı

| Bounded context | Panel yeteneği | API yüzeyi | Yetki |
| --- | --- | --- | --- |
| Identity | Kullanıcı listeleme ve ayrıntı (aktif tenant scope'u) | `/api/users` | `Permissions.Users.View` |
| Identity | Kullanıcı oluşturma, düzenleme, pasifleştirme/aktifleştirme, rol, e-posta ve parola yönetimi | `/api/users` | `SystemAdmin` + ilgili `Permissions.Users.*` |
| Identity | Kullanıcının aktif refresh oturumlarını metadata olarak listeleme, tek/toplu oturum sonlandırma ve MFA sıfırlama | `/api/users/{id}/sessions`, `/api/users/{id}/mfa/reset` | `SystemAdmin` + `MfaRequired` + `Permissions.Users.Edit` |
| Identity | Rol ve permission CRUD'u, role-permission eşlemesi | `/api/roles`, `/api/permissions` | `Permissions.Roles.*`, `Permissions.Permissions.*` |
| Identity | Kurum listeleme/detay ve yönetici listesi (SystemAdmin global, diğer yöneticiler yalnız aktif kendi tenant'ı) | `/api/institutions` | `Permissions.Institutions.View` |
| Identity | Kurum oluşturma, aktiflik ve lisans/kapasite yönetimi | `/api/institutions` | `SystemAdmin` + `Permissions.Institutions.Manage` |
| Identity | Kendi tenant'ının iletişim bilgileri ve kurum yöneticileri | `/api/institutions` | Aktif tenant yöneticisi + `Permissions.Institutions.Manage` |
| Identity | Secret içermeyen feature flag ve sistem ayarları, log ve retention yönetimi | `/api/configurations`, `/api/system-logs` | `Permissions.Operations.View` ve mevcut SystemAdmin mutasyon politikaları |
| Identity / Notification / Coaching | Servis-lokal, append-only yönetici denetim kayıtlarını filtreleme ve sayfalama | `/api/admin-audit/{service}` | Yalnız `SystemAdmin` + `Permissions.Operations.View` |
| Identity | SystemAdmin TOTP kurulumu, doğrulama ve tek kullanımlık kurtarma kodları | `/api/auth/mfa/*` | Parola/Google ilk faktöründen üretilen 5 dakikalık challenge |
| Notification | Destek talebi listeleme, filtreleme, notlandırma, işlenmiş işareti ve yanıt | `/api/support/requests`, `/api/support/reply` | `Permissions.Support.View/Reply` |
| Notification | E-posta şablonu listeleme, oluşturma, konu/gövde/aktiflik güncelleme | `/api/email-templates` | `Permissions.Notifications.Templates` |
| Notification | Kullanıcının kendi bildirimleri | `/api/notifications` | Oturum sahibi |
| Coaching | Kullanıcıların assignment/exam/session/goal akışları ve tenant/rol kontrolleri | `/api/assignments`, `/api/exams`, `/api/sessions`, `/api/goals` | Teacher/Student/SystemAdmin domain policy'leri |
| Coaching | Global, bounded ve PII'siz operasyon özeti | `/api/coaching-admin/overview` | Yalnız `SystemAdmin` + `Permissions.Coaching.View` |

## Coaching neden doğrudan CRUD değil?

Koçluk kayıtları öğrenci notu, geri bildirim, sınav sonucu ve katılım bilgisi
içerir. Bunları genel bir admin CRUD ekranına açmak tenant ve eğitim verisi
mahremiyeti açısından güvenli bir varsayılan değildir. Bu nedenle:

- Öğretmen/öğrenci yazma ve okuma işlemleri mevcut domain policy'leriyle sınırlıdır.
- SystemAdmin paneli yalnız sayısal özet ve sınırlı son ödev listesini görür; öğrenci
  PII'si ve not detayları dönmez.
- İleride idari override gerekirse ayrı `Manage` command'ları, tenant scope,
  audit actor/reason ve iki aşamalı onay ile eklenmelidir; mevcut genel endpoint'lere
  bypass eklenmemelidir.

## Bilerek panel dışında kalan sınırlar

Bunlar eksik CRUD olarak değerlendirilmez; farklı bir güvenlik ve işletim sınırıdır:

- PostgreSQL/Redis/RabbitMQ veritabanı tabloları ve migration çalıştırma.
- JWT, SMTP, database, Redis ve service API key secret'ları.
- Docker/Kubernetes replica, network, ingress/TLS ve deploy/rollback işlemleri.
- Prometheus/Grafana/Tempo/Alertmanager kural ve credential'ları.
- Henüz repository'de bounded context'i bulunmayan Blog, Content, Analytics,
  Billing veya Search modülleri.

Bu alanlar CI/CD, secret manager, migration job ve observability runbook'larıyla
yönetilir. Admin paneline secret veya altyapı yazma yetkisi vermek, platform
admininin JWT'si ele geçirildiğinde blast radius'u gereksiz şekilde büyütür.
`ConfigurationDataType.Secret` ile oluşturulmuş eski kayıtlar migration sürecinde
tanınır ancak yönetim API'sinde listelenmez, okunmaz veya değiştirilemez. Yeni
secret kaydı oluşturma da fail-closed olarak reddedilir.

## Yetkilendirme ve işletim kuralları

1. Permission anahtarları `shared/EduPlatform.Shared.Contracts/PlatformPermissions.cs`
   içinde tek sözleşmedir; Identity seed'i bunları permission tablosuna ekler.
2. Frontend `permissionGuard` ve sidebar filtreleri token'daki permission claim'ini
   kullanır; backend `[HasPermission]`/`[Authorize]` kontrolü olmadan hiçbir işlem
   güvenli kabul edilmez.
3. Access token yalnız tarayıcı belleğinde, refresh token ise `HttpOnly`,
   `SameSite=Strict` cookie'de tutulur. Uygulama açılışında ve access token süresi
   dolarken refresh rotasyonu yapılır. Parola, rol ve hesap aktifliği değişiklikleri
   tüm refresh oturumlarını iptal eder; mevcut kısa ömürlü access token en geç kendi
   süresi sonunda geçersiz olur. Admin panelindeki oturum sonlandırma ve MFA
   sıfırlama işlemleri refresh oturumlarını anında kapatır; refresh token'ın
   kendisi hiçbir API yanıtında dönmez.
4. Support submit anonim kalır; body limiti, validator, idempotency key ve gateway
   + service rate limit'i birlikte uygulanır.
5. Coaching admin endpoint'i SystemAdmin rolüyle ek fail-closed kontrol taşır;
   yalnız permission claim'ine güvenerek global öğrenci verisi açılmaz.
6. SystemAdmin rolü oluşturma/atama yalnızca mevcut SystemAdmin tarafından yapılır;
   kurum yöneticisi kullanıcı ekranında salt-okunur tenant görünümüne sahiptir.
7. Son aktif SystemAdmin hesabı silinemez, pasifleştirilemez veya SystemAdmin rolü
   kaldırılamaz; platformun yönetimsiz kalması fail-closed olarak engellenir.
8. Kurum yöneticisi üyeliği pasifleştirildiğinde kullanıcının refresh oturumları
   iptal edilir; yeniden etkinleştirme yeni bir güvenli oturum açılmasını gerektirir.
9. Kurum mutation'ları handler seviyesinde de scope kontrolü yapar; controller
   metadata'sı tek başına tenant izolasyonu olarak kabul edilmez.
10. Notification SignalR bağlantısı oturum sahibine bağlıdır. Logout veya kullanıcı
   değişiminde eski bağlantı durdurulur, istemci bildirim state'i temizlenir ve
   reconnect her çağrıda güncel access token'ı alır.
11. Kullanıcı, kurum, destek ve audit listeleri bounded server-side pagination
   kullanır; panel hiçbir liste için ilk 100 kaydı tam sonuç olarak varsaymaz.
12. Yazma düğmeleri yalnız sayfa görüntüleme iznine göre değil, karşılık gelen
    `Create/Edit/Delete/Activate/Reply/ManagePermissions` iznine ve endpoint'in
    gerektirdiği role göre gösterilir. Sunucu policy'si yine nihai otoritedir.
13. SystemAdmin access/refresh tokenı TOTP veya tek kullanımlık kurtarma kodu
    doğrulanmadan üretilmez. TOTP secret'ı Data Protection ile şifrelenir;
    kurtarma kodları yalnız hash olarak saklanır ve ilk kurulumda bir kez gösterilir.
14. Kritik rol, izin, kullanıcı, parola ve konfigürasyon mutasyonları ayrıca
    `MfaRequired` politikasını ister. JWT `amr=mfa` ve `auth_time` taşır; güven
    seviyesi refresh rotasyonu boyunca korunur. Eski MFA'sız SystemAdmin refresh
    tokenları iptal edilip yeniden girişe zorlanır.

## Tamamlanma doğrulaması

- Identity, Notification ve Coaching Release build'leri `--warnaserror` ile temiz.
- Admin metadata ve InMemory handler testleri integration test projesinde çalışır.
- Angular unit testleri, SSR production build'i ve route lazy-load derlemesi CI'de
  kapıdır.
- Compose Gateway route'ları `docker compose ... config --quiet` ile doğrulanır.
- Production öncesi gerçek kullanıcı verisiyle değil, disposable tenant ve staging
  smoke/E2E akışlarıyla doğrulama yapılır.

## Gelecek planı

Yeni bir bounded context eklendiğinde şu sırayı izleyin:

1. Domain owner ve tenant scope'u belirle.
2. Query/command DTO ve permission anahtarlarını Shared.Contracts'a ekle.
3. Backend policy + validation + audit + pagination yaz.
4. Gateway route ve API contract testini ekle.
5. Panel service, lazy route, permission guard ve erişilebilir tablo/form ekle.
6. Unit, integration, E2E ve load smoke kanıtlarını aldıktan sonra menüye ekle.
