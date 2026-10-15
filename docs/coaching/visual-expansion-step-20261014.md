# Görsel genişleme: her doğru cevapta küçük ilerleme

> Güncel kural (15 Ekim 2026, 20:35): normal öğrenci çalışması ve önizlemede **her doğru cevapta** bir adım ilerlenir; yanlışta genişlik aynı kalır. Sayaçlar ilk cevaptan başlar. Aşağıdaki metin 14 Ekim yayınının tarihsel kaydıdır. Güncel doğrulama ve yayın ayrıntıları: [15 Ekim son yayın raporu](../reports/visual-expansion-each-correct-20261015.md).

14 Ekim 2026 saat 21:36'da (Türkiye), `2026-10-14T18:36:07Z` tarihinde canlıya alındı. Uygulama değişikliği `325c3ef7`; son doğrulama/yayın kaynağı `ca70d5619df538bba29bab1601352b04a0bd501f`. Yayın etiketi `visual-expansion-step-20261014`.

Normal öğrenci egzersizinde ve önizlemede her tam doğru cevap göreli uzaklığı bir birim artırır. Yanlış veya kısmen doğru cevap uzaklığı değiştirmez. Hedef sınırı aşılmaz. Hedefe ulaşılınca önceki süre kuralı korunur: üç ardışık doğru cevap gösterim süresini %5 azaltır; alt sınır 100 ms'dir. Bu sayı fiziksel açı ölçümü değil, mevcut ekran yerleşiminin yapılandırma birimidir.

Eski protokol 1 normal eğitim seansları da yeni +1 kuralını kullanır. Duraklatma/devam ve oturumun başlangıç/hedef/süre anlık görüntüsü korunur. Eski seviye tespitinin +2/süre kuralı ile bloklu seviye tespitinin sabit uzaklık ve 200 ms gösterimi değiştirilmedi. Kayıtlı eğitim ve değerlendirme yapılandırmaları güncellenmedi; migration yok.

Doğrulama:

- Değişiklik öncesi testler üç doğru bekleme ve eski eğitimde +2 artışı yeniden üretti. Sonrasında 145 seçili backend/assessment testi ve 24 frontend motor/assessment testi geçti. Gerçek session present/answer, yanlışta bekleme, pause/clear/resume, hedefte süre sınırı ve yalnız gerçekten sunulan uzaklığın raporlanması kapsandı.
- İzole frontend üretim derlemesi ve Application/Infrastructure derlemeleri başarılı. Önceden mevcut egzersiz SCSS bütçe uyarısı sürüyor.
- Yerel üretim arayüzünde 24 masaüstü/mobil senaryosu geçti. Dört yaş grubunun 1/5 seviyelerinde her doğru sonrası konum artışı; 8 → 9 → 9 → 10 doğru/yanlış dizisi; yatay/dikey/radyal yerleşim, sonuçlar ve mola/devam kontrol edildi.
- Yayın adayının gerçek Application ve Infrastructure DLL'leriyle canlı PostgreSQL'in adı sabitlenmiş yalıtılmış kopyasında 122 kontrol geçti. Normal ve eski eğitim seansları, eski ve bloklu değerlendirme seansları ile tamamlama doğrulandı. Gerçek üretim hesaplarında giriş veya seans yazımı yapılmadı. Kopya veritabanı silindi.
- İlk prova, fixture'ın açık null protokolü ve eksik değerlendirme tamamlama bayrağını yakaladı. Yalnız doğrulama verisi/isteği düzeltildi; ürünün değerlendirme kuralları değiştirilmedi. Geçmeyen adaylar canlıya alınmadı.
- Canlıdan sunulan arayüzde aynı 24 masaüstü/mobil tarayıcı senaryosu da geçti. API yanıtları fixture'dır, service worker'lar engellenir; gerçek API yazımı yapılmaz.
- Katalog yapılandırmalarının yayın öncesi/sonrası SHA256 değeri aynı: `49ca8718555e287c74af5363ba4e804a8c9c28cbddfdb9858a18ef5207886438`. Değerlendirme kayıtları da bu karşılaştırmaya dahildir.
- C#, frontend ve yayın incelemeleri tamamlandı. Yeniden build eski onay işaretlerini geçersiz kılar; PostgreSQL prova onayı aday API imajına ve iki DLL'in hash'ine bağlanır.

Üretimdeki backend `b777b545` kaynağı izole edildi; gerçek yayımlanmış kaynakla karşılaştırmada yalnız `VisualExpansionRoundRules.cs` ve `OwnedSpeedReadingExerciseSessions.cs` değişti. API imajına yalnız Application/Infrastructure DLL ve PDB'leri kopyalandı; diğer API bağımlılıkları önceki gerçek imajdan korunur. Frontend de son yayımlanmış izole kaynak üzerinde derlendi. Çalışma alanındaki ilgisiz değişiklikler yayına dahil edilmedi.

Yalnız `speed-reading-service` ve `speed-reading-frontend` değişti. Önceki iki imaja doğrulanmış geri dönüş hazırlandı; diğer konteyner kimlikleri korundu. Yayın sonrası iki dakikalık izleme ve canlı tarayıcı kontrolleri boyunca readiness başarılı, restart sayıları 0 ve public HTTP 200 kaldı. Prova veritabanının bulunmadığı da doğrulandı. Yayın imajları: API `sha256:8b96efaee2bbc35a382a6b432ae3da97fd4a67fc3c19653ab44e8f4ae066a85b`, frontend `sha256:f4ee7e35cc51fe651cf0e4f5f92906a9bc5606f1d3b262829d7e9e707531ce45`.

Kanıtlar: `artifacts/visual-expansion-per-correct-backend.log`, `visual-expansion-per-correct-frontend.log`, `visual-expansion-per-correct-build.log`, `visual-expansion-step-verification-build.log`, `visual-expansion-step-browser.log`, `visual-expansion-step-drill-r4.log`, `visual-expansion-step-deployment.log`, `visual-expansion-step-live-browser.log`, `visual-expansion-step-live-monitor.log`, `visual-expansion-step-catalog-before.json` ve `visual-expansion-step-catalog-after.json`. Yayın betikleri ve PostgreSQL doğrulaması: `tools/releases/visual-expansion-step-20261014/`.
