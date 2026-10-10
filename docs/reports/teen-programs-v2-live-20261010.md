# Genç programları v2 — geliştirme ve canlı geçiş

## Kapsam

- 13–16 yaş grubundaki beş eğitim programı: 28 gün, 182 görev.
- İlk 14 gün 6, son 14 gün 7 görev; görsel/dikkat dengesi haftalara göre 4/4/3/2.
- İleri türler uygun seviyelerde son iki haftanın okuma görevlerinin yerini alır.
- Gruplama en fazla 4; Serbest Okuma yalnız 1–3, üst seviyelerde Anlama kullanılır.
- Takvimsel zorluk artışı yok; eski ölçüm puan aralıkları ve kişiselleştirme üst sınırları korunur.
- Tekrarlı görev oluştururken alt seviyedeki kullanılmamış egzersizin seçilmesi düzeltildi.
- Yalnız Hızlı Okuma API ve `speedreading_owned_db`; yeni migration veya frontend yayını yok.
- Identity, Koçluk, Notification, Gateway, admin/staff panelleri değiştirilmez.
- Test kullanıcılarının geçmişini silmeye izin verildi; geçiş için gerekmedikçe veri silinmez.
- Tasarım süre hedefleri gerçek oturum süreleriyle ölçülmedi. Mevcut egzersiz süreleri/ayarları korunur;
  görev sayısı süre garantisi veya eğitim etkinliği kanıtı değildir.

## TDD ve yerel doğrulama

- `35983bc2`: plan yokken 7 Node kontrolü RED.
- `046aadf7`: beş planla aynı 7 kontrol GREEN.
- `b38be135`: gerçek PostgreSQL'de eksik geçiş scripti RED.
- `8a4d7ff8`: ikinci tekrarlı görevde seviye düşmesini doğrulayan iki birim testi RED.
- `898be2f1`: motor düzeltmesiyle ilgili 8 birim testi GREEN.
- `534c187a`: SQL geçişiyle 10 PostgreSQL senaryosu GREEN.
- Hızlı Okuma birim testleri: 988 geçti, atlanan yok.
- Gerçek PostgreSQL: 10 Genç geçiş, 6 Çocuk geçiş, 2 eşzamanlı tamamlanma ve 2 tekrarlı görev testi geçti.
- ChromeHeadless: 17 liste/filtre, tekrarlı görev gezintisi ve program sonu bileşen testi geçti.
  Hata senaryolarındaki beklenen `offline` logları test hatası değildir.
- Hızlı Okuma API Release publish başarılı.
- Bunlar tüm ürünün uçtan uca test edildiği veya yüzde 100 kod kapsaması anlamına gelmez.
- Son inceleme sonrası Node kontrolleri gerçek Genç egzersiz kataloğuyla da eşleştirildi;
  27. gün Hata Analizi konumu ayrıca doğrulandı. Aynı 7 kontrol geçti.

## Canlı hazırlığı ve geri dönüş

- Son salt okunur kontrol: eski beş Genç programında sıfır ilerleme kaydı, 78 migration.
- Yedek alınmadan ve paket doğrulanmadan uygulama veya içerik değiştirilmeyecek.
- Önce geriye uyumlu API yayımlanır ve readiness doğrulanır; sonra tek transaction içerik geçişi yapılır.
- API başlatma başarısızsa içerik değiştirilmeden eski imaja dönülür.
- SQL başarısızsa bütün içerik transaction'ı geri alınır; yeni API eski programlarla çalışabilir.
- Yeni programa kayıt oluştuktan sonra tam yedek otomatik geri yüklenmez; yeni sonuçları koruyan
  ileri düzeltme tercih edilir. Tam geri yükleme tatbikatı canlı üzerinde yapılmaz.
- Katalog, diğer yaş programları/seviye tespitleri ve migration geçmişinin önce/sonra özetleri karşılaştırılır.

## Yayın sonucu

- Yayın: 10 Ekim 2026, 19:04:49 Türkiye saati.
- GitHub `codex/platform-hardening` dalına geliştirme ve yayın hazırlığı gönderildi (`068725b9`).
  Remote HEAD eşleşmesi doğrulandı. Bu dal için GitHub Actions çalışması listelenmedi;
  burada belirtilen test kanıtları yerel çalıştırmalardır, CI başarı iddiası değildir.
- Çalışan API imajı: `eduivme/speed-reading-service:teen-programs-v2-534c187a`.
- Paket SHA256: `288fe3adbf2b8bbe71dd7cce0f9009206d65667df8c2af5596d726ec7a7bfe0d`.
- VPS yedeği: `/var/lib/eduivme/releases/teen-programs-v2-20261010-534c187a/speedreading-before.dump`.
  1.584.590 bayt, izin 600; `pg_restore --list` başarılı.
  SHA256: `057b027ea2a88fdab441b804cf4ca048c498fbbf2cbf1b60146420cb8a3efc3a`.
- Eski beş şablon fiziksel olarak silindi; yedekten kurtarılabilir. Beş yeni şablonda ayrı ayrı
  28 gün / 182 görev ve günlük en az 6, en fazla 7 görev doğrulandı.
- Eski şablonlara bağlı ilerleme ve `review_items` kayıtları yoktu. Kullanıcı geçmişi silinmedi.
  Tekrar kaydı kontrolü yayın ön kontrolüne eklendi; eşzamanlı yeni ilişki oluşursa FK silmeyi
  engeller ve içerik transaction'ı bütünüyle geri alınır.
- Katalog, egzersiz türleri, korunan programlar ve 78 migration kaydının önce/sonra özetleri birebir aynı.
  Konteynır karşılaştırmasında yalnız Hızlı Okuma API konteynırı değişti; frontend değişmedi.
- API readiness `Healthy`, ana sayfa HTTP 200, oturumsuz korumalı program API'si HTTP 401.
- Gerçek admin oturumunda Eğitim Programları → Genç filtresi tam beş yeni başlığı gösterdi.
  Program ayrıntısı 28 gün / 182 görev / ilk 14 gün 6, son 14 gün 7 görev bilgisini gösterdi.
  Mevcut Genç Yetişkin aktif programı ve ilerlemesi korunuyordu; yeni kayıt oluşturulmadı.
  Bu sayfadaki hata/uyarı konsol kontrolü boş döndü.
- Canlı içerik kontrolü: Genç seviyelerinin her birinde soruları olan 15 aktif okuma metni var.
  Kelime motoru Genç ve ortak (`target_age_group_id IS NULL`) havuzu birlikte kullanır;
  seviyelere göre uygun kelime sayıları 299 / 488 / 323 / 271 / 105.
- Yayın sonrası 15 dakikalık sağlık takibi tamamlandı: 30/30 ölçüm `Healthy`, yeniden başlama 0.
  Log taramasında `fail`, `crit`, `fatal` seviyeleri veya `Unhandled exception` bulunmadı.
  Bu kontrol tüm öğrencilerin gerçek kullanımda bütün egzersizleri tamamladığı anlamına gelmez.
