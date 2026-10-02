# Koçluk öğrenci planlama ve raporlama geliştirmesi

Tarih: 2026-10-02. Kapsam: yalnız Koçluk öğrenci paneli; bu çalışma canlı yayın değildir.

## Kesin sınırlar

- Angular öğrenci paneli, mevcut .NET Koçluk servisi ve `coaching_db/coaching` şeması kullanılır.
- Identity kimlik/giriş/ürün yetkisinin sahibidir. Yeni kullanıcı veya parola tablosu oluşturulmaz.
- Hızlı Okuma veritabanı ve ürün akışları değiştirilmez.
- Dış uygulama kodu, marka metinleri, içerik URL'leri veya API bağlantıları kopyalanmaz.
- Kaynak klasör salt okunurdur. Veri çekme betikleri çalıştırılmaz.
- Kaynak öğrenci hedefleri, takvim, rapor, tercihler ve kullanım olayları aktarılmaz veya Git'e eklenmez.
- Katalogların kaynak ve kullanım yetkisi doğrulanmadan öğrencilere yayın yapılmaz. Kaynak bilgisi gizlenmez.

## Kaynak envanteri

Kaynak: kullanıcı tarafından sağlanan `kanka-veritabani/real-data/current`.
Yerel dosyalar 2026-10-02 tarihinde sayıldı: 169 ders, 768 türetilmiş ünite,
1.998 üst konu, 3.222 konu, 21.602 üniversite programı, 3.010 LGS programı.
Bu sayılar tüm kaynak sistemin eksiksizliği anlamına gelmez.

- Üniteler konulardan türetilmiştir; sıraları bilinmez, nullable kalır.
- Program puanlarının yılı dosyalarda bulunmaz. Güncel yıl veya yerleşme olasılığı uydurulmaz.
- Dersler sınıf GUID'i taşır; normal okul sınıfları ile LGS/YKS kategorileri ayrı eşlenir.
- Konu -> üst konu -> ünite -> ders ilişkisi doğrulanır; ad benzerliği ilişki için yeterli değildir.
- Örnek SQL ve boş CSV'ler migration veya gerçek veri değildir.

## Hedef eşleme

| Kaynak | Karar |
|---|---|
| students | aktarılmaz; oturumdaki Identity UserId kullanılır |
| lessons/units/upper-subjects/subjects | Koçluk katalog tablolarına kontrollü yerel aktarım |
| universities/faculties/programs/schools | hedef kataloğu; isim tekilleştirme ve kaynak kimliği korunur |
| placement_statistics | yıl/kaynak doğrulanmışsa karşılaştırma; bilinmeyen veri nullable |
| student_goals | yeni kayıtlar mevcut AcademicGoal genişletilerek oluşturulur; kişisel kaynak verileri aktarılmaz |
| exams/exam_attempts/results | mevcut Exam/ExamResult genişletilir; ikinci sınav sistemi kurulmaz |
| preferences/availability | Koçluk'a ait çalışma tercihleri; öğrenci kullanırken oluşturulur |
| plans/revisions/slots/calendar | yeni plan/sürüm/tarihli görev tabloları |
| mutation_requests | mevcut IdempotencyRecord ve transaction altyapısı |
| flow_sessions | gerekirse Koçluk'a ait devam edilebilir sihirbaz taslağı |
| notifications | mevcut Notification; olaylar outbox ile yayınlanır |

Kaynak ve yerel katalog kimlikleri ayrı tutulur. Kaynak + kaynak kimliği benzersizdir.
İçe aktarma tekrar çalıştırılabilir olmalı; çakışmalar sessizce atlanmamalıdır.
Kataloglar varsayılan pasiftir, fiziksel silme öğrenci geçmişini bozmamalıdır.

## Aşamalar ve kabul şartları

1. Envanter/eşleme: tamamlandı. Kişisel ve katalog dosyaları ayrıldı.
2. Katalog altyapısı: ders/ünite/konu ilişkileri ve yalnız Koçluk migration'ı;
   gerçek PostgreSQL'de aynı ders/üniteye bağlılık, mükerrer kaynak ve geri dönüş testleri.
3. Aktarım: yerel allowlist; dry-run; sayım, ilişki ve değişiklik raporu; transaction;
   yinelenen aktarımda aynı kimlikler ve sıfır mükerrer kayıt.
4. Hedefler: mevcut AcademicGoal'u ölçülebilir hedefler ve hedef program bağlantılarıyla genişlet;
   öğrenci/öğretmen hedef sahipliğini koru.
5. Manuel plan: müsaitlik, sürümler, görevler, gerçekleşen çalışma;
   tek aktif sürüm/taslak, optimistic concurrency ve çakışma testleri.
6. Otomatik plan: sunucuda kural tabanlı taslak; sabitlenmiş görevleri ve tamamlanan geçmişi koru;
   uygun süre yetmiyorsa açıklama ver, kullanıcı onaylamadan yayımlama.
7. Sonuç girişi: doğru/yanlış/boş ve ders/konu sonuçları; sınav türüne göre kurallar;
   öğrenci beyanını ölçülmüş değerlendirmeden ayır.
8. Raporlar: plan uyumu, konu/deneme/hedef; veri yokluğunu sıfır başarı sayma;
   her otomatik bulguda dönem/kaynak/gerekçe göster.
9. Entegrasyon: öğrenci ana sayfa, bildirim, dışa aktarma ve erasure kapsamı.
10. Yayın: E2E, yetki ve performans; yedek ve rollback; ayrı kullanıcı onayıyla canlı yayın.

Her aşamada test -> uygulama -> doğrulama yapılır. Şu an öğrenci ekranları tamamlanmış değildir.

## Yerel ilerleme — 2026-10-02

- Ders, ünite ve konu modelleri ile yalnız Koçluk şemasına ait migration hazırlandı.
- Katalog sınıf aralığı mevcut Koçluk profilleriyle uyumlu olarak 1–12'dir;
  kaynak dosyalarda olmayan sınıflar için veri üretilmez.
- Kataloglar pasif başlar; kaynak kimlikleri benzersizdir. Bileşik yabancı anahtarlar
  konunun yanlış ders/üniteye veya başka ünitedeki üst konuya bağlanmasını engeller.
- Katalog ve mevcut CMS/abonelik kapsamındaki Docker gerektirmeyen 63 test geçti.
  EF model/migration eşleşmesi doğrulandı.
- Docker açıldıktan sonra katalog, CMS/abonelik ve PostgreSQL retry regresyon kapsamındaki
  77 testin tamamı geçti. PostgreSQL'de mükerrer kaynak, yanlış ders/ünite ve yanlış
  üst konu bağlantısı reddedildi; katalog migration geri alma işlemi doğrulandı.
  Ders/ünite/konu altyapısı doğrulandı; hedef program/okul katalogları ve aktarım henüz yapılmadı.
- Canlı veritabanına migration uygulanmadı; kaynak katalog veya kişisel veri aktarılmadı.
