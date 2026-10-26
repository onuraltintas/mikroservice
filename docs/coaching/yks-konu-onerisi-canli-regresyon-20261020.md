# YKS konu önerisi ve onaylı plan: canlı regresyon

20 Ekim 2026. TYT, AYT Sayısal, AYT Eşit Ağırlık, AYT Sözel ve İngilizce YDT için öğrenci ve atanmış öğretmen hesaplarında toplam **10 uçtan uca senaryo geçti**.

## Test matrisi

Her satırda ilgili rolden üç deneme sonucu oluşturuldu. Üç denemenin her birine aynı dinamik katalog konusundan 1 doğru, 3 yanlış, 1 boş ve “Konu bilgisi” nedeni girildi. Böylece toplam 15 sınıflandırılmış soru üzerinden 60 dakikalık başlangıç önerisi üretildi. Öneri bilimsel öğrenme teşhisi olarak sunulmadı.

| Kapsam | Öğrencinin seçtiği ders / konu | Öğretmenin seçtiği ders / konu | Sonuç |
|---|---|---|---|
| TYT | Matematik (TYT) / Dörtgenler ve Özellikleri | Türkçe (TYT) / Cümlenin Ögeleri | İki rol geçti |
| AYT Sayısal | Matematik (AYT) / Birim Çember | Fizik (AYT) / Elektriksel Kuvvet | İki rol geçti |
| AYT Eşit Ağırlık | Matematik (AYT) / Birim Çember | Türk Dili ve Edebiyatı (AYT) / Cumhuriyet Dönemi Türk Hikâyesi-1 | İki rol geçti |
| AYT Sözel | Türk Dili ve Edebiyatı (AYT) / Cumhuriyet Dönemi Türk Hikâyesi-1 | Tarih (AYT) / Atatürk Dönemi İç Politikadaki Gelişmeler | İki rol geçti |
| İngilizce YDT | İngilizce (YDT) / Prepositions of Time, Place & Movement | İngilizce (YDT) / Prepositions of Time, Place & Movement | İki rol geçti |

- **30 yeni deneme ve 30 konu analizi** arayüzden kaydedildi. Öğrenci beyanları ile öğretmen kayıtları ayrı değerlendirildi; her öneri grubunda üç farklı deneme görüldü.
- Ders toplamlarında 1 doğru ve 3 yanlış için **0,25 net** görüldü; dört yanlış bir doğruyu götürüyor. Kalan sorular boş olarak kaydedildi; bunlara konu atanmadı.
- Her senaryoda katalogdan konu seçildi, üç denemeden öneri görüldü, takvim önizlemesi açıldı. Onay kutusu boşken uygulama düğmesi kapalıydı; açık onaydan sonra kayıt başarılı oldu.
- Her 60 dakikalık öneri farklı iki güne 30'ar dakika olarak yerleşti. Son İngilizce YDT senaryosunda gün/hafta/ay görünümleri ayrıca kontrol edildi ve öneri kartı günlük görünümde görüldü.
- Öğrenci ve öğretmenin AYT kapsamları birbirine karışmadı. Aynı konu farklı sınav kapsamlarında veya kaynaklarda kullanılsa da kapsam/kaynak kimliği ayrı tutuldu.
- Öğrenci ekranı yeniden açıldığında **20 tekrar ve 1 başlangıç çalışması** kalıcı olarak görünüyordu. SQL karşılaştırmasında 10 ayrı öneri anahtarı, toplam **600 dakika** ve **sıfır çakışma** doğrulandı.

## Bulunan hata ve düzeltme

Canlı katalogda “Matematik (TYT)”, “Fizik (AYT)”, “İngilizce (YDT)” ve “Felsefe (TYT/AYT)” gibi sınav eki taşıyan ders adları bulunuyordu. Konu analizi eşleştirmesi bu ekleri tanımadığı için TYT Matematik konu listesi boş geliyordu. Bu hata diğer YKS derslerini de etkileyebiliyordu.

`CoachingPracticeAdaptationService.Fits` içinde yalnız katalog sınavıyla uyumlu bilinen son ekler kaldırılarak mevcut kesin ders adı eşleştirmesi korundu. Katalog adı, kimliği veya veritabanı kaydı değiştirilmedi. Yanlış sınav eki, başka sınav kataloğu ve özel ders adları kabul edilmedi; İngilizce YDT testi Arapça dersine bağlanmadı.

Yeni regresyon testi gerçek katalog adlarını ve olumsuz eşleşmeleri kapsıyor. Mevcut altı kapsamlı PostgreSQL testi de sınav ekli ders adlarıyla güçlendirildi. Düzeltmeden önce iki test hatayı yeniden üretti; düzeltmeden sonra **30 deneme/analiz/uyarlama backend testi geçti**, başarısız veya atlanan test yok. Yerel PostgreSQL yalnız localhost'ta çalıştırıldı ve test sonunda durduruldu.

API üretim paketi başarıyla derlendi. Düzeltme `eduivme/coaching-service:coaching-practice-adaptation-20261020-r3` olarak canlıya alındı. Veritabanı geçişi uygulanmadı; portallar ve ilgisiz servisler yeniden başlatılmadı. API hazır olma kontrolü ve kesin imaj kimliği doğrulandı. İki tarayıcıda hata konsolu boş, güncel API günlüklerinde hata/istisna işareti yok.

## Mevcut kayıtların korunması

Başlangıçta Coaching ve Identity yedekleri alındı; eski satırların parmak izleri kaydedildi. Test sonunda her eski satırın hâlâ bulunduğu doğrulandı. Plan karşılaştırmasında yalnız beklenen `Version` ve `UpdatedAt` değişiklikleri dışarıda tutuldu. Identity oturum yenileme kayıtları karşılaştırmadan ayrı tutuldu.

- Önceden var olan **61.995 çalışma**, **1 tamamlanan çalışma** ve **61.979 kazanım bağlantısı** korundu. Eski deneme sonuçları, analizleri, hedefleri, hesapları ve ilişkileri de karşılaştırmadan geçti.
- LGS planına YKS tekrarı eklememek için ayrı manuel test planı oluşturulup geçici olarak yayımlandı: `15aba7c7-9907-470b-9b5d-585e5c412659`.
- Müsaitlik değiştirilmedi; daha önce kaydedilmiş Pazartesi 17.00–18.00 kullanıldı. Önizleme ufku açıkça 18 Ocak 2027 seçildi. Bu, müsaitlik süresini kendiliğinden artırmadı.
- Test planında başlangıç çalışması ve 20 yeni tekrar tutuluyor. Test sonunda bu plan arşivlendi; kayıtları silinmedi. Yeni denemeler test başlığı `YKS-REVIEW26` ile ayrılıyor.
- Asıl **`a4cea155-5485-4db6-9f52-1ec4df8080c3` LGS planı yeniden aktif**, sürümü 7 ve bitişi **1 Haziran 2027**. Eski çalışmaları ve kazanım bağlantıları aynı. Planın geçici arşivlenip geri etkinleştirilmesi sürüm/zaman bilgisini değiştirdi.
- Toplam çalışma sayısı test eklemeleriyle **62.016** oldu. Tamamlanan çalışma sayısı 1, kazanım bağlantısı sayısı 61.979 olarak kaldı.

## Kanıtlar

Klasör: `artifacts/coaching-practice-yks-regression-20261020/`.

- `browser-results.json`: On senaryonun öneri, önizleme ve onay sonrası durumları; 30 test denemesi.
- `student-*-preview.txt`, `teacher-*-preview.txt`: Senaryo başına canlı takvim önizlemesi.
- `teacher-YDT_EN-preview.png`: YDT aylık takvimi, önerilen saatler ve tarih etkisi.
- `teacher-applied.png`, `student-applied.png`: Başarılı kayıt ekranları.
- `public-verification/results.txt`: Veritabanındaki sonuç/analiz sayıları, plan durumu ve çakışma kontrolü.
- `public-verification/preservation.txt`: Başlangıçtaki satırlardan kaybolan veya değişen kayıt olmadığını gösteren karşılaştırma.
- `suffix-red.log`, `suffix-green.log`: Hatanın yeniden üretimi ve geçen regresyon testleri.

Bu sonuçlar belirtilen on akışın canlı doğrulamasıdır; bütün olası kullanıcı girdileri için hatasızlık garantisi değildir.
