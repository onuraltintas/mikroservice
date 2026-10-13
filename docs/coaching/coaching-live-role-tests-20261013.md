# Koçluk canlı rol testleri — 13 Ekim 2026

## Sonuç

Kullanıcı canlı yayını, migration uygulamasını ve dört panelde temel akışları test etmeyi; daha sonra eksik öğretmen/öğrenci/kurum test hesaplarını oluşturmamızı açıkça yetkilendirdi. Gerçek admin `admin@eduivme.com` ve üç yeni sentetik rol hesabıyla normal canlı girişler yapıldı. Haftalık plan ve finans döngüleri tamamlandı. Uygulama imajları önceki doğrulanmış `coaching-completion-20261013-ac014834` sürümünde kaldı; rol testi için yeni uygulama veya migration yayımlanmadı.

| Rol / akış | Canlıda gözlenen sonuç |
| --- | --- |
| Öğretmen girişi ve öğrenci ilişkisi | Yalnız Koçluk/Teacher erişimi; kendi test kurumundaki 1 öğrenci listelendi, raporu açıldı. |
| Öğretmen planı | Taslak kaydetme → açık onay → yayımlama başarılı; kaynak/sayfa, 45 dakika, 30 soru öğrenciye yansıdı. |
| Öğrenci girişi ve tamamlama | Normal Identity girişi; kendi planındaki görev 40 dakika/24 soru olarak tamamlandı. |
| Öğretmen raporu | 12–18 Ekim: 1/1, %100, 45/40 dakika, 30/24 soru; 5–11 Ekim önceki haftayla karşılaştırma açıldı. |
| Kurum öğrencisi ve raporu | Kurumun aynı öğrencisi/öğretmeni doğrulandı; aynı çalışma ve haftalık karşılaştırma görüldü. Plan oluşturma/tamamlama kontrolleri yok. |
| Kurum lisansı / öğrenci erişimi | Özel ücretsiz deneme 20 Ekim'e kadar aktif; kurumda 1/1 koltuk, öğrencide aynı lisans ve “Erişim açık”. |
| Uygulama içi bildirim | Öğrenci ekranında `StudyPlanPublished` ve `StudyPlanReminder` gerçek kayıtları göründü; canlı bağlantı anlık güncelleniyor. |
| Kurum finans yazımı | 100 TL tek taksitli sentetik ücret ve 25 TL nakit manuel tahsilat kaydı başarılı; kalan 75 TL. Banka işlemi yapılmadı. |
| Öğrenci finansı | 100/25/75 TL toplam/net/kalan; ücret oluşturma/tahsilat düğmeleri yok. |
| Admin finans denetimi | Test ücretinin 75 TL açık borcu, 25 TL net kaydı, hesabı ve geçmişi görünür; oluşturma/tahsilat/düzeltme düğmeleri yok. |
| Finans testini kapatma | Kurum 25 TL negatif düzeltme ve ücret iptali yaptı; borç ve net tahsilat 0. Admin açık ücret/kasa yenilemesi de 0; iki denetim hareketi korunuyor. |
| Kuruma bağlı öğretmenin finans kapsamı | Kurum ücret kaydı öğretmenin hesabında görünmedi; yeni ücret/tahsilat düğmesi yok. Öğretmenin kendi eski bağımsız ücretleri için genel bilgilendirme metni ayrı kapsamda kalıyor. |

Öğrenci çalışma kaydı 19:42:23 UTC / 22:42:23 İstanbul'da tamamlandı. Salt okunur veritabanı kontrolünde tek aktif plan revizyonu `04d47c42-ee36-467b-8cab-52a28d06b467`, tamamlanan görevde planlanan/gerçek dakika `45/40`, soru `30/24` doğrulandı. Ücret `98727c97-f5dd-49f5-95ce-89c027fb1746` iptal edilmiş, net tahsilatı `0.00`, iki hareketin toplamı `0.00`.

## Test verisi ve hazırlık güvenliği

Fixture etiketi `coaching-live-20261013-r1`, kurum `fc4a2a09-87de-46d8-b034-f09f7dae8850`. Hesaplar gerçek kişiye ait değildir:

| Rol | Test e-postası |
| --- | --- |
| Teacher | `testogretmen@eduivme.com` |
| Student | `testogrenci@eduivme.com` |
| InstitutionAdmin | `testkurum@eduivme.com` |

Tablo güncel giriş adreslerini gösterir. İlk canlı döngüde `teacher/student/manager.coaching-live-20261013-r1@example.invalid` adresleri kullanıldı; aşağıdaki hesap güncellemesi aynı kullanıcı kimliklerini korur.

Hesap oluşturma terminalden, gerçek domain/EF modelleri ve üretim PasswordHasher ile yapıldı; kayıt formu/e-posta teslimatı testi olarak sunulmuyor. Sentetik hesapların e-postaları operatör tarafından doğrulanmış başlangıç durumunda oluşturuldu. Her hesapta yalnız tek Koçluk rolü ve ürün erişimi var; yeni SystemAdmin oluşturulmadı. Mevcut admin yalnız kayıt aktör kimliği olarak okunuyor. Kurum limiti 1 öğretmen/1 öğrenci, lisans özel/ücretsiz/7 gün/1 koltuk. Genel erişim ayarları, MFA, CAPTCHA veya JWT denetimi değiştirilmedi; tarayıcıya token veya sahte oturum yerleştirilmedi.

İki üretim veritabanı yeni yedeklerle ayrı kopyalara geri yüklendi. Fixture önce kopyalarda çalıştı; yinelenen hesap oluşturma, canlı hedefte yetki bayrağı eksikliği, aktif öğretmen–öğrenci ilişkisi bozulması ve lisans iptali kontrolleri beklenen şekilde reddedildi. İlişki/lisans negatif değişiklikleri yalnız kopyalarda yapıldı. Prova 19:36:28 UTC'de, canlı oluşturma 19:37:59 UTC'de tamamlandı. Üretim hedefinde yalnız bu fixture'ın yeni kayıtlarına yazıldı; mevcut hesaplar ve kurumlar üzerine yazılmadı.

Gerçek yayımlanan API derlemeleri .NET 10; Identity EF 9.0.1, Coaching EF 9.0.2 kullanıyor. Fixture bu nedenle ayrı iki çalışma dosyasıyla derleniyor. İlk hazırlık denemelerindeki framework/EF referans farkı, .NET 10 array/Contains çevirisi ve Jsonb dynamic JSON yapılandırması prova sırasında düzeltildi. Son kaynakta List.Contains ve üretim DI ile eşleşen EnableDynamicJson kullanılıyor; iki son publish hatasız ve uyarısız. Son DLL özetleri canlı fixture paketindeki dosyalarla aynı. İki bağımsız kod/C# incelemesinde engelleyici bulgu yok.

## Kanıtlar ve saklama

Yerel kanıt dizini: `artifacts/coaching-live-20261013/`.

- `teacher-plan-published-live.png`, `student-plan-completed-live.png`, `teacher-weekly-report-live.png`, `institution-weekly-report-live.png`
- `institution-subscription-live.png`, `student-subscription-live.png`, `student-notifications-live.png`
- `student-finance-live.png`, `institution-finance-live.png`, `admin-fixture-finance-live.png`
- `institution-finance-cancelled-live.png`, `admin-finance-zero-live.png`, `teacher-finance-scope-live.png`
- `roles-evidence/`: parolasız fixture kapsamı, pozitif/negatif prova kanıtları, zaman işaretleri, yedek özetleri ve `role-db-proof.log`.
- `role-fixture-identity-final-build.log`, `role-fixture-coaching-final-build.log`, `live-smoke-after-roles.log`.

Son fixture arşivi `coaching-role-fixtures-final.tar.gz`; SHA256 yerel/sunucu eşleşmesi `840d211d11f98a2afe9946e84268fe4e377b40dfdc81d9a44e5a9ffc0941d080`. Manifestte 176 içerik dosyası, manifestle toplam 177 dosya var; tamamı sunucuda tekrar doğrulandı. Önceki deneme arşivleri son paket değildir.

Özel sunucu dizini `/var/lib/eduivme/releases/coaching-live-role-tests-20261013-r1` root erişiminde. Parolalar yalnız 0700/0600 korumalı state dosyasında; rapora, Git'e veya public kanıt paketine eklenmedi. Test hesapları/kurumu ve tamamlanan plan kanıt için korunuyor; özel lisans kullanıcının sonraki isteğiyle 20 Kasım 2026'ya uzatıldı. Sentetik finans kaydı iptal edildi ve net 0, geçmişi korunuyor. Personel test oturumu kapatıldı. İki kendi prova veritabanımız 19:56:26 UTC'de temizlendi; üretim yedekleri korundu.

Yeni rol testi öncesi yedek SHA256:

| Veritabanı | SHA256 |
| --- | --- |
| identity_db | `bcd2ad40dfa0e202b8dc220841e572af929fa0dc603ee87027b351b3a2f8a292` |
| coaching_db | `cd86216341eaa9500b27f8c5caee9806e12db6869e26b5a7ea7f3dfd9003e66d` |

## Son sağlık kontrolü ve sınırlar

19:53:13 UTC / 22:53:13 İstanbul: üç API Healthy ve yayın başlangıcından itibaren log hata sayısı 0; beş hedef serviste restart 0. Migration geçmişi birebir, çakışma trigger'ları 4, finans/hatırlatma tabloları 4. Altı anonim korumalı uç 401; dört public adres 200; üç eski koçluk adresi 302. Hedef dışı container kimlikleri/imajları yayından öncekiyle aynı.

Bu rapor dört rolün temel canlı akışlarını kanıtlar. Yerel 767 backend, 308 öğrenci/admin, 180 personel ve 12 farklı E2E sayıları tümünün canlıda tekrar koşulduğu anlamına gelmez. Süresi bitmiş lisans ve başka kuruma erişim gibi negatif senaryoların tümü canlıda yeniden yürütülmedi; canlı genel ayarlar değiştirilmedi. Gerçek ödeme sağlayıcısı/Zoom/SMS teslimatı veya yeni kullanıcı kayıt formu/e-posta doğrulama zinciri test edilmedi. Soru bankası → süreli sınav → otomatik puanlama kullanıcının kararıyla ileride ayrı servis kapsamındadır.

## Test hesaplarını güncelleme — 23:20 İstanbul

Kullanıcının açık isteğiyle üç test hesabı yukarıdaki `eduivme.com` adreslerine taşındı ve verilen ortak test parolası üretim PasswordHasher ile ayrı salt'larla hashlenerek uygulandı. Eski aktif refresh tokenları iptal edildi, parola sıfırlama tokenları temizlendi. Kullanıcı kimlikleri, Koçluk rolleri, kurum/profil ilişkileri, öğrenci ataması ve tamamlanan çalışma planı korundu. Bu işlem gerçek kişilerin hesaplarına veya admin parolasına uygulanmadı.

Kendi kurumunun Identity lisansı ile Coaching aboneliğinin mevcut 20 Ekim bitişlerine bir takvim ayı eklendi. Yeni bitişler sırasıyla `2026-11-20T19:37:47.682044Z` ve `2026-11-20T19:37:53.070508Z`; İstanbul'da 20 Kasım 22:37. Özel ücretsiz plan ve 1/1 koltuk aynı kaldı; katalog planının süresi değiştirilmedi, yalnız mevcut abonelik uzatıldı.

Bakım aracı `tools/releases/coaching-test-account-update-20261013/` altında. Sabit fixture kimlikleri/kurum/rol/lisans denetimleri ve başka hesaba ait e-posta çakışma koruması var. Parola kaynak koduna girmez; özel request dosyasından okunur. İki çalışma derlemesi ve bağımsız kod/C# incelemeleri geçti. Yeni üretim yedekleri iki özel kopyaya geri yüklendi; e-posta/parola/lisans doğrulamaları ve ikinci çalıştırmada ek ay eklememe kontrolü başarılı (20:19:37 UTC). Canlı güncelleme ve hash/tarih doğrulaması 20:20:30 UTC / 23:20:30 İstanbul'da tamamlandı. Yeni uygulama imajı veya migration yayımlanmadı.

Üç hesapla da normal canlı giriş yeniden yapıldı. Öğrenci abonelik ekranında `Aktif · 13.10.2026 – 20.11.2026 / Erişim açık`, kurum ekranında `20.11.2026 tarihine kadar`, `1/1` ve yeni öğrenci e-postası görüldü. Öğretmenin 1 öğrencisi ve öğrencinin mevcut tamamlanmış planı korundu. Test oturumları kapatıldı; yalnız bu doğrulama için açılan sekmeler temizlendi. Yeni iki prova veritabanı 20:22:10 UTC'de temizlendi; yedekler sunucuda korundu. Son kontrolde Identity ve Coaching Healthy.

Yeni kanıtlar `artifacts/coaching-account-update-20261013/`: üç ekran (`teacher-login-updated.png`, `student-subscription-updated.png`, `institution-subscription-updated.png`), derleme günlükleri ve `evidence/` altında parolasız prova/canlı doğrulamaları. Bakım arşivi SHA256: `06a5930d37a062a761e4cfb898bf75ef5235fe199fe6e9c8659f28aa99586e32`, sunucuda aynı özet doğrulandı. Parola girdi dosyasının yerel geçici kopyası temizlendi; korumalı sunucu state kaydı yeni bilgilerle güncellendi. Eski fixture verifier eski `example.invalid` adreslerini beklediğinden bundan sonra yeni bakım dizininde `bash update.sh verify` kullanılmalıdır.
