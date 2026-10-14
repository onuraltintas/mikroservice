# Master Hızlı Okuma kayıt tasarımı ve sekme simgesi

14 Ekim 2026, `2026-10-14T16:36:10Z` tarihinde canlıya alındı. Kaynak commit'i `56fae4fa`; yayın etiketi `master-registration-20261014`.

Öğrenci (`/auth/register`), öğretmen (`/auth/register-teacher`) ve kurum (`/auth/register-school`) sayfaları ortak kayıt kabuğunu kullanır. Masaüstünde koyu yeşil tanıtım alanı ve açık renk form, mobilde sade başlık ve tek sütun form vardır. Kayıt türü bağlantıları davet dönüş adresini korur. Kurum formu kurum, konum ve yönetici başlıklarıyla ayrılır. Erişilebilir bildirimler, otomatik doldurma ve okunaklı açıklama renkleri eklendi.

Chrome sekme simgesi, mevcut ONAL marka işaretinin birebir kopyası `brand/master-favicon.svg` ile yenilendi. Eski `favicon.ico` bağlantısı kaldırıldı; HTML tek SVG favicon bağlantısı içerir. Yeni dosya adı eski simge önbelleğinden ayrıdır.

Üretimdeki `838fc1b` kaynak sürümü ayrı dizine çıkarıldı ve yalnız bu tasarım/simge değişiklikleri uygulandı. Çalışma alanında önceden bulunan öğrenci Google kayıt ve yasal onay değişiklikleri korunarak bu yayına dahil edilmedi. Kayıt metotları, alan bağlamaları, Google düğme kimlikleri, yasal onaylar, mevcut hesapla katılım ve CAPTCHA akışı korunur.

Doğrulama:

- Üretim derlemesi başarılı; yalnız önceden mevcut egzersiz oyuncusu SCSS bütçe uyarısı var.
- Canlı kaynak üzerine uygulanan değişikliklerde seçili üç kayıt bileşeni testi geçti.
- Yerel üretim derlemesinde 9, canlıdan sunulan arayüzde 9 senaryo geçti: üç rol × 1440/390/320 piksel.
- Belge onayı olmadan gönderim engeli, parola uyuşmazlığı, parola görünürlüğü, mevcut hesap görünümü ve geri dönüşte taslağın korunması, il/ilçe seçimi, yatay taşma olmaması, fareyle sayfa sonuna erişim ve 409 sonrası hata/yeniden deneme doğrulandı.
- Tarayıcı senaryolarında bütün API yanıtları fixture ile karşılandı ve service worker'lar engellendi. CAPTCHA yapılandırması test fixture'ında açık; `auth_register` aksiyonu ve token başlığı doğrulandı. Üretimde hesap açılmadı, e-posta gönderilmedi, CAPTCHA kapatılmadı. Google düğme çizimi de test fixture'ıdır; gerçek OAuth kaydı testi yapılmadı.
- Yeni favicon gerçek canlı URL'den HTTP 200 ile alındı; içerik mevcut ONAL işaretiyle eşleşti.

Yalnız `speed-reading-frontend` güncellendi. Migration/veritabanı yazımı yok. Aday nginx imajı ağsız önizlemede üç kayıt rotası ve favicon için doğrulandı. Canlı geçişte eski imaj, aday imaj kimliği, readiness ve diğer konteynerlerin korunması kontrol edildi; hata için önceki tek ön yüz imajına doğrulanmış geri dönüş hazırlanmıştı. Son canlı sağlık kontrolü başarılı, restart sayısı 0.

Kanıtlar: `artifacts/master-registration-build.log`, `master-registration-component-tests.log`, `master-registration-browser-proof.log`, `master-registration-live-browser-proof.log`, `master-registration-live-health.log`; ekran görüntüleri `artifacts/master-registration-ui/` ve `artifacts/master-registration-live-ui/`. Yayın betikleri `tools/releases/master-registration-20261014/`.
