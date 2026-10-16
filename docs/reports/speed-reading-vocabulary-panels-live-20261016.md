# Kelime sonuçları — öğretmen ve kurum canlı kontrolü

Tarih: 16 Ekim 2026. Kapsam: mevcut canlı kelime testinin öğretmen ve kurum panellerinde görünürlüğü. Kontroller tamamlandı; **personel panellerine eksiksiz yansıma onaylanamadı**. Aşağıdaki üç engel bulundu. Uygulama veya veritabanı değişikliği yapılmadı.

## Korunacak başlangıç

Test öğrencisi `testogrenci@eduivme.com`, test öğretmeni `testogretmen@eduivme.com`, kurum yöneticisi `testkurum@eduivme.com`. Öğrenci test kurumuna ve öğretmenine bağlıdır.

Başlangıç XP **476**, tamamlanmış sonuç **21**, mevcut programa bağlı günlük kayıt **12**; program **Gün 3**, kazanılmış ek gün **1**. Önceki kelime testi 12/12 doğru, skor 100 ve +60 XP olarak kaydedilmiştir; bu test katalogdan başlatılmış bağımsız çalışmadır, atanmış ödev veya günlük program görevi değildir.

Sonuç kimliği `0ba93f94-d20f-4634-a7b7-6127be95309f`, oturum `2836eb5e-d266-4ad0-9325-f61b6b548291`. Puan, günlük programın bütün alanları, eski oturum/sonuç satırları, kelime ilerlemeleri ve başarımlar kontrol öncesi salt okunur SQL ile kaydedildi.

## Kurum oturumu — doğrulananlar

Mevcut gerçek kurum oturumundan Hızlı Okuma ürününe geçildi. Aktif kurum, tek test öğrencisi ve bağlı “Canlı Test teacher” arayüzde görüldü. Kurum üyeleri, genel özet ve içerik analizi API istekleri HTTP 200 döndü.

Kurum özetinde **20 etkinlik**, **%87 anlama**, ölçülmüş WPM için **Veri yok** görüldü. İçerik analizi **12 günlük egzersiz** ve mevcut okuma kayıtlarını gösteriyor; bağımsız Kelime Bilgisi Testi satırı bulunmuyor. Önceki öğrenci raporunda da gözlenen kapsam burada aynıdır: analitik günlük egzersiz ve okuma kayıtlarını kullanıyor, yeni bağımsız kelime oturumu bu toplama dahil edilmiyor. Bu nedenle kurum raporunda yeni kelime sonucunun görünürlüğü geçti olarak işaretlenmez.

### Kurum öğrenci ayrıntısı engeli

Öğrenci satırındaki **Rapor** düğmesine basıldığında ayrıntı açılmadı. Yenilemeden sonra doğal üyeler isteğinin yanıtı incelendi: `role: 1`. Canlı staff kaynak tabanında `openStudentReport` ise `member.role !== 'Student'` koşulunda geri dönüyor. Sayı/metin sözleşmesi uyuşmuyor. Bu bir yetki ataması eksikliği değildir; aktif öğrenci ve öğretmen bağlantısı yanıt ve tabloda vardır. Testte rol veya erişim değiştirilmedi.

Kanıtlar: [Kurum içerik ekranı](../../artifacts/vocabulary-panel-verification-20261016/institution-content.jpg), [ekran metni](../../artifacts/vocabulary-panel-verification-20261016/institution-content.txt), [rol sözleşmesi](../../artifacts/vocabulary-panel-verification-20261016/institution-role-contract.json). Kimlik doğrulama token'ı, cookie veya parola kanıtlara alınmadı.

## Öğretmen oturumu — sayfanın altındaki hata

Otomatik giriş CAPTCHA aşamasında beklediği için kullanıcı normal tarayıcıda öğretmen girişini tamamladı. Açık yeni gerçek öğretmen sekmesi bulundu ve Hızlı Okuma ürününe geçildi. CAPTCHA ayarı değiştirilmedi. Öğretmene bağlı test öğrencisi, test kurumu, **20 etkinlik**, **%87 anlama** ve WPM için **Veri yok** görüldü.

**Ayrıntılı rapor** açılınca sayfanın altında “Öğrenci raporu yüklenemedi. Öğretmen-öğrenci bağlantısını kontrol edip tekrar deneyin.” hatası çıktı. Normal **Tekrar dene** işlemiyle sorun yeniden üretildi. API yanıtları: `summary`, `reading-speed`, `comprehension` HTTP **200**; `activity` HTTP **500**. Bu yüzden birleşik ayrıntı raporu gösterilemiyor; kelime ayrıntısını burada doğrulamak mümkün olmadı.

Sunucu stack trace'i hatayı `OwnedSpeedReadingAnalytics.GetStudentActivityAsync` satır 570'e bağladı. Yakın sorgular GUID dizilerinin `.Contains` çağrılarını kullanıyor. Gerçek hata, .NET 10 `ReadOnlySpan<Guid>` overload'u ile EF sorgu parametresinin değerlendirilmesi arasındaki uyumsuzluktur. Bu bir kullanıcı/öğrenci bağlantısı eksikliği olarak gösterilmemelidir; hata mesajı gerçek sunucu sorununu yansıtmıyor. Bu test turunda sorgu veya mesaj değiştirilmedi.

Kanıtlar: [Öğretmen ekranı](../../artifacts/vocabulary-panel-verification-20261016/teacher-report-error.jpg), [ekran metni](../../artifacts/vocabulary-panel-verification-20261016/teacher-report.txt), [HTTP durumları](../../artifacts/vocabulary-panel-verification-20261016/teacher-http-status.json), `teacher-activity-server-error.log` ve `teacher-activity-server-frames.log` (aynı kanıt dizini).

## Son veri karşılaştırması

İki panel kontrolünden sonra başlangıç ve bitiş snapshot'ları tam JSON içerikleriyle eşit çıktı. **476 XP, 21 sonuç, 12 programa bağlı günlük kayıt, Gün 3, ek gün 1 ve 12 kelime ilerleme kaydı korundu.** Programın bütün alanları, gamification satırı, eski oturumlar, sonuçlar, kelime ilerlemeleri ve başarımlar değişmedi. Test sonucunu görünür kılmak amacıyla yeni oturum, ödev, puan veya günlük ilerleme kaydı oluşturulmadı. [Koruma kanıtı](../../artifacts/vocabulary-panel-verification-20261016/preservation-result.json).

## Açık bulgular

1. Öğretmen ayrıntı raporu aktivite sorgusunun HTTP 500 hatası nedeniyle açılamıyor; ekrandaki bağlantı hatası yönlendirmesi yanıltıcı.
2. Kurumun öğrenci Rapor düğmesi sayı/metin rol sözleşmesi nedeniyle ayrıntıyı açmıyor.
3. Katalogdan başlatılan bağımsız Kelime Bilgisi Testi mevcut günlük/okuma analitiğine dahil değil; iki personel özetinde önceki 20 etkinlik var. Öğrencinin 21. oturum sonucu ve 60 XP'si kayıp değil, rapor kapsamının dışında. Atanmış kelime ödevi döngüsü bu turda çalıştırılmadı.

Bu bulgular giderilmiş veya iki panel testi geçmiş olarak raporlanmıyor. Önceki [kelime puanlama düzeltmesi](speed-reading-vocabulary-scoring-20261016.md) ve veri koruma kanıtları geçerlidir; bu rapor personel raporlarının ayrıca sınanmasıyla elde edilen yeni bulguları kaydeder.
