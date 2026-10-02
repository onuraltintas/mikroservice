# Öğrenci planlama: yerel doğrulama kaydı

Tarih: 2026-10-02. Bu kayıt, [ilk yol haritasının](student-planning-roadmap.md)
manuel ve otomatik plan adımlarındaki güncel durumu tamamlar. Canlı yayın değildir.

## Son doğrulama eki

Önceki sayılar aşağıda tarihsel kontrol noktası olarak korunmuştur; toplam test sayısı değildir.

- Gerçek yerel Koçluk API'siyle 2 E2E testi geçti: kimlik/ürün/rol/sahiplik sınırları ve tarayıcıda manuel plan oluşturma, yayımlama, görev tamamlama, öğrenci beyanı sınav girişi ve API raporu.
- Identity oturumu yalnız localhost üzerinde, tek kullanımlık ortam anahtarıyla imzalı test oturumudur. Gerçek Google/kayıt/giriş doğrulaması değildir.
- MVC record doğrulama metadata hatası düzeltildi; ilgili MVC/manuel/otomatik seçiminde 46 test geçti.
- Oturum yenilemenin AuthService oluşturulurken aynı servisi interceptor'dan yeniden çözmesi düzeltildi. Oturum servisi/interceptor seçiminde 10 test geçti.
- Notification için silinen alıcıların asgari işaret kaydı ve alıcı bazlı eşzamanlılık koruması yerelde geliştirildi. Canlı migration uygulanmadı. Eski silme kayıtları için canlı geçiş öncesinde kimlik eşleştirme/backfill kontrolü gerekir.
- Otomatik plan/hedef/rapor ekranlarının daha geniş tarayıcı kapsamı, katalog işletim adımı ve genel son regresyon hâlâ tamamlanmalıdır. Canlıya çıkış ayrıca onay gerektirir.

## Hazır ve doğrulanmış

- Öğrenciye ait müsaitlik saatleri, sürüm ve saat dilimi doğrulaması.
- Manuel taslak, ayrı yayımlama, çalışma tamamlama/tarih değiştirme ve geçmiş.
- Öğrenci hedefini aktif okul/üniversite programıyla ilişkilendirme; öğretmen hedefi salt okunur.
- Aktif ders/ünite/üst konu filtresiyle yalnız yaprak konu arama; sınıf, sınav ve metin filtreleri, sayfalama.
- Kapasiteye göre deterministik otomatik önizleme; eksik süreyi öğrenci belirtir.
- Öğrenci onayıyla sunucuda yeniden hesaplanan otomatik taslak. İstemciden görev listesi veya öğrenci kimliği kabul edilmez.
- Tamamlanmış işler özgün revizyonun geçmişinde kalır; gerçek süreler kopyalanmaz.
- Tamamlanmamış sabit işler, önizleme tarih aralığı dışındakiler dahil, yeni taslağa alınır.
- Yayımlamada çalışma saati ve kaynak aktif plan sürümleri yeniden doğrulanır. Saat satırı yayın işlemi boyunca kilitlenir.
- Eski taslağı sürüm kontrollü arşivleyip yeniden üretme; fiziksel silme yok.
- Korunan sabit işler dahil 500 görev sınırı önizlemede kontrol edilir.

## Doğrulama

- İlgili backend regresyon seçimi, sınav sonucu doğrulamaları dahil: **104 geçti, 0 başarısız, 0 atlanan**.
- Öğrenci planlama ve bağlı ekranlar: **43 geçti**, 7 test dosyası. Yeniden oluşturulan önizleme, önceki kaydetme onayını sıfırlar.
- Angular üretim derlemesi başarılı. Önceden mevcut Hızlı Okuma katalog stil bütçesi uyarısı devam ediyor.
- Seçili önizleme/taslak/manuel plan/konu arama servisleri ve otomatik controller kapsamı:
  **312/312 satır (%100)**, **%87,5 dal kapsamı**. Bu oran tüm ürünün kod kapsamı değildir.
- Gerçek, geçici PostgreSQL üzerinde migration uygulama ve plan şemasını geri alıp yeniden kurma doğrulandı.
- Kimliği doğrulanmış tarayıcı uçtan uca testi henüz yapılmadı. Frontend sayısal kapsamı,
  eksik `@vitest/coverage-v8` nedeniyle doğrulanmış değildir.

## Devam eden yol haritası

1. Mevcut Exam/ExamResult üzerinde öğrenci beyanı ve ders/konu sonuç girişi; ikinci sınav sistemi kurmadan.
2. Plan uyumu, konu/deneme/hedef raporları; veri yokluğunu başarı sıfırı olarak göstermeden.
3. Öğrenci ana sayfası, bildirim outbox, veri dışa aktarma ve silme kapsamı.
4. Katalog içeriği/kullanım yetkisi doğrulaması ve kontrollü kalıcı aktarım/aktivasyon.
5. Gerçek oturumlu E2E, güvenlik/performans, yedek ve geri dönüş doğrulaması.
6. Ayrı kullanıcı onayından sonra canlı yayın.

## Yerel şema değişikliği

`20261002111022_TrackAutomaticStudyPlanSource`, yalnız `coaching.study_plan_revisions`
tablosuna üç nullable kaynak/sürüm alanı ekler. Identity ve Hızlı Okuma şemaları değişmez.
Canlı veritabanına uygulanmadı. Üretimde eski/yeni uygulama uyumu ve yedek ayrıca kontrol edilmelidir.

## Sonuç girişi hazırlığı

Mevcut `ExamResult.SetAnswerStatistics` negatif doğru/yanlış/boş değerlerini artık
değişiklik yapmadan reddeder; sıfır değerleri geçerlidir. Üç negatif durum ve sıfır
durumu test edildi. Bu, öğrenci sonuç girişi ekranının tamamlandığı anlamına gelmez.

Mevcut sınav modeli öğretmen oluşturucusunu zorunlu tutuyor. Öğrenci beyanı
öğretmen kimliği alanına yazılmayacak. Sonraki uygulama sırası:

1. Aynı Exam/ExamResult modeli üzerinde açık öğrenci sahipliği ve kayıt kaynağı;
   öğretmen oluşturucusu ile öğrenci oluşturucusunun karşılıklı dışlanması.
2. Öğretmen/kurum değiştirme ve silme yollarının öğrenci beyanını sahiplenmesini
   engelleyen yetki regresyonları; DTO, migration ve gerçek PostgreSQL doğrulaması.
3. Yalnız kendi sonucunu oluşturan öğrenci API'si; aktif ders/yaprak konu kimliği,
   soru sayısı tutarlılığı, sürüm kontrollü düzenleme ve sınırlandırılmış istekler.
4. Öğrenci ekranı ve kaynak etiketli rapor entegrasyonu. Beyan edilen puan,
   resmî sınav puanı veya doğrulanmış ölçüm gibi sunulmayacak.
