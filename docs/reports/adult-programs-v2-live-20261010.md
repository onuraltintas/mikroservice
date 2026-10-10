# Yetişkin programları v2 — geliştirme ve yayın kontrolü

## Kapsam

22 yaş ve üzeri için beş seviye, her biri 28 gün / 182 görev.
İlk iki hafta 6, son iki hafta 7 görev. Görsel/dikkat dağılımı 4/4/3/2; okuma/dil 2/2/4/5.
Takvimsel seviye artışı kapalı; kaynak puan aralıkları ve kişisel üst sınırlar korunur.
Mevcut yetişkin katalog, seviye tespit, metinler/sorular/kelimeler ve diğer yaşlar korunur.
Program çizelgeleri content-packs/adult-programs/v2/programs.md dosyasındadır.

## Test kanıtları

- TDD: 7 plan kontrolü RED, yeni çizelge ile GREEN. Kimlik tekilliği kontrolü ayrıca RED→GREEN.
- 8 yetişkin plan kontrolü; dört yaş grubunun toplam 23 Node kontrolü geçti.
  Operasyon güvenliği kontrolleriyle birlikte son birleşik Node koşusu: 31/31 başarılı.
- 11 yetişkin PostgreSQL senaryosu: normal/tekrar çalıştırma, ilerleme, tekrar geçmişi,
  kimlik çakışması, eksik egzersiz, sürüm/yaş/yapı/tür hataları, mükerrer seviye ve yanlış kaynak yaş.
- Dört yaş grubu geçiş grubu: 45 PG testi; tekrarlı görev ve eşzamanlı bitirme: 4 PG testi geçti.
- 988 Hızlı Okuma birim testi ve 8 ChromeHeadless program/günlük görev bileşen testi geçti.
- C#/SQL incelemesinde yeni kimlik çakışması giderildi ve yeniden üretici test eklendi.
  Yaş aralığı/aktiflik yayın ön koşuluna eklendi; tüm psql çağrılarında ON_ERROR_STOP=1 zorunlu.
- Kaynak score/ceiling/program türü/sınav türü korunumu PG testinde ayrıca doğrulandı.
- 8 sahte Bash operasyon testi: belirsiz stop/commit yanıtı, yeniden başlatma sonrası hata,
  işlem niyeti ve yayın kilidi, log okuma hatası, küçük/büyük ciddi log ve temiz log kontrolü geçti.
- Test çıktıları artifacts/adult-programs-v2-tests altında. Tam ürün E2E veya %100 kod kapsaması değildir.
  Yalnız veri paketi değiştiği için .NET coverage yüzdesi SQL kapsamını ölçmez.

## Canlı ön kontrol

Salt okunur VPS kontrolünde beş eski yetişkin eğitim şablonu aktif, hiçbirine bağlı
ilerleme veya tekrar kaydı yok. Yetişkin yaş aralığı 22+, seviye tespit aktiftir.
Aktif soru içeren yetişkin metin sayıları: 11 / 19 / 15 / 15 / 15.
Yetişkin/ortak kelime havuzu: 299 / 488 / 323 / 271 / 105.
Başlangıç tarayıcı kontrolünde yetişkin filtresi eski beş programı gösteriyor;
adminin mevcut genç program kaydı korunacak, yeni canlı kayıt oluşturulmayacak.

## Yayın ve geri dönüş

Yalnız Speed Reading API durdurulur, aynı konteynır/imaj yeniden başlatılır.
Ön koşullar → pg_dump -Fc ve pg_restore --list → 85 kalıcı tablo kapsam dışı özetleri →
atomik SQL geçişi → son kontrol ve özet eşitliği → readiness/HTTP/oturumlu görünüm → 15 dakika izleme.
Diğer servisler/veritabanları, frontend veya migration değişmez; staging oluşturulmaz.
Eski beş program ancak yeni beş program doğrulandıktan sonra fiziksel silinir.
İlerleme veya tekrar geçmişi varsa işlem durur; hiçbir kullanıcı/oturum/sonuç silinmez.
SQL hatasında transaction geri alınır. Uygulama/commit yanıtı belirsizse veya sonraki doğrulama hatalıysa API kapalı kalır;
yedek ile kapsamlı inceleme yapılır, yeni kullanıcı verisi üzerine otomatik tam restore yapılmaz.
Tam restore tatbikatı canlıda yapılmaz. Günlük süre ve eğitim etkililiği garanti edilmez.

## Yayın sonucu

- Yayın: 10 Ekim 2026, 20:51:48 Türkiye saati. Veri paketi commit'i `4619ccca`, GitHub dalıyla eşleşti.
- Paket SHA256: `ec42daaaaea6a04c13258116a381e1a351d51e325c988c4f4be6851b762ae0a1`.
- VPS yedeği: `/var/lib/eduivme/releases/adult-programs-v2-20261010-4619ccca/speedreading-before.dump`.
  1.575.732 bayt, izin 600; pg_restore --list doğrulandı. Tam geri yükleme tatbikatı yapılmadı.
  SHA256: `d984a44fe61c168ab53b6176614cb0e5c571cbf17bdbf131094f2b5f535809a2`.
- Eski beş yetişkin şablonu fiziksel olarak silindi; yeni beş şablonun her biri 28 gün / 182 görev.
  Silinen şablonlar yedekten kurtarılabilir. Kullanıcı, ilerleme, sonuç veya oturum silinmedi.
- 85 kalıcı tablonun kapsam dışı kayıt sayıları ve özetleri önce/sonra birebir aynı.
  Seviye tespit, diğer yaşlar, katalog, içerik, abonelik/ödeme ve mevcut genç program kaydı korundu.
- API ve diğer konteynırların kimlik/imajları aynı kaldı. Yeni migration veya frontend yayını yok.
- Readiness Healthy, ana sayfa HTTP 200, oturumsuz korumalı program API'si HTTP 401.
- İlk paket Bash kontrolünde Windows satır sonu nedeniyle servise/veritabanına dokunmadan durdu.
  Açılan paket içindeki iki .sh dosyası LF'ye normalize edilip bash -n doğrulandı; sonra geçiş başarılı oldu.
  Sonraki paketler için yalnız bu sürüm klasörüne scoped `.gitattributes` ile LF kuralı eklendi.
- Gerçek admin oturumunda Yetişkin filtresi beş yeni adı gösterdi; Seviye 1 ayrıntısında 28 gün,
  182 görev ve ilk 14 gün 6 / son 14 gün 7 açıklaması doğrulandı. Aktif genç programı ve geçmişi duruyor.
  Aktif program nedeniyle yeni kayıt düğmeleri beklenen şekilde devre dışı; kayıt oluşturulmadı.
- Ekran kanıtı: `artifacts/adult-programs-v2-tests/live-adult-programs.jpg` (yerel).
- Konsol kaydında yayın sonrasına ait hata yok. Yayın öncesi 17:43 UTC tarihli bir HTTP/session
  completion hata kaydı ve eklenti listener mesajları var; bu veri yayınının sonucu olarak değerlendirilmedi.
  Canlı egzersiz tamamlama için yeni kayıt/sonuç oluşturulmadığından bu akışın tam E2E geçtiği iddia edilmez.
- Yayın sonrası 15 dakikalık izleme tamamlandı: 30/30 readiness ölçümü Healthy,
  yeniden başlatma sayısı 0 ve değişmedi; izleme aralığındaki loglarda ciddi hata işareti bulunmadı.
  Sonuç: `MONITOR_OK`. İzleme kaydı VPS sürüm klasöründeki `monitor.txt` dosyasında.
