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

Henüz yayımlanmadı; kod inceleme ve kontrollü yayın doğrulaması bekleniyor.
