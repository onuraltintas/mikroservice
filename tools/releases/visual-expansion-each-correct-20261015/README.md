# Görsel genişleme · her doğru cevapta ilerleme · 15.10.2026

Yayın `2026-10-15T17:35:10Z` tarihinde tamamlandı. Ürün kaynağı `badab8fdfb90aec51f0e88338f7dbcf545b0810d`; kapsam, testler ve canlı öğrenci kanıtları [yayın raporunda](../../../docs/reports/visual-expansion-each-correct-20261015.md).

Bu dizin operasyon kaynakları ve özgün payload hash manifestini saklar. Büyük payload, üretim ortamı ve yedekler depoda tutulmaz; erişimi kısıtlı özgün dizin `/var/lib/eduivme/releases/visual-expansion-each-correct-20261015`.

`deploy.sh` sırası `prepare → build → drill → promote → validate`. Yayın tamamlandığından tekrar promote çalıştırılmaz. `observe.sh` salt okunur doğrulama, public HTTP, restart ve yayın sonrası HTTP hata/gecikme özeti verir; CAPTCHA'nın açık ve 0,5 eşikte olduğunu sınar.

Önceki gerçek canlı `visual-expansion-two-correct-20261015` kaynağından yalnız Application ilerleme kuralı ve öğrenci motoru değiştirilerek derlendi. API'ye yalnız Application DLL/PDB yayımlandı; Domain/Infrastructure korunur. Migration veya katalog/değerlendirme ayarı değişikliği yoktur. Diğer servislerin imaj/konteyner kimlikleri korunur. Eski hashli öğrenci arayüzü varlıkları tutulur.

`Verification` varsayılan olarak depo kökündeki Infrastructure projesini referans alır. İzole kaynaktan derlemek için `-p:SpeedApiSourceRoot=/path/to/backend/` kullanılır. Sunucu provası, doğrulayıcının referans DLL'leri yerine gerçek aday `/app/*.dll` dosyalarını kullanır. Bağlantı yalnız `VISUAL_EXPANSION_DRILL_CONNECTION` ortamından alınır; yalnız `visual_expansion_each_correct_drill_20261015` adlı ayrı kopya kabul edilir. 146 kontrol geçti, kopya kaldırıldı.

Geri dönüş frozen compose ile önceki iki tam imaj kimliğini kullanır. Yeniden hazırlama/derleme önceki prova onayını geçersiz kılar. Bu yayında DB migration yoktur; güncel yazıları kaybetmemek için tam DB yedeği otomatik geri yüklenmez.
