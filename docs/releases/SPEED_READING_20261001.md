# Hızlı Okuma yayını — 2026-10-01

Uygulama commit: `0e264dd8221de5c98b18dfcbeca0761ed6586998`.
GitHub dalı: `codex/platform-hardening`.
Canlı imaj etiketi: `staff-training-20261001`.

## Kapsam

Hızlı Okuma API, öğrenci frontend ve ortak öğretmen/kurum frontend yayımlandı.
Program seçim/ilerleme/döngü düzeltmeleri, sonraki program önerisi/onayı ve yalnız öğrenci rolü olmayan admin/öğretmen için serbest takvimli eğitim kataloğu bu sürümdedir.
Koçluk, Identity, Notification, Gateway ve admin frontend imajları değiştirilmedi. Diğer commitlenmemiş çalışmalar topluca commitlenmedi.

## Veri ve geri dönüş

Yedek: `/var/lib/eduivme/backups/staff-training-20261001/speedreading_owned_db.dump`.
Yedek arşivi `pg_restore -l` ile okunabildi.
Yalnız Hızlı Okuma DB migrationları uygulandı:

- `20261001010000_LinkAssessmentProgramCycles`
- `20261001020000_AddStaffTrainingMode`

Yayın dizini: `/var/lib/eduivme/releases/staff-training-20261001`.
Önceki üç imaj `rollback.override.yml` içinde korunur; uygulama geri dönüşü `bash deploy.sh rollback` ile yapılır. Eklenen şema alanları uygulama geri dönüşünde korunur; canlı veriyi otomatik silen migration geri dönüşü yapılmaz.
Yeni kritik akış hatası, readiness başarısızlığı veya sürekli 5xx görülürse uygulama geri dönüşü değerlendirilir.

## Doğrulama

Yayın öncesi: 760 backend birim, 39 ilgili entegrasyon, 380 öğrenci frontend, 226 ortak panel frontend ve 2 takvim sözleşme testi geçti. API publish başarılı.
Canlı: API doğrudan `/health/ready` = Healthy; öğrenci ana sayfası 200; `/staff/` 200; özel eğitim katalog endpoint'i oturumsuz 401; yeni iki sütun mevcut. Koçluk ve admin ana adresleri giriş yönlendirmesi 302 döndürdü.
API başlangıç loglarında RabbitMQ bağlantısı başarılı; ilk kontrollerde hata görülmedi.
Kimliği doğrulanmış admin/öğretmen eğitim kayıt-tamamlama tarayıcı testi ve 15 dakikalık metrik izlemesi bu raporda tamamlanmış sayılmamıştır. CI çalıştırma durumu doğrulanmadı; yukarıdaki testler yerel sonuçlardır.
