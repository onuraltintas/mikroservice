# Haftalık kararların çalışma ve ödev bağlantıları

Geliştirme: 20 Ekim 2026. Canlı dağıtım ve öğrenci/öğretmen doğrulaması: 21 Ekim 2026, tamamlandı. Son sürümler, düzeltmeler ve kayıt koruma kanıtları [canlı doğrulama raporunda](karar-calisma-odev-canli-20261021.md).

## Kullanım

- Öğretmen, öğrencinin haftalık kararında **Çalışma ve ödevleri bağla / takip et** düğmesini açar.
- Her karara bir çalışma ve bir ödev birlikte bağlanabilir. Çalışmalar aktif plandan; ödevler öğrencinin aynı kurumdaki, kararı yazan öğretmene ait iptal edilmemiş ödevlerinden aranır ve sayfalanır.
- Bağlantılar kaydedildiğinde mevcut işlerin tarihleri ve içerikleri değişmez. Bağ kaldırmak işi silmez.
- Öğrenci karar kartındaki **İlgili çalışmayı aç** bağlantısıyla doğru planı ve çalışma ayrıntısını, **İlgili ödevi aç** bağlantısıyla ödevi açar.
- Öğretmen ve öğrenci bağlı çalışmanın bekleme/tamamlama durumunu, planlanan ve gerçekleşen süreyi; ödevin teslim/değerlendirme durumunu, son teslim gününü ve puanını görür. Gecikme ayrıca belirtilir. Bilgiler ilgili kayıtlardan okunur.
- İşin tamamlanması kararın durumunu kendiliğinden değiştirmez. Kararın gerçekleşmesini öğretmen ayrıca değerlendirir ve karar durumunu günceller.

## Karardan iş oluşturma ve takvim değişikliği

Öğretmen mevcut yayımlanmış aktif plana karar başlığıyla yeni genel çalışma ekleyebilir veya karara bağlı, tamamlanmamış çalışmayı taşıyabilir. Yeni çalışma otomatik konu/kazanım seçmez. Taşıma mevcut çalışma kimliğini, süresini ve kazanım bağlantılarını korur. Aktif plan yoksa mevcut plan oluşturma/yayımlama akışına yönlendiren açıklama gösterilir.

1. Tarih, saat ve süre girilir. Taşımada süre değiştirilemez.
2. Sunucudan salt okunur takvim önizlemesi alınır: eski/yeni tarih ve saat, saat dilimi, o günkü diğer çalışmalar ve toplam planlanan süre.
3. Tamamlanan çalışmayı değiştirme, saat çakışması, geçmiş gün, plan dönemi dışı tarih ve günlük süre sınırı sunucuda reddedilir. Saati olmayan işler toplam süreye katılır ve saat çakışması kontrol edilemediği açıklanır.
4. Öğretmen onay kutusunu işaretleyip **Onayla ve plana uygula** düğmesine basar. Bu akışta onayı değişikliği yapan öğretmen verir; ayrı bir öğrenci onay kuyruğu yoktur.
5. Uygulama sırasında yetki, karar/plan sürümleri ve önizleme özeti yeniden doğrulanır. Bu sırada değişmiş kayıt için yeni önizleme gerekir. Formu değiştirmek mevcut önizleme/onayı kaldırır.

Öğretmen ayrıca karar başlığı ve açıklamasından yalnız ilgili öğrenciye dijital ödev oluşturabilir; gelecek bir son teslim tarihi ve isteğe bağlı tahmini süre girer. Yeni çalışma/ödev önceki karar bağlantısının yerini alır; eski iş korunur ve bu durum formda açıklanır.

## Veri modeli ve kayıt koruma

Geçiş: `20261020143601_WeeklyDecisionWorkLinks`. Yalnız `coaching.weekly_decisions` tablosuna iki boş bırakılabilir `uuid` alan ekler:

| Alan | Anlamı |
|---|---|
| StudyTaskId | İlgili mevcut çalışma |
| AssignmentId | İlgili mevcut ödev |

Eski kararlar boş bağlantılarla çalışır. Geçiş hiçbir eski planı, hesabı, çalışma/teslim kaydını veya kazanım bağlantısını silmez. Karar geçmişine isteğe bağlı bağlantı kimlikleri ve işlem türü eklendi; eski geçmiş JSON kayıtları okunmaya devam eder.

Bağlantılar uygulama tarafından doğrulanan yumuşak referanslardır. Mevcut plan yenileme/silme davranışını değiştiren silme zinciri veya yeni hedef yabancı anahtarı eklenmedi. Erişilemeyen hedefin kimliği/geçmişi korunur; kart erişilemiyor durumunu gösterir ve açma bağlantısını kapatır. Arşivlenmiş eski çalışmanın bilgileri okunabilir; yeni bağlantı seçiminde aktif plan kullanılır.

Yeni iş ve karar değişikliği, mevcut öğrenci bazlı veritabanı kilidi ve işlem içinde kaydedilir. Aynı işlem kimliğinin tekrar gönderilmesi iş veya bildirim çoğaltmaz. Farklı içerikle aynı işlem kimliği reddedilir. Çalışma/ödev ile karar bildirimleri mevcut EF Bus Outbox üzerinden işlemle birlikte kaydedilir; ikinci kayıt/bildirim aşamasındaki hata ilk değişiklikleri de geri alır. Kişisel veri silme kapsamına yeni işlemlerin tekrar önleme kayıtları da dahil edildi.

## İlk geliştirme doğrulaması

- PostgreSQL entegrasyon/regresyon: **91 başarılı, 0 başarısız, 0 atlanan**. Yetki ve öğrenci ayrımı, bağlantılar, gerçek iş durumu, taşıma/kazanım koruma, çakışma, tamamlanmış iş engeli, eski önizleme, açık onay, tekrar gönderim, işlem geri alma, eski satırların geçişte korunması ve kişisel veri silme kapsamı dahil.
- Öğrenci arayüzü: **120 başarılı**. Öğretmen arayüzü: **120 başarılı**. Ortak bileşen testleri her iki uygulamada da çalışır; bunlar 240 farklı senaryo olarak yorumlanmamalıdır.
- İki uygulamanın üretim derlemesi başarılı. Mevcut paket/stil bütçe uyarıları sürüyor; derlemeyi durduran hata yok.
- Yerel tarayıcıda gerçek bileşenler ve sentetik API verileriyle çalışma+ödev bağlama, taşıma önizlemesi, onaysız düğmenin kapalı olması, onay sonrası tarih değişikliği ve öğrencide doğru bağlantıların görünmesi doğrulandı. Bu kontrol canlı uçtan uca test veya gerçek sunucu testi değildir; gerçek veritabanı davranışı yukarıdaki entegrasyon testlerinde doğrulandı.
- Elle inceleme: öğretmen yetkisi/karar sahipliği, öğrenci ve kurum sınırları, sayfalama, sürüm kontrolü, tekrar önleme, işlem/bildirim atomikliği ve tamamlanan kayıtların korunması kontrol edildi.

Kanıtlar:

- `artifacts/weekly-decision-work-regression.log`
- `artifacts/weekly-decision-work-ui.log`
- `artifacts/weekly-decision-work-production.log`
- `tests/Integration/Coaching.Curriculum.IntegrationTests/TestResults/weekly-decision-work-regression.trx`
- `artifacts/weekly-decision-student-links.png`

Coaching API geçişi ve iki arayüz canlıya yayımlandı. Gerçek öğrenci/öğretmen oturumlarında bağlantı, takvim önizlemesi, açık onay, taşıma ve teslim/tamamlama sonrası gerçekleşme takibi doğrulandı. Dağıtım sırasında bulunan iki hata düzeltilip yeniden test edildi; ayrıntılar [21 Ekim canlı doğrulama raporunda](karar-calisma-odev-canli-20261021.md).
