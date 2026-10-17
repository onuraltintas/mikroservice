# Haftalık takvim — canlı sürüm, 17 Ekim 2026

Haftalık takvim `https://eduivme.com/coaching-portal/weekly-plan` adresinde canlıya alındı.
Öğrenci menüsünde **Haftalık programım** adıyla bulunur. Gün/hafta/ay, sağ tıkla ders ekleme,
30/60 dakikalık kart taşıma ve iki kenardan boyutlandırma, haftalık taslak kaydı yayındadır.
Kazanımları 1 Haziran'a kadar yıllık dağıtma bu sürüme dahil değildir.

## Yayın

| Bilgi | Değer |
|---|---|
| Yayın kimliği | `coaching-weekly-calendar-20261017` |
| Uygulama kontrol noktası | `12951c5a5a1d674bc14480e9d0d5301bf7e58188` |
| Önceki canlı kaynak | `dfbb4335e885328df8b43f85c1100f99c1d19c8e` |
| Canlıya geçiş | 17.10.2026 05:50:17 UTC / 08:50:17 Türkiye |
| Paket SHA256 | `8e3f89873abcb2b397507040f98ced28fd1e4312c50a65c62b97668347aab612` |
| Coaching imajı | `6d39d2d0e881100d581196fc655b6a7a31e78546845210bb5a194cd6d0983421` |
| Arayüz imajı | `efd0ead5bf0455f077b103d992a382f3365c16e78c141f9911a6bbe1fb6b989a` |

Önceki canlı kaynaklara yalnız 20 üretim dosyasının değişiklikleri uygulandı. Çalışma
klasöründeki diğer değişiklikler yayına alınmadı. Linux için dört Coaching DLL/PDB'si
ve izole üretim arayüzü derlendi. Shared bağımlılıklar mevcut canlı tabandan korundu.
Paketin ve 304 dosyalık yükün SHA256 kontrolleri sunucuda geçti.

Yalnız `coaching-service` ve `admin-panel` değiştirildi. Identity, hızlı okuma, personel
paneli ve diğer servislerin konteyner kimlikleri korundu. Kimlik/rol/erişim değiştirilmedi.

## Geçiş ve mevcut veriler

`20261016213358_WeeklyStudyDraft` geçişi yalnız
`coaching.study_weekly_drafts` tablosunu ve öğrenci bazında benzersiz indeksini ekledi.
Eski hesaplara, planlara ve çalışma kayıtlarına dönüştürme veya silme uygulanmadı.
Yeni taslak StudentId alanında ortak UserId kullanır; kayıtlı ders kartları JSONB'dir.

Üretim Coaching yedeği ayrı bir veritabanına geri yüklendi. Geçiş iki kez çalıştırıldı;
mevcut iş tablolarının satır sayıları ve içerik özetleri değişmedi. Başarıdan sonra
prova veritabanı kaldırıldı. Canlı geçişte iki hedef servis kısa süre durduruldu;
Identity ve Coaching yedekleri alındı. Geçişten önce, sonra ve servisler açılınca hesap
ve mevcut Coaching iş tablolarının özetleri karşılaştırıldı: aynı kaldı.

Canlı tarayıcı testi sonrasında karşılaştırma tekrar geçti:

| Veri | Önce / sonra |
|---|---|
| Mevcut plan | 4 / 4 |
| Mevcut çalışma görevi | 5 / 5 |
| Tamamlanmış görev | 4 / 4 |
| Tamamlanmış gerçek çalışma dakikası | 86 / 86 |
| Katalog kazanımı | 52 / 52 |
| Temel hesap/rol/erişim ve eski iş kayıtları | İçerik özetleri aynı |
| Yeni haftalık taslak | 0 / 1; test öğrencisine ait iki yarım saatlik kart |

Temel hesap karşılaştırmasında normal giriş/MFA zaman ve sayaç alanları dışarıda
tutulur. Eski Coaching iş tabloları tüm alanlarıyla karşılaştırılır; teknik outbox/inbox,
yönetici denetim, geçiş geçmişi ve yeni taslak tablosu bu eski veri özetine dahil değildir.

## Gerçek canlı öğrenci akışı

Önceden açık normal test öğrenci oturumu kullanıldı. Parola, CAPTCHA, MFA veya yetki
değiştirilmedi. API yanıtları taklit edilmedi; gerçek servis ve veritabanı kullanıldı.

| Senaryo | Canlı sonuç |
|---|---|
| Yeni ekran | Menüde Haftalık programım açıldı; başlangıçta taslak ve kart yoktu. |
| Ay → gün → hafta | Ekim ayı açıldı; 19 Ekim günlük görünümüne ve haftaya geçildi. |
| Sağ tıkla ekleme | Pazartesi 18.00 hücresinde menü, sonra ders formu açıldı. |
| Katalog | Altı LGS dersi geldi; Matematik seçilip 60 dakika eklendi. |
| Taşıma | Kart gövdeden sürüklenerek Pazartesi'den Salı'ya taşındı; saat 18.00 kaldı. |
| Alt tutamaç | 18.00–19.00 kartı 18.00–18.30'a küçültüldü. |
| Üst tutamaç | 30 dakika, 17.30–18.30 olarak 60 dakikaya uzatıldı; tekrar 18.00–18.30'a getirildi. |
| Aynı saate ikinci ders | Salı 18.30–19.00 Türkçe eklendi; toplam 60 dk/hafta. |
| Kalıcı kayıt | Taslağı kaydet → başarılı kayıt mesajı; iki kart DB'ye yazıldı. |
| Yeniden açma | Tam sayfa yenileme ve başka ekrandan dönüş sonrası iki kart aynı geldi. |
| Aylık tekrar | Kartlar Salı günlerinde tekrarlandı; haftalık toplam 60 dakika kaldı. |
| Çakışma | Matematik 60 dakika yapılınca Türkçe ile çakışma engellendi; formdan vazgeçilince kayıtlı kartlar korundu. |
| Eski aktif plan | Dört plan listelendi; 12 dk/0 soru ve 17 dk/8 soru kayıtları aynıydı. |
| Eski arşiv plan | 40 dk/24 soru kaydı açıldı; geçmiş planın değiştirilemez olduğu gösterildi. |
| Yönetici arayüzü | Ders ve Müfredat Yönetimi yeni sürümle yeniden yüklendi; sınav türleri ve tarih alanları korundu, kayıt değiştirilmedi. |

Test öğrencisinde Salı 18.00 Matematik ve 18.30 Türkçe kartları **sentetik doğrulama
taslağı** olarak bırakıldı. Bunlar gerçek çalışma veya tamamlanmış görev sayılmaz.
Tarayıcı son durumda kayıtlı haftalık taslak ekranında bırakıldı.

Ekran kanıtları `artifacts/coaching-weekly-calendar-live-20261017/proof/` altında:
`live-saved-week.jpg`, `live-week-reopened.jpg`, `live-month-reopened.jpg`,
`live-day-reopened.jpg`, `live-existing-plan.jpg`, `live-admin-curriculum.jpg`.

## Test dayanağı ve işletim

Önceki uygulama doğrulaması: 51 müfredat/gerçek PostgreSQL testi, 694 mevcut Coaching
regresyon testi, 184 öğrenci arayüz testi başarılı. Yayında ayrıca doğrudan izole
yayın derlemesine karşı sekiz masaüstü/mobil tarayıcı senaryosu tekrar geçti.
Gerçek mobil dokunma olayları yerel tarayıcı testlerinde, canlı fare işlemleri normal
masaüstü öğrenci oturumunda sınandı. İzole üretim derlemeleri başarılıdır; stil boyutu
ve mevcut template uyarıları derleme günlüklerinde korunmuştur.

Canlı son tanılama: iki hedef serviste hata günlüğü satırı 0, yeniden başlatma 0.
`/api/coaching/study-planning/weekly-draft` ve katalog API'si anonim isteğe 401 döndürdü.
Yayın sonrası 31 örnekli sağlık izlemi 06:05:33 UTC / 09:05:33 Türkiye'de başarıyla
tamamlandı; yayın başlangıcından itibaren 15 dakika 16 saniye geçti. Giriş, yeni takvim
ve eski plan adresleri bütün örneklerde HTTP 200 döndürdü. İki servis de çalışır
durumda ve sıfır yeniden başlatmayla kaldı. Bu pencerenin sonunda mevcut hesap ve eski
iş kayıtlarının özetleri bir kez daha karşılaştırıldı: aynı kaldı. Tarayıcıda da yayın
sonrasına ait yeni hata görülmedi.

Son kanıtlar `artifacts/coaching-weekly-calendar-live-20261017/live-final.log`,
`preservation-final.log` ve `diagnostics-final.log` dosyalarıdır.

Yedekler, önceki iki uygulama imajı, sabit compose ve geri dönüş bilgileri yalnız
sunucudaki özel `/var/lib/eduivme/releases/coaching-weekly-calendar-20261017` dizinindedir.
Uygulama geri dönüşü eski iki imajla yapılabilir; taslak tablosu korunur. Otomatik
Down veya üretim verisini geri yükleme yapılmaz. Geri dönüş tetikleyicileri readiness
kaybı, yeniden başlatma döngüsü, eski veri özetinin değişmesi veya temel öğrenci akışının
çalışmamasıdır. Yayın araçları `tools/releases/coaching-weekly-calendar-20261017/` altında.
