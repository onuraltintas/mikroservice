# Hızlı Okuma ödev düzeltmesi · 15.10.2026

Bu dizin yayımlanmış paketin operasyon kaynaklarını ve payload hash manifestini saklar. Parola, bağlantı dizesi ve dondurulmuş üretim ortamı depoya alınmaz. Büyük derlenmiş payload sunucudaki erişimi kısıtlı `/var/lib/eduivme/releases/speed-reading-assignment-fixes-20261015` dizinindedir.

Kaynak düzeltmesi `febe5b6c19d8885f1f53750c829ff78622561b04`; gerçek canlı tabanlar ve testler [yayın raporunda](../../../docs/reports/speed-reading-assignment-fixes-20261015.md).

`deploy.sh` sırası `prepare → build → drill → promote → validate`. Adımlar aynı yayın dizininde ve yalnız tanımlı önceki imajlarla çalışır. Bu yayın zaten tamamlanmıştır; yeniden `promote` çalıştırılmaz. `validate` canlı imaj kimliklerini, sağlık durumunu, diğer konteynerlerin korunduğunu ve CAPTCHA ayarını kontrol eder. `observe.sh`, bunlara dış giriş HTTP durumlarını, yayın sonrası HTTP hata/gecikme özetini ve salt okunur test verisi sorgularını ekler.

`Verification` kaynakları özgün izole paket yerleşiminde `../backend/services/speed-reading-service` yolunu referans alır. Yeniden üretimde `Verification` yanında `backend` dizinine raporda belirtilen canlı tabanı çıkartıp yalnız bu yayının Domain, Infrastructure ve migration değişikliklerini uygulayın. Çalışma alanındaki diğer geliştirmeleri pakete katmayın. Derlenmiş doğrulayıcı aday uygulamanın gerçek DLL'leriyle eşleştirilir. Bağlantı yalnız `SPEED_MAINTENANCE_DRILL_CONNECTION` ortamından alınır; araç üretim DB adını reddeder ve yalnız `speed_maintenance_drill_20261015` adlı ayrı kopyada çalışır.

Migration ölçülmeyen ödev metriklerini temizler; şema değiştirmez. Domain metot imzası nullable olduğundan Domain ve Infrastructure DLL'leri birlikte yayımlanır. `Down` yapay sıfırları yeniden üretmez. Başarısız yayında dondurulmuş eski imajlara dönüş hazırlanmıştır; yeni kullanıcı yazılarını kaybetmemek için tam DB geri yükleme otomatik değildir.
