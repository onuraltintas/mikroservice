# İki servis için abonelik zorunluluğu

14 Ekim 2026 tarihinde canlıya alındı. Uygulama kaynak commit'i: `b777b545`.

## Kullanım

- Hızlı Okuma: `/dashboard/speed-reading/subscriptions` → **Abonelik ayarı** → **Hızlı Okuma aboneliği zorunlu** → **Ayarı kaydet**.
- Koçluk: `/dashboard/coaching/subscriptions` → **Abonelik ayarı** → **Koçluk aboneliği zorunlu** → **Ayarı kaydet**. Banka bilgileri ayrı **Banka ayarı** sekmesindedir.

Açıkken öğrencinin çalışma erişiminde aktif abonelik aranır. Kapalıyken bu serviste abonelik aranmaz. Kullanıcı girişi, servis yetkileri, rol ve kurum kapsamı kontrolleri devam eder. Ayarlar bağımsızdır; ödeme, abonelik ve kurum bağlantısı kayıtlarını değiştirmez veya yeni lisans oluşturmaz. Mevcut ilk değerlendirme/profil kurulum istisnaları korunur.

Dağıtım sonrası gerçek veritabanından okunan durum: **Hızlı Okuma açık**, **Koçluk kapalı**. Test amacıyla canlı genel anahtarlar değiştirilmedi.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| Seçili backend, erişim middleware ve gerçek MVC gövde bağlama testleri | 79 geçti |
| Admin bileşen ve servis testleri | 53 geçti |
| Canlı PostgreSQL'in yalıtılmış kopyasında migration ve HTTP kontrolleri | 30 geçti |
| Yerel üretim derlemesinde masaüstü/mobil aç-kapat-yenile | 4 senaryo geçti |
| Canlıdan sunulan arayüzde masaüstü/mobil aç-kapat-yenile | 4 senaryo geçti; API ve oturum yanıtları tarayıcıda fixture ile karşılandı |

PostgreSQL provası üretimdeki `EnableRetryOnFailure` ayarını kullanır. İlk kayıtta `false` değerinin kalıcılığı, aynı idempotency anahtarıyla eşzamanlı kayıt, banka ayarı ve abonelik anahtarının aynı anda ilk kez oluşturulması, banka bilgilerinin korunması ve aboneliksiz öğrencinin açık-kapalı-açık erişimi doğrulandı. Gerçek controller ve middleware zincirinde öğrenci, öğretmen ve kurum rollerinin genel ayarı değiştiremediği; admin için permission ve MFA gerektiği; yanlış ürün oturumunun reddedildiği kontrol edildi. TestServer kimlik doğrulaması yalnız yalıtılmış doğrulama uygulamasında vardır; üretim kimlik doğrulaması ve CAPTCHA değiştirilmedi.

Admin, ayar yüklenmediyse kayıt göndermez. Bekleyen ayar okuma/yazma istekleri çakıştırılmaz; kayıt hata verirse kaydedilmiş durum korunur. Koçluk anahtarı değiştirilirken yüklenmiş banka bilgileri gönderilir; Hızlı Okuma anahtarının ayrı PUT isteği yalnız boolean değeri taşır.

## Dağıtım ve geri dönüş

Yalnız `speed-reading-service` ve `admin-panel` güncellendi. Diğer konteynerlerin kimlikleri doğrulanarak korundu. API, önceki canlı Speed API kaynak ağacından; admin, önceki canlı admin kaynak ağacından yalnız ilgili dosyalar kopyalanarak derlendi. Çalışma alanındaki diğer değişiklikler bu dağıtıma dahil edilmedi.

Migration: `20261014144635_SubscriptionRequirementToggle`. Yalnız `speed_reading.bank_transfer_payment_settings.RequireActiveSubscription` boolean sütununu, `NOT NULL DEFAULT TRUE` ile ekler. Sıra: yedek → migration → API → admin. Banka alanlarının checksum'ı migration öncesi/sonrası aynı kaldı. Yedekler ve çalışma ortamı dosyaları sunucuda yalnız root erişimli yayın dizininde tutulur; gizli değerler rapora dahil edilmez.

İlk admin imajında paket dosyaları `node` kullanıcısına okunamadığı için başlangıç başarısız oldu. Sağlık kontrolü iki servisi önceki imajlarına geri döndürdü. Additive sütun korundu; eski API bunu kullanmaz ve önceki abonelik zorunluluğunu uygular. Admin Dockerfile'ında paket sahipliği/izinleri düzeltildi, ağsız gerçek imaj önizlemesi ve başarılı önizleme/imaj kimliği promotion kapıları eklendi. İkinci geçiş başarılı oldu; uygulanmış migration tekrar çalıştırılmadı.

Canlı yayın etiketi: `subscription-requirement-20261014`. API imajı `8403a4cd…`, admin imajı `31490b50…`. Canlı geçiş saati: `2026-10-14T15:31:04Z`. Dağıtım sonrası 15 dakikalık izleme; servis readiness, tekrar başlatma sayısı, üç kamu giriş adresinin HTTP durumu ve yanıt süresini kaydeder. Geçiş sırasında başarısız sağlık/imaj doğrulamasında önceki iki imaja dönüş uygulanır; yeni sütunu düşürmek gerekmez. Sonraki bir geri dönüş, Hızlı Okuma anahtarı kapalıysa eski API'nin yeniden abonelik isteyeceği dikkate alınarak yapılmalıdır.

Kanıtlar: `artifacts/subscription-requirement-backend-tests.log`, `subscription-requirement-ui-tests.log`, `subscription-requirement-relational-results.json`, `subscription-requirement-live-state.log`, `subscription-requirement-ui/`, `subscription-requirement-live-ui/`. Yayın betikleri: `tools/releases/subscription-requirement-20261014/`.

İzleme `2026-10-14T15:46:35Z` tarihinde tamamlandı: 90 kamu HTTP kontrolünün tamamı 200, en yüksek ölçülen yanıt süresi 0,366 saniye; iki servis sağlıklı ve restart sayıları 0. Son imaj eşleşmesi, diğer konteynerlerin korunması ve CAPTCHA yapılandırması doğrulandı. Kanıt: `artifacts/subscription-requirement-final-health.log` ve `artifacts/subscription-requirement-20261014/monitor-*.log`.

## Koçluk ayarının ayrı sekmeye taşınması

`639cb289` kaynak commit'i, `coaching-subscription-layout-20261014` etiketiyle `2026-10-14T15:59:07Z` tarihinde canlıya alındı. Yalnız admin paneli güncellendi; migration yok. Koçluk ayarlarının tamamının hash'i önce/sonra aynı, diğer konteynerlerin kimlikleri aynı ve yeni admin imajı sağlıklı/restart 0 olarak doğrulandı.

Koçluk **Abonelik ayarı** artık ilk ve varsayılan sekmedir; yalnız anahtar, kaydedilmiş durum ve kayıt düğmesi gösterilir. **Banka ayarı** sekmesinde anahtar bulunmaz. Abonelik kaydı kaydedilmiş banka bilgilerini, banka kaydı kaydedilmiş abonelik değerini kullanır; diğer sekmenin kaydedilmemiş taslağı gönderilmez.

16 bileşen testi, yerel üretim derlemesinde 6 masaüstü/mobil senaryo ve canlıdan sunulan arayüzde aynı 6 senaryo geçti. Tarayıcı senaryolarında API/oturum yanıtları fixture ile karşılandı; canlı genel ayarlar test amacıyla değiştirilmedi. Kanıt: `artifacts/coaching-subscription-setting-layout-tests.log`, `artifacts/coaching-subscription-layout-browser-proof.log`, `artifacts/coaching-subscription-layout-live-browser-proof.log`. Betikler: `tools/releases/coaching-subscription-layout-20261014/`.
