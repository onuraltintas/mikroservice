# Hızlı Okuma rapor, marka ve editör erişimi — 16 Ekim 2026

## Tamamlanan değişiklikler

| Sorun | Düzeltme | Doğrulama |
| --- | --- | --- |
| Pasifleştirilen metnin eski ölçümü bazı raporlardan çıkıyordu | Geçmiş ölçüm sorguları güncel katalog aktifliğine bağlı olmaktan çıkarıldı | Aktif ve pasif metin birlikte: anlama %50, hız 225 kelime/dakika |
| Dönem başlangıcındaki ölçüm önceki döneme de giriyordu | Önceki dönemin bitişi başlangıçtan kesin küçük olacak şekilde değiştirildi | Sınırdaki tek kayıt, karşılaştırmada yalnız yeni dönemde |
| Öğrenci grafikleri özetten farklı kayıt kümesini kullanıyordu | Grafikler aynı dönemli rapor özetinden besleniyor; ayrı sınırlı geçmiş isteği kaldırıldı | Gerçek %0 korunuyor, ölçülmeyen gün anlama grafiğine alınmıyor |
| Gün/saat dağılımları UTC ile gruplandırılıyordu | Günlük/saatlik dağılım, aktif gün ve her iki program zaman çizelgesi Europe/Istanbul kullanıyor | UTC 21:30 ve ertesi gün 00:30, aynı Türkiye gününde |

Hız, anlama ve başarı ortalamaları ölçülmüş kayıtlara göre kontrol edildi. Gerçek sıfır sonuçlar hesaba katılıyor; ölçülmeyen egzersizler başarı ortalamasına katılmıyor. Öğrenci ve öğretmen/kurum rapor servisleri aynı test verisinde karşılaştırıldı. Puan, XP ve ilerleme hesapları değiştirilmedi.

Master giriş ve öğrenci/öğretmen/kurum kayıt sayfaları mevcut öğrenci panelinin ortak indigo, beyaz ve slate renk değişkenlerine bağlandı. Masaüstü ve 390×844 mobil görünümde yatay taşma yok. Canlı üç kayıt sayfasında ve girişte `#4f46e5` marka, `#f8fafc` sayfa rengi doğrulandı.

Editor rolü Eğitim Programları, Aktif Eğitimim ve Egzersiz Önizlemesi için hem API hem rota korumasına eklendi. Eğitim kaydı yalnız kişinin kendi personel eğitimine yapılabilir. Başkasının eğitimi ve Student+Editor birleşimi erişim kazanmaz. Önizleme sonuç kaydetmez.

## Testler ve veri korunması

- 1.322 C# birim testi, 46 PostgreSQL/HTTP yetki testi geçti. İlk 97 arayüz testine ek olarak son giriş düzeltmelerini de kapsayan 111 kayıt/giriş/interceptor/yetki/rapor regresyon testi geçti.
- Ana değişikliklerde ölçülebilen değişmiş C# satırları: 33/33 kapsandı. Bu tüm uygulamanın kapsama yüzdesi değildir.
- Canlı veritabanı kopyasında gerçek aday DLL'lerle 16 rapor/veri koruma senaryosu geçti; kopya silindi.
- Yayın öncesi/sonrası 86 tablo ve 8.610 orijinal satırın parmak izleri eşleşti. Migration veya geçmiş sonuç güncellemesi yapılmadı.
- Test öğrencisinin 476 XP, 21 egzersiz sonucu, 12 günlük kayıt, 12 kelime testi ve 3. gün ilerlemesi eşleşti.
- İncelemelerde bulunan program zaman çizelgesi ve test veri ad çakışması sorunları giderildi; son incelemeler onaylı.

## Yayın

Ana yayın: `reports-brand-editor-20261016`, kaynak `eb5821c4`, 16 Ekim 12:32:39 TSİ. API ve öğrenci ön yüzü değişti; personel ve kimlik servisi değişmedi. Yayın sonrası 15 dakika izleme tamamlandı. Son ana yayın kontrolünde 33 API isteği, sıfır 5xx ve sıfır uygulama ERR/FTL kaydı görüldü.

Editör test hesabı `testeditor@eduivme.com` oluşturuldu. Şifre önceki kullanıcı tarafından belirlenen test hesaplarıyla aynıdır; burada kaydedilmez. Yalnız Hızlı Okuma Editor rolü ve ürün erişimi var. Kurum, öğrenci, koçluk ve yönetici yetkisi verilmedi. Oluşturma öncesi kimlik yedeği alındı; işlem önce geri alınan transaction ile prova edildi.

## Canlı kontrolde bulunan ek giriş sorunu

Google reCAPTCHA bağlantısı yanıt vermediğinde giriş düğmesi süresiz bekliyordu. Configuration, script/ready ve execute aşamalarına ayrı 15 saniyelik zaman aşımı eklendi. Başarısız doğrulamada auth isteği gönderilmez; tekrar deneme mümkündür. CAPTCHA kapatılmadı, eşik değiştirilmedi.

Dört takılma ve yeniden deneme senaryosu ile gerçek HTTP hata zinciri dahil 28 CAPTCHA/interceptor/login testi geçti; üretim derlemesi başarılı. Normalizasyon sırasında kaybolan hata gövdesi de korundu; CAPTCHA hatası yanlış genel giriş mesajı yerine kendi mesajıyla gösterilir.

İlk ek yayın `captcha-wait-20261016`, kaynak `84ed5c59`, 13:05:55 TSİ. Son ek yayın `captcha-wait-20261016-r2`, kaynak `578da813`, 13:10:30 TSİ: yalnız öğrenci ön yüzü değişti. R2 operasyon betikleri incelenen ilk betiklerden yalnız yayın kimliği ve bir önceki imaj SHA'sı değiştirilerek üretildi. Önizleme ve tam önceki imaj SHA'sına geri dönüş koruması var. Diğer servislerin konteyner kimlikleri korundu. Son canlı uygulama ana paketi `main-O7CTLJR5.js`.

## Gerçek editör oturumuyla canlı doğrulama

Kullanıcının CAPTCHA açıkken normal girişi tamamlamasının ardından `testeditor@eduivme.com` oturumunda üç ekran test edildi:

- Eğitim Programları: 20 program yüklendi; Yetişkin — Seviye 1 programı arayüzden başlatıldı. Veritabanında yalnız bu editör adına `is_staff_training=true` programı oluştu.
- Aktif Eğitimim: ilk göz takibi 90 saniye çalıştırılıp tamamlandı. “Sonuç kaydedildi” görüldü; günlük ilerleme 1/6 oldu ve ikinci egzersiz açıldı. Sayfa yenilendiğinde ilerleme korundu. Sonuç ölçülmemiş olarak kaydedildi.
- Egzersiz Önizlemesi: Schulte seviye 1, 16 sayı arayüzden tamamlandı; “Önizleme sonucu — kaydedilmedi” görüldü. Öncesi/sonrası editörün program, oturum, sonuç, günlük kayıt ve puan parmak izleri birebir aynı kaldı.

Katalog tekrar açıldığında aktif program kartı ve devam bağlantısı doğru göründü. Son sayfa yenilemesinden itibaren yakalanan tarayıcı hata kaydı boştu. Eski test öğrencisinin tam veri görüntüsü de bu canlı testlerden sonra ilk görüntüyle eşleşti. Gerçek editör oturumu için bekleyen giriş adımı kalmadı.

Son ek yayının 15 dakikalık izlemesi 13:25:49 TSİ'de tamamlandı; tüm izleme örneklerinde genel giriş adresi HTTP 200 döndü. Canlı editör akışları sonrasındaki kontrolde 44 API isteği, sıfır 5xx ve sıfır uygulama ERR/FTL kaydı vardı. Katalog açık editör sekmesi kullanıcıya bırakıldı.

Kanıtlar: `artifacts/reports-brand-editor-20261016/` altındaki RED/GREEN test günlükleri, `drill-result.json`, `changed-line-coverage.json`, `preservation-result.json`, `source-isolation-changes.json`, `final-health.log`, canlı ve yerel ekran görüntüleri; ek yayın kanıtları `artifacts/captcha-wait-20261016-r2/` altında tutulur. Ana kaynak karşılaştırmasında yalnız bu işe ait 6 API/uygulama/persistence ve 10 öğrenci çalışma dosyası değişmiştir; ilgisiz çalışma alanı değişiklikleri yayına alınmamıştır.
