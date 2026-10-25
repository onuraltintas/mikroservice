# Fiili çalışma haftası ve geçmiş değerlendirme takibi — 25 Ekim 2026

## 5. Gerçekleşme plan tarihiyle taşınmaz

Çalışmanın planlanan tarihi ile fiili çalışma günleri ayrıldı. Yeni kayıtta öğrenci fiili günü seçebilir; tarih verilmezse İstanbul'daki bugün kullanılır. Gelecek güne gerçekleşme yazılamaz. Eski istemcilerin birikimli süre/soru sözleşmesi korunur: önceki toplam ile yeni toplam arasındaki fark seçilen güne yazılır. Örneğin 28 Eylül'de toplam 20 dakika, 5 Ekim'de toplam 35 dakika kaydedilirse bu günlerin gerçekleşmeleri 20 ve 15 dakikadır. Görevin planı 26 Ekim'e taşınsa da bu miktarlar eski çalışma haftalarında kalır.

Raporlar planlanan iş yükünü plan tarihinden, gerçekleşen süre/soruyu çalışma günlerinden hesaplar. Plan tarihi dönem dışında olsa da o dönemde çalışılan görev sorguya girer. Aynı kökten gelen devam çalışmalarının geçmiş gerçekleşmeleri kopyalanmaz. Planlanmış iş bulunmayan ama fiili çalışma olan hafta da raporda miktar gösterir. Tamamlanma oranı, seçilen döneme planlanmış işlerin güncel durumunu göstermeye devam eder; fiili çalışma sayısı değildir.

Öğrenci ve personel çalışma raporları, haftalık değerlendirme özeti ve karar sonuçlarının çalışma kanıtı aynı dönem hesabını kullanır. Çalışma ekranı gün dağılımını gösterir; arşiv, yönetici okuması ve veri dışa aktarımı bu dağılımı korur. Mevcut değerlendirmelerin kaydedilmiş sonuç görüntüleri ve önceki sürümleri yeniden hesaplanıp yazılmaz.

Gerekçeli miktar düzeltmesinde fark ilgili fiili güne uygulanır. Seçilen gündeki miktardan fazla azaltma reddedilir. Tarih yanlışsa kaynak gün ve yeni fiili gün gerekçeyle belirtilerek günlük kayıt taşınabilir; miktar aynı kalır. Hatalı gerçekleşmenin tümüyle geri alınması günlük dağılımı temizler, önceki dağılım denetim geçmişinde kalır. Düzeltmenin kaydedildiği gün ile çalışmanın fiili günü ayrı tutulur.

## Eski veriler

`20261024223755_ActualStudyWorkDates` geçişi `ActualWorkJson`, `WorkFromDate`, `WorkToDate` alanlarını ve dönem sorgusu indeksini ekler. Mevcut bir gerçekleşmede fiili gün bilinmediği için eski plan günü yalnız yeni tarih alanlarına sabitlenir. Eski süre, soru, plan tarihi, durum, sürüm, tarihçe ve tamamlanma zamanı değiştirilmez. Bu kayıtlar `IsLegacyDate` ile işaretlenir: arayüz bunları fiili tarihi bilinmeyen, eski plan tarihine dayalı kayıt olarak gösterir. Böylece mevcut rapor atfı korunur ama sonraki plan taşıması miktarı başka haftaya götürmez. Kullanıcı fiili günü biliyorsa gerekçeyle düzeltebilir.

Yeni tarihlenmiş gerçekleşme verisini veya sabitlenmiş eski tarih atfını kaybettirecek sürüm düşürme engellenir. Yeni isteğe bağlı günlük veriler mevcut dışa aktarım sözleşmesini genişletir.

## 6. Geçmiş haftaların eksik değerlendirmeleri

Yalnız son haftayı kontrol eden sorgu yerine, öğrencinin bilinen koçluk başlangıcından itibaren tamamlanmış haftalarda kaydı olmayan değerlendirmeler hesaplanır. Başlangıç için öğrenci profilinin ve geçmiş öğretmen ilişkilerinin en erken kayıt tarihi kullanılır. İçinde bulunulan tamamlanmamış hafta için eksik kayıt üretilmez. Yeni öğretmen atanması başlangıcı sıfırlamaz; mevcut erişim ve paylaşım izinleri uygulanır.

Eksik değerlendirmeler ve gönderilmiş ama bu öğretmen tarafından güncel sürümü incelenmemiş değerlendirmeler, takip filtresinin başlangıcından daha eskiyse de kuyrukta kalır. Bitiş tarihi üst sınır olarak korunur. Arayüz bu kapsamı açıklar. Böylece varsayılan son 30 gün filtresi eski bekleyen işi düşürmez. Diğer çalışma/ödev/karar türlerinin dönem filtresi değişmez. Sonuç sınırı aşılırsa toplamlar sessizce kırpılmaz.

Her öğrenci-hafta ayrı satırdır; arayüz anahtarı hafta bilgisini içerir. Tarih ve kimliklerle kararlı sıralama/sayfalama uygulanır. Öğrenci bir haftayı gönderdiğinde yalnız o hafta eksik kuyruğundan çıkar ve inceleme kuyruğuna geçer. Öğretmen incelemeyi kaydedene kadar burada kalır. Öğrenci ana ekranı eksik hafta sayısını gösterir ve en eski eksik haftayı açar. Mevcut değerlendirmeler, inceleme görüntüleri ve kararlar dönüştürülmez veya silinmez.

## Doğrulama

PostgreSQL üzerinde çok haftalı gerçekleşme, plan taşıması, tarih/miktar düzeltmesi, eski kayıt geçişi, kayıpsızlık ve aşağı sürüme geçiş koruması test edildi. Kuyruk testleri bir öğrencinin birden fazla eski haftası, sayfalama, eski haftanın gönderilmesi ve incelenmesi, öğretmen değişimi, kayıt başlangıcı, bitiş tarihi ve erişim kaybını kapsar.

Test ve derleme günlükleri `artifacts/coaching-work-dates-reflection-backlog-20261025/` altındadır. Testler geçici PostgreSQL veritabanlarında çalıştırıldı. Canlı ortama dağıtım veya canlı veritabanına geçiş uygulanmadı.

Sonuçlar:

- Sunucu regresyonu: 243 geçti, 0 hata, 0 atlanan.
- API, rapor hesabı ve veri koruma: 24 geçti.
- Öğrenci/admin arayüzü: 162 geçti.
- Öğretmen/personel arayüzü: 143 geçti.
- Çalışma raporu ekranı: 15 geçti.
- Toplam: 587 başarılı test çalıştırması.
- API Release derlemesi: 0 hata, 0 uyarı. İki arayüzün üretim derlemeleri başarılı; mevcut paket ve stil boyutu bütçesi uyarıları devam ediyor.
