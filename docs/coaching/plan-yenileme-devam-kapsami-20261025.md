# Plan yenilemede çalışma devamlılığı — 25 Ekim 2026

## Uygulanan davranış

Açık karara bağlı çalışma, yeni plan yayımlandığında aynı kökten gelen devam görevine bağlanır. Devir; önceki görev kimliği, yeni görev kimliği ve işlemi yapan kişiyle karar geçmişine eklenir. Kapalı kararların tarihsel bağlantıları korunur. Plan, karar ve bildirimler aynı işlem içinde kaydedilir; hata durumunda birlikte geri alınır.

Kısmi çalışma, sabitlenmemiş olsa da yeni plana taşınır. Yeni görevin planlanan süresi, açıkça belirtilen kalan süredir. Örneğin 60 dakikalık işte 20 dakika çalışılmış ve kalan süre 25 dakika olarak belirtilmişse yeni plan 25 dakika ayırır. Gerçekleşen süre, öğrenilen kazanım kapsamı olarak yorumlanmaz. Önceki gerçekleşme eski görevde kalır; devam görevine kopyalanmaz. Yeni gerçekleşmeler yalnız yeni görevde yapılan çalışmayı kaydeder.

Kalan süre ve isteğe bağlı kalan soru sayısı, kısmi gerçekleşme kaydında veya “Yalnız kalan kapsamı kaydet” işlemiyle öğrenci ya da yetkili öğretmen tarafından belirtilebilir. İkinci işlem gerçekleşme toplamını değiştirmez. Eski kısmi kayıtların kalan kapsamı tahmin edilmez; kullanıcıdan belirtmesi istenir. Aktif devamı bulunan eski görev yerine aktif görev güncellenir.

Otomatik, kazanım temelli ve elle oluşturulan planlar devam görevlerini korur. Aynı kazanım kapsamı ikinci kez yeni iş olarak üretilmez. Kazanım bağlantıları kaynak ve devamın toplam kapsamını temsil eder; tamamlanma kredisi tamamlandığında verilir. Aynı gün aynı kökün tarihsel plan paydası kısaltılmaz.

Devam görevleri uygun zaman aralığına kalan süreyle yerleştirilir. Tek görev parçalara bölünmez; yeterli kesintisiz zaman yoksa açık hata verilir. Kaynak görev taslak oluşturulduktan sonra değişirse yayımlama engellenir ve güncel önizleme istenir.

## Veri koruma

`20261024220155_StudyContinuationScope` geçişi dört nullable alan ekler: RemainingMinutes, RemainingQuestions, PredecessorTaskId, PredecessorVersion. Eski görevler, gerçekleşmeler ve karar geçmişleri yeniden yazılmaz. Yeni alanlar geçmiş kayıtlar için boş kalır. Yeni kalan kapsam ve devir işlemleri eklemeli denetim geçmişi üretir. Devam verisi mevcutsa veri kaybına yol açacak sürüm düşürme engellenir.

## Doğrulama

- PostgreSQL destekli koçluk regresyonu: 235 geçti, 0 hata, 0 atlanan.
- API ve veri koruma testleri: 20 geçti.
- Öğrenci/admin arayüzü: 157 geçti; personel arayüzü: 140 geçti.
- API Release derlemesi: başarılı, 0 uyarı, 0 hata.
- Öğrenci/admin ve personel üretim derlemeleri: başarılı. Mevcut paket/stil boyutu bütçesi uyarıları devam ediyor; ayrıntılar derleme günlüklerinde.

Yeni testler ardışık devir, geçmiş kayıtların korunması, kalan kapsam zorunluluğu, eski kopyaların kapsam beyanıyla devamı, elle/otomatik plan, mükerrer kazanımın önlenmesi, tarihsel rapor paydası, eski şemadan geçiş ve outbox hata/yeniden deneme senaryolarını kapsar.

Kanıtlar: `artifacts/coaching-study-continuation-20261025/`. Testler geçici PostgreSQL veritabanlarında çalıştırıldı; canlı ortama dağıtım veya canlı veritabanı geçişi yapılmadı.
