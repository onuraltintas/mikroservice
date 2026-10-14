# Mevcut hesapla öğrenci Koçluk kaydı

14 Ekim 2026 saat 08:05:59 (Europe/Istanbul) tarihinde canlıya alındı. Kaynak commit: `532469bc`.

## Sorun ve düzeltme

Hızlı Okuma hesabı olan bir e-posta ile Koçluk öğrenci kayıt formu gönderildiğinde ortak kimlik servisinin e-posta benzersizlik kontrolü `Identity.UserExists` / HTTP 409 döndürüyordu. Frontend gerçek `description` alanını göstermediği için kullanıcı yalnız genel kayıt hatası görüyordu.

Mevcut, aktif ve e-postası doğrulanmış hesap kendi mevcut şifresini doğrulayarak Koçluk'a öğrenci olarak katılabilir. Aynı kullanıcı kimliği korunur; Koçluk ürün erişimi, Koçluk kapsamındaki Student rolü, gerekiyorsa bağımsız öğrenci profili ve güncel yasal metin kabul kayıtları tek SaveChanges ile kaydedilir. Hızlı Okuma erişimi, mevcut parola, ad/soyad ve e-posta doğrulaması değiştirilmez. Yeni hesap kayıt akışı korunur. Frontend gerçek API hata açıklamasını gösterir ve mevcut şifre kullanımını açıklar.

MFA açık hesaplar bu şifreyle katılım yolunu kullanamaz. Önceden herhangi bir Koçluk erişim kaydı/rolü olanlar tekrar kaydedilmez; iptal edilmiş erişim açılmaz. Pasif veya kuruma bağlı öğrenci profili üzerine bağımsız profil yazılmaz. Gerçek kullanıcıya doğrudan SQL ile erişim verilmedi; katılım kullanıcının normal form üzerinden mevcut şifre ve yasal kabul işlemleriyle tamamlanır.

Bu yayın yalnız mevcut hesaba **Koçluk öğrenci** erişimi ekleyen yolu tamamlar. Koçluk hesabından Hızlı Okuma'ya aynı kayıt formu mantığıyla katılım ve mevcut öğretmen/kurum hesaplarının ikinci servise kendilerinin katılması bu değişiklikte tamamlanmadı. Ortak kimlik modeli ve personel paneli bir hesapta iki ürünü destekler. İkinci ürün için rol ve erişim ayrı atanır; öğretmen profili ve kurum bağlantısı da ilgili servis için doğru kurulmalıdır. Ürün erişimi abonelik satın alımı anlamına gelmez.

## Doğrulama

- İlgili backend testleri: 53 başarılı; `artifacts/coaching-registration-related-integration-tests.log`. İzole kaynakta 18 testlik alt küme de başarılı; bu sayı 53'e eklenmez.
- Öğrenci kayıt ekranı: 6 başarılı; `artifacts/coaching-registration-student-ui-tests.log`.
- Identity integration test projesi ve izole production API/frontend derlemeleri başarılı. Frontend mevcut SCSS bütçe uyarıları hata sınırının altında.
- Gerçek PostgreSQL, gerçek repository/kimlik/parola/yasal kabul servisleriyle ayrı veritabanında 7 senaryo: başarılı katılım, mevcut bağımsız profil, yanlış şifre, pasif profil, iptal edilmiş erişim, MFA, gerçek FK hatasında rollback. Yeni context ile eski kimlik ve Hızlı Okuma erişiminin korunduğu doğrulandı. Test veritabanı sonrasında kaldırıldı; yedek sunucuda korundu.
- Canlı kayıt sayfasında yeni açıklama görüldü. Yetkilendirilmiş test öğrenci hesabıyla normal giriş sonrası `/coaching-portal` açıldı; mevcut haftalık plan ve ödev göründü. Kullanıcının çalışma verileri değiştirilmedi.
- Identity, Coaching ve Notification readiness 200. Kayıt sayfası, öğrenci paneli ve staff adresi HTTP 200. Bu HTTP kontrolleri dört rolde işlevsel uçtan uca test yerine geçmez.
- Canlıya doğrudan otomatik kayıt isteği eksik reCAPTCHA nedeniyle 403 `Auth.CaptchaFailed` aldı. Bu istek canlı katılım iş mantığını doğrulamaz. CAPTCHA koruması kapatılmadı; gerçek kullanıcının kendi hesabıyla formu tamamlaması beklenir.
- `refresh-token` 400 ve tarayıcı eklentisi listener kayıtlarının bu yayınla düzeldiği iddia edilmez; kayıt 409'un somut nedeni ortak hesaptaki e-posta çakışmasıydı.

## Yayın ve geri dönüş

Yalnız Identity ve admin-panel yayımlandı; migration yok. Mevcut doğrulanmış Koçluk kaynaklarından izole paket hazırlanarak çalışma alanındaki başka servislerin yayımlanmamış değişiklikleri dışarıda bırakıldı. Diğer canlı container kimlikleri korunarak kontrol edildi. C# / TypeScript / dağıtım incelemeleri onaylandı.

- İmajlar: `eduivme/identity-service:coaching-registration-20261014`, `eduivme/admin-panel:coaching-registration-20261014`.
- Sunucu yayın dizini: `/var/lib/eduivme/releases/coaching-registration-20261014` (özel erişim).
- Paket SHA256: `76882AE28DC3DACF5F2C986FEC83281266DC2B397AFB5AC8DD7EB63FAC8CD197`.
- Son `deploy.sh validate` başarılı; iki container running, restart sayısı 0.
- Geri dönüş konfigürasyonu `rollback.compose.json` önceki iki imajı sabitler. Başarısız promotion otomatik geri dönüş yapar, hem imaj kimliğini hem sağlık durumunu doğrular. Sonraki elle geri dönüşte de aynı iki servis `--no-deps --pull never` ile açılmalı ve iki imaj/sağlık kontrolü tekrarlanmalıdır. Veritabanı değişikliği olmadığından schema rollback gerekmez.

Canlı ekran kanıtları: `artifacts/coaching-registration-20261014/live-registration.png`, `artifacts/coaching-registration-20261014/live-student-login.png`. Parolalar rapora veya pakete kaydedilmez.
