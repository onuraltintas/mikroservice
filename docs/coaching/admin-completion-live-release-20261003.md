# Koçluk admin tamamlayıcı yayın — 3 Ekim 2026

- Kaynak: `688350a335ac8a0442f8fc57d8f4404055c9dd74`, GitHub `codex/platform-hardening` dalında doğrulandı.
- Canlı imajlar: `eduivme/coaching-service:admin-completion-20261002`, `eduivme/admin-panel:admin-completion-20261002`.
- Kapsam: katalog oluşturma/düzenleme/yayın durumu, kontrollü altı dosyalı aktarım ve yayın; global admin öğrenci plan–hedef–rapor incelemesi; ayrı Manage yetkisiyle gerekçeli, sürüm denetimli düzeltme/arşiv ve denetim geçmişi.
- Son backend regresyonu: 179 geçti, 0 başarısız, 0 atlanan. Genişletilmiş rol/hata/mobil Edge testleri iki tekrar halinde 6/6 geçti. Yerel öğrenci senaryolarında Identity profilleri fixture kullanır; gerçek Identity uçtan uca testi olarak sayılmaz.
- Gerçek yönetici oturumunda yayın öncesi Identity özeti ve 169 kayıtlı canlı katalog listesi başarıyla görüntülendi. Yayın sonrasında Chrome bağlantısı kesildiğinden yeni sürümün oturumlu son ekran kontrolü bekliyor.
- VPS üretim derlemeleri başarılı. Koçluk yedeği geçici veritabanına geri yüklendi ve yeni imajla migration kontrolü geçti. Canlı migration sayısı 33; yeni şema geçişi yok.
- Yayın sonrası admin healthy; Koçluk running/restart=0; Eduİvme, Koçluk portalı ve Hızlı Okuma HTTP 200. Anonim admin katalog isteği 401. Başlangıç loglarında hata görülmedi.
- Identity, Hızlı Okuma, Notification ve diğer servisler yeniden başlatılmadı. Staging oluşturulmadı.
- Yedek ve imaja sabitlenmiş geri dönüş dosyaları `/var/lib/eduivme/releases/coaching-admin-completion-20261002` altında korundu. Geri dönüş: kaynak paketindeki release betiğinin `rollback` modu.

Bu rapor seçili kapsamın yayınını doğrular; tüm ürün için yüzde yüz test kapsamı veya yayın sonrası tüm gerçek rol ekranlarının doğrulandığı iddiası değildir.
