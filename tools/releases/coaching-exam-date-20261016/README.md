# Coaching sınav tarihi sürümü

Kaynak: `dfbb4335`, taban: canlı `coaching-curriculum-20261016` / `ddc089ef`.
Canlıya alınan değişiklik: nullable `ExamDate`, yönetici formu, liste/detay ve kimliği doğrulanmış katalog yanıtı.

`prepare.mjs` önceki canlı kaynaklarını kopyalar, yalnız bu değişikliğin patch'ini uygular ve önceki dört derlemenin özetini doğrular. `package.mjs` yalnız dört Coaching DLL/PDB'sini ve yönetici paketini taşır. Shared ve personel paneli değiştirilmez.

Sunucu adımları: `stage.sh`, paket yükleme, `unpack.sh`; sonra `deploy.sh prepare`, `build`, `drill`, `promote`, `validate`. Prova gerçek yedeği ayrı veritabanına geri yükler, geçişi iki kez çalıştırır ve mevcut iş kayıtlarını karşılaştırır. İmajlar mevcut canlı kimliklerine sabitlenmiştir; farklı bir canlı tabana körlemesine uygulanmamalıdır.

`verify-preserved.sh`, tarayıcı testinde aynı pasif `LIVE261016` kaydının tarihini temizledikten sonra çalışır; temel hesap ve iş kayıtlarının canlı geçişten önceki özetleriyle aynı olduğunu kontrol eder. `monitor.sh` 15 dakika boyunca 31 örnek alır. `diagnostics.sh` hata satırı sayılarını ve anonim API erişiminin 401 kaldığını kontrol eder. Sunucudaki özel yedekler ve bağlantı dosyaları indirilmez veya Git'e eklenmez.

Uygulama imajları geri alınabilir; veritabanı geri yüklemesi veya `Down` otomatik yapılmaz. Ayrıntılı sonuçlar: `docs/coaching/curriculum-live-release-20261016.md`.
