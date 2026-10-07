# Hızlı Okuma yayın doğrulaması — 7 Ekim 2026

- Ürün kaynak sürümü: `259d104e`; yalnız tarayıcı test fixture düzeltmesini içeren son commit: `a0aca9ec`.
- GitHub dalı: `codex/platform-hardening`. Her iki commit gönderildi.
- Canlı adres: https://masterhizliokuma.com
- İmajlar: `eduivme/speed-reading-service:exercises-20261007-259d104e` ve `eduivme/speed-reading-frontend:exercises-20261007-259d104e`.
- Yalnız bu iki konteynır yeniden oluşturuldu. Veritabanı migration'ı çalıştırılmadı; diğer mikroservisler yeniden başlatılmadı.

## Testler

- Backend birim testleri: 952/952 başarılı.
- Frontend birim testleri: 677/677 başarılı.
- Hızlı Okuma PostgreSQL entegrasyon testleri: 119/119 başarılı.
- Metin Solma masaüstü/mobil tarayıcı testleri: 10/10 başarılı.
- Tarama masaüstü/mobil tarayıcı testleri: başarılı; önceki test fixture'ındaki eski `skimming` motor adı `scanning` ile düzeltildi.
- Canlı SPA yolları, yeni service worker sürüm işareti ve frontend konteynır sağlığı doğrulandı.
- Backend `/health/ready`: Healthy.
- Oturumsuz `/api/speed-reading/student-program/my-programs`: 401; erişim koruması devrede.
- Yayın sonrası 08:05–08:19 UTC arasında birer dakika aralıkla 15 kontrol: tamamında Healthy ve HTTP 200; örneklenen sayfa yanıtları 42–136 ms. Son bir dakikalık backend günlüklerinde `fail:`/`Unhandled exception` işaretleri görülmedi. Bu kontrol tüm kullanıcı işlemlerinin hata oranı ölçümü değildir.

Tarayıcı regresyonları yerel mock API ile, PostgreSQL testleri geçici Testcontainers veritabanında çalıştırıldı. Canlıda gerçek kullanıcı hesabıyla tüm egzersizlerin uçtan uca tamamlandığı iddia edilmez.

## Geri dönüş

VPS sürüm dizini: `/var/lib/eduivme/releases/exercises-20261007-259d104e`.
Önceki iki imajın immutable kimlikleri `rollback.override.yml` içinde korunur. Bu dizinde `bash release.sh rollback` yalnız iki Hızlı Okuma bileşenini önceki imajlara döndürür.
Geçici kaynak arşivleri ve test çıktıları GitHub'a eklenmedi.
