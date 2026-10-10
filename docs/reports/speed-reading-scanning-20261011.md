# Tarama egzersizi — uygulama ve doğrulama raporu

Tarih: 11 Ekim 2026. Durum: yerel geliştirme tamamlandı; GitHub'a gönderilmedi, canlıya uygulanmadı.

## Tamamlanan kapsam

- Dört yaş grubu ve beş seviye için 20 ayar, her egzersize üç metin olmak üzere 60 özgün kurgusal duyuru hazırlandı. Parametrelerin tamamı ve geçiş talimatları: [paket açıklaması](../../content-packs/scanning/v1/README.md).
- Kayıtlı oturumda aktiflik, yaş, seviye, egzersiz bağlantısı ve kelime uzunluğu sınırları birlikte uygulanıyor. Kullanıcının belirttiği metin de aynı denetimlerden geçiyor.
- İlk iki seviyede farklı hedef sözcükler rastgele seçiliyor; sürekli en uzun sözcükleri seçme davranışı kaldırıldı. Hedef uzunluğu yaş ayarıyla sınırlandırılıyor.
- Üçüncü seviyeden itibaren metinde kişi, tarih, sayı ve diğer bilgileri bulma görevleri kullanılıyor. Cevaplar ilgili metinle birebir doğrulanıyor; isim/NLP tahmini yapılmıyor. Bu sürüm tek token cevapları destekliyor.
- Önizleme gerçek metin kullanıyor; sabit yedek metin kaldırıldı. Bilgi görevleri yalnız eşleştirilen metinlerle açılıyor. Özel hedef sayısı azaltılabilir; artırılırsa gerçek metinden ek farklı sözcükler seçilir. Önizleme katalog ayarını veya öğrenci sonucunu değiştirmiyor.
- Bulunmamış hedeflere özel CSS vurgusu kaldırıldı. Görev etiketleri soru biçiminde gösteriliyor. Bozuk görev eşleştirmeleri kontrollü doğrulama hatası veriyor.
- Arama egzersizi okuma/anlama testi gibi raporlanmıyor; yapay kelime/dakika değeri üretilmiyor. Sunucu sonuçları, süre sonu kısmi sonuçları ve oturum devamı korunuyor.

## Veri geçişi

Yeni tablo veya sütun yok. Mevcut egzersiz JSON ayarları ve `reading_texts` kullanılıyor. `content-packs/scanning/v1/apply.sql` yalnız 20 Tarama ayarını güncelleyip 60 metni ekler; programları, kullanıcıları ve geçmiş sonuçları silmez.

Geçiş atomiktir ve tekrarlanabilir. Beklenmedik kaynak ayarı, kimlik çakışması veya yanlış metin–görev eşleşmesi işlemi durdurur. Dosya yalnız geçici PostgreSQL test veritabanında çalıştırıldı; canlı kayıtlar değiştirilmedi.

## Doğrulama

Önce başarısız tekrar testleri yazıldı, ardından düzeltmeler ve regresyon kontrolleri tamamlandı.

| Kontrol | Sonuç |
| --- | --- |
| SpeedReading.Application birim testleri | 1050/1050 geçti |
| Frontend ChromeHeadless testleri | 743/743 geçti |
| Tarama gerçek PostgreSQL entegrasyonu | 4/4 geçti |
| İçerik/SQL eşleşmesi ve hedef işaretleme kontrolleri | 3/3 geçti |
| Masaüstü/mobil Tarama tarayıcı akışları | 10/10 geçti |
| Frontend production derlemesi | Başarılı |
| `git diff --check` | Başarılı |

Tarama motorunun kapsaması: satırlar %96,5; karar dalları %83,52; fonksiyonlar %97,61. Bunlar tüm projenin kapsaması değildir. Frontend genel kapsaması satırlarda %62,49 ve karar dallarında %54,81; backend kapsam yüzdesi ölçülmedi.

Tarayıcı testleri gerçek UI/motor üzerinde API ve Identity yanıt fixture'larıyla çalışır; canlı yönetici oturumu doğrulaması değildir. PostgreSQL testleri gerçek veritabanında kayıtlı sonuç/timeout ve paket geçişini doğrular; ikinci uygulamada çoğaltma olmaması ve değiştirilmiş kaynakta atomik ret test edilir.

Derlemede önceden var olan `exercise-player.component.scss` boyut uyarısı sürüyor: 128,93 kB / 120 kB bütçe. Bu çalışmada bütçe artırılmadı veya ilgisiz stiller yeniden düzenlenmedi. Ayrı lint komutu yapılandırılmadığından lint çalıştırıldığı iddia edilmez.

## Yayın öncesi kalan operasyon

1. GitHub gönderimi ve kontrollü yayın için onay alınması.
2. Hızlı Okuma veritabanı yedeği ve canlıdaki 20 kaynak egzersizin yeniden doğrulanması.
3. Uyumlu API/frontend sürümü ve ardından yalnız Hızlı Okuma DB'sinde paket geçişi.
4. Gerçek öğrenci oturumu ve admin/öğretmen önizlemesinde canlı sağlık/işlev kontrolü.

Süreler ve metin bantları bilimsel yaş normu değil, kullanıcı denemeleriyle değerlendirilecek pilot değerlerdir. Her egzersizde üç yeni metin başlangıç havuzudur; sonsuz içerik çeşitliliği sağlamaz. Tamamen yeni görev türleri veya çok sözcüklü cevap desteği bu kapsamda eklenmedi.
