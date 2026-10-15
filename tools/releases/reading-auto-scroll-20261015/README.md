# Okuma egzersizlerinde otomatik kaydırma · 15.10.2026

Öğrenci ön yüzü kaynak commit'i `e94442006df9da3932f606efa687dad4315f3949`. Kapsam ve doğrulama [yayın raporunda](../../../docs/reports/reading-auto-scroll-20261015.md).

Bu dizin incelenmiş operasyon betikleri ve özgün payload hash manifestini saklar. Büyük derleme çıktıları, üretim ortamı ve yedekler depoda tutulmaz. Özgün erişimi kısıtlı yayın dizini `/var/lib/eduivme/releases/reading-auto-scroll-20261015`.

`deploy.sh` sırası `prepare → build → promote → validate`. Yayın tamamlandıktan sonra tekrar promote çalıştırılmaz. Aday ağdan yalıtılmış konteynerde sınanır. Yayın yalnız `speed-reading-frontend` servisini `--no-deps` ile günceller; diğer konteyner kimlikleri doğrulanır. CAPTCHA açık ve eşik 0,5 olarak korunur. Migration yoktur.

Derleme, önceki gerçek canlı `visual-expansion-each-correct-20261015` kaynağından yalnız okuma kaydırması ve ilgili dar ekran düzeni değiştirilerek hazırlanmıştır. Önceki hashli JavaScript varlıkları tutulur. Başarısız promote tam önceki ön yüz imajına ve frozen compose ortamına geri döner.

`observe.sh` salt okunur: sürüm/servis sağlığı, public HTTP, restart ve yayın sonrası API hata/gecikme özeti. Canlı kanıtlar ve 15 dakikalık gözlem sonucu raporda kaydedilir.
