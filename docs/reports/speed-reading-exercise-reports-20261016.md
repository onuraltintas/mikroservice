# Hızlı Okuma egzersiz raporları · 16 Ekim 2026

Bu yayın, [önceki canlı kontrolde](speed-reading-vocabulary-panels-live-20261016.md) bulunan üç hatayı giderir: öğretmen etkinlik raporundaki HTTP 500, kurum öğrenci raporunu engelleyen sayı/metin rol sözleşmesi ve bağımsız egzersiz sonuçlarının raporlara dahil edilmemesi. Canlı yayın zamanı **09:26:34 TSİ**. Öğrenci, öğretmen ve kurum gerçek oturum kontrolleri geçti. CAPTCHA nedeniyle bekleyen öğretmen kontrolü, kullanıcının normal girişinden sonra **09:54 TSİ**'de tamamlandı.

## Davranış

- Kaydedilmiş `ExerciseSessionResults`, normal egzersiz raporlarının ana kaynağıdır. Bağımsız kelime testleri de günlük göreve veya ödeve dönüştürülmeden raporlanır.
- Bağlı sonuç bulunan günlük kayıt ikinci kez sayılmaz. Sonucu olmayan eski günlük kayıtlar korunur. Eşleştirme aynı öğrenci ve oturum/legacy oturum kimliğiyle yapılır; tarih veya başlık benzerliğinden bağlantı türetilmez.
- Okuma kaydıyla örtüşen egzersiz sonucu etkinlik özetinde tekrar sayılmaz. İçerik analizi fiziksel egzersiz tamamlanmasını sayar. AdaptiveFluency'nin iki ayrı okuma ölçümü korunur; bir tamamlanmış egzersiz ve iki okuma ölçümü olarak farklı raporlarda gösterilir.
- Değerlendirme fazı sonuçları normal egzersiz akışına karıştırılmaz. Tarih ve öğrenci/kurum kapsamı filtreleri korunur. Arşivlenen egzersiz türleri geçmiş sonuçları rapordan düşürmez.
- Ölçülmeyen sonuç puan ortalamasına girmez; ayrıntıda ve egzersiz türü analizinde `Ölçülmedi` görünür. Gerçek ölçülmüş sıfır ise `0%` kalır. Kelime puanı, WPM veya anlama ölçümüne dönüştürülmez.
- Egzersiz geçme eşiği mevcut günlük akışla tutarlı biçimde 70'tir. Aynı görünen adı taşıyan farklı tür kimlikleri ayrı analiz satırları olarak kalır.
- Kurum üyeleri API'sinin Student=1 / Teacher=2 yanıtı istemci sınırında normalize edilir. Bilinmeyen değerler rol yetkisine çevrilmez. Sunucu yetkilendirmesi değişmedi.
- HTTP 500 artık yanlış biçimde öğretmen/öğrenci bağlantı eksikliği olarak sunulmaz; yeniden deneme korunur.

## Test kanıtları

| Kontrol | Sonuç |
|---|---|
| Başlangıç hatalarını yeniden üreten PostgreSQL testleri | 2 RED; düzeltmeden sonra GREEN |
| Kurum rolü ve hata mesajını yeniden üreten arayüz testleri | 3 RED; düzeltmeden sonra GREEN |
| 65 puan eşiği ve aynı görünen tür adı | 2 RED; düzeltmeden sonra GREEN |
| Uygulama birim testleri | 1.317 / 1.317 |
| PostgreSQL ve HTTP entegrasyon paketi | 23 / 23 |
| Hızlı Okuma personel ekranı testleri | 57 / 57, 10 dosya |
| Değişen C# satır kapsamı (PostgreSQL koşusu) | 62 / 76 ölçülebilir satır, %81,58 |
| Üretim verisinin ayrı kopyasında aday DLL ile prova | 4 senaryo geçti; 86 tablo / 8.609 mevcut satır korundu |
| Şema | Bekleyen migration yok; model değişikliği yok |

21 tür matrisi Schulte, takistoskop, hızlı okuma, anlama, scanning, chunking, text fade, görsel genişleme, fixation, sakkad, regresyon, subvocalization, visualization, focus, vocabulary, free reading, error analysis, RSVP, skimming, göz takibi ve sınav simülasyonunun **kaydedilmiş sonuçlarını** öğrenci/öğretmen/kurum raporlarından geçirir. Bu, 21 oynatıcının canlıda yeniden elle oynandığı iddiası değildir. Ek fixture eski günlük kayıt, legacy oturum, arşiv içerik, tarih sınırı, başka öğrenci, değerlendirme modu, ölçülmeyen ve gerçek sıfır puan ile AdaptiveFluency ölçümlerini sınar.

Kanıt dizini: [artifacts/exercise-reports-20261016](../../artifacts/exercise-reports-20261016). İlgili dosyalar `backend-red.log`, `staff-red.log`, `boundary-red.log`, `full-unit.log`, `full-postgres.log`, `staff-full.log`, `drill-result.json` ve kaynak izolasyonu listeleridir. Kapsam ölçümü `coverage-run.log`, Cobertura XML ve `changed-line-coverage.json` ile kaydedildi; yüzde tüm eski uygulamanın toplamı değildir. Üretim derlemesi geçti; mevcut başlangıç paketi ve SCSS boyut uyarıları sürüyor. İzole test tsconfig'i yalnız Hızlı Okuma testlerini içerir; canlı tabanda kalan ilgisiz course-management testleri bu kapsama alınmadı.

## Canlı doğrulama

Kurumun gerçek oturumunda Hızlı Okuma açıldı. Öğrenci satırındaki **Rapor** düğmesi çalıştı. `summary`, `reading-speed`, `comprehension` ve `activity` isteklerinin dördü HTTP 200 döndü. Ayrıntı tablosundaki mevcut **Kelime Bilgisi Testi = 100%**, WPM/anlama `—`. Göz takibi ölçülmeyen kayıtları `Tamamlandı · Ölçülmedi` gösteriyor; gerçek yanlış cevaplı takistoskop/görsel genişleme kayıtları `0%` kalıyor.

Kurum özetinde 21 etkinlik; içerik analizinde 11 kullanılan tür, 21 tamamlanma ve **Kelime Hazinesi: 1 tamamlanma / 100%**; zaman raporunda haftalık ve aylık toplam 21 görüldü. Mevcut ödev raporu tamamlanan öğrenci için 100 puan göstermeyi sürdürüyor. Bağımsız kelime testi yeni ödev olarak eklenmedi.

Salt okunur canlı katalog taraması 21 tür buldu. 11 türde toplam 21 normal egzersiz sonucu mevcut; diğer 10 tür için canlı sonuç bulunmuyor. Bu 11 türün rapor tamamlanma sayıları SQL kaynak sayılarıyla eşleşiyor. Diğer türlerin rapora taşınması PostgreSQL test matrisiyle doğrulandı; canlı öğrenciye örnek sonuç enjekte edilmedi. Kaynak dökümü `catalog-report-coverage.sql/log` dosyalarındadır.

Öğrencinin mevcut raporu da 21 toplam etkinlik gösteriyor. Program görünümü Hafta 1 / Gün 3 ve 0/6 durumunda kaldı; faz ölçümü geçmişi korunuyor. Bu testte yeni egzersiz oynatılmadı, puan veya ilerleme oluşturulmadı.

Önceki öğretmen sekmesi yenilendiğinde, Chrome'un aynı kökendeki ortak oturumundan kurum yöneticisi açıldı; bu kontrol öğretmen başarısı olarak kaydedilmedi. Kurum kontrolleri bu doğrulanmış kurum oturumunda yapıldı. Sonrasında normal öğretmen girişi Google CAPTCHA bağlantı hatasında bekledi; kullanıcıdan normal giriş istendi. CAPTCHA değiştirilmedi, token kopyalanmadı ve oturum taklit edilmedi.

Kullanıcının **“öğretmen hazır”** yanıtından sonra açık sekmede gerçek **Öğretmen** rolü doğrulandı ve Hızlı Okuma'ya geçildi. Sınıf özeti **21 etkinlik**, **%87 anlama** ve ölçülmüş WPM için **Veri yok** gösterdi. **Ayrıntılı rapor** açıldı; `summary`, `reading-speed`, `comprehension`, `activity` isteklerinin dördü ve sınıf özeti HTTP **200** döndü. Önceki HTTP 500 yeniden oluşmadı. Mevcut **Kelime Bilgisi Testi 100%**, WPM/anlama `—`, geçmiş **21 oturum** olarak görüldü. Ölçülmeyen kayıtlar `Tamamlandı · Ölçülmedi`, gerçek sıfır puanlar `0%` kaldı. Kanıtlar: `teacher-overview.txt`, `teacher-student-report.txt`, `teacher-http-status.json`, [öğretmen ekranı](../../artifacts/exercise-reports-20261016/teacher-student-report.jpg). Öğretmen canlı doğrulamasında bekleyen adım kalmadı.

Önceki rapordaki 20 → 21 değişiminin nedeni: önceki kaynak iki chunking oturumunu günlük ve okuma tablosundan iki kez sayıyor, iki bağımsız göz takibi ile bir bağımsız kelime sonucunu atlıyordu. Yeni toplam **8 okuma + 10 örtüşmeyen günlük/sonuç + 3 eksik bağımsız sonuç = 21**. Kayıt sayısı veya XP artırılmadı.

Bu yayın sırasında ölçülmüş WPM bulunmayan hesapta eski ayrıntı hız kartlarının 0 WPM göstermesi ayrıca not edilmişti. Ardından [ölçümsüz raporlar yayınıyla](speed-reading-no-measurement-reports-20261016.md) öğrenci, öğretmen ve kurum ekranlarında **Veri yok** gösterimi ve ölçüm olmadan öneri üretmeme tamamlandı. Ölçülmeyen egzersizler için yeni puan üretilmedi.

## Veri koruma ve yayın

Yayın öncesi gerçek API/Staff imajları sabitlendi, iki yayın kilidi alındı, PostgreSQL yedeği ve geri dönüş compose'u hazırlandı. API yazıcısı kapalıyken, aday oluşturulmadan önce ve oluşturulduktan sonra **86 tablonun tüm satır hash çoklukları birebir eşit** çıktı. Veritabanı migration, güncelleme veya backfill yapılmadı.

Test öğrencisinin başlangıç/bitiş tam JSON'u eşit: **476 XP, 21 sonuç, 12 günlük kayıt, 12 kelime ilerleme kaydı, Gün 3 ve 1 erken geçiş günü**. Bütün program alanları, oturum/sonuç hash'leri, kelime ilerlemesi ve başarımlar korundu. Kanıt: [preservation-result.json](../../artifacts/exercise-reports-20261016/preservation-result.json).

Öğretmen canlı kontrolünden sonra alınan `student-after-teacher.log` da başlangıç tam JSON'uyla eşit çıktı; bu doğrulama sırasında eski sonuçlar ve ilerlemeler değişmedi.

Kaynak yalnız canlı API tabanından alınan 3 Infrastructure dosyası ve canlı Staff tabanından alınan 4 TS/HTML dosyasıyla değişti. API'ye yalnız Infrastructure DLL/PDB eklendi. Öğrenci frontend'i, admin, kimlik servisi ve diğer konteynerler değiştirilmedi. Staff eski hash'li varlıkları açık sekmeler için korur. Önizleme ilk pakette eksik `/staff/` base-href'i canlıya geçmeden reddetti; `/staff/` ile yeniden derlenen paket önizleme ve ana JS erişimi kontrolünden geçti. Geri dönüş hem API hem Staff eski imajlarını ve sağlıklarını doğrular.

Kaynak revizyonu `003ef2e2`. API imajı ve Staff imajı `built-image.txt` / `built-staff-image.txt` kanıtlarında kayıtlı. Operasyon kaynakları [tools/releases/exercise-reports-20261016](../../tools/releases/exercise-reports-20261016) dizinindedir; gizli ortam dosyaları/yedek yalnız sunucudadır.

Son gözlem **09:41:59 TSİ**, yayından **15 dakika 25 saniye** sonra: 40 HTTP isteğinde 5xx sayısı **0**; dış öğrenci/personel sayfaları HTTP 200. API ve Staff yeniden başlama sayıları 0; uygulama ERR/FTL sayısı 0. İki aday imaj ve diğer üretim konteynerlerinin kimlikleri değişmedi. Öğrenci tam JSON karşılaştırması son kez eşit çıktı. Kanıtlar `monitoring.log`, `monitoring-complete.txt`, `final-health.log`, `student-before.log` ve `student-after.log`.

Yayınlanan API `sha256:bbdf788e18283899b884dc184b384b38bfb5e97910e99fd28e3b330bec3c28d2`; Staff `sha256:c556ca8d3f1ed5ada7206f1e3dd2f2b46375a37487e1f42ca7da70d7f084b1d1`. Infrastructure DLL SHA-256 `2694fd16e9d33d549b620f1a65a9f7696ac6e93fc1e2119bfcc46af378db16aa`.
