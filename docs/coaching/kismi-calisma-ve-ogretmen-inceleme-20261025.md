# Kısmi gerçekleşme, gerekçeli düzeltme ve öğretmen inceleme kuyruğu

25 Ekim 2026. Çalışma yerel kaynak koduna uygulanmıştır; canlı ortam veya kullanıcı veritabanına dağıtım yapılmamıştır.

## Öğrenci gerçekleşmesi

- Öğrenci aktif plandaki tamamlanmamış bir çalışmaya toplam süre ve isteğe bağlı toplam soru sayısı girerek **Kısmi kaydet** işlemini kullanır. Değerler o görevde şimdiye kadar biriken toplamdır; önceki toplama tekrar eklenmez. Aynı değerleri güncel sürümle yeniden kaydetmek sürümü veya geçmişi büyütmez.
- **Tamamla** aynı toplamları kullanır. Kısmi süre ve soru sayısı raporlara girer, çalışma tamamlanan görev sayısına veya tamamlanmış kazanım kredisine girmez.
- Kayıtlı toplamı azaltmak, soru bilgisini kaldırmak veya tamamlanmış gerçekleşmeyi değiştirmek için **Gerçekleşmeyi gerekçeyle düzelt** gerekir. Gerekçe 5–1000 karakterdir. Öğrenci kendi kaydını düzeltir; öğretmen öğrencinin gerçekleşmesini değiştiremez.
- Düzeltme aktif planda ve daha önce yayımlanmış arşiv planda yapılabilir. Süre, soru sayısı ve tamamlanma durumu düzeltilebilir. Tamamlanmış durum korunuyorsa ilk tamamlanma zamanı korunur. Yeniden devam ediyor durumuna alınırsa önceki tamamlanma zamanı geçmişte saklanır. Tamamen hatalı bir gerçekleşme 0 dakika, boş soru alanı ve tamamlanmadı durumuyla gerekçeli olarak geri alınabilir; geçmişi silinmez.
- Her değişiklikte önceki/yeni toplamlar, önceki/yeni tamamlanma durumu, önceki tamamlanma zamanı, işlem türü, kullanıcı, zaman ve gerekçe saklanır. Eski kayıtlar için tarihçe uydurulmaz; ilk düzeltme mevcut eski değerleri tarihçeye alır.
- Gerçekleşme içeren görev silinemez. Plan yenilenince önceki görev ve gerçekleşmesi arşivde kalır; yeni görev kopyasına önceki gerçekleşme tekrar yazılmaz. Aynı kökten ve aynı tarihten gelen görevlerin planlanan yükü bir kez, ayrı görevlerde kaydedilmiş emekleri toplam olarak hesaplanır. Raporlar mevcut durumda planlanan dönemin hesabıdır; geçmiş bir tarihteki durumun dondurulmuş görüntüsü değildir.

## Öğretmen incelemesi

Takip merkezindeki **İnceleme bekleyen haftalık değerlendirmeler**, öğrencinin kaydettiği değerlendirmeleri listeler. Bu, öğrenciden henüz gelmeyen değerlendirme hatırlatmasından ve değerlendirme bekleyen ödev teslimlerinden ayrı bir kuyruktur.

- Kuyruk yalnız son haftaya bağlı değildir. Tarih filtresi değerlendirilen haftanın pazar gününe uygulanır; eski haftalar için dönem genişletilir. Varsayılan filtre son 30 gündür.
- Haftayı açmak inceleme sayılmaz. Öğretmen 5–2000 karakterlik takip notuyla **İnceledim, kuyruğu kapat** işlemini kullanır. Yeni karar gerekiyorsa karar alanından ayrıca oluşturulur.
- İnceleme belirli öğretmene ve öğrencinin belirli değerlendirme sürümüne bağlıdır. İncelenen metnin tamamı ayrıca korunur. Öğretmenin notu öğrencinin değerlendirme sürümünü değiştirmez.
- Öğrenci değerlendirmeyi değiştirirse yeni sürüm yeniden kuyruğa girer. Eski inceleme ve incelenen metin silinmez. Öğretmen değişince yeni yetkili öğretmenin incelemediği kayıtlar kendi kuyruğunda görünür; eski öğretmenin geçmiş incelemeleri korunur.
- Atama, aktif profil ve öğrenci verisini okuma izinleri mevcut yetki politikalarıyla doğrulanır. Sürümü değişmiş değerlendirme üzerine eski metin incelenmiş gibi kayıt atılamaz.

## Veri geçişi ve uçlar

`20261024211012_PartialWorkAndReflectionReviews` eski süre, soru, tamamlanma zamanı ve plan kayıtlarını değiştirmeden `RealizationHistoryJson` alanını boş diziyle ekler. Yeni `weekly_reflection_reviews` tablosu inceleme sürümünü, öğretmenini, notunu ve incelenen metni saklar. Yeni geçmiş, kısmi gerçekleşme veya inceleme kaydı varsa veri kaybettirecek şema geri dönüşü reddedilir.

Yeni uçlar:

- `PUT /api/coaching/study-planning/plans/{id}/tasks/{taskId}/progress`
- `POST /api/coaching/study-planning/plans/{id}/tasks/{taskId}/realization-corrections`
- `POST /api/coaching/staff/students/{studentId}/study/weekly-review/{weekStart}/review`
- Takip merkezi `mode=weekly-review`; yeni sayaç `awaitingWeeklyReviewCount`.

Gerçekleşme istekleri `expectedVersion`, `actualMinutes`, `actualQuestions`, `isCompleted`, düzeltmede ayrıca `reason` taşır. İnceleme isteği `expectedReflectionVersion` ve `note` taşır. Yazımlar öğrenci bazında aynı işlem kilidini ve veritabanı işlemini kullanır. Çakışmada eski değerler silinmez. Dışa aktarım şeması 1.7 yeni gerçekleşme geçmişini ve incelemeleri içerir; kişisel veri silme/değerlendirme akışları da incelemeleri kapsar.

## Doğrulama

- Gerçek PostgreSQL 17 üzerinde tüm koçluk regresyonu: **222 başarılı, 0 başarısız, 0 atlanan**.
- Veri silme/değerlendirme ve plan API sözleşmeleri: **15 başarılı**. Yeni uçların hız/sınır/sürüm sözleşmeleri ayrıca **7 başarılı** testle doğrulandı.
- Öğrenci arayüzü koçluk regresyonu: **152 başarılı**; öğretmen arayüzü: **137 başarılı**.
- Takvim bileşeni: **13 başarılı**; kısmi çalışma tamamlandı olarak gösterilmez.
- Koçluk API Release derlemesi: **0 uyarı, 0 hata**. Her iki Angular uygulamasının üretim derlemesi başarılı; mevcut stil/paket bütçesi uyarıları devam eder.
- Yeni senaryolar: toplamların tekrar gönderimi, sessiz azaltmayı reddetme, arşivlenmiş gerçekleşmenin gerekçeli düzeltmesi, eski tamamlanma zamanının korunması, eski kayıt biçiminden göç, kayıplı geri dönüşün engellenmesi, eski haftaların kuyruğu, yalnız güncel sürümün incelenmesi, öğretmen devri, dışa aktarım ve başarısız yazımdan sonra aynı kapsamda güvenli yeniden deneme.

Test çıktıları `artifacts/coaching-progress-review-20261025/` altında tutulur. Testler yerel, ayrı ve geçici veritabanlarında çalıştırılmıştır.
