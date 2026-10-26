# Çalışma planı son canlı regresyonu — 19 Ekim 2026

## Sonuç

LGS, TYT, AYT Sayısal, Eşit Ağırlık, Sözel ve İngilizce YDT için öğrenci/öğretmen × manuel/otomatik olmak üzere **24 nihai senaryo geçti**. Taslak oluşturma, yayımlama ve karşı tarafın yayımlanan planı açması canlı kullanıcı arayüzlerinde doğrulandı; veritabanı sonuçları ayrıca denetlendi.

| Kapsam | Öğrenci manuel | Öğrenci otomatik | Öğretmen manuel | Öğretmen otomatik |
|---|---|---|---|---|
| LGS | Geçti | Geçti | Geçti | Geçti |
| TYT | Geçti | Geçti | Geçti | Geçti |
| AYT Sayısal | Geçti | Geçti | Geçti | Geçti |
| AYT Eşit Ağırlık | Geçti | Geçti | Geçti | Geçti |
| AYT Sözel | Geçti | Geçti | Geçti | Geçti |
| İngilizce YDT | Geçti | Geçti | Geçti | Geçti |

## Testin sınırları ve yöntemi

Manuel senaryoda **Yeni taslak** düzenleyicisinden 2 Kasım 2026 08.00 için 30 dakikalık, 10 soruluk bir çalışma eklendi; taslak kaydedildi, onaylanarak yayımlandı ve diğer kullanıcıda açıldı. Bu serbest giriş yolunda katalog kazanımı zorunlu değildir. Bu son seri, önceki testlerde doğrulanan haftalık takvimin bütün sağ tık/taşıma/boyutlandırma etkileşimlerini yeniden kapsamıyor.

Otomatik senaryoda hedefe uygun bütün dersler seçildi; sihirbaz önerileri haftalık taslağa eklendi. Öğrenci için haftada 28, öğretmen için 35 saatlik takvim kullanıldı. 26 Ekim 2026–1 Haziran 2027 önizlemesi, boşalan saatleri kullanma, ders bitiş özetleri, taslak kaydı, onay ve yayımlama kontrol edildi. Öğretmen seçili öğrenci adına işlem yaptı; yayımlanan plan öğrenci tarafından da açıldı.

## Bulunan ve düzeltilen hata

Sihirbazın her yeni önerisi ders listesinin başından başlıyordu. Sözel kapsamındaki 16 dersin ilk 14'ü tekrar önerilebildiği için AYT Edebiyat ve TYT Türkçe dışarıda kalabiliyordu. İlk sonuç 37.013 dakika iken tam kapsam 42.763 dakikadır; fark 5.750 dakikadır.

Yeni öneriler mevcut taslakta daha az saat ayrılmış derslere öncelik veriyor. Ayrıca seçilmiş ancak mevcut taslak ve öneride saati olmayan dersler adlarıyla belirtiliyor. Mevcut kartlar korunuyor. Ortak bileşen sayesinde düzeltme öğrenci ve öğretmen için geçerli.

Önce hata testlerle üretildi: 4 başarısız, 36 başarılı. Düzeltmeden sonra **40/40 test geçti**. Her iki portalın üretim derlemesi geçti. Düzeltme canlıya alındıktan sonra 12 otomatik senaryonun tamamı yeniden yayımlanarak doğrulandı. Önceden geçen 12 manuel senaryonun yolu değiştirilmedi.

Hızlı gezinme sırasında gözlenen taslak açma gecikmesi ayrıca iki gecikmeli yanıt testiyle incelendi. Mevcut korumalar testleri geçti; doğrulanmış bir uygulama hatası olmadığı için bu konuda üretim kodu değiştirilmedi.

## Nihai otomatik plan sonuçları

Farklı bitiş tarihleri haftalık 28/35 saat farkından kaynaklanır. Her planda seçili derslerin tamamı bulunur.

| Kapsam | Ders | Toplam dakika | Öğrenci çalışma sayısı | Öğretmen çalışma sayısı | Öğrenci son gün | Öğretmen son gün |
|---|---:|---:|---:|---:|---|---|
| LGS | 6 | 11.970 | 504 | 503 | 14.12.2026 | 04.12.2026 |
| TYT | 9 | 31.048 | 1.820 | 1.821 | 04.03.2027 | 06.02.2027 |
| AYT Sayısal | 13 | 51.393 | 2.841 | 2.847 | 28.05.2027 | 15.04.2027 |
| AYT Eşit Ağırlık | 13 | 46.058 | 2.572 | 2.574 | 05.05.2027 | 28.03.2027 |
| AYT Sözel | 16 | 42.763 | 2.867 | 2.867 | 22.04.2027 | 17.03.2027 |
| İngilizce YDT | 10 | 35.998 | 2.190 | 2.191 | 24.03.2027 | 22.02.2027 |

Sunucu denetimleri: çalışma tarihleri hedef aralığında; saat çakışması yok; otomatik çalışmalarda kazanım ve süre tahmini bağlantıları mevcut; puan türü kapsamları doğru; seçilen bütün dersler bulunuyor ve dakika toplamları beklenen bütçeyle aynı.

## Veri koruma ve test temizliği

Testlerden önce üç veritabanının yedeği ve mevcut iş kayıtlarının satır bazında karşılaştırma özetleri alındı. İlk denemeler ve tekrarlar dahil 37 test planı yayımlandı, ardından **arşivlendi**; geçmişleri silinmedi. Nihai matris bunların 24 son başarılı senaryosunu içeriyor.

Orijinal `a4cea155-5485-4db6-9f52-1ec4df8080c3` LGS planı tekrar aktif bırakıldı. 560 çalışma, tamamlanan 1 çalışma, 55 dakikalık gerçekleşen süre ve 560 kazanım bağlantısı korundu. Öğrencinin başlangıçtaki LGS eğitim bağlamı, ders tercihleri, konu seçimleri, 12 kartlık haftalık taslağı ve müsaitlik ayarları geri yüklendi.

Önceden mevcut iş kayıtları, hesap/rol/erişim ilişkileri ve 22 bildirim satırının içerikleri değişmedi. Test yayımlamalarının 37 yeni bildirimi tutuldu; toplam 59 bildirim bulunuyor. Giriş zamanı gibi oturum metaverileri hesap karşılaştırmasının dışında tutuldu.

Bildirimlerin eski satırlarını doğrulamak için yedek ayrı bir karşılaştırma veritabanına açıldı; canlı bildirim veritabanının üzerine geri yükleme yapılmadı.

## Canlı sürüm ve kanıtlar

Yalnız iki portal güncellendi: `coaching-wizard-coverage-20261019`. Backend `coaching-yks-20261019-r3` olarak kaldı; bu düzeltmede şema geçişi yapılmadı. Portal imajları ve hazır olma kontrolleri doğrulandı; diğer konteynerler korunarak dağıtıldı.

- Canlı compose: `/var/lib/eduivme/releases/coaching-wizard-coverage-20261019/production.compose.json`
- Paket SHA256: `4a4a15b8c2886f1e8fc52d167488f90b49d64bc1c5c5245b9040d6d4de2d349a`
- [24 nihai senaryo ve plan kimlikleri](../../artifacts/coaching-final-regression-20261019/matrix-final.json)
- [Sunucu sonuçları ve kapsam/çakışma denetimi](../../artifacts/coaching-final-regression-20261019/server-results-final.log)
- [40 testin son başarılı çıktısı](../../artifacts/coaching-final-regression-20261019/coverage-green-final.log)
- [Dar kapsamlı üretim düzeltmesi](../../tools/releases/coaching-final-regression-20261019/wizard-coverage-fix.patch)
- [Öğretmende korunan orijinal LGS takvimi](../../artifacts/coaching-final-regression-20261019/teacher-original-lgs-restored-final.png)

Ham ekran görüntülerinin bir bölümünde tarayıcı boyaması önceki görünümü yakaladı; bunlar tek başına nihai kanıt sayılmadı. Sonuçlar güncel arayüz metni, yayımlama sonucu, diğer kullanıcıda açılma ve sunucu kayıtları birlikte denetlenerek doğrulandı.

Katalog kaynakları, 2026 resmî kapsamının geçerliliği ve çalışma sürelerinin editoryal tahmin niteliği [önceki aktarım raporunda](yks-tyt-ayt-ydt-20261019.md) açıklanmıştır. Bu test sonucu bütün olası gelecekteki kullanım koşulları için hatasızlık garantisi değildir; yukarıdaki canlı senaryolarda açık hata kalmamıştır.
