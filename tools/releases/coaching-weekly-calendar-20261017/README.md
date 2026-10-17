# Haftalık takvim — canlı yayın

Uygulama kaynağı `12951c5a`, tabanı önceki canlı sınav tarihi sürümü `dfbb4335`.
Yalnız `coaching-service` ve `admin-panel` değiştirilir. Identity, personel paneli,
hızlı okuma ve diğer servisler sabit kalır.

`prepare.mjs` önceki canlı kaynaklarını kopyalar ve yalnız 20 üretim dosyasının
değişikliklerini uygular. Çalışma klasöründeki diğer değişiklikler alınmaz. Linux
yayınında yalnız dört Coaching DLL/PDB'si taşınır; shared bağımlılıklar canlı tabandan
korunur. Arayüz temiz kaynakta derlenir. `playwright.coaching-weekly-release.config.mjs`
doğrudan bu derlemeyi sınar.

`create-operations.mjs` önceki incelenmiş dağıtım akışını yeni sürüm kimliğine uyarlar.
Üretilen betik farkı incelenmelidir. İşlem sırası: `stage`, yükleme, `unpack`, sonra
`deploy.sh prepare`, `build`, `drill`, `promote`, `validate`. Sunucudaki mevcut imajlar
beklenen kimliklerle eşleşmezse hazırlık durur.

Prova üretim yedeğini farklı bir veritabanına geri yükler, geçişi iki kez çalıştırır
ve mevcut iş tablolarının satır/sayı özetlerini karşılaştırır. Canlı geçişte iki hedef
servis kısa süre durdurulur; Identity ve Coaching yedeklenir. Eski tabloların özetleri
geçişten önce, sonra ve servisler başlatılınca aynı olmalıdır. Yeni
`coaching.study_weekly_drafts` tablosu kasıtlı olarak eski iş tablosu özetine dahil değildir.

Hata halinde yalnız eski uygulama imajları geri getirilir; yeni tablo, yedekler ve
taslaklar korunur. Otomatik `Down`, veri silme veya üretim yedeğini geri yükleme yoktur.
Geri dönüş nedenleri: readiness kaybı, yeniden başlatma döngüsü, eski kayıt özetinde
fark veya temel öğrenci akışının çalışmaması.

Canlı öğrenci testinden sonra `verify-preserved.sh` eski kayıtları yeniden karşılaştırır
ve test hesabındaki yeni iki yarım saatlik kartın kalıcılığını doğrular. Yeni taslak
test kanıtı olarak bırakılır. `diagnostics.sh` yeni servis hatalarını ve anonim erişimin
401 kalmasını denetler. `monitor.sh` 15 dakika boyunca 31 sağlık örneği toplar.

Sunucudaki yedek, ortam ve compose dosyaları özel yayın dizininde kalır; yerel Git'e
veya rapora alınmaz. `remote.mjs` olarak önceki `coaching-outcomes-20261016` yayınının
bilinen SSH aracısı kullanılır; parola ve token üretilmez veya tarayıcıdan alınmaz.
