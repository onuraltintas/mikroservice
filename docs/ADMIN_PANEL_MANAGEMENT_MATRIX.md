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
| Identity / Notification / Coaching | Servis-lokal, append-only yönetici denetim kayıtlarını işlem, kaynak ve değişen alan adlarıyla filtreleme/sayfalama | `/api/admin-audit/{service}` | Yalnız `SystemAdmin` + `Permissions.Operations.View` |
| Identity | Kullanıcının MFA kurulumu, doğrulaması ve tek kullanımlık kurtarma kodları | `/api/auth/mfa/*` | Profil ayarlarında mevcut parola ile yeniden doğrulama; girişte yalnız MFA etkinse 5 dakikalık challenge |
| Notification | Destek talebi listeleme, filtreleme, notlandırma, işlenmiş işareti ve yanıt | `/api/support/requests`, `/api/support/reply` | `Permissions.Support.View/Reply` |
| Notification | E-posta şablonu listeleme, oluşturma, konu/gövde/aktiflik güncelleme | `/api/email-templates` | `Permissions.Notifications.Templates` |
| Notification | Kullanıcının kendi bildirimleri | `/api/notifications` | Oturum sahibi |
| Coaching | Kullanıcıların assignment/exam/session/goal akışları ve tenant/rol kontrolleri | `/api/assignments`, `/api/exams`, `/api/sessions`, `/api/goals` | Teacher/Student/SystemAdmin domain policy'leri |
| Coaching | Global, bounded ve PII'siz operasyon özeti | `/api/coaching-admin/overview` | Yalnız `SystemAdmin` + `Permissions.Coaching.View` |
| Coaching | Sayfalı assignment listesi; kaynak, kitap aralığı, teslim ve attachment sayaçları | `/api/coaching-admin/assignments` | Yalnız `SystemAdmin` + `Permissions.Coaching.View` |
| Coaching | Assignment ayrıntısı; öğrenci teslim durumu, not/geri bildirim ve güvenli attachment metadata'sı | `/api/coaching-admin/assignments/{id}` | Yalnız `SystemAdmin` + `Permissions.Coaching.View` |
| Coaching | Yalnız `Clean` durumundaki fotoğraf attachment'ını yetkili stream olarak indirme | `/api/assignments/{id}/students/{studentId}/attachments/{attachmentId}/content` | Assignment domain policy + aktif attachment |
| Coaching | Sayfalı seans, sınav ve hedef operasyon listeleri; arama/durum filtreleri | `/api/coaching-admin/sessions`, `/api/coaching-admin/exams`, `/api/coaching-admin/goals` | Yalnız `SystemAdmin` + `Permissions.Coaching.View` |
| Coaching | Seans ve sınav ayrıntısı; katılım ve sonuç ölçüm alanları | `/api/coaching-admin/sessions/{id}`, `/api/coaching-admin/exams/{id}` | `SystemAdmin` + `Permissions.Coaching.View`; kimlikler yalnız admin operasyonu için |
| Coaching | Assignment oluşturma, düzenleme, güvenli öğrenci yeniden atama/iptal/silme/notlandırma; fotoğraf teslimlerinin güvenli yönetimi | `/api/coaching-admin/assignments*` | `SystemAdmin` + `Permissions.Coaching.Manage` + `MfaRequired` |
| Coaching | Seans oluşturma, düzenleme/yeniden planlama, katılım güncelleme/iptal/silme | `/api/coaching-admin/sessions*` | `SystemAdmin` + `Permissions.Coaching.Manage` + `MfaRequired` |
| Coaching | Sınav oluşturma, düzenleme, sonuç ekleme/düzeltme/silme | `/api/coaching-admin/exams*` | `SystemAdmin` + `Permissions.Coaching.Manage` + `MfaRequired` |
| Coaching | Hedef oluşturma, düzenleme/nullable hedef temizleme, ilerleme güncelleme/silme | `/api/coaching-admin/goals*` | `SystemAdmin` + `Permissions.Coaching.Manage` + `MfaRequired` |

## Coaching admin yönetimi neden explicit command olarak tasarlandı?

Koçluk kayıtları öğrenci notu, geri bildirim, sınav sonucu ve katılım bilgisi
içerir. Paneldeki yönetim işlemleri genel tablo CRUD'u olarak değil, domain
komutlarını kullanan açık aksiyonlar olarak sunulur. Böylece:

- Öğretmen/öğrenci yazma ve okuma işlemleri mevcut domain policy'leriyle sınırlıdır.
- SystemAdmin paneli genel özetin yanında sayfalı operasyon listelerini ve gerekli
  assignment/seans/sınav ayrıntılarını görür. Öğrenci teslim durumu, katılım,
  not, geri bildirim ve attachment metadata'sı yalnızca korumalı admin ayrıntı
  endpoint'lerinden döner;
  ham object-storage anahtarı, bekleyen/taranmamış dosya içeriği ve doğrudan bucket
  erişimi hiçbir API yanıtına eklenmez. Fotoğraf içeriği yalnızca `Clean` tarama
  durumundan sonra yetkili stream endpoint'i ile okunabilir.
- Her create çağrısı `Idempotency-Key` ile korunur; tekrar gönderim aynı kaydı
  döndürür, farklı gövdeli tekrar `409 Conflict` üretir.
- Mutasyonlar `MfaRequired` ve `Permissions.Coaching.Manage` ister. Komut
  handler'ları öğretmen/öğrenci/kurum ilişkilerini yeniden doğrular; controller
  metadata'sı tek başına yetki sınırı değildir.

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
   iptal edilir; yeniden atama mevcut üyeliği tek satır olarak yeniden etkinleştirir,
   rolü günceller ve yeni bir güvenli oturum açılmasını gerektirir.
9. Kurum pasifleştirildiğinde o kuruma bağlı yönetici, öğretmen, öğrenci ve veli
   üyelerinin aktif refresh oturumları iptal edilir; başka kuruma bağlı olmayan
   kullanıcılar etkilenmez. Yeniden etkinleştirme yeni güvenli oturum açılmasını gerektirir.
10. Kurum mutation'ları handler seviyesinde de scope kontrolü yapar; controller
   metadata'sı tek başına tenant izolasyonu olarak kabul edilmez.
11. Notification SignalR bağlantısı oturum sahibine bağlıdır. Logout veya kullanıcı
   değişiminde eski bağlantı durdurulur, istemci bildirim state'i temizlenir ve
   reconnect her çağrıda güncel access token'ı alır.
12. Kullanıcı, kurum, destek ve audit listeleri bounded server-side pagination
   kullanır; panel hiçbir liste için ilk 100 kaydı tam sonuç olarak varsaymaz.
13. Yazma düğmeleri yalnız sayfa görüntüleme iznine göre değil, karşılık gelen
    `Create/Edit/Delete/Activate/Reply/ManagePermissions` iznine ve endpoint'in
    gerektirdiği role göre gösterilir. Sunucu policy'si yine nihai otoritedir.
14. MFA varsayılan olarak kapalıdır. MFA etkin bir SystemAdmin access/refresh tokenı
    TOTP veya tek kullanımlık kurtarma kodu doğrulanmadan üretilmez; MFA kapalı
    hesaplar normal ilk faktör oturumu açabilir. TOTP secret'ı Data Protection ile
    şifrelenir, kurtarma kodları yalnız hash olarak saklanır ve ilk kurulumda bir kez
    gösterilir. Kurulum mevcut parola ile yeniden doğrulama ister.
15. Kritik rol, izin, kullanıcı, parola ve konfigürasyon mutasyonları ayrıca
    `MfaRequired` politikasını ister. JWT `amr=mfa` ve `auth_time` taşır; güven
    seviyesi refresh rotasyonu boyunca korunur. MFA sonradan etkinleştirilen
    SystemAdmin'in eski MFA'sız refresh tokenları iptal edilip yeniden girişe
    zorlanır.

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
