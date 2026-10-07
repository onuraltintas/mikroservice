# Hızlı Okuma içerik ve arayüz yayını — 7 Ekim 2026

- Kaynak: `70acb9d6`, GitHub `codex/platform-hardening` dalına gönderildi.
- Canlı imajlar: `eduivme/speed-reading-service:exercises-20261007-70acb9d6` ve `eduivme/speed-reading-frontend:exercises-20261007-70acb9d6`.
- Yalnız iki Hızlı Okuma konteynırı değiştirildi; migration çalıştırılmadı.
- Kapsam: gerçek katalogdan seviyeye uygun önizleme metni seçimi, boş Regresyon metni koruması, Takistoskop tekrar/yerleşim düzeltmeleri, Tarama SVG işaretleri, Gruplama tam sayı temposu, Görsel Genişleme otomatik odak ve saniye gösterimi.

## Doğrulama

- Frontend: 690/690 birim testi başarılı.
- Backend: 954/954 birim testi başarılı.
- Tarama masaüstü/mobil: 2/2; Görsel Genişleme: 4/4; Regresyon admin/öğretmen: 4/4; sabit metin kimliği olmayan Regresyon: 2/2 tarayıcı testi başarılı.
- Tarayıcı testleri gerçek frontend/motor ve deterministik API fixture kullanır; canlı kullanıcı uçtan uca testi değildir.
- VPS üretim frontend/backend imaj derlemeleri başarılı.
- Yeni service worker işareti, public SPA yolları, frontend container health ve backend `/health/ready` doğrulandı.
- Oturumsuz öğrenci program API'si 401 döndürdü.
- İlk backend günlük örneğinde hata görülmedi; bu tüm kullanıcı akışlarının hata oranı ölçümü değildir.
- PostgreSQL entegrasyon testleri bu tur yeniden çalıştırılmadı: yerel Docker motoru kapalıydı. Veritabanı şeması değişmedi.
- Frontend derlemesinde mevcut component SCSS boyut bütçesi uyarısı var; derleme başarılı.

## Geri dönüş

`/var/lib/eduivme/releases/exercises-20261007-70acb9d6` içinde `bash release.sh rollback` önceki iki immutable imaja döner. Kaynak arşivi ve geri dönüş dosyaları korunur. Geçici yerel arşivler ve test çıktıları Git'e alınmadı.
