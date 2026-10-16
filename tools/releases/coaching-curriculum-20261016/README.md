# Sekmeli Coaching müfredat sürümü

Kaynak: `ddc089ef`, taban: canlı `coaching-outcomes-20261016` / `374f79e2`.

Paket yalnız Coaching ve yönetici panelini değiştirir. Shared derlemeleri ve diğer servisler korunur. `prepare.mjs` ilgisiz çalışma klasörü değişikliklerini dışarıda bırakarak önceki canlı kaynaklarına ilgili patch'i uygular. `package.mjs` paket/dosya SHA256 kayıtlarını üretir.

`deploy.sh` hazırlık, imaj derleme, gerçek yedekten ayrı veritabanında iki geçiş provası, canlıya geçiş ve doğrulama adımlarına sahiptir. Canlı iş kayıtlarının içerik özetleri karşılaştırılır; yalnız yeni sütunlar eski şemayla karşılaştırma için normalleştirilir. Yeni sınav/grup tabloları bu ilk geçişte ayrıca doğrulanır.

`verify-preserved.sh`, tek pasif canlı test dersini ayrı tutarak bütün eski iş kayıtlarını yeniden karşılaştırır. `monitor.sh` 15 dakikada 31 sağlık örneği alır. Özel yedek ve bağlantı dosyaları sunucuda tutulur. Tarih alanının devam sürümü `coaching-exam-date-20261016` dizinindedir. Sonuç raporu: `docs/coaching/curriculum-live-release-20261016.md`.
