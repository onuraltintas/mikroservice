# Koçluk öğrenci planlama canlı yayını

Kaynak sürüm: `0d604256`, GitHub dalı: `codex/platform-hardening`.

2026-10-02 tarihinde birikmiş 209 commit kullanıcının açık onayıyla GitHub'a gönderildi.
Canlı yayın yalnız Koçluk API, Notification ve Koçluk öğrenci ekranlarını da sunan
admin-panel imajlarını `planning-0d604256` sürümüne aldı. Identity, Hızlı Okuma,
staff-portal ve gateway imajları değiştirilmedi. Staging oluşturulmadı.

## Geçiş ve kontroller

- Koçluk ve Notification custom-format yedekleri VPS'te yayın dizininde korundu.
- İki yedek ayrı geçici veritabanlarına hatasız geri yüklendi.
- Koçluk migration sayısı 22'den 31'e çıktı; Notification alıcı silme koruması eklendi.
- Geçmiş Notification silme execution sayısı 0 olduğu için geriye dönük eşleştirme gerekmedi.
- Altı kullanım/yayın hakkı onaylı katalog dosyasından 30.769 kayıt pasif aktarıldı,
  içerik doğrulamasıyla 30.769 kayıt etkinleştirildi. Kişisel öğrenci dosyaları aktarılmadı.
- İç ağ Koçluk ve Notification readiness: `200 Healthy`.
- Gerçek portal/admin/Hızlı Okuma ana sayfaları ve Koçluk planları: HTTP 200.
- Yeni öğrenci raporu oturumsuz erişimi: HTTP 401.
- İlk servis kontrolünde üç yeni konteynır da running, restart sayıları 0.

15 dakikalık periyodik sağlık izlemesi başlatıldı; son sonucu ayrıca doğrulanmalıdır.
Gerçek öğrenci oturumuyla manuel plan/otomatik plan/hedef/sınav/rapor işlemleri bu
yayın turunda doğrulanmış sayılmaz. Önceki yerel E2E gerçek Google/Identity oturumu değildi.

## Geri dönüş sınırları

Yayın betiği: `artifacts/coaching-planning-release-0d604256.sh`.
İki migration başarılı olmadan deploy kapısı açılmaz. İmaj geri dönüşü Koçluk ve
admin-panel ile sınırlıdır; eski Notification worker silinen alıcı korumasını bilmediği
için geri başlatılmaz. Şema Down otomatik uygulanmaz. Öğrenci beyanı sınav veya
puan ölçeği içeren yeni kayıtlar oluştuktan sonra eski Koçluk sürümüne dönmeden önce
veri uyumluluğu ayrıca değerlendirilmelidir. Yedeğe dönüş yeni kayıt kaybı doğurabilir.

Portal: https://onuraltintas.net/ — yönetim: https://eduivme.com/
