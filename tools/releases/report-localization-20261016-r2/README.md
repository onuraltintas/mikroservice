# Türkçe raporlar · Katalog kodları tamamlaması · 16.10.2026

Son uygulama revizyonu `7dd0f5cfa6fc56d5ef80b7d56cdce6ef67518244`. [İlk Türkçeleştirme yayınının](../report-localization-20261016/README.md) üstüne yalnız ortak etiket sözlüğüne dört eski katalog alias'ı ekler: `grouping`, `guided_reading`, `rsvp`, `subvocalization`. Salt okunur canlı katalog taramasında bulunan 10 teknik kategori kodunun tümü öğrenci testiyle doğrulandı. Son paket 63 personel ve 29 öğrenci testinden ve iki üretim derlemesinden geçti.

Kaynak izolasyonu, iki arayüz kapsamı ve veri koruma kuralları ilk yayınla aynı. API/veritabanı değişmez. Son öğrenci/personel ana paketleri `main-IDG6HB6Q.js` / `main-RACSK3ZE.js`.

Sıra `prepare → build → preview → promote → validate`. Bu tamamlanmış yayında `promote` tekrar çalıştırılmaz. Base compose ve iki geri dönüş imajı ilk Türkçeleştirme yayınına bağlıdır. Değişmeyen API'nin imajı, önceki `no-measurement-reports-20261016/built-image.txt` kaydıyla doğrulanır. İki hedefin ortam eşitliği, kesin önizleme HTTP 200, diğer konteyner kimlikleri ve iki eski imaja geri dönüş kontrolleri korunur. Paket 154 statik dosyanın SHA-256 manifestini içerir.

Yayın **11:01:15 TSİ** gerçekleşti. İlk Türkçeleştirme gözlemi yeni imajlarla geçersiz kaldığından başarıyla tamamlanmış sayılmaz. Son 15 dakikalık izlem bu yayının `deployed.txt` zamanından başlar; `monitoring-complete.txt` yalnız başarılı bitişte üretilir. Sunucu dizini `/var/lib/eduivme/releases/report-localization-20261016-r2`.

Sonuçlar [ana yayın raporunda](../../../docs/reports/speed-reading-report-localization-20261016.md). Test ve ilk rol kanıtları `artifacts/report-localization-20261016`, son paket rol/izlem kanıtları `artifacts/report-localization-20261016-r2` altındadır. Ortam/compose/yedekler depoya alınmaz.
