# Anlama içerik paketi — 11 Ekim 2026

## Kapsam

40 normal Anlama egzersizi için yaş/seviye/çalışma-değerlendirme ayarları, egzersiz başına iki metin kaydı ve toplam 588 açıklamalı soru tamamlandı. Dört seviye tespit egzersizi, programlar, eski içerik ve kullanıcı geçmişi değiştirilmez.

80 kayıt, altı kaynak hikâyeden seçilen 35 farklı metin gövdesi içerir; 80 bağımsız hikâye değildir. 89 kaynak soru farklı seçkilere eşlenmiştir. Aynı metni/soruyu daha önce görmüş kullanıcı için bağımsız sınav formu sayılmaz. Kaynak, SHA-256, tam paragraf seçimi ve her sorunun metin içi kanıtı manifestte tutulur. Bu pilot paket akademik yaş normu veya standardize ölçme aracı değildir; öğretmen editör değerlendirmesi ve öğrenci pilotu ayrı kalite adımlarıdır. Eski ortak metin havuzunun tamamı editoryal olarak yeniden denetlenmedi.

## Doğrulama ve yayın takibi

- Kanonik içerik/SQL/kaynak/bütünlük kontrolleri: 7/7 geçti.
- Gerçek geçici PostgreSQL içerik testleri: 5/5 geçti; tüm 40 akışın seçimi, soru sayısı, anahtar gizliliği, sahiplik, sonuç kaydı ve metin rotasyonu doğrulandı.
- Geri dönüş: eski ayarlar geri gelir, yeni metinler pasifleştirilir; soru ve sonuçlar silinmez. İkinci geri dönüş metin/egzersiz sürümlerini değiştirmez. Sonradan değişen metin veya metadata'da otomatik geri dönüş durur.
- Mevcut Anlama PostgreSQL regresyonları: 4/4 geçti; yeni paket testleriyle toplam 9/9.
- Playwright: 94/94 geçti (47 masaüstü, 47 mobil); 40 seçeneğin her birinde metin, sorular, açıklamalar ve önizlemede kayıt oluşturmama kontrolü. Tarayıcı API/Identity yanıtları fixture'dır; gerçek canlı kayıt testi değildir.
- Canlı yedeği geri yüklenmiş geçici veritabanında iki uygulama ve iki geri dönüş geçti. Hedef dışındaki her tablo/satırın fingerprint'i eşleşti. Prova veritabanı kaldırıldı; PostgreSQL kataloğunda kalmadığı doğrulandı.
- JS/SQL/Bash ve C# odaklı kod incelemeleri tamamlandı; geri dönüş metadata koruma bulgusu giderildi. Tüm platform güvenlik denetimi iddiası yok.
- Yayın sonrası geniş regresyon: backend birim testleri 1124/1124, frontend birim testleri 812/812 geçti.
- İzleme betiğinde log okuma hatasını başarı sanma riski giderildi. Eski davranış çalıştırılmış RED testinde yeniden üretildi; düzeltme sonrası okunabilir log, ağır hata ve okunamayan log senaryoları 3/3 geçti; tekrar kod incelemesinde blocker kalmadı.

## Canlı geçiş

Kullanıcı onayıyla 11 Ekim 2026 **14:26:35 (Türkiye)** tarihinde yalnız Hızlı Okuma veritabanına uygulandı. Paket kaynak commit'i `a229aedd4fa606beda901ba9ca70415a5715b707` GitHub'a gönderildi. API uygulama imajı önceki doğrulanmış `exercises-20261011-6f315ff3` sürümünde kaldı; bu paket uygulama kodu değil, içerik/ayar geçişidir.

- Yayın ve geri dönüş dosyaları: VPS'te `/var/lib/eduivme/releases/comprehension-20261011-v1`.
- Geçiş öncesi `speedreading-before.dump` yedeği alındı; checksum ve geri yükleme provası doğrulandı. Yedek root erişimiyle sınırlı tutuldu.
- Aynı transaction'da 80 metin, 588 soru eklendi ve 40 ayar güncellendi. Seviye tespiti ve kullanıcı geçmişi korunarak hedef dışındaki satır fingerprint'leri eşleşti.
- Yalnız Hızlı Okuma API'si durdurulup başlatıldı; aynı container/imaj korundu. Identity, Koçluk, Notification ve diğer container kimlikleri/imajları değişmedi.
- Salt okunur canlı doğrulama: 40 ayar, 80 aktif metin kaydı, 35 farklı metin gövdesi, 588 soru; 40 yaş/seviye/mod havuzunun her birinde iki uygun metin ve tam soru sayısı.
- Gerçek admin oturumuyla canlı önizleme: değerlendirme modunda 201 kelimelik metin/5 soru, erken cevap anahtarı yok, finalde açıklamalar ve ilk okuma hızı; öğrenme modunda 202 kelimelik metin/5 soru, metne dönüş ve anlık açıklamalar, bağımsız okuma hızı yok. İkisi de “Önizleme sonucu — kaydedilmedi” ile tamamlandı. Canlı kullanıcı eğitim/sonuç kaydı oluşturulmadı.
- HTTPS kontrolleri: Hızlı Okuma, Eduİvme dashboard ve Koçluk portalı 200; API readiness `Healthy`.
- 15 dakikalık yayın sonrası izleme geçti: 30/30 readiness kontrolü `Healthy`, yeniden başlama sayısı 0 ve değişmedi. İlk izleme tamamlandıktan sonra düzeltilmiş log kapısı ayrıca çalıştırıldı; loglar başarıyla okundu ve ağır hata belirteci bulunmadı. Nihai readiness 14:43:43 (Türkiye) tarihinde `Healthy`. Bu kontrol tam trafik hata oranı veya yük testi değildir.

Paketin üretim komutu ve sınırlar [README](../../content-packs/comprehension/v1/README.md) içinde. `settings.json` tek başına uygulanmaz. Öğretmen editör değerlendirmesi ve öğrenci pilotu henüz teknik testlerin yerine geçecek bir bilimsel geçerlilik kanıtı değildir.
