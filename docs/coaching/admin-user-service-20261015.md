# Admin yeni kullanıcı formunda servis seçimi

15 Ekim 2026 tarihinde canlıya alındı. Uygulama kaynak commit'i `a12f470a8af78e38369c010d23a03cc4b15be2d6`; prova hazırlığı düzeltmesi `69c2c439`. Canlı geçiş: `2026-10-15T11:24:34Z` (Türkiye 14:24).

## Kullanım

`https://eduivme.com/dashboard/identity/users` → **Kullanıcı Ekle** → bilgileri ve rolü doldur → **Servis: Koçluk / Hızlı Okuma** → **Kullanıcı Oluştur**.

Öğrenci, öğretmen, kurum yöneticisi ve kurum sahibi dahil servis rollerinde seçim zorunludur; başlangıçta servis seçili gelmez. Başarı ekranı oluşturulan rolü ve servisi gösterir. İstek sürerken tekrar kayıt ve kapatma engellenir; hata olursa seçim korunur.

SystemAdmin genel rol olarak kalır: servis alanı yerine açıklama görünür ve sunucuya `product: null` gönderilir. Önceden seçilmiş servise ayrıca erişim açılmaz. Abonelik ve kurum bağlantıları bu form tarafından oluşturulmaz/değiştirilmez; mevcut ayrı yönetim akışları sürer. Mevcut e-posta adresiyle yeni hesap oluşturma isteği mevcut hesabı veya başka servisteki bağlantılarını değiştirmez.

## Sunucu davranışı

Eski `CreateUserCommandHandler` Koçluk'a sabitti. Artık seçilen servise rol ve aktif ürün erişimi tanımlanır. Koçluk öğrenci/öğretmen profili yalnız Koçluk seçildiğinde oluşturulur; Hızlı Okuma hesabı Koçluk profil tablosuna eklenmez.

`CreateUserCommand.Product`, eski constructor imzasını bozmayan bir init özelliğidir. Yeni form açıkça `1` veya `2` gönderir. Açık `null`, `0`, `3` ve diğer geçersiz ürünler servis rollerinde kayıt yapılmadan reddedilir. Eski çağrılarda alan tamamen yoksa Koçluk varsayılanı korunur; eski CSV içe aktarma derlenmiş constructor yoluyla da doğrulandı. SystemAdmin sunucuda da genel kapsama normalize edilir.

`UserCreatedEvent.Product` da eski constructor/deconstruct imzalarını koruyan opsiyonel init özelliğidir. Değeri istemciden doğrudan alınmaz; sunucu seçilen enum'u canonical `coaching` / `speed-reading` değerine çevirir.

- Notification tüketicisi davet şablonunu, marka adını ve parola kurulum bağlantısını açık servis bilgisinden seçer. Koçluk: `https://eduivme.com`; Hızlı Okuma: `https://masterhizliokuma.com`. Eski event'lerdeki null kapsamın davranışı korunur. Public URL yardımcılarının mevcut metot imzaları korundu, ürün alan overload'lar eklendi.
- Coaching tüketicisi açıkça başka servise ait olaylarda karşılama hedefi oluşturmaz. Eski null kapsamlı olayları işlemeye devam eder.

Kullanıcı oluşturma mevcut iki Save ve hata halinde telafi silmesi düzenini korur; bu yayın akışı tek veritabanı transaction'ına dönüştürmez. Hata testi telafi çağrısını doğrular; gerçek veritabanı rollback'inin tamamını kanıtladığı iddia edilmez.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| Admin bileşen/servis regresyonları | 48 geçti |
| Kullanıcı oluşturma, olay tüketimi, profil ve ürün kapsamı backend testleri | 61 geçti; başarısız/atlanan yok |
| Yalıtılmış canlı PostgreSQL kopyasında gerçek HTTP ve broker provası | 15 senaryo geçti |
| Yerel üretim admin derlemesinde masaüstü/mobil yeni kullanıcı akışları | 18 senaryo geçti |
| Canlıdan sunulan admin arayüzünde aynı yeni kullanıcı akışları | 18 senaryo geçti |
| Canlı arayüzde önceki tekli/toplu rol yönetimi regresyonu | 20 kontrol geçti |

Gerçek API provasında dört rol × iki servis için oluşturma, seçili rol/erişim, yalnız Koçluk profili, gerçek broker olayının ürün alanı, tek kullanımlık parola kurma, seçilen servise giriş ve diğer servise girişin reddi kontrol edildi. Geçersiz ürün, anonim oluşturma, MFA ve eski CSV import yolu ayrıca sınandı. Her senaryo ayrı yeni `.test` adresi ve UUID kullandı.

İlk prova sekiz hesap döngüsünü geçti; son MFA kontrolü kopyadaki mevcut politikanın disabled olması nedeniyle beklenen 403'ü alamadı. Yalnız doğrulayıcı düzeltildi: doğrulanmış kopya API'de `users` MFA politikası required yapıldıktan sonra MFA'sız isteğin 403 olduğu doğrulandı. Uygulama DLL'leri bu test hazırlığı değişikliğinde yenilenmedi. **Canlı MFA ve CAPTCHA ayarları değiştirilmedi.** CAPTCHA yalnız yayınlanmayan prova API'sinde kapalıdır; bu sonuç gerçek CAPTCHA/human login testi değildir.

Prova yalnız isim kontrolü yapılan geçici veritabanını, ayrı RabbitMQ ve Redis'i kullandı; dışarı port yayımlanmadı. Gerçek kullanıcılara e-posta gönderilmedi. Sonunda prova konteynerleri ve veritabanı kaldırıldı. Tarayıcı kontrollerinde bütün API/oturum çağrıları fixture ile karşılandı; canlı kullanıcılar oluşturulmadı veya değiştirilmedi.

Kanıtlar: `artifacts/admin-user-product-tests/admin-user-product-confirmed.trx`, `artifacts/admin-user-product-relational-results.json`, `artifacts/admin-user-service-local/`, `artifacts/admin-user-service-live/`, `artifacts/admin-role-services-live/`.

## Yayın ve geri dönüş

Migration yok. Kimlik kaynakları gerçek canlı `838fc1b0` temelinden yalnız bu görevin yedi backend dosyasıyla; admin gerçek canlı `98438809` temelinden yalnız üç dosyayla hazırlandı. Coaching/Notification Application ve Shared.Contracts kaynaklarının kendi canlı `ac014834` temelinden `838fc1b0`'a değişmediği doğrulandı. Mevcut imajlara yalnız ilgili Application + Shared.Contracts DLL/PDB dosyaları katmanlandı; API/Infrastructure ve diğer bağımlılıklar korundu. Çalışma alanındaki diğer değişiklikler yayımlanmadı.

Sıra: uyumlu **Coaching tüketicisi → Notification tüketicisi → Identity üreticisi → admin**. Gerçek ortam değerleri ve diğer servislerin gerçek imajları sabitlendi. Aday imaj kimlikleri, payload hash'leri, ağsız admin önizlemesi ve prova sonucu promotion önkoşuludur. Yeni imaj build'inde eski prova marker'ları geçersizleştirilir; prova kimlikleri promotion öncesi tekrar kontrol edilir.

Identity geçişi başladıktan sonra geri dönüş gerekirse admin ve Identity önceki imajlarına döner; kuyrukta kalabilen yeni servis bilgili olaylar için uyumlu yeni tüketiciler korunur ve sağlık/imajları doğrulanır. Üretici geçişinden önceki hatalarda tüm hedefler önceki imajlarına dönebilir.

Yeni imajlar:

- Coaching: `sha256:79ce347b0e44bda398dd60e0891d09adeca0de1eba7adf60987187f84adfafd2`
- Notification: `sha256:bb0195b628c121eaa71629e7bb5acacc26813d873c7f6dcdad8b05a0b69ce53d`
- Identity: `sha256:3e3cad93d2c8619feca414821e24623dc7c0ca2c51a32c877248aefc6000b7c1`
- Admin: `sha256:9aa7fc3cfff2d4ca1df6c5674d1175e568d2829ac5965285c516b17371bb3c1d`

Yayın sonu dört servis running/restart 0; diğer servis konteyner kimlikleri aynı. Betikler `tools/releases/admin-user-service-20261015/`, root erişimli sunucu dizini `/var/lib/eduivme/releases/admin-user-service-20261015`.
