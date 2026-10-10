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

Henüz yayımlanmadı; kontrollü geçişin ardından kanıtları eklenecek.
