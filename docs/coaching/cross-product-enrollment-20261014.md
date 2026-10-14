# Koçluk ve Hızlı Okuma arasında mevcut hesapla katılım

14 Ekim 2026 tarihinde canlıya alındı. Identity ve Hızlı Okuma API yayını 12:59:33; giriş alan adı düzeltmesini içeren son iki ön yüz yayını 13:10:32 (Europe/Istanbul).

## Tamamlanan davranış

Öğrenci, öğretmen ve kurum hesapları hem Hızlı Okuma → Koçluk hem Koçluk → Hızlı Okuma yönünde aynı e-posta ve mevcut şifreyle katılabilir. Altı kayıt ekranındaki **Mevcut hesabımla katıl** seçeneği yeni hesap formundan ayrı çalışır. MFA etkin hesaplar yeni altı haneli uygulama kodunu girer. Katılım yeni oturum üretmez; tamamlandıktan sonra mevcut şifreyle ilgili ürünün giriş ekranı açılır.

| Hesap | Koçluk katılım ekranı | Hızlı Okuma katılım ekranı |
| --- | --- | --- |
| Öğrenci | https://eduivme.com/auth/register/student | https://masterhizliokuma.com/auth/register |
| Öğretmen | https://eduivme.com/auth/register/teacher | https://masterhizliokuma.com/auth/register-teacher |
| Kurum | https://eduivme.com/auth/register/institution | https://masterhizliokuma.com/auth/register-school |

Öğrenci girişleri Koçluk için `eduivme.com/auth/login`, Hızlı Okuma için `masterhizliokuma.com/auth/login` adresine gider. Öğretmen ve kurum girişleri `onuraltintas.net/staff/?product=coaching` veya `?product=speed-reading` adresini kullanır. Onuraltintas Koçluk/staff alanıdır; bağımsız Hızlı Okuma kayıt yolları bu alanda yayımlanmaz.

API: `POST /api/auth/{coaching|speed-reading}/register/join`. Route ürün bilgisini belirler; body ile diğer ürün yetkisi istenemez. Body yalnız e-posta, mevcut şifre, hesap türü, gerekirse MFA kodu ve güncel yasal metin sürümlerini taşır. Normal öğrenci/öğretmen/kurum kayıt handler'ları da mevcut e-posta durumunda aynı katılım iş mantığına başvurur.

## Korunan kayıtlar ve yetki sınırları

- Kullanıcı kimliği, e-posta, ad/soyad, şifre hash/salt ve e-posta doğrulaması korunur. Var olan kullanıcı silinmez veya yeniden oluşturulmaz.
- Var olan öğrenci/öğretmen profil kimliği ve kurum bağlantısı korunur. Ortak profil yoksa yalnız Koçluk katılımında bağımsız profil oluşturulur; kaynak ürünün kendi üyelikleri değiştirilmez.
- Kurum katılımında mevcut aktif kurum kimliği ve Owner/Admin/Manager bağlantı rolü korunur; yeni kurum oluşturulmaz. Manager kendiliğinden Owner yapılmaz. Owner için kaynak ürünün Owner rolü ayrıca doğrulanır.
- Ürün erişimi, ürün kapsamındaki rol, gerektiğinde profil/kurum bağlantısı ve hedef ürünün üç yasal kabul kaydı aynı SaveChanges içinde yazılır. Abonelik, borç, tahsilat veya çalışma verileri değiştirilmez; katılım satın alma yerine geçmez.
- Aktif, doğrulanmış hesap ve kaynak üründe eşleşen rol gerekir. Öğretmen hesabı öğrenci formuyla kendisine öğrenci rolü ekleyemez. İptal edilmiş hedef erişimi açılmaz. Tekrarlı katılım HTTP409 döndürür.
- Birden fazla aktif kaynak kurum bağlantısı otomatik katılım için belirsiz kabul edilir ve reddedilir. Bu durumda kurum seçimi yönetici tarafından netleştirilmelidir; mevcut bağlantılar değiştirilmez.
- Katılım ile MFA giriş doğrulaması aynı kullanıcıya ait PostgreSQL advisory lock altında yeniden okunur. MFA kodu tekrar kullanımı, kayıp başarısız-deneme sayacı ve lockout yarışı engellenir. Lock bağlantısı normal EF havuzundan ayrıdır; iptal ve dispose kilidi bırakır.
- Hızlı Okuma kurum ilerleme ve öğrenci araması artık Hızlı Okuma'nın aktif üyelikleri ve canlı ürün/kurum yetkisiyle sınırlandırılır. Koçluk kurum listesi Hızlı Okuma raporu için kullanılmaz. SystemAdmin'ın global rapor erişimi canlı hesap/rol kontrolünden sonra korunur. Koçluk kurum yetkilendirmesi de Koçluk rolü, erişimi ve kurum bağlantısıyla sınırlandırılır.

## Doğrulama kanıtları

- Son ilgili Identity test koşumu: 102 başarılı. Speed Reading kurum/öğretmen kapsam testleri: 14 başarılı.
- Ön yüz testleri: admin31 ve Speed Reading14; toplam otomatik birim/entegrasyon kontrolü161. Ortak stil/tip dosyaları dahil gerçek izole kaynaklardan dört production derlemesi başarılı; mevcut Sass/SCSS bütçe uyarıları hata değildir.
- Canlı veritabanı yedeğinden oluşturulmuş özel PostgreSQL kopyasında yeni API'nin gerçek HTTP uçlarıyla21 kontrol başarılı. Altı rol/yön, bağımsız profil, kurumOwner, tekrarlı kayıt, yanlış şifre/rol, iptal edilmiş erişim, eksik yasal/MFA bilgisi ve pasif profil doğrulandı. Gerçek FK hatasında transaction rollback, eşzamanlı MFA giriş/katılım, beş paralel hatalı kodun lockout'u ve iptal edilmiş kilit bekleyicisinin temizlenmesi test edildi. MFA kod eşleştirmesi bu yarış testinde kontrollü stub'dır; PostgreSQL kilidi, yeniden okuma ve kayıtlar gerçektir.
- Prova API'si portsuz; veritabanı, RabbitMQ, Redis ve DataProtection anahtar alanı canlıdan ayrıdır. CAPTCHA yalnız bu özel Development kopyasında kapalıdır. Başarılı prova sonunda kopya veritabanı ve geçici container'lar kaldırıldı; özel yedek korundu.
- Canlı altı kayıt formu temiz Chrome oturumlarında açıldı. Mevcut hesap formu, şifre/MFA alanları, üç güncel yasal metnin yüklenmesi ve eksik bilgi/onayla gönderimin engellenmesi doğrulandı. Yasal onay kutuları işaretlenmedi, gerçek kayıt gönderilmedi.
- Altı kayıt URL'si, öğrenci paneli ve staff girişi HTTP200; iki yeni katılım uç noktası CAPTCHA olmadan HTTP403 `Auth.CaptchaFailed` döndürdü. Bu son kontrol korumayı doğrular, başarılı canlı kayıt yerine geçmez.
- Yetkili üç test hesabıyla normal canlı UI giriş denemeleri headless CAPTCHA tarafından HTTP403 ile reddedildi. Öğrenci/öğretmen/kurum panel oturumları bu yayında otomatik olarak doğrulanmış sayılmaz. CAPTCHA kapatılmadı veya atlanmadı.
- Canlı test hesaplarının kullanıcı, rol, erişim, kurum, kurum yönetici bağlantısı ve profil satırları yayın öncesi/sonrası hash karşılaştırmasında aynı. Test hesaplarının mevcut şifreleri, abonelikleri ve çalışma kayıtları değiştirilmedi.

Kanıtlar: `artifacts/cross-product-relational-results.json`, `artifacts/cross-product-live-public-smoke.json`, `artifacts/cross-product-live-ui/results.json`, `artifacts/cross-product-live-ui/role-results.json` ve aynı dizindeki altı gerçek ekran görüntüsü. Başarısız canlı giriş görüntüleri başarılı panel kanıtı değildir.

## Yayın ve geri dönüş

API kaynak commit'i `bb4e1bb320da105c533164794541843a5d3fe963`; son ön yüz kaynak commit'i `2c0bbf21accb674b0286ba5d30b89cde95fde380`. Prova betiğinin izole servis açılışı/ham environment satırları ve testin PostgreSQL tarih hassasiyeti düzeltmeleri ayrı helper commit'lerindedir; test helper'ın son kaynak commit'i `fffeb0b5`. Ön yüz takip yayın betiği `69bf2f34`.

- API imaj etiketi: `cross-product-enrollment-20261014`; iki ön yüzün son etiketi `cross-product-enrollment-20261014-r2`.
- API ve ilk dört servis yayını: `/var/lib/eduivme/releases/cross-product-enrollment-20261014`.
- Son iki ön yüz yayını: `/var/lib/eduivme/releases/cross-product-enrollment-origin-20261014`.
- İlk paket SHA256: `24557fdfff88a9b470286d43e7783d86dd5200a8ebc25e33674369cd84ea306f`; son ön yüz paketi SHA256: `12de3c7d71959c3f3d4d65803ca30507eda73d5fbc08ac9556609dd80a81aab3`.
- Sunucuda her payload SHA256 manifesti doğrulandı. İmajlar önceki gerçek canlı kaynaklarına yalnız ilgili değişiklikler uygulanarak hazırlandı; çalışma alanındaki başka servis/Google/Course Management değişiklikleri yayımlanmadı.
- Yeni migration yok. İki API ve iki ön yüz running, restart0; hedef dışı container kimlikleri korunmuş durumda.
- API yayınının `rollback.compose.json` dosyası önceki dört imajı; ön yüz takip yayınının `previous.compose.json` dosyası önceki iki ön yüzü sabitler. Promotion hatasında yalnız ilgili hedefler `--no-deps --pull never` ile geri alınır; eski image ID ve sağlık kontrolleri zorunludur. Son durumu takip yayınındaki `frontend-origin.sh validate` ve iki API'nin `/health/ready` kontrolüyle doğrulayın; ilk yayın validate'i son r2 ön yüzleri beklemez.

İlk öğrenci-only yayın raporu tarihsel kayıttır: [önceki yayın](coaching-registration-existing-account-20261014.md). Bu yayın o rapordaki ters yön, öğretmen/kurum ve MFA katılımı eksiklerini tamamlar.
