# Otomatik kazanım kredisi ve gerekçeli tarih değişikliği — 25 Ekim 2026

## Otomatik planda tamamlanan kapsam

Otomatik plan, seçilen kazanımların kalan yükünü hesaplarken öğrencinin aktif ve yayımlanmış arşiv planlarındaki tamamlanan kazanım paylarını düşer. Haftalık planlayıcı da aynı tamamlanmış kanıt okuyucusunu kullanır. Başka öğrencinin çalışması veya hiç yayımlanmamış arşiv taslağı kredi sağlamaz.

Kredi fiili çalışma dakikasından değil, göreve kaydedilmiş kazanım kapsamından hesaplanır. Örneğin 25 dakika gerçekleşme kaydı bulunan 30 dakikalık kazanım payı, seçilen 60 dakikalık yükü 30 dakikaya indirir. Aynı çalışmanın kökü ve kazanımı bir kez sayılır; devralınmış tamamlanma ikinci kredi üretmez. Eski süre sürümündeki tamamlanmış pay da korunur. Konuya özel pay kendi konusundan düşer; genel pay seçilen konular arasında bir kez tüketilir. İndirim seçilen kapsamı aşamaz ve kalan yük negatif olamaz.

Önce tamamlanan kapsam, sonra korunacak açık devam çalışmalarının temsil ettiği kapsam düşülür. Devam görevleri kendi kalan süreleriyle korunur; yeni görevler ve kazanım bağlantıları yalnız hesaplanan yeni yük için üretilir. Mevcut görevler, fiili günler, gerçekleşmeler, kazanım dağılımları ve geçmiş plan hesapları yeniden yazılmaz. Kazanım seçilmeden doğrudan girilmiş konu süresi, açık bir süre talebi olarak korunur; kazanım kapsamı tahmin edilmez.

Önizleme tamamlanan kazanımlardan düşülen dakikayı gösterir. Yeni veya devreden iş kalmadığında boş taslak kaydetme sunulmaz. Tamamlanan kanıtın parmak izi yeni istemcinin kaydetme onayında doğrulanır. Taslak üzerinde seçilen kazanım kimlikleri ve kanıt parmak izi saklanır; yayımlama sırasında tekrar kontrol edilir. Arşivdeki bir tamamlanma düzeltilmişse, aktif planın sürümü aynı kalsa bile eski yükle yayın yapılamaz.

Eski otomatik taslakta bu kanıt bulunmuyorsa ve görev kazanımları için tamamlanmış kapsam varsa yeniden önizleme istenir; eski taslak silinmez. Mevcut eski istemciler kaydederken kalan yük sunucuda yeniden hesaplanır. Yeni kanıt kişisel veri dışa aktarımında da yer alır.

## Kararın gelecekteki son tarihini değiştirme

Açık kararın gelecekteki son tarihi, sorumlu öğretmen tarafından gerekçeyle ileri veya geri alınabilir. Yeni tarih bugünden sonra ve kaynak değerlendirme haftasının ardından olmalıdır. Gecikmiş karar için mevcut gerekçeli erteleme yolu korunur; aynı yeni işlem gecikmiş karar üzerinde kullanılırsa erteleme kuralları uygulanır. Tamamlanmış ve iptal edilmiş kararların tarihi değiştirilmez.

Her gerçek tarih değişikliği, karar geçmişine yeni kayıt ekler. İlk son tarih, kaynak değerlendirme haftası ve kaynak değerlendirme/önceki karar bağlantıları korunur. `DeadlineChange` kaydı önceki tarih, yeni tarih, gerekçe, işlem zamanı ve sorumlu öğretmeni saklar. Önceki geçmiş kayıtları değiştirilmez. Bağlı çalışma ve ödev tarihleri taşınmaz.

Genel karar düzenlemesi başlık, not ve durum için kullanılmaya devam eder; mevcut kararın tarihi bu işlemde gerekçesiz değiştirilemez. Öğretmen ekranında açık kararların tümünde gerekçeli tarih değişikliği açılabilir. Yeni tarih ve gerekçe hem arayüzde hem sunucuda doğrulanır. Öğrenci ve salt okunur personel görünümü tarih değiştiremez.

Öğrenci kilidi, karar sürümü ve sorumlu öğretmen yetkisi kontrol edilir. Aynı başarılı isteğin tekrarı yeni geçmiş veya bildirim üretmez; eski sürümle farklı değişiklik reddedilir. Tarih kaydı ve bildirim outbox işlemi aynı transaction içindedir; yayın başarısızlığında birlikte geri alınır. Değişen son tarih mevcut sonuç karşılaştırmasına da katıldığı için önceki incelemenin takip durumu güncel kanıta göre hesaplanır.

## Veri geçişi ve testler

`20261024234143_AutomaticCompletedOutcomeEvidence`, plan revizyonuna nullable JSON kanıt alanı ekler. Geçiş sırasında mevcut plan satırları ve eski karar/gerçekleşme geçmişleri yeniden yazılmaz. Kaydedilmiş otomatik plan kanıtını kaybettirecek sürüm düşürme engellenir. Karar tarihi değişikliği mevcut tarih ve geçmiş alanlarını kullanır; eski kararlara tahmini gerekçe eklenmez.

Yeni testler aktif/arşiv kanıtını, fiili süre ile kapsam ayrımını, kök üzerinden tekilleştirmeyi, başka öğrenci ve yayımlanmamış taslak sınırını, eski/yeni süre sürümlerini ve konu paylarını, sıfır kalan yükü, önizleme–taslak–yayın–tamamlama akışını, arşiv düzeltmesi sonrası eski onay/taslak reddini, dışa aktarımı ve veri geçişini kapsar. Karar testleri gelecekteki tarihi iki yönde değiştirmeyi, ilk tarihi ve bağlı işleri korumayı, gerekçe/sürüm/yetki kontrollerini, gerçek MVC doğrulamasını, tekrar isteğini ve outbox başarısızlığında geri almayı doğrular.

Önceden otomatik planda tamamlanan kapsamı yeniden bekleyen kapsam testleri yeni kalan yük hesabına güncellendi. 500 görev sınırı testi, korunacak devam görevlerinin önce yerleşebileceği yeterli saatle sınırı sınayacak şekilde düzenlendi. Eski sütunların değişmediğini karşılaştıran geçiş testleri yeni nullable alanı karşılaştırma dışında tutar; mevcut sütunlar aynen denetlenir. Otomatik plan ekranı testleri sürekli arayüz regresyon komutuna da eklendi.

Günlükler ve TRX raporları: `artifacts/coaching-automatic-credit-deadline-20261025/`. Testler geçici PostgreSQL veritabanlarında çalıştırıldı. Canlı veritabanına geçiş veya dağıtım yapılmadı.

Sonuçlar:

- PostgreSQL destekli sunucu regresyonu: 275 geçti, 0 hata, 0 atlanan.
- Otomatik/manuel plan API, zamanlayıcı, rapor hesabı ve veri koruma: 47 geçti.
- Öğrenci/admin arayüzü: 180 geçti.
- Öğretmen/personel arayüzü: 150 geçti.
- Toplam: 652 başarılı test çalıştırması. Son tarih ve otomatik planla ilgili 42 arayüz testi son ekran değişikliğiyle ayrıca doğrulandı.
- API Release: 0 hata, 0 uyarı. İki arayüzün üretim derlemeleri başarılı; mevcut paket/stil boyutu bütçesi uyarıları sürüyor.
