# Master Hızlı Okuma giriş tasarımı

14 Ekim 2026, `2026-10-14T17:17:49Z` tarihinde canlıya alındı. Kaynak commit'i `e363282c245c213068f7c68984dda39bcdf3c00f`; yayın etiketi `master-login-20261014`. İmaj: `sha256:ccddec671d2948cf0be96473edb872026e683167ae8336f17a63006d0f4f4bf0`.

`/auth/login`, yeni kayıt sayfalarının ortak koyu yeşil tanıtım alanını ve açık renk form yerleşimini kullanır. Mobilde marka başlığı ve tek sütun form görünür. Öğrenci, öğretmen ve kurum kayıt bağlantıları aynı kartta bulunur; davet dönüş adresleri korunur. Parola otomatik doldurma, görünürlük düğmesi, beni hatırla ve şifre sıfırlama bağlantısı korunmuştur. MFA doğrulama, kurulum ve kurtarma kodları ile Google belge onayı aynı yerleşime taşındı. Form bildirimlerine erişilebilir roller eklendi.

Gerçekte yayındaki kayıt sürümünün izole kaynak ağacına yalnız giriş tasarımı değişiklikleri uygulandı. Çalışma alanındaki diğer Google, zaman aşımı ve servis geliştirmeleri korunarak yayına dahil edilmedi. Giriş ve kimlik doğrulama metotları değişmedi.

Doğrulama:

- Üretim derlemesi başarılı. Önceden mevcut egzersiz oyuncusu SCSS bütçe uyarısı devam ediyor.
- İzole kaynakta 11 seçili giriş ve kayıt bileşeni testi geçti.
- Yerel üretim arayüzünde ve canlıdan sunulan arayüzde ayrı ayrı 12 senaryo geçti: parola, MFA doğrulama/kurtarma, MFA kurulum ve Google belge onayı × 1440/390/320 piksel.
- Ortak yerleşimin kayıt sayfaları için 9 regresyon senaryosu geçti.
- Parola görünürlüğü, otomatik doldurma, davet bağlantıları, e-posta doğrulama bildirimi/yeniden gönderim, hata sonrası yeniden deneme, belge onayı zorunluluğu, iptalde taslağın korunması ve mobilde sayfa sonuna erişim doğrulandı. Yatay taşma ve JavaScript sayfa hatası yok.
- Tarayıcı testlerinde API yanıtları fixture ile karşılandı; service worker'lar engellendi. CAPTCHA yapılandırması fixture'da açık ve token başlığı doğrulandı. Gerçek Google OAuth veya canlı hesapla kimlik doğrulama yapılmadı; üretimde hesap/veri yazımı ve CAPTCHA yapılandırma değişikliği yok.
- Görsel inceleme ve iki bağımsız kod/yayın incelemesi tamamlandı; engelleyici bulgu yok.
- Yalnız `speed-reading-frontend` değişti. Ağsız nginx önizlemesinde giriş, üç kayıt rotası ve favicon doğrulandı. Eski/adayı sabitleme, readiness ve önceki imaja doğrulanmış geri dönüş hazırlandı. Diğer konteyner kimlikleri korunuyor. Son sağlık kontrolü başarılı; ön yüz healthy, restart sayısı 0. Yeni SVG favicon erişilebilir.

Kanıtlar: `artifacts/master-login-build.log`, `master-login-component-tests.log`, `master-login-browser-proof.log`, `master-login-registration-regression.log`, `master-login-live-browser-proof.log`, `master-login-deployment.log`, `master-login-live-health.log`. Ekran görüntüleri: `artifacts/master-login-ui/` ve `artifacts/master-login-live-ui/`. Yayın/test betikleri: `tools/releases/master-login-20261014/`.
