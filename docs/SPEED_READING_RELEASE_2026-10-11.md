# Hızlı Okuma — kontrollü yayın, 11 Ekim 2026

## Kapsam ve sürümler

Kullanıcı onayıyla RSVP, N-back/Odaklanma, Kelime Hazinesi, Serbest Okuma ve Hata Analizi birlikte yayımlandı. Yalnız Hızlı Okuma API, öğrenci arayüzü ve `speedreading_owned_db` değişti. Identity, Koçluk, Notification, admin, ortak öğretmen/kurum paneli ve gateway bu yayında değiştirilmedi. Staging kurulmadı.

- API/paket kaynak sürümü: `2ee9462acfa250275c673cf82902ed992585cba2`.
- Son arayüz kaynak sürümü: `4bcc4bac3f6deef418bb4c02f190122233e71cbb`.
- GitHub dalı: `codex/platform-hardening`.
- API imajı: `eduivme/speed-reading-service:pending-20261011`.
- Arayüz imajı: `eduivme/speed-reading-frontend:preview-age-20261011`.
- Ana geçiş: 11 Ekim 2026 23:12:32 (Türkiye saati).
- Son arayüz düzeltmesi: 11 Ekim 2026 23:27:12 (Türkiye saati).
- Gerçek adres: <https://masterhizliokuma.com/>.

## Veritabanı paketleri

| Paket | Katalog güncellemesi | Yeni katalog kaydı | Yeni metin | Yeni soru |
|---|---:|---:|---:|---:|
| N-back/Odaklanma v1 | 20 | 0 | 0 | 0 |
| Kelime Hazinesi v1 | 28 | 1 | 0 | 0 |
| Serbest Okuma v1 | 13 | 7 | 100 | 0 |
| Hata Analizi v1 | 8 | 0 | 40 | 0 |
| RSVP v2 | 11 | 0 | 22 | 110 |
| Toplam | 80 | 8 | 162 | 110 |

Beş paket tek transaction içinde uygulandı. Önceki metinler, sorular, eğitim programları, seviye tespiti ve kullanıcı geçmişi silinmedi. Hedef katalog kayıtlarının izin verilen alanları dışında kalan alanları da koruma envanterine dahil edildi. Son salt okunur doğrulama 88 katalog kaydı, 162 metin ve 110 soruyu doğruladı.

## Yayın öncesi doğrulama

| Kontrol | Sonuç |
|---|---|
| Hızlı Okuma API birim testleri | 1199/1199 geçti |
| Son arayüz birim testleri | 903/903 geçti |
| Beş paketin katalog/PostgreSQL testleri | 87/87 geçti |
| Gerçek EF/PostgreSQL özellik testleri | 20/20 geçti |
| Yayın aracı testleri | 5/5 geçti |
| Masaüstü/mobil tarayıcı testleri | 70/70 geçti |
| API Release publish | Başarılı |
| Arayüz production build ve TypeScript kontrolü | Başarılı |
| Değişiklik farkı kontrolü | `git diff --check` geçti |

Tarayıcı testlerinin dağılımı: Odaklanma 22, Kelime Hazinesi 14, Serbest Okuma 10, Hata Analizi 10, RSVP 14. Yaş filtresi düzeltmesinden sonra Serbest Okuma/Hata Analizi/RSVP'nin toplam 34 testi, gerçek API alan adlarıyla yeniden çalıştırıldı ve geçti. Son kaynak sürümünde Odaklanma/Kelime Hazinesi'nin diğer 36 testi de yeniden çalıştırıldı ve geçti. Bu tarayıcı testleri kontrollü API fixture'ları kullanır; canlı API kontrolleri ayrıca aşağıda belirtilmiştir.

Son arayüz kapsaması: statements %63,38; branches %57,27; functions %55,15; lines %64,42. Testlerin tamamının geçmesi, %100 kod kapsamı veya tüm platformun hatasız olduğunun garantisi değildir. Uygulamanın tümünü kapsayan %80 hedefi henüz sağlanmıyor. Ayrı lint komutu projede tanımlı değil. Mevcut universal-player SCSS dosyasında 120 kB bütçeye karşı 128,99 kB uyarısı devam ediyor; bu yayında komşu tasarım kodu yeniden düzenlenmedi. GitHub dalında bu yayın için çalışan bir Actions run bulunmadı; doğrulama yerelde yapıldı.

## Yedek, prova ve veri koruması

VPS sürüm dizini: `/var/lib/eduivme/releases/pending-speed-reading-20261011`.

1. Canlı Hızlı Okuma yedeği geçici `eduivme_pending_check_20261011` veritabanına geri yüklendi.
2. Beş paket provada iki kez uygulandı. İlk ve ikinci uygulamanın tam tablo/satır envanteri aynı çıktı; tekrar uygulama ek değişiklik üretmedi.
3. Prova sırasında kapsam dışı verinin önce/sonra envanteri aynı çıktı. Geçici prova veritabanı kaldırıldı.
4. Gerçek geçişte yalnız Hızlı Okuma API durduruldu; yeni `speedreading-before.dump` yedeği alındı. Yedeğin içerik listesi ve SHA256 doğrulandı.
5. Canlı geçiş sonrası `inventory-before.txt` ve `inventory-after.txt` aynı çıktı. Geçiş öncesindeki kullanıcı geçmişi korundu.
6. Önceki API/arayüz imajları ve eski hash'li frontend dosyaları saklandı.

Kanıtlar sürüm dizininde: `rehearsal.log`, `rehearsal-passed.txt`, `rehearsal-before.dump`, `rehearsal-before.dump.sha256`, `rehearsal-first.txt`, `rehearsal-second.txt`, `speedreading-before.dump`, `speedreading-before.dump.sha256`, `inventory-before.txt`, `inventory-after.txt`, `containers-before.txt`, `containers-after.txt`.

## Canlı kontrolde yakalanıp düzeltilen hata

API egzersiz yaşını `targetAgeGroupConfigurationId` alanıyla gönderirken okuma önizlemesi eski `targetAgeGroupId` alanını kullanıyordu. Hata Analizi bütün gerçek metinleri eliyor; diğer okuma önizlemeleri yanlış yaş grubundaki metni kabul edebiliyordu. Altı yeni birim testi önce bu hatayı yeniden üretti. Canonical alanın öncelikli kullanılması ve eski alias uyumluluğunun korunmasıyla düzeltildi. Tarayıcı fixture'ları da gerçek API sözleşmesine geçirildi.

Bu düzeltme için yalnız arayüz yeniden yayımlandı; API/veritabanı tekrar değiştirilmedi. Diğer konteynırların kimlik/imaj envanteri aynı kaldı. Son düzeltmenin sürüm dizini `/var/lib/eduivme/releases/preview-age-20261011`.

## Canlı doğrulama

- HTTPS ana sayfa yeni `main-FZLVNS4F.js` derlemesini sunuyor.
- 135 JS/CSS dosyasının tamamı yeni `ngsw.json` SHA1 değerleriyle eşleşti; canlı manifest yerel build ile aynı.
- `ngsw.json` cache-control: `no-cache, no-store,must-revalidate`.
- Yetkisiz program erişimi 401 döndürüyor.
- Açık admin oturumunda gerçek API üzerinden kayıtsız önizlemeler kontrol edildi; normal eğitim kaydı oluşturulmadı.
- Kelime Hazinesi: 4 gerçek kelimelik quiz, doğru yanıt dönütü ve %100 sonuç açıldı.
- Serbest Okuma: gerçek 360 kelimelik metin yüklendi, minimum süre uygulandı, sorusuz sonuç açıldı. Anlama puanı/XP verilmediği ve temponun bitirme beyanına dayandığı açıklandı.
- Hata Analizi: gerçek metindeki 2 bozulmuş kelime yüklendi; 15 saniye sonunda kaçırılan hata ve doğru yazım dönütleri açıldı. Okuma hızı/anlama ölçümü iddia edilmedi.
- RSVP: gerçek 515 kelimelik metin gösterildi, 5 anlama sorusu geldi, 5/5 doğru sonuç açıldı. Gösterim temposunun ölçülmüş okuma hızı olmadığı belirtildi.
- N-back: özel 1200 ms/4 uyaran/1-back önizlemesi, uyaran gösterimi ve sonuç kontrolü yapıldı.
- Bütün önizleme sonuçlarında kaydedilmediği belirtildi.
- Son arayüz yayınından sonraki canlı tarayıcı konsol kayıtlarında yeni error kaydı görülmedi.

RSVP canlı ekran kanıtı: `artifacts/pending-preview-age-20261011/rsvp-live-proof.jpg` (yerel, GitHub'a kullanıcı ekranı yüklenmedi).

Son arayüz yayını sonrası 15 dakikalık sağlık gözlemi tamamlandı: 30/30 örnek `Healthy`; API restart sayısı 0 ve sabit; arayüz `healthy`; ana yayından itibaren ciddi API hata belirteci yok. Sonuç `MONITOR_OK` olarak kaydedildi. Kanıt: `/var/lib/eduivme/releases/preview-age-20261011/monitor.log`. İlk ana yayın gözlemi son arayüz yeniden başlatılmasıyla 29 örnekte kesildi; bunu tamamlanmış 15 dakika olarak saymıyoruz. Sonucun dayanağı arayüz düzeltmesinden sonra tamamlanan yeni gözlemdir.

## Geri dönüş

Arayüzün önceki imajı korunuyor; frontend-only deploy aracının `rollback` adımı yalnız arayüzü önceki imaja döndürür. API ve içerik paketlerinin geri dönüşü birlikte değerlendirilmelidir: eski API imajının yeni içerik ayarlarıyla uyumlu olduğu varsayılmaz. Canlı yazılar oluşmuşsa tam DB restore bunları kaybettirebilir; otomatik restore/silme yapılmaz. Önce API durdurulur, veri farkı incelenir ve yeni kayıtları koruyan ayrı geri dönüş planı uygulanır.
