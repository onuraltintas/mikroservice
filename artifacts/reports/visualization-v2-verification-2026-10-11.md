# Görselleştirme v2 — geliştirme ve doğrulama raporu

Tarih: 11 Ekim 2026. Kapsam: Hızlı Okuma Görselleştirme egzersizi ve admin sahne düzenleme ekranı. Başlangıç sürümü: `bd146d74`; çalışma dalı: `codex/platform-hardening`.

## Sonuç ve yayın sınırı

Onaylanan yerel geliştirmeler tamamlandı. İçerik geçişi yalnız geçici Testcontainers PostgreSQL veritabanında uygulandı. VPS, canlı veritabanı, GitHub ve Koçluk/Identity/Notification servisleri değiştirilmedi. Yayın için uygulama dağıtımı ve Hızlı Okuma veritabanında `content-packs/visualization/v2/apply.sql` geçişi ayrıca gereklidir.

Mevcut kullanıcı oturumları, sonuçları ve eğitim programları silinmedi/değiştirilmedi. Yeni içerik için 60 eski sahneyi geçmişi koruyarak arşivleyen, 120 yeni sahneyi ekleyen geçiş hazırdır; bu arşivleme canlıda henüz yapılmadı.

## Tamamlanan adımlar

### 1. Admin ve cevap sözleşmesi

- Admin API'si `detail`, `color`, `position`, `count`, `spatial`, `sequence` soru türlerini kabul eder.
- Admin ekranı gerçek seçenek metnini `answerFormat: text` ile gönderir. Sunucu cevabı A–D anahtarına dönüştürür; öğrenci puanlamasında anahtar kullanılır.
- `key` ve `text` ayrımı, seçenek metninin kendisi A/B/C/D olduğunda yanlış seçeneğin doğru sayılmasını engeller. Belirsiz `auto` talepleri reddedilir; mevcut metin biçimindeki eski cevaplar desteklenir.
- Düzenleme yeni soru kimlikleri üretir; eski oturum snapshot'larının soru kimlikleri ve cevapları değişmez.
- Aynı sahnedeki eşzamanlı güncelleme/silme, transaction ve satır kilidiyle sıralanır. Canlıdaki `EnableRetryOnFailure` ayarıyla uyumlu EF execution strategy kullanılır; tekrar denemede önceki attempt'in takip edilen nesneleri yeniden yüklenir.

### 2. İçerik havuzu

- Dört yaş grubu × beş seviye × altı sahne = **120 sahne, 360 soru**.
- Deney, park, mutfak, müze, istasyon ve tasarım bağlamları; her sahnede ayrıntı, mekânsal ilişki ve işlem sırası sorusu.
- Doğru cevap için kaynak cümle/kanıt ve dört benzersiz seçenek doğrulanır. Sayısal çeldiriciler aynı nesne için farklı miktarlardır.
- Üst seviyelerde ayrıntı yükü, ilişkili konumlar ve sıra sorularının talepleri artar. Yaş bağlamları farklılaşır; eski süreler korunur.
- İçerik metinseldir: kullanıcı zihninde canlandırır. Görsel dosyası üretimi veya yeni bir görsel depo eklenmedi.
- Bu düzenleme pedagojik içerik geliştirmesidir. Yaş/seviye parametreleri bilimsel olarak kalibre edilmiş norm veya klinik ölçüm olarak sunulmaz; bilişsel gelişim/okuma hızı artışı garanti edilmez.

### 3. Sahne seçimi ve geçmiş

- Kayıtlı çalışmada öğrenci ve egzersize özel son 200 oturum kullanılır; üç uygun sahne kullanım sıklığı ve son kullanıma göre seçilir. Eşitlikler ve sunum sırası rastgeledir.
- Yeni pakette her oturum **3 sahne × 3 soru = 9 soru** içerir. Daha önce tamamlanmış oturumlar yeni havuzla yeniden yazılmaz.
- Kayıtlı cevaplar sunucuda doğrulanır. Görselleştirme okuma hızı (WPM) ölçmez; bu ölçümün yerine sahne sorularının değerlendirmesi kullanılır.
- Merkezi önizleme üç rastgele sahne yükler; önizleme kullanım geçmişi/puan/ilerleme kaydetmez.

### 4. Özel ayarlı önizleme

- Katalogda inline sahne olmayan Görselleştirme için menü açılmadan önce merkezi sahneler yüklenir. Menü doğru mevcut sahne süresini başlangıç değeri olarak kullanır.
- Merkezi sahneler geldikten sonra motorun nested yapılandırması doldurulur ve özel gösterim süresi uygulanır. Boş inline dizi merkezi sahneleri gölgelemez.
- Elle yapılandırılmış dolu inline sahneler korunur. Kayıtlı eğitim ayarları değiştirilmez.
- Rol sınırı, yerel navigasyon state'i, yükleme hatası mesajı ve bileşen kapanınca isteğin iptali korunur.

### 5. Veri geçişinin güvenliği

- Tek transaction, sınırlı kilit bekleme süresi ve tablo/advisory kilitleri.
- 120 yeni ve 60 eski benzersiz sahne kimliği, 360 benzersiz yeni soru kimliği, egzersiz/yaş/seviye ve cevap/kanıt kontrolü.
- Adminin değiştirdiği açıklama, seçenek, cevap, soru türü, sıra, ipucu veya sonradan eklediği aktif soru varsa tüm geçiş durur ve geri alınır. Yeni ID çakışmaları da engellenir.
- Tekrar çalıştırma değişmemiş içerikte idempotenttir. Eski sorular ve oturum/sonuç kayıtları korunur.
- Yedek, dağıtım sırası ve geri dönüş sınırları `content-packs/visualization/v2/README.md` içinde açıklanmıştır.

## Doğrulama sonuçları

TDD yaklaşımıyla hatayı gösteren RED testleri önce çalıştırıldı, ardından düzeltme ve GREEN doğrulamaları yapıldı. Bağımsız C# ve TypeScript incelemelerinin son sonucu bu kapsam için engelleyici bulgu bulunmadığı yönündedir.

| Kontrol | Sonuç |
| --- | --- |
| SpeedReading.Application tüm birim testleri | 1124 geçti, 0 başarısız, 0 atlanan |
| Hızlı Okuma tüm frontend birim testleri | 812 geçti |
| Görselleştirme gerçek PostgreSQL testleri | 11 geçti, 0 başarısız, 0 atlanan |
| Admin cevap biçimi Vitest testi | 1 geçti |
| İçerik doğrulama Node testleri | 4 geçti |
| İçerik üretiminin güncellik kontrolü | `generate.mjs --check` geçti |
| Playwright masaüstü ve mobil | 10 geçti |
| SpeedReading.API Release derlemesi | 0 hata, 0 uyarı |
| Hızlı Okuma üretim frontend derlemesi | Başarılı; önceden mevcut SCSS bütçe uyarısı |
| Admin üretim derlemesi | Başarılı; önceden mevcut katalog SCSS bütçe uyarısı |

PostgreSQL senaryoları: eski oturumun devamı ve puanlanması, yeni dokuz cevabın sunucu puanlaması, sonraki oturumda farklı üç sahne seçimi, iki kez geçiş, eşzamanlı admin yazımı (retry açık/kapalı), silme/idempotent tekrar, değiştirilmiş açıklama/ipucu, ekstra soru, mükerrer yeni soru veya baseline sahne kimliği ve boş seçeneklerin geri alınması.

Playwright gerçek UI/motor ve fixture API yanıtları kullanır; canlı Identity girişini doğruladığı iddia edilmez. Masaüstü ve mobilde yeni paketin üç sahne/dokuz soruluk özel süre önizlemesi, static/guided/flash duraklatma-zamanlama ve öğrenci cevap hatasından sonra yeniden deneme senaryoları geçmiştir. Önizlemede auth refresh dışı API yazımı yapılmadığı kontrol edilmiştir.

## Kapsama ve kalan yayın işi

Hızlı Okuma frontend genel kapsaması: statements %62,29; branches %56,20; functions %54; lines %63,36. Görselleştirme motorunda statements %91,11 ve lines %93,18; branches %79,41. Testlerin geçmesi tüm projenin %100 kod kapsamasına sahip olduğu anlamına gelmez. PostgreSQL testlerinin coverage çıktısı üretildi; bu dar senaryo seti tüm backend kapsaması için yeterli değildir.

Yerel testler canlı ortamdaki DNS, proxy, gerçek Identity oturumu veya canlı içerik envanterinin yerine geçmez. Canlı yayın onayı sonrası yedek/doğrulama, backend ve frontend dağıtımı, yalnız Hızlı Okuma DB geçişi ve canlı admin/öğrenci/öğretmen kontrolleri yapılmalıdır.
