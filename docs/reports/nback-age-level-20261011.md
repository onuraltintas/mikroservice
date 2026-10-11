# N-back yaş/seviye düzenlemesi — 11 Ekim 2026

## Tamamlanan kapsam

- Dört yaş grubunda beşer normal eğitim kaydı için onaylanan N, uyaran aralığı ve tur sayıları hazırlandı. Normal katalog ile kayıtlı ayarlı önizleme aynı kaynağı kullanır.
- N oturum boyunca sabit tutuldu; daha önce uygulanan ardışık konum tekrar sınırları korundu.
- Sekiz uyaranlık puanlanmayan alıştırma ve önceki N ile alıştırma eklendi. Öğrenci N>1 normal eğitimi başlatırken mevcut N alıştırmasını tamamlar, ardından gerçek egzersizi başlatır. Önizlemede isteğe bağlıdır.
- Alıştırma sunucu sonucuna veya program ilerlemesine yazmaz; puanlanan oturum saatini başlatmaz. Duraklatma ve asıl egzersize dönüş masaüstü/mobil akışlarda doğrulandı.
- Konum/kelime/ikili özel önizleme talimatları seçilen moda göre gösterilir.
- Günlük Focus süre tahmini gerçek yapılandırmadaki uyaran aralığı ve sayısından hesaplanır. Eski/hatalı JSON için önceki beş dakikalık tahmin korunur.
- Yalnız 20 normal kayıt için atomik, idempotent, yapılandırma sapmasında işlemi durduran SQL paketi hazırlandı. Seviye tespiti, atamalar ve geçmiş oturumlar korunur.
- 20 programın 560 günlük süre etkisi üretildi. Tekrarlı Focus görevleri ayrı sayılır; diğer egzersizler için önceki beş dakikalık tahmin kullanıldığından günlük toplam gerçek süre garantisi değildir.

Parametre matrisi ve geçiş yönergeleri: `content-packs/focus/v1/README.md`.

## Doğrulama sonuçları

| Kontrol | Sonuç |
| --- | --- |
| Katalog matrisi ve 560 günlük süre etkisi | 5/5 geçti |
| Katalog SQL paketi, geçici PostgreSQL | 15/15 geçti |
| Backend birim testleri | 1160/1160 geçti; 25 süre/regresyon testi dahil |
| Focus gerçek PostgreSQL entegrasyon testleri | 8/8 geçti |
| Frontend birim testleri | 868/868 geçti |
| Focus ve özel önizleme odaklı birim testleri | 64/64 geçti |
| Masaüstü/mobil tarayıcı testleri | 22/22 geçti |
| Frontend üretim derlemesi | Başarılı |
| C# biçim kontrolü ve TypeScript tür kontrolü | Başarılı |
| Bağımsız genel, C# ve TypeScript kod incelemeleri | Onay; açık engelleyici bulgu yok |

İlk tarayıcı turunda öğrenci testinin eksik günlük-ilerleme taklidi düzeltildi; bir önizleme zamanlama testi yeniden kontrol edildi. Son tam turda 22 test birlikte geçti. Tarayıcı testleri taklit Identity/API ile yürütüldü; gerçek canlı oturum testi yapılmadı.

Mevcut universal-player SCSS boyut uyarısı sürüyor (128,99 kB; uyarı eşiği 120 kB); bu kapsamda stil dosyası değiştirilmedi. ESLint kurulumu bulunmadığından lint sonucu iddia edilmez. Testlerin geçmesi tüm ürünün veya tüm kodun yüzde 100 kapsandığı anlamına gelmez.

## Yayın durumu

Geliştirme ve testler yerelde tamamlandı. Bu çalışma kapsamında GitHub'a gönderim veya canlı veritabanı/servis değişikliği yapılmadı. Canlı uygulama için ayrı yayın onayı, yedek, kontrollü SQL geçişi ve son sağlık/oturum kontrolleri gerekir.
