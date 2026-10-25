# Identity sertleştirme — 25.10.2026

Kapsam: `services/identity-service`, ortak `RedisCacheService` ve `LoggerExtensions`, compose
dosyaları. Coaching ve Speed Reading kaynakları (korunan ürün sözleşmesi) değiştirilmedi.
İnceleme: proje dokümanı `claude/identity-review.md`.

## Davranış değişiklikleri

| Alan | Önce | Sonra |
| --- | --- | --- |
| MFA | Yalnız yönetici rolleri girişte doğrulanıyordu | MFA açmış her hesap (şifre, Google, Course Management oturumu); MFA'sız açılmış oturum yenilenemez |
| Google bağlama | Doğrulanmamış hesaba bağlanınca önceden konmuş şifre çalışır hâle geliyordu | Doğrulanmamış hesapta şifre, bekleyen sıfırlama linki ve oturumlar silinir; herhangi bir üründe personel rolü olan hesap açık bağlama ister |
| Deneme sınırı | Yalnız IP başına 60/dk + reCAPTCHA | Hesap başına 15 dk'da 10 hata → 15 dk kilit (Redis, adres hash'li anahtar; Redis yoksa izin verir). Giriş ve "mevcut hesapla katıl" ortak |
| Kullanıcı keşfi | Olmayan adres anında dönüyordu | Olmayan adreste de bir PBKDF2 doğrulaması |
| Refresh token | Düz metin, index yok, her yenilemede 7 gün uzuyor, tekrar kullanım fark edilmiyor | SHA-256 özet + benzersiz index; aile (family) ve tekrar kullanımda tüm aile iptal; bir giriş en fazla 30 gün; 14 günden eski kayıtlar saatlik temizlenir |
| Şifre sıfırlama linki | Düz metin, `!=` karşılaştırma | SHA-256 özet, sabit zamanlı karşılaştırma |
| E-posta | Olduğu gibi saklanıyor, arama `ToLower()` ile tam tarama, eşzamanlı kayıtta çift hesap | Küçük harf + trim saklanır, index kullanılır; yarışan kayıt `Identity.UserExists` alır |
| Kurum yöneticileri | `Institutions.Manage` olan herkes (Müdür dahil) Sahibi pasifleştirebiliyordu | Sahip/Yönetici rollerini yalnız Sahip yönetir; Müdürleri Sahip veya Yönetici; kendi üyeliğini kimse değiştiremez; son aktif Sahip pasifleştirilemez |
| MFA politikası | Şifreyle girmiş SystemAdmin MFA'yı kapatabiliyordu | Gevşetmek için MFA'lı oturum (`amr=mfa`); sıkılaştırmak serbest |
| Redis kesintisi | Girişte 500; servis Redis'siz başlarken çöküyordu | Ayarlar veritabanından okunur; ortak Redis kaydı `abortConnect=false` |
| Sağlık | `/health/ready` = `/health` (Redis yok), compose healthcheck yok | Ready: veritabanı + Redis + mesaj kuyruğu; compose healthcheck `/health/ready` |
| Container | root | UID 1654 (`USER $APP_UID`) |
| `.env` | Her ortamda yükleniyor ve ortam değişkenlerini eziyordu | Yalnız Development, var olanı ezmez |
| Log | Seq her zaman `localhost:5341`'e | Seq yalnız `SEQ_URL` varsa (veya yerel Development); `LOG_FORMAT=json` ile JSON konsol |

## Yayın adımları

1. **E-posta çakışma kontrolü** (sonuç boş olmalı; doluysa hesapları bilinçli birleştirin):
   ```sql
   SELECT lower(btrim("Email")) AS email, array_agg("Id") AS accounts
   FROM identity.users GROUP BY 1 HAVING count(*) > 1;
   ```
   `NormalizeUserEmails` migration'ı çakışma varsa hiçbir şeyi değiştirmeden durur.
2. **Secret dosya izinleri (production):** Identity artık UID 1654 ile çalışır. Host'taki
   PFX ve reCAPTCHA secret dosyaları bu kullanıcı tarafından okunabilmeli:
   `chgrp 1654 <dosya> && chmod 0640 <dosya>`. `identity-secrets-preflight` okunamıyorsa
   yayını, çalışan identity değiştirilmeden önce durdurur.
3. Migration'lar (`identity-migrations`): `HashSessionTokens` (mevcut token'ları yerinde
   hash'ler; oturumlar düşmez), `NormalizeUserEmails`. Geri alma: `HashSessionTokens` Down,
   özetler geri çevrilemediği için açık oturumları ve sıfırlama linklerini iptal eder.
4. `identity-volume-permissions` tek seferlik servisi Data Protection volume'ünü UID 1654'e verir
   (compose otomatik çalıştırır).
5. İsteğe bağlı ayarlar (varsayılanlar yeterli):
   `Identity__LoginLockout__MaxFailedAttempts` (10), `__WindowMinutes` (15), `__LockoutMinutes` (15),
   `Identity__Session__MaxSessionDays` (30), `__RetentionDays` (14), `LOG_FORMAT=json`.

## Kullanıcıya görünen etkiler

- MFA'yı kendisi açmış öğretmen/öğrenci/veliler bir kez yeniden MFA ile giriş yapacak.
- Uzun süredir yenilenen oturumlar 30 günü aşmışsa bir kez yeniden giriş gerekecek.
- Kilitlenen hesap: "Çok fazla başarısız giriş denemesi. Lütfen N dakika sonra tekrar deneyin."
  (`Auth.TooManyAttempts`, HTTP 400 — mevcut hata gösterimiyle uyumlu).
- Admin panelde Müdür, Sahip/Yönetici üyeliklerini değiştiremez; MFA politikasını gevşetmek için
  MFA ile giriş gerekir (403 `Auth.MfaRequired`).

## Doğrulama

- Her adım ayrı commit; her adımda tam Identity paketi önceki duruma göre karşılaştırıldı —
  fark yalnız önceden de kararsız MassTransit consumer testleri.
- Yeni testler: MFA/Google (9), kilit (8, Redis'li 2), token ve e-posta domain testleri,
  gerçek PostgreSQL'de token saklama/aile/temizlik (4) ve e-posta (2), kurum hiyerarşisi ve
  MFA politikası (20+), operasyon (4).
- Migration'lar PostgreSQL 16'da: ileri, veri dönüşümü, geri ve tekrar ileri; çakışmada durma.
  `has-pending-model-changes` eşdeğeri: bekleyen değişiklik yok (CI "EF migration drift" yeşil).
- Compose: 9 overlay kombinasyonu geçerli; healthcheck ve preflight komutları ayrıca çalıştırıldı.
- Gateway testleri 140/140; altı servis derleniyor.

## Sonraya kalan

- JWT imzalamayı RS256/ES256 + JWKS'e geçirmek (altı servisi etkiler, ayrı plan).
- Course Management oturum yükseltmesinde yeni oturumu kaynak oturumun ömrüyle sınırlamak
  (istemci akışı değişikliği gerektirir).
- Tek merkezden paket sürümü (Directory.Packages.props) ve 10.0.x hizalaması.
- OpenTelemetry'ye Npgsql/MassTransit/Redis izleri; istisnaların tek ve doğru seviyede loglanması
  (ortak kod, tüm servisler).
- Kurumlar arası öğretmen ataması ve KVKK silmede görev ayrılığı (düşük öncelik).
