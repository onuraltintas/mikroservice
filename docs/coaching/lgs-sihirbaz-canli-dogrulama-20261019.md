# Öğrenci LGS sihirbazı — canlı uçtan uca doğrulama

Tarih: 19.10.2026 (Europe/Istanbul). İşlem öğrenci hesabının açık tarayıcı oturumundan yapıldı. Plan oluşturma, kaydetme ve yayımlama için veritabanına doğrudan yazılmadı; veritabanı yalnız sonuç doğrulamak için okundu. Uygulama kodunda değişiklik gerekmedi.

Plan: [LGS çalışma planım · 19.10.2026](https://eduivme.com/coaching-portal/study-plans?plan=a4cea155-5485-4db6-9f52-1ec4df8080c3)

## Test girdileri

Başlangıçta plan/görev sayısı sıfırdı. Öğrencinin mevcut eğitim bağlamı LGS idi, ancak yalnız Matematik seçiliydi. Tüm LGS kapsamını test etmek için kurulum ekranından altı ders seçilip kaydedildi. Mevcut “tekrar etmek istiyorum” konu beyanı korundu; çalışılmış diye dışlanan konu yoktu.

Sihirbazda pazartesi–cumartesi, 18.00 başlangıç, günde 90 dakika seçildi. Haftada 540 dakikalık 12 adet 30/60 dakikalık ders kartı önerildi ve haftalık taslağa kaydedildi. Pazar boş bırakıldı. Başlangıç 19.10.2026, varsayılan hedef 01.06.2027 idi.

## Önizleme ve çözüm seçimi

İlk önizleme: 11120 dakika yerleşti, Fen 480 ve İnkılap Tarihi 370 dakika olmak üzere 850 dakika sığmadı. Sistem kayıt/onay alanını kapattı; 26.07.2027’ye uzatma, boşalan saatleri kullanma ve ders saati artırma seçeneklerini sundu. Bu eksik kapasite uyarısı beklenen davranıştır; uygulama hatası değildir.

“Erken biten derslerin saatlerini kullan” seçildi. Hedef 01.06.2027 kaldı. Yeni önizlemede 11970 dakikanın tamamı yerleşti, kalan 0 dakika oldu. Öğrenme, soru/alıştırma ve kontrol bütçeleri dahildir.

| Ders | Toplam dakika | Son çalışma tarihi |
| --- | ---: | --- |
| Din Kültürü ve Ahlak Bilgisi | 1405 | 2027-01-07 |
| Fen Bilimleri | 2430 | 2027-03-22 |
| Matematik | 2480 | 2027-03-12 |
| T.C. İnkılap Tarihi ve Atatürkçülük | 2320 | 2027-03-22 |
| Türkçe | 1840 | 2027-02-03 |
| İngilizce | 1495 | 2027-03-22 |

Son çalışma 22.03.2027’dir. Motor konular bitince yeni görev üretmez; çalışmaları sırf hedefe kadar uzatmak için tekrar etmez. 1 Haziran son çalışma zorunluluğu değil, aşılmayan hedef sınırıdır. Bu süreler mevcut düşük güvenli ilk tur tahminleridir; tekrar ve ek deneme çalışmaları otomatik eklenmedi.

## Kayıt ve yayımlama

1. Önizleme onayından önce veritabanında 0 plan / 0 görev olduğu doğrulandı.
2. “LGS çalışma planım · 19.10.2026” başlığıyla taslak oluşturuldu. Status=Draft, IsActive=false, Version=0.
3. 560 görev ve 560 kazanım bağlantısı kaydedildi. WeeklySource; başlangıç, bitiş, saat dilimi, taslak sürümü, bağlam sürümü, önizleme parmak izi ve ReuseFreedSlots=true değerlerini içeriyor.
4. Plan ekranındaki yayımlama onayı işaretlendi ve “Planı yayımla” seçildi. Status=Active, IsActive=true, Version=1 oldu.
5. Sayfa tamamen yeniden yüklendi; tek plan aktif olarak tekrar açıldı. Gün, hafta ve ay görünümleri kontrol edildi.

## Veritabanı kontrolleri

- 144 alt konu, 269 farklı kazanım ve 319 konu/kazanım/süre sürümü payının tamamı planda mevcut.
- Her payın görevler arasında ayrılan dakikası, yeni kayıtlı öğrenme+pratik+kontrol payına birebir eşit. Eksik/fazla pay: 0.
- Eski süre sürümüne bağlı görev: 0. Toplam süre: 11970 dakika (199 saat 30 dakika).
- Aynı öğrenci/gün içinde çakışan saat aralığı: 0. 19.10.2026–01.06.2027 aralığı dışında görev: 0.
- Paragrafta Anlam 1–4’ün dördü de planda; ortak kazanımlar yalnız ilk konuya düşmüyor.
- Taslak ve yayımlama sonrası ders bazında görev, konu, dakika ve tarih sayımları aynı.
- 8 ortak hesabın kimlik özeti işlem öncesi/sonrası aynı. Katalog 269 kazanım / 269 güncel tahmin / 319 pay olarak korundu.
- Son sayfa yüklemesinin tarayıcı hata/uyarı kaydı boş.

Sonuç: İstenen canlı öğrenci sihirbazı → öneri → yıllık önizleme → kapasite çözümü → taslak → yayımlama → yeniden açma akışı geçti. Bu koşullarda hata görülmedi; test planı aktif bırakıldı. Çalışma tamamlama veya öğretmen bildirimi bu testte değiştirilmedi.

Kontroller: `tools/releases/coaching-lgs-e2e-20261019/verify.sh`. Arındırılmış sonuçlar: `artifacts/coaching-lgs-e2e-20261019/`. Ekran kanıtı: `active-plan.png`.
