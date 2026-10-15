# Görsel genişleme · iki doğruda ilerleme · 15.10.2026

Yayın `2026-10-15T17:10:20Z` tarihinde tamamlandı. Ürün kaynağı `51b530423c36d3a848a1a53c28d7025526d68662`; kapsam, testler ve canlı öğrenci kanıtı [yayın raporunda](../../../docs/reports/visual-expansion-two-correct-20261015.md).

Bu dizin operasyon kaynakları ve özgün payload hash manifestini saklar. Büyük derlenmiş payload, üretim ortamı ve yedekler depoda tutulmaz. Erişimi kısıtlı özgün yayın dizini: `/var/lib/eduivme/releases/visual-expansion-two-correct-20261015`.

`deploy.sh` sırası `prepare → build → drill → promote → validate`. Bu yayın tamamlandığından yeniden promote çalıştırılmaz. `validate` etkin imaj/health/diğer konteyner kimliklerini kontrol eder. Ayrı yüklenen salt okunur `observe.sh` public HTTP, restart ve yayın sonrası HTTP durum/gecikme özetini de verir; CAPTCHA'nın açık ve 0,5 eşiğinde olduğunu sınar.

Gerçek önceki yayın `speed-reading-assignment-fixes-20261015` kaynakları izole edildi. API'ye yalnız Application DLL/PDB kopyalandı; önceki Domain/Infrastructure korunur. Arayüz aynı önceki canlı kaynaktan yalnız ilgili motor ve alıştırma etiketi düzeltmesiyle derlendi. Migration veya kayıtlı katalog/değerlendirme ayarı değişikliği yoktur. Eski hashli arayüz dosyaları tutulur.

`Verification` varsayılan olarak depo kökündeki SpeedReading Infrastructure projesini referans alır. Özgün izole kaynakla yeniden üretirken `-p:SpeedApiSourceRoot=/path/to/backend/` verin. Sunucudaki prova çalıştırması derlenen referans DLL'leri yerine aday imajın gerçek `/app/*.dll` dosyalarını kullanır; böylece üretimin önceki Domain/Infrastructure dosyaları da sınanır. Bağlantı yalnız `VISUAL_EXPANSION_DRILL_CONNECTION` ortamından alınır; doğrulayıcı yalnız `visual_expansion_two_correct_drill_20261015` adlı ayrı kopyayı kabul eder. Normal eğitim/eski eğitim/eski değerlendirme/bloklu değerlendirme ve pause/clear/resume dahil 146 kontrol geçti. Kopya kaldırıldı.

Geri dönüş, frozen compose içindeki önceki iki tam imaj kimliğini kullanır. Güncel kullanıcı yazılarını korumak için DB yedeği otomatik geri yüklenmez; zaten bu yayında migration yoktur. Yeniden hazırlama ve imaj oluşturma önceki prova onayını geçersiz kılar.
