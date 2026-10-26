# Notification sertleştirme — 26.10.2026

Kapsam: `services/notification-service`, compose dosyaları. Mesaj sözleşmeleri, kuyruk adları ve
veritabanı modeli değiştirilmedi (yeni migration yok). İnceleme: proje dokümanı
`claude/notification-review.md`.

## Davranış değişiklikleri

| Alan | Önce | Sonra |
| --- | --- | --- |
| Kuyruklar | 5 coaching consumer kayıtlıydı ama kuyruğu yoktu; güncelleme bildirimleri (ödev, sınav, sınav sonucu, seans, hedef) hiç gitmiyordu | Tek liste (`NotificationReceiveEndpoints.All`) kaydı ve kuyruğu birlikte üretir; 33 kuyruk, her biri retry + EF inbox/outbox |
| E-posta içeriği | İsim, davet mesajı ve davet eden adres HTML'e ham ekleniyordu; davet linki olduğu gibi kullanılıyordu | Kullanıcı metni HTML-encode; konu satırında satır sonu yok; linkler yalnız platform adreslerine (`PublicAppUrl`) izinli, değilse giriş sayfası |
| Destek yanıtı | E-posta kuyruğa alınıp istek "işlendi" işaretlenmesi ayrı kayıtlardı | Tek transaction; yanıt en fazla 5000 karakter |
| Anonim destek formu | Her gönderimde forma yazılan adrese, isim konu satırında, onay e-postası | Adres başına günde en fazla 3 onay; konu sabit metin |
| Identity'ye iletim | Her hata tekrar deneniyordu | 4xx (408/429 hariç) kalıcı ret, diğerleri `MaxAttempts`'e kadar |
| E-posta gönderimi | SMTP gönderimi veritabanı kilidi altında | Kilit yalnız durum kontrolü için; gönderim kilit dışında |
| Saklama | Gönderim ve iletim kayıtları sınırsız birikiyordu | Biten gönderim/iletim kayıtları 180, okunmuş bildirimler 365 gün sonra silinir; bekleyen iş silinmez |
| Outbox | Serializable | ReadCommitted |
| Başlangıç | `.env` her ortamda; Redis kaydı (kullanılmıyordu); hatalı `SMTP_PORT` gönderimde patlıyordu | `.env` yalnız Development ve var olanı ezmez; Redis yok; SMTP portu başlangıçta doğrulanır |
| Container | root | UID 1654 (`USER $APP_UID`); `notification-volume-permissions` volume'ü hazırlar |

## İkinci tur (N6–N9)

| Alan | Önce | Sonra |
| --- | --- | --- |
| Bildirim saatleri | Coaching ve Course Management UTC yayınlıyor; 11 bildirim metni ve destek şablonu saati çevirmeden yazıyordu (3 saat geri) | Türkiye saati (`TurkeyTime`) |
| Anlık bildirim | SignalR, consumer transaction'ı commit edilmeden gönderiliyordu | Outbox üzerinden `notification-realtime` kuyruğu; yalnız commit edilmiş ve alıcısı silinmemiş kayıt gönderilir |
| Destek yanıt ucu | İki uç (`/support/reply` ve `/support/requests/{id}/reply`) | Tek uç; admin paneli bildirim penceresi de onu kullanır |
| Destek talebi silme | Yoktu | `Support.Delete` yetkisi; talep, personel bildirim kopyaları, bekleyen iletim ve destek e-postalarıyla birlikte silinir; denetim kaydı yazılır |
| Saklama | Destek talepleri süresiz | Yanıtlanan (işlenen) talepler yanıttan 6 ay, yanıtlanmayanlar gelişten 3 ay sonra otomatik silinir |
| Anonim destek formu | CAPTCHA yok | reCAPTCHA (giriş ekranıyla aynı site anahtarı); token'ı Identity doğrular, doğrulanamazsa reddedilir; giriş yapmış kullanıcıya sorulmaz |
| Worker'lar | İki kopya döngü | Ortak `LeasedDeliveryWorker`; davranış aynı |
| Course Management | 8 kuyruk ve bildirim API'si CM yayında değilken de açık | `CourseManagement__Enabled` (varsayılan kapalı); yalnız CM compose overlay'leri açar. Release dalında satır silmeye gerek yok |
| Tekrarlar | İki aynı dispatcher, dağınık MessageId kontrolleri ve id üreticileri | `RecipientNotificationDispatcher`, `MessageIds`; üretilen id'ler değişmedi (testle sabit) |

**MFA ve silme:** `Support.Delete`, yanıtlama gibi İçerik (cms) MFA politikasına bağlıdır. MFA'yı açmış
admin her zaman MFA ile giriş yaptığı için (Identity sertleştirmesi) silmede ayrıca sorulmaz. MFA
kullanmayan adminin silebilmesi, admin panelindeki İçerik MFA politikasının "kapalı" olmasına bağlıdır;
"zorunlu" ise yanıtlamada olduğu gibi silmede de MFA gerekir.

## Temizlik

`Notification.API.http`, `Coaching_NewCourse` şablonu (hiçbir yerden gönderilmiyordu), kullanılmayan `Microsoft.AspNetCore.OpenApi` ve `MassTransit.RabbitMQ`
(Infrastructure) paketleri, boş `OnConnectedAsync`, `GetCompanyName`, `Jwt:Authority` ayarı,
iki kopya bağlantı dizesi kurucusu, Dockerfile'daki gereksiz build adımı.

## Yayın adımları

1. Yeni kuyruklar ilk açılışta oluşur; eski kuyruklar aynı adla kalır.
2. **Volume izni:** notification artık UID 1654 ile çalışır. `notification-volume-permissions`
   tek seferlik servisi volume'ü sahiplendirir (compose otomatik). Sunucuda elle
   hazırlanan paketlerde bu servis ve `runtime-secrets-preflight` bağımlılığı da olmalı.
3. Secret dosyaları identity notunda anlatıldığı gibi (`chgrp 1654`, `chmod 0640`).
4. İsteğe bağlı: `SMTP_PORT` boşsa 1025; geçersizse servis başlamaz.
5. **Identity önce:** Anonim form CAPTCHA'sı Identity'nin `/api/internal/captcha/verify` ucunu
   kullanır. Bu uç olmadan form 403 döner; Identity bu notification sürümüyle birlikte ya da önce
   yayınlanmalı. Identity'de `AuthRecaptcha` kapalıysa token istenmez.
6. `Support.Delete` yetkisi Identity açılışında otomatik eklenir; SystemAdmin'de vardır, diğer
   roller için Roller ekranından verilir.
7. Yeni kuyruk: `notification-realtime` (ilk açılışta oluşur).
8. Course Management notification'da varsayılan kapalı; CM yayına çıkarken
   `docker-compose.course-management.production.yml` ile birlikte açılır.

## Doğrulama

- Her adım ayrı commit (N1–N12); her adımda tam paket önceki duruma göre karşılaştırıldı —
  fark yalnız Docker gerektiren testler ve önceden kararsız MassTransit consumer testleri.
- Yeni testler: kuyruk eşleşmesi (her consumer'ın kuyruğu var), e-posta enjeksiyonu ve link
  doğrulama, gerçek PostgreSQL'de destek yanıtı transaction'ı, onay sınırı ve saklama
  temizliği, iletim sonucu sınıflandırma, operasyon (Dockerfile/compose/SMTP portu).
- Compose: 9 overlay kombinasyonu geçerli.

## Sonraya kalan

- Şablonların veritabanında HTML olarak saklanması (yönetici paneli düzenlemesi): şablon
  değişkenleri artık encode ediliyor, şablonun kendisi güvenilir içerik sayılıyor.
- SignalR hub'ı için ayrı ölçek planı (birden fazla instance'ta backplane).
