# Okuma kataloğu ve günlük görev bağlantısı · 15.10.2026

## Kök neden ve davranış

Önceki kaydırma testinde metin eksikliği olarak ayrılan hata, öğrencinin profil yaşı ile seçilen katalog yaşının uyuşmamasından kaynaklanıyordu. Test öğrencisi genç yetişkin grubundayken katalog yetişkin seviyelerini de sunuyordu. Sunucunun yaş filtresi, yetişkin egzersizine bağlı metni genç yetişkin öğrenciye vermiyordu. Salt okunur katalog denetimi Gruplama ve Regresyon paketlerinde metinlerin mevcut olduğunu doğruladı.

Öğrenci kataloğu artık yalnız profil yaşına ait ve yaş bağımsız egzersizleri gösterir. Çift öğrenci/öğretmen rolünde de öğrenci filtresi uygulanır; öğrenci rolü olmayan personel önizlemesinde bütün yaşlar korunur. Yanlış yaş grubuna ait eski okuma bağlantıları açıklayıcı `Exercise.AgeGroupMismatch` HTTP 400 döndürür. Gerçekten uygun metni bulunmayan okuma seviyesi `Exercise.ReadingTextUnavailable` HTTP 400 döndürür. Arayüz sunucunun açıklamasını korur. Başka yaş, seviye veya egzersizin metniyle sessiz tamamlama yapılmaz. Mevcut oturumların devamı ve seviye tespiti snapshot'ları korunur.

Genel katalogdan veya bağımsız ödevden tamamlanan çalışma, sunucuda sonuç kaydı olarak saklanmaya devam eder; günlük görev bilgisi olmadan günlük tamamlama isteği gönderilmez. Günlük tamamlama için sıra, program günü ve program ilerleme kimliği birlikte gerekir. Öğrenme yolu, tekrar, değerlendirme, tamamlanmamış okuma ve pratik tekrarlarının mevcut ayrımı korunur. Günlük hedefler ek çalışmayla yanlışlıkla tamamlanmaz.

## Otomatik doğrulama

- Hata öncesi C# reproducer: 4 başarısız / 2 başarılı. Düzeltmeden sonra 6/6 başarılı.
- Hata öncesi öğrenci reproducer: 5 başarısız / 3 başarılı. Düzeltmeden sonra ek hata mesajı kontrolüyle 9/9 başarılı.
- Tüm C# birim testleri: **1300/1300 başarılı**. Eski beş testteki 500 türü istisna beklentisi yeni iş kuralı hatasıyla güncellendi; seviye izolasyonu beklentisi korunur.
- Tüm izole öğrenci birim testleri: **1036/1036 başarılı**.
- Gerçek PostgreSQL entegrasyonu: **16/16 başarılı**. Gruplama/Regresyon katalog paketleri, tekrarlı günlük görevlerin bağımsız tamamlanması ve idempotency, eşzamanlı tamamlama, öğretmen kapsamı ve ölçülmeyen ödev sonuçları kapsanır.
- Gerçek masaüstü/mobil tarayıcı: 12 akış kontrolü ve 2 öğrenci yaş filtresi kontrolü başarılı. Katalog tamamlaması günlük POST göndermez; günlük görev tamamlaması doğru oturum/sıra/gün/program kimliği gönderir. Önizleme kaydı yazılmaz, gösterim temposu ölçülmüş WPM olarak gönderilmez. Yaş filtresi testindeki seçenek/etiket ayrımı düzeltildikten sonra son iki kontrol yeniden geçti.
- Son incelemede negatif günlük POST kontrolünün yanıt işlenmeden geçebilmesi giderildi (`aea1c631`): tamamlanma yanıtı ve gerçek “Sonuç kaydedildi” durumu beklenir. İlgili masaüstü/mobil dört test yeniden başarılı.
- Üretim öğrenci derlemesi başarılı. Önceki sürümde de bulunan 135,19 kB oynatıcı stil bütçesi uyarısı sürüyor; bu yayında stil kaynağı değişmedi.

Kanıtlar `artifacts/reading-content-progress-20261015/` dizininde. Genel test kapsamı için ölçülmemiş bir yüzde iddia edilmez.

## Aday imaj ve gerçek katalog provası

Üretimdeki sahipli PostgreSQL veritabanının ayrı `reading_content_progress_drill_20261015` kopyası kullanıldı. Doğrulayıcı yalnız bu isimdeki veritabanını kabul eder. Canlı katalog kimlik/tür/yaş/seviye snapshot'ı ile eşitlik aranır; yalnız toplam sayının tutması yeterli değildir.

Gerçek adayın `/app/*.dll` dosyalarıyla **103 seviye başarı / 0 hata / 6 yanlış yaş reddi** doğrulandı. Dağılım: Gruplama 20, Regresyon 19, RSVP 11, Hızlı Okuma 20, İç Ses Azaltma 13, Metin Solma 20. Her metinde yaş, seviye, aktiflik ve egzersiz bağı kontrol edildi. Yanlış yaş denemelerinde beklenen hata kodu ve sıfır yeni oturum doğrulandı. Test kopyası kaldırıldı; canlı katalog verileri değiştirilmedi.

## Yayın

15.10.2026 **22:18:59 Türkiye saati** (`19:18:59Z`) yayımlandı. Ürün kaynak revizyonu `2768e42bca405dbabac4ca9747e09bca893f0103`; tarayıcı test güncellemesi `60b80d87`. Son arşiv SHA-256 `440086950e3ac033353eba834e1610cac8792d9d0a73014e19560004ebd690f6`.

- API: `sha256:a1750e44674b9aba3753c8e1b89124537ebed2b47f0f8288853985df4900c134`.
- Öğrenci arayüzü: `sha256:455114e9466fa46a24b932634e41d95cbc9cb3f91e03ffef829b424fc1b94f38`.
- Önceki tam API `sha256:f41b9cf5f0193d8299473c9c8aa101e28fa4a748a0c73da3725c1fbc03f04c54` ve ön yüz `sha256:4dad6b775af9289e9812136174f760d6abcf282dbf85c232c6bcc8ff4a438b8d` geri dönüş için korunur.

API'ye yalnız `SpeedReading.Infrastructure.dll/pdb` yayımlandı. Domain, Application ve API bağımlılıkları önceki gerçek imajda korunur. Öğrenci kaynağı önceki gerçek `reading-auto-scroll` yayınının üzerine yalnız katalog ve günlük bağlantı değişiklikleri uygulanarak derlendi. İlgisiz çalışma dosyaları yayına alınmadı. Migration yoktur; abonelikler, kurum bağlantıları, kimlik doğrulama ve CAPTCHA eşiği değişmedi. Diğer servis konteyner kimlikleri doğrulanır.

Operasyon kaynakları ve doğrulayıcı [yayın dizininde](../../tools/releases/reading-content-progress-20261015/README.md).

## Canlı paneller

Başlangıç: öğrenci raporunda toplam aktivite **3**, kurum özetinde tamamlanan etkinlik **3**, günlük hedef **1/6**. Aynı test öğrencisi mevcut test kurumuna ve test öğretmenine bağlıdır. Katalogda beş Gruplama seviyesinin de genç yetişkin olarak sunulduğu ve ilk seviyenin 180 kelime/dakika / ikili gruplarla açıldığı doğrulandı.

Canlı Gruplama katalog çalışması 458 kelime / 243 grup / %100 gösterimle tamamlandı ve kaydedildi. Günlük hedef 1/6 kaldı; kurum etkinlik sayısı 3'ten 4'e çıktı. Günlük görev uyarısı ve tarayıcı hatası oluşmadı.

Kurumun test öğretmenine ait yalnız test öğrencisine, genç yetişkin Regresyon 1 düzeyinde “Okuma Bağlantı Testi · 15.10.2026” ödevi atandı. Teslim tarihi 16 Ekim olarak her iki listede korundu. Gerçek 169 kelimelik okuma ve beş anlama sorusu tamamlandı: 5/5 doğru, 100 puan, %100 anlama. Öğrenci ödev listesi kendiliğinden tamamlananlara geçti. Öğretmen ve kurum gerçek oturumlarında ödev 1/1 tamamlandı, öğrenci ayrıntısı 100 puan olarak doğrulandı. Üç panel toplam aktivite 5 ve %100 anlama değerinde eşleşti. Gösterim temposu ölçülmüş WPM ortalamasına katılmadı; personel raporlarında hız “Veri yok” kaldı.

Günlük ikinci görev Takistoskop 30 yanıtla normal UI üzerinden tamamlandı. Bu ek senaryo bilerek yanlış yanıtlarla çalıştırıldı: 0 doğru / 30 yanlış / 0 puan doğru kaydedildi. Bu bir anlama testi olmadığından %100 anlama ortalaması değişmedi. Günlük sayaç 1/6'dan 2/6'ya çıktı; üçüncü görev açıldı. Öğrenci, öğretmen ve kurum aktivite toplamları **6** ve anlama ortalamaları **%100** olarak eşleşti. Katalog ve bağımsız ödev bu günlük sayacı değiştirmemişti. CAPTCHA kapatılmadan öğretmen ve kurum normal giriş testleri başarılı oldu. Öğrenci konsolunda hata yok; personel oturumunda yalnız yayından önceki 19:03:40Z tarihli iki tarayıcı eklentisi mesajı vardı.

### Ayrı görüntüleme bulgusu

Regresyon sonuç ekranındaki soru ayrıntıları, doğru yanıt ve 5/5 sonucu korunmasına rağmen seçilen cevabı “Cüre Doldu” etiketiyle gösteriyor. Canlı ekran kaydı `live-assignment-completed.txt` içindedir; şablonda `answer.selectedAnswer || 'Cüre Doldu'` bulunur. Bunun veri eşleme nedeni bu yayında değiştirilmedi; düzeltildi olarak raporlanmaz. Toplam puan, anlama, ödev tamamlanması ve üç panel karşılaştırması yukarıdaki gibi doğrulandı.

Sonraki yayın: Bu görüntüleme bulgusu 15.10.2026 22:51:15'te [Regresyon cevap gösterimi düzeltmesiyle](regression-answer-label-20261015.md) giderildi; doğru ve yanlış seçimler yeni sürümde canlı doğrulandı.

## Yayın sonrası izleme

`19:18:59Z–19:34:13Z` aralığında **15 dakika 14 saniye** izlendi. Son ölçümde 179 API isteği, **0 sunucu 5xx**, ortalama 54,38 ms, en yüksek 749,87 ms; dış öğrenci sayfası HTTP 200 / 72 ms. İki servis beklenen imajlarda çalışıyor, yeniden başlama sayısı 0. CAPTCHA açık ve eşik 0,5 olarak doğrulandı. Son salt okunur kanıt `artifacts/reading-content-progress-20261015/live-health-final.log`.
