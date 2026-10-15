# Hızlı Okuma gün ilerletme — 15/16 Ekim 2026

## Yayınlanan davranış

Genel ayar Admin → Hızlı Okuma → Program ilerleme ekranına eklendi; varsayılan kapalı. Kullanıcı düzenlemede Hızlı Okuma öğrencileri için Genel ayarı kullan / Beklemeden ilerleyebilir / Ertesi günü beklesin seçenekleri ayrı kaydediliyor. Kullanıcı seçimi genel ayardan öncelikli.

İzinli öğrencide günün bütün görevleri bitince Sonraki güne geç düğmesi görünür. Tek gün açılır; görevler tamamlanmış sayılmaz. Aynı isteğin tekrarı ikinci gün açmaz. Eski/yabancı program veya gün bağlamı reddedilir. İzin kapatılsa da kazanılmış günler korunur.

Program başlangıç tarihi, görev/gün tamamlanma sayaçları, sonuç kayıtları, roller, abonelikler ve kurum bağlantıları bu işlemle değiştirilmez. Personel eğitim programlarının davranışı korunur. Kullanıcı silme envanteri ve temizliği yeni tercihi kapsar; genel ayar ve diğer kullanıcıların tercihleri korunur.

## Test sonucu

| Kontrol | Sonuç |
| --- | --- |
| Gün ilerleme ve kullanıcı tercihi birim testleri | 15/15 geçti |
| PostgreSQL migration / eşzamanlı istek / transaction retry ve HTTP rol-yetki-MFA testleri | 14/14 geçti |
| Öğrenci günlük liste testleri | 8/8 geçti |
| Admin ayar ve kullanıcı düzenleme testleri | 22/22 geçti |
| Gerçek üretim öğrenci paketinde masaüstü ve mobil Playwright akışları | 8/8 geçti |
| API, öğrenci ve admin üretim derlemeleri | Geçti |
| Canlı veritabanı kopyasında migration ve aday DLL provası | Geçti; mevcut 85 tablo korundu |
| Canlı migration öncesi/sonrası karşılaştırma | Geçti; mevcut 85 tablo korundu |
| Canlı dosya hashleri | 8 API, 141 öğrenci ve 274 admin dosyası eşleşti |

Geniş C# birim test çalıştırmasında **1314/1315** geçti. Tek başarısız örnek `VocabularyPersistenceTests.Persisted_reviews_are_owned_complete_and_never_reading_speed("quiz")`: kelime başarı ödülü ile oturum puanı sırasında iki gamification kaydı oluşması. Aynı test, bu özellik eklenmeden önceki canlı API kaynak kopyasında da aynı şekilde başarısız oldu. Bu bulgu yeni gün ilerletme regresyonu olarak değerlendirilmedi; ilgisiz çalışma ağacı değişiklikleri yayına dahil edilmedi. Bütün geniş testlerin geçtiği iddia edilmiyor.

## Canlı yayın

Uygulama kaynağı: `7c40fc20694de76e4c9233f42008e4df6d7d9243`. Yayın zamanı: `2026-10-15T20:43:46Z` (23:43 Türkiye).

`20261015202217_AddProgramDayAdvance` yalnız ek alan ve tercih tablosu oluşturdu. Üretim veritabanı yedeklendi. Geçiş sırası migration → API → öğrenci → admin idi. Geri dönüşte eski üç uygulama imajının tam kimliği ve sağlığı kontrol ediliyor; additive şema korunuyor.

Son kontrol `2026-10-15T21:19:29Z` zamanındaydı: 35 dakikadan uzun izlemede üç servis çalışıyor, restart sayıları sıfır ve yeni error/fatal/unhandled-exception kayıtları sıfırdı. Öğrenci ve admin tarayıcı oturumlarında console error kayıtları boştu. Diğer üretim konteynerlerinin kimlikleri değişmedi.

Canlı admin oturumunda genel kapalı ayarı GET/PUT ile doğrulandı. Test öğrencisine özel `allow` tercihi kaydedildi; eksik görev varken buton görünmedi. İlk günün altı görevi gerçek öğrenci oturumunda tamamlandı. Test gece yarısı sınırını geçtiği için ikinci gün takvimden doğal açıldı; bu işlem manuel geçiş olarak sayılmadı.

İkinci günün altı görevi de normal arayüzden tamamlandı. Genel ayar kapalıyken kullanıcı tercihi `deny` ve `inherit` yapıldığında tamamlanmış günde buton gizlendi; `allow` yapıldığında göründü. Butona tek tıklamayla aynı takvim gününde üçüncü gün açıldı ve yeni gün 0/6 görevle başladı.

Canlı geçiş öncesi/sonrası karşılaştırmada ek gün hakkı **0 → 1** değişti. Programın başlangıç tarihi ve bütün diğer ilerleme alanları, **12 günlük görev kaydı, 20 oturum ve 20 sonuç** sayıları ve içerik hashleri aynı kaldı. İzin tekrar `inherit` yapılıp sunucudan yeniden yüklendi; genel kapalı ayarı uygulanırken açılmış üçüncü gün korundu. Geçmiş günler bölümünde ikinci günün altı görevi erişilebilir kaldı. Öğrenci raporunda 20 tamamlanan aktivite, %86,7 ortalama kavrama ve önceki başlangıç ölçümü görüldü.

Son durum: genel ayar **kapalı**, `testogrenci@eduivme.com` tercihi **Genel ayarı kullan**, program üçüncü günde **0/6**, kazanılmış ek gün sayısı **1**. Test hesabında normal arayüzden girilen negatif cevaplar da vardır; bu veriler eğitim performansı yorumu için değil işleyiş doğrulaması içindir.

Canlı görüntüler: [Buton](../../artifacts/day-advance-20261015/live-button.jpg), [Açılan üçüncü gün](../../artifacts/day-advance-20261015/live-day-three.jpg), [Kullanıcı seçenekleri](../../artifacts/day-advance-20261015/live-user-options.jpg). Kayıt karşılaştırması: [JSON](../../artifacts/day-advance-20261015/live-advance-comparison.json).

Kanıt günlükleri `artifacts/day-advance-20261015/` dizinindedir. Kullanım açıklaması: [Program ilerleme](../speed-reading-program-progression.md). Yayın aracı: [Yayın notları](../../tools/releases/day-advance-20261015/README.md).
