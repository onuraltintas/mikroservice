# Çoklu kurum seçimi ve iki servis canlı doğrulaması

14 Ekim 2026. Çoklu kurum seçimi iki katılım yönünde tamamlandı ve canlıya alındı. Üç test hesabında iki ürünün yetkileri ayrı; mevcut koçluk bağlantıları ve aboneliği korunuyor. CAPTCHA üretimde açık ve minimum puanı 0,5.

## Katılım davranışı

Kurum hesabı yeni servise katılırken bir aktif kaynak kurum varsa aynı kurum kullanılır. Birden fazla bağlantıda mevcut şifre ve gerekiyorsa MFA doğrulandıktan sonra yalnız hesaba ait aktif kurumlar listelenir. Kullanıcı seçmeden erişim, kurum bağlantısı veya yasal kabul kaydı eklenmez.

Başvuru sırasında seçim yeniden kontrol edilir: başka hesaba ait kurum, pasif kurum veya yetkisi kaldırılmış bağlantı reddedilir. Hedefe yalnız seçilen kurum ve aynı bağlantı rolü eklenir; Owner yetkisi için kaynak Owner rolü ayrıca gerekir. Kaynak bağlantılar, profiller ve abonelikler değiştirilmez. Kurum listesi gösterilirken MFA kodu tüketilmez; kod yalnız tamamlanan katılımda tüketilir.

API: POST /api/auth/{coaching|speed-reading}/register/join. Institution hesabında isteğe bağlı institutionId alanı kullanılır. Çoklu bağlantıda seçim eksikse HTTP400 Auth.InstitutionSelectionRequired ve institutions dizisi döner. Body ürün bilgisi route kapsamını değiştiremez.

## Giriş hatası düzeltmesi

Gecikmeli CAPTCHA403 yanıtı sonrası staff formunun düz durumu değişiyor, fakat Angular ekranı yenilemiyordu. Login, MFA, kurtarma ve ürün geçişi hata durumlarında değişiklik kontrolü tetikleniyor. Katılım formunda da gecikmeli kurum listesi ve hata/success yanıtı ekrana aktarılıyor. Kurum seçme gereği role=alert ile duyuruluyor.

Gerçek canlı CAPTCHA403 sonrasında mesaj görüldü ve submitEnabled=true doğrulandı: artifacts/multi-institution-live-login-diagnostic/result.json. Google ret tanısı yalnız success, score ve action/hostname eşleşme boolean'larını loglar; token, anahtar ve parola loglanmaz. Gözlenen otomatik tarayıcı puanları girişte 0,3, katılımda 0,1/0,3 idi; 0,5 eşiği korunarak bu istekler reddedildi.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| CrossProductEnrollmentTests | 22 başarılı |
| AuthRecaptchaSecurityTests | 32 başarılı |
| Admin katılım arayüzü | 9 başarılı |
| Speed Reading katılım arayüzü | 8 başarılı |
| Staff giriş/rol/ürün arayüzü | 14 başarılı |
| Canlı PostgreSQL yedeğinin izole kopyasında gerçek API HTTP testleri | 29 başarılı |
| Canlı altı kayıt ekranının mevcut hesap/yasal metin korumaları | 6 başarılı |

Toplam 85 ilgili birim/entegrasyon testi ve 29 PostgreSQL/HTTP prova geçti. Prova; iki yönde kurum listesi JSON serileştirmesini, yanlış şifreyle listenin verilmemesini, yabancı/revoked seçimleri, seçilen tek bağlantıyı, abonelik korunmasını, route kapsamını, gerçek FK rollback ve eşzamanlı MFA giriş/katılım yarışını kapsıyor. Üç ön yüz ve Identity üretim derlemeleri başarılı; mevcut stil/bundle bütçe uyarıları sürüyor.

Normal Chrome ile önceki canlı oturum denemesinde öğrenci ve öğretmen Koçluk panelleri gerçek API200 yanıtlarıyla açıldı. Son otomatik canlı giriş/katılım denemelerini Google CAPTCHA reddetti; bunlar başarılı oturum veya geçiş kanıtı sayılmadı.

Kullanıcı normal tarayıcıda CAPTCHA açıkken son canlı doğrulamaları yaptı ve şu sonuçları bildirdi:

| Rol | Manuel canlı doğrulama |
|---|---|
| Kurum | Giriş başarılı; Hızlı Okuma → Koçluk panellerinin ikisi de açıldı |
| Öğretmen | Giriş ve Hızlı Okuma → Koçluk geçişleri başarılı |
| Öğrenci | eduivme.com ve masterhizliokuma.com üzerinde iki servise de giriş başarılı |

Bu manuel sonuçlar kullanıcı bildirimiyle doğrulanmıştır; otomatik tarayıcıdan alınmış panel ekran görüntüsü olarak sunulmaz.

## Test hesapları ve veri korunması

Yalnız daha önce oluşturulmasına izin verilen testogrenci@eduivme.com, testogretmen@eduivme.com, testkurum@eduivme.com hesaplarına ayrı Speed Reading yetkisi hazırlandı. Sabit üç kullanıcı ID'si, tam e-posta, kaynak rol/profil, özgün test kurumu ve Admin bağlantı rolü doğrulanır. Tek serializable transaction hedef role/access kaydını ve kurum hesabının aynı kurum bağlantısını ekler. Kaynak kurum yöneticisi Owner yapılmaz.

Bu hazırlık yönetici test verisi eklemesidir; halka açık CAPTCHA korumalı katılım testi başarılıymış gibi raporlanmaz. SelfRegistration onayı veya satın alma kaydı üretilmez. Yeni Speed Reading aboneliği verilmez; öğrencinin ilk servis kullanımı profil kurulumu gerektirebilir.

Fixture kopyasında grant/verify, tekrar eklememe, revoked hedef erişimini yeniden açmama ve kaynak satırların tam hash eşitliği geçti. Canlı grant öncesi/sonrası kullanıcı, Koçluk rol/access, kurum, kurum yöneticisi bağlantısı ve öğrenci/öğretmen profil hash'leri aynıydı. Parolalar değişmedi. Koçluk aboneliği Active; bitişi 20 Kasım 2026 19:37:53 UTC. Gerçek kullanıcı veya kurum kayıtlarına test yetkisi eklenmedi.

## Yayın ve operasyon kaydı

- Uygulama kaynak commit'i: 838fc1b023f31f36db267bce5dad207087877aac.
- Identity/admin/Speed Reading frontend etiketi: multi-institution-enrollment-20261014.
- Son staff etiketi: multi-institution-enrollment-20261014-r2; yayın helper commit'i 91dc44a7.
- Fixture helper commit'i: 386e469e; canlı grant yalnız üç test hesabıyla sınırlı.
- Yeni migration yok. Speed Reading API ve hedef dışı servisler değiştirilmedi.
- Ana yayın dizini: /var/lib/eduivme/releases/multi-institution-enrollment-20261014.
- Son staff yayın dizini: /var/lib/eduivme/releases/multi-institution-staff-origin-20261014.
- Test yetkisi prova/grant dizini: /var/lib/eduivme/releases/multi-institution-fixtures-20261014-r2.

İlk staff paketinde /staff/ base href derleme parametresi eksik kaldı. Canlı tarayıcı testi giriş ekranının yüklenmediğini yakaladı; yalnız staff hemen eski çalışan imaja döndürüldü. Aynı kod doğru /staff/ parametresiyle yeniden derlenip tek servis olarak yayımlandı. Sağlık kontrolüne exact base href ve gerçek main JavaScript dosyası kontrolü eklendi. Son staff yayını 16:21:05 Europe/Istanbul; 15 dakikalık tekrar izleme 16:36:19'da tamamlandı. Dört servis running/restart0 ve CAPTCHA true/0,5 kontrolü geçti.

Rollback manifestleri yalnız hedef imajları sabitler; --no-deps --pull never kullanılır. Ana yayın validate eski ilk staff imajını bekler; son durum için staff-origin.sh validate ve üç diğer servisin image/health kontrolü kullanılmalıdır. Yedekler ve bağlantı bilgileri sunucuda özel yayın dizinlerindedir.

Kanıtlar: artifacts/multi-institution-relational-results.json, artifacts/multi-institution-public-smoke/results.json, artifacts/multi-institution-post-deploy-health.log, artifacts/multi-institution-fixture-live-grant.json, artifacts/multi-institution-live-login-diagnostic/result.json. Reddedilen otomatik oturum denemeleri artifacts/multi-institution-live-enrollment/results.json ve artifacts/multi-institution-live-switch/results.json içinde açıkça failed kayıtlıdır.
