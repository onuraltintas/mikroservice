# Genç yetişkin programları v2 — geliştirme ve canlı geçiş

## Kapsam

- 17–21 yaş için beş seviye: 28 gün / 182 görev.
- İlk iki hafta 6, son iki hafta 7 görev/gün. Görsel/dikkat görevleri haftalara göre 4/4/3/2;
  okuma, anlama ve kelime görevleri 2/2/4/5.
- Desteklenen seviyelerde son iki haftaya RSVP, Metin Solma, Göz Gezdirme, Hata Analizi ve
  Sınav Simülasyonu eklenir; Gruplama en fazla seviye 4, Serbest Okuma yalnız seviye 1–3.
- Takvimsel zorluk artışı kapalı; ölçüm puan aralıkları ve kişiselleştirme üst sınırları korunur.
- Mevcut genç yetişkin egzersiz kataloğu, metinler, sorular ve seviye tespit programı korunur.
- 25–30 / 30–40 dakika tasarım hedefidir; mevcut ayarlar değişmedi, gerçek süre ölçülmedi.
  Görev sayısı süre garantisi veya bilimsel etkinlik kanıtı değildir.
- Yalnız `speedreading_owned_db` içerik geçişi. Yeni migration, API imajı veya frontend yayını yok.
- Kullanıcının açık onayıyla eski Seviye 1 test ilerlemesi ve ona bağlı üç oturum/sonuç silinir.
  Kullanıcı hesabı, diğer geçmiş, abonelik, ödeme, katalog ve diğer servisler korunur.

## Test ve inceleme

- Plan TDD: 7 Node kontrolü RED (`1babf350`), aynı kontroller GREEN (`e20edfd9`).
- Geçiş TDD: 14 PostgreSQL senaryosu RED (`f14ef8c9`), GREEN (`dc73a14a`).
- İnceleme sonrası iki geniş kapsam senaryosu RED (`c9cb62c0`), düzeltme GREEN (`d3e2cadc`).
- Ek sonuç ve dolaylı tekrar geçmişi senaryoları RED (`a091da33`); düzeltme ile 36 ilgili PG testi GREEN.
- 988 Hızlı Okuma birim testi, 7 Node kontrolü, 12 ChromeHeadless bileşen testi geçti.
- Yayın sonrasında Çocuk/Genç/Genç Yetişkin planları birlikte tekrar kontrol edildi: 15 Node testi geçti.
- PostgreSQL grubu: 18 genç yetişkin geçiş, 10 genç geçiş, 6 çocuk geçiş, 2 eşzamanlı tamamlanma.
  Ayrıca 2 tekrarlı görev PostgreSQL testi geçti; toplam 38 ilgili PG senaryosu başarılı.
- JS/katalog incelemesi engelleyici bulgu yok. SQL incelemesinin kapsam ve tekrar geçmişi bulguları
  testlerle giderildi; API geçiş boyunca durdurulacak, böylece geç okuma/yazma yarışı önlenecek.
- Yerel test kanıtları `artifacts/young-adult-programs-v2-tests/` altında. Bütün ürün E2E veya
  yüzde 100 kod kapsaması iddiası değildir; veri SQL'i için .NET coverage oranı uygulanmaz.

## Geçiş ve geri dönüş sırası

1. GitHub sürümünü ve paket SHA256 değerini doğrula.
2. Yalnız mevcut Hızlı Okuma API konteynırını durdur; hata halinde de aynı konteynırı başlatan trap kullan.
3. `preflight.sql`: canlıdaki tam bir ilerleme kaydı ve üç oturum kimliğini yeniden doğrula.
4. Yalnız Hızlı Okuma DB'sinin `pg_dump -Fc` yedeğini al, izin 600 ve `pg_restore --list` doğrula.
5. `inventory.sql` ile her kalıcı tablonun kapsam dışı kayıtlarını say/özetle.
6. Tek transaction `apply.sql`: açık onay tek Seviye 1 UUID'si; tam üç log/oturum/sonuç;
   diğer işlem/tekrar geçmişi bağlantısı veya beklenmeyen ilerlemede abort. Yeni planlar doğrulanmadan silme yok.
7. `verify.sql` ve kapsam dışı tüm tablolarda önce/sonra özet eşitliği.
8. Aynı API'yi başlat; readiness, HTTP ve oturumlu liste/ayrıntı kontrolü; 15 dakika sağlık takibi.

SQL hatasında transaction bütünüyle geri alınır ve API eski içerikle başlatılır. Yeni kullanıcı
verisi oluştuysa tam yedek otomatik geri yüklenmez; veriyi koruyan ileri düzeltme tercih edilir.
Tam geri yükleme canlıda denenmez. Kimlik/Koçluk/Notification veritabanlarına dokunulmaz.

## Yayın sonucu

- Yayın zamanı: 10 Ekim 2026, 20:13:32 Türkiye saati.
- GitHub `codex/platform-hardening` dalına `0e88dde3` gönderildi; remote HEAD eşleşti.
  Bu dal için GitHub Actions çalışması listelenmedi; test kanıtları yerel çalıştırmalardır.
- Yalnız veri paketi uygulandı; API imajı `eduivme/speed-reading-service:teen-programs-v2-534c187a`
  ve frontend aynı kaldı. Tüm konteynır kimliği/imajları önce/sonra birebir aynı.
- Paket SHA256: `c5df98f9967318b64efcde0d2846f59f1144d71dfd19dcb247e09804ed48085b`.
- VPS yedeği: `/var/lib/eduivme/releases/young-adult-programs-v2-20261010-0e88dde3/speedreading-before.dump`.
  1.585.769 bayt, izin 600; `pg_restore --list` başarılı. Tam geri yükleme tatbikatı yapılmadı.
  Yedek SHA256: `b916143ae66d2f9c77bb60bf6ea8cd198d9780721c8348c8e1aa4f7879657cf7`.
- Beş eski şablon, bir onaylı Seviye 1 ilerleme kaydı, üç günlük görev logu ve bunların üç oturumu
  fiziksel olarak silindi; FK cascade ile üç sonuç ve 16 cevap kaldırıldı. Yedekten kurtarılabilir.
  Hesap silinmedi; bu, bütün kullanıcı geçmişini veya gamification toplamlarını sıfırlama değildir.
- Beş yeni programın her birinde 28 gün / 182 görev doğrulandı. Seviye tespit şablonu korundu.
- Kapsam dışındaki kayıtlar için 85 kalıcı tablonun sayım/özetleri birebir aynı.
  Diğer yaş programları, egzersiz kataloğu, metinler/sorular, profil/abonelik/ödeme verileri ve
  78 migration kaydı korundu. Identity/Koçluk/Notification veritabanları değiştirilmedi.
- Readiness `Healthy`, ana sayfa HTTP 200, oturumsuz korumalı program API'si HTTP 401.
- Gerçek admin oturumunda Genç Yetişkin filtresi beş yeni programı gösterdi. Ayrıntıda
  28 gün / 182 görev / ilk 14 gün 6, son 14 gün 7 görev görüldü; eski aktif eğitim ve geçmiş
  kalktı, yeni kayıt butonları etkin. Hiçbir programa yeni canlı kayıt oluşturulmadı.
- Yayın sonrasına ait konsol hata/uyarı kaydı yok. Daha önceki tarayıcı eklentisi mesajları
  yayın öncesine aittir, API hatası olarak değerlendirilmedi.
- Ekran kanıtı: `artifacts/young-adult-programs-v2-tests/live-young-adult-programs.png` (yerel).
- Canlı içerik kontrolü: her seviyede soruları olan 15 aktif genç yetişkin metni;
  genç yetişkin/ortak kelime havuzunda seviyelere göre 299 / 488 / 323 / 271 / 105 kayıt.
- Aktif Eğitimim sayfası temizlenen hesapla hata vermeden boş duruma geldi. Yeni kayıt veya egzersiz
  oturumu oluşturulmadı. Boş ekrandaki seviye değerlendirmesi açıklaması öğrenciye yönelik mevcut
  metindir; admin/öğretmen yeni programa Eğitim Programları kataloğundan kaydolur. Bu yayın frontend'i değiştirmedi.
- Yayın sonrası 15 dakikalık sağlık takibi tamamlandı: 30/30 ölçüm `Healthy`, yeniden başlama 0.
  `fail` / `crit` / `fatal` seviyeleri veya `Unhandled exception` bulunmadı. Bu, bütün öğrencilerin
  gerçek kullanımda tüm egzersizleri tamamladığı anlamına gelmez.
