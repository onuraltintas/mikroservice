# Plan geçmişi ve tamamlanan kazanım yükü

24 Ekim 2026. Yerel uygulama ve test notu; canlı dağıtım değildir.

## Geçmiş raporun korunması

Yayımlanan veya yeniden etkinleştirilen plan `WasPublished` işaretini kalıcı olarak taşır. Plan arşivlendiğinde bu işaret silinmez. Öğrenci çalışma raporu, haftalık değerlendirme ve öğretmen çalışma özeti, yayımlanmış arşivlerin tamamlanmamış çalışmalarını da hesaba katar. Böylece yeni plan yayımlamak geçmişte yapılmayan çalışmaları paydadan çıkarmaz. Yayımlanmadan arşivlenen taslaklar rapora girmez.

Otomatik taslak ve haftalık kazanım planı oluşturulurken kopyalanan sabit işler `OriginalTaskId` ile ilk çalışmaya bağlanır. Aynı kaynak işin aynı güne ait kopyaları raporda tek kez sayılır; tamamlanmış kopyanın gerçekleşmesi tercih edilir. Farklı günlere ait plan taahhütleri ayrı kalır. Bu raporlar seçilen tarihe planlanan işlerin güncel durumudur; geçmişte belirli bir anda görünen ekranın değişmez görüntüsü değildir.

## Tamamlanan kazanım yükünün devri

Haftalık takvimden yıllık kazanım planı üreten servis, öğrencinin aktif ve arşivlenmiş planlarındaki tamamlanan işlerin kayıtlı kazanım paylarını okur. Aynı kazanımın güncel gereksiniminden bu payları düşürür; ardından bekleyen sabit işlerin paylarını ayırır ve yalnız kalan yükü takvime yerleştirir.

- Kredi `AllocatedMinutes` üzerinden hesaplanır. Örneğin 30 dakikalık kazanım payı 25 veya 300 dakika gerçek süreyle tamamlandığında kredi 30 dakikadır.
- Kredi güncel kazanım yükünü aşamaz; kalan süre negatif olmaz.
- Kapsam payı bulunan eski süre sürümleri yalnız kendi konularına kredi verir. Eski bütün-kazanım payı, toplam yükü aşmadan eşleşen kapsamlar arasında dağıtılır.
- Aynı kaynak çalışma/kazanım tekrar sayılmaz; başka öğrencilerin kayıtları kullanılmaz.
- Konu beyanı otomatik olarak `Studied` yapılmaz. Çalışmanın tamamlanması, öğrenme yeterliliği kararı değildir.
- Yeni tamamlanma veya ilgili kayıt değişikliği önizleme özetini değiştirir; eski önizlemeyle taslak oluşturulamaz.
- Tamamlanan çalışma, gerçek süre/soru, kazanım bağlantısı, süre sürümü ve metin anlık görüntüleri değiştirilmez veya yeni plana kopyalanmaz.

Bu değişiklik haftalık kazanım planlayıcısının kalan yük hesabını kapsar. Elle konu/süre seçilen bağımsız otomatik plan önizlemesi, açıkça gönderilen çalışma talebini dağıtmaya devam eder.

## Veri geçişi ve eski verinin sınırları

`20261024202303_PreservePublishedPlanHistory` iki alan ekler. Mevcut aktif planları, tamamlanmış çalışma içeren arşivleri ve başka bir planın aktif kaynak planı olarak kaydettiği arşivleri yayımlanmış olarak işaretler. Mevcut çalışma ve kazanım satırlarını değiştirmez; hiçbir eski sütunu, kimliği, sürümü, süreyi veya bağlantıyı silmez.

Hiç tamamlanmış çalışması ve kaynak-plan kanıtı olmayan eski arşivin eskiden aktif plan mı yoksa iptal edilmiş taslak mı olduğu mevcut kayıtlardan kesin belirlenemez. Bu arşivler tahminle rapora katılmaz. Eski sabit iş kopyalarının kaynak kimlikleri de tahminle doldurulmaz. Yeni yayımlama ve kopyalama işlemleri bu ayrımları kalıcı olarak kaydeder.

Geçiş uygulamadan önce çalıştırılmalıdır; eski şemayla yeni API birlikte çalıştırılmaz. Üretimde mevcut tek seferlik `--migrate-only` dağıtım akışı kullanılmalıdır. Bu görevde yalnız geçici yerel test veritabanlarına geçiş uygulanmıştır.

## Doğrulama

Yeni testler `PlanHistoryAndCompletedCreditTests` içinde: 10 planlanan/4 tamamlanan hesabının arşivlemede korunması, sıfır tamamlanmış yayımlanmış plan, yayımlanmamış taslak ayrımı, aynı kaynak işin tek sayılması, aktif/arşiv kazanım kredisi, başka öğrenci ayrımı, sabit işle birlikte kredi, eski önizleme reddi, ardışık önizlemelerde çift kredi olmaması, PostgreSQL üzerinde yayımlama öncesi/sonrası rapor eşitliği ve eski kayıtların korunması.

Kapsam ve süre sürümü geçişleri `OutcomeScopePlanningTests` içinde ayrıca sınanır. Veri geçişi testi, dolu eski şemadan yükseltip yeniden çalıştırır; yeni alanlar dışındaki bütün plan/çalışma sütunlarını JSON karşılaştırmasıyla doğrular ve bekleyen model değişikliği olmadığını kontrol eder.

Son doğrulama: PostgreSQL etkin geniş regresyon grubunda **208 başarılı, 0 başarısız, 0 atlanan** test. Coaching API Release derlemesi **0 hata, 0 uyarı** ile geçti. Kanıtlar: `artifacts/coaching-plan-history-20261024/regression.trx`, `regression.log`, `release-build.log`. Testler geçici yerel PostgreSQL veritabanlarında çalıştırıldı; test sunucusu tamamlandıktan sonra kaldırıldı.
