# Net gelişimi, hedef karşılaştırması ve onaylı telafi

20 Ekim 2026. Geliştirme ve testler yereldir. Bu aşamada canlıya dağıtım ve canlı veritabanında temizlik yapılmadı.

## Kullanım

Öğrenci **İlerlemem** ekranında, öğretmen ise **Öğrencilerim → Öğrenci → Haftalık plan ve çalışma raporu** bölümünde aynı gelişim bileşenini görür. Raporun başlangıç/bitiş tarihleri değiştirilebilir; varsayılan son 90 gündür.

- LGS, TYT, AYT Sayısal/Eşit Ağırlık/Sözel ve İngilizce YDT ayrı gruplardır. Öğrenci beyanı ve öğretmen kaydı da ayrı gösterilir.
- Her grubun son altı denemesi eskiden yeniye sıralanır. Aynı gün/saat tarihli kayıtlarda kayıt zamanı kullanılır; sonuç UUID'si son denemeyi belirlemez.
- Her testin ortalama neti, net/soru oranı ve ilk/son deneme farkı gösterilir. Net/soru oranı %50'nin altındaysa yanlış/boşları inceleyip tekrar zamanı ayırma önerilir. Bu eşik bir ürün kuralıdır; belirli bir konunun öğrenilmediğine dair ölçüm değildir.
- Eski farklı sınavları aynı yüzde grafiğinde birleştiren trend kaldırıldı. Geçmiş sonuç listesi ve kullanımda olan genel istatistikler devam eder.

## Net hedefi

Öğrenci/öğretmen, hedefin **Okul / üniversite hedefini görüntüle → Net hedefini düzenle** bölümünden kapsam ve hedef neti kaydeder. Mevcut sahiplik kuralı geçerlidir: öğrenci öğretmenin oluşturduğu hedefi değiştiremez; öğretmen yalnız yetkili olduğu öğrencide kendi hedefini değiştirir.

Hedef net 0 ile seçilen kapsamın soru toplamı arasındadır; iki ondalığa izin verilir. Okul hedefi LGS kapsamını kullanır. Üniversite hedefinde TYT veya programın puan türüne uygun AYT/YDT kapsamı seçilir. Uyumsuz bir katalog değişikliği yapmadan önce net hedefinin kaldırılması gerekir.

Rapor, son neti ve `max(0, hedef net − son net)` değerini her kayıt kaynağı için ayrı gösterir. Kapsamda deneme yoksa karşılaştırma uydurulmaz. Kullanıcının kaydettiği hedef ilerleme yüzdesi ve tamamlanma işareti değiştirilmez.

Katalog adı, katalog yılı, taban puanı, puan yılı, yerleştirme aşaması, sıra ve yüzdelik mevcut katalogdan gösterilir. Eksikler **Belirtilmemiş** kalır. Taban puanı nete dönüştürülmez; yerleşme olasılığı veya kabul garantisi üretilmez.

## Plan önerisi ve onay

İlk uygulanabilir öneri türü **günü geçmiş çalışmayı boş bir saate taşımadır**:

1. Aktif plandaki tamamlanmamış, sabitlenmemiş ve başlangıç saati bilinen çalışmalar incelenir. Günü geçmiş, planın saat dilimine göre bugünden önceki gündür.
   Aynı dersin aynı gündeki bitişik kazanım parçaları tek öneride birlikte tutulur; bir ders kartı küçük parçalara bölünerek taşınmaz.
2. Haftalık müsaitlik saatleri kullanılır. Bunlar yoksa aynı saat dilimindeki haftalık taslağın ders saatleri kullanılır; bitişik pencereler birleştirilir.
3. Yarından başlayarak en çok 14 gün içinde yer aranır. Yıllık planın kayıtlı bitiş tarihi aşılmaz. Bitiş tarihi tanımlanmamış manuel planda bu 14 günlük pencere kullanılır.
4. Dolu saatlere, tamamlanmış çalışma saatlerine ve diğer önerilere çakışan yer önerilmez. Saati belirsiz çalışma bulunan gün güvenilir biçimde boş sayılamadığından atlanır. Sabit veya saatsiz eski çalışma otomatik taşınmaz.
5. En çok 10 öneri gösterilir. **Öneriyi incele** yalnız önizlemedir. Kullanıcı tarih/saat değişikliğini işaretleyip **Onayla ve plana uygula** düğmesine basınca mevcut takvim taşıma API'si çağrılır.
6. Her uygulamadan sonra rapor yenilenir. Sürüm değişmişse veya çakışma oluşmuşsa sunucu reddeder; yeni önizleme gerekir. İşlem sürerken çift gönderim ve sayfadan ayrılma engellenir.

Yeni plan veya kopya çalışma üretilmez. Çalışmanın kimliği, süresi, konu ve kazanım bağlantıları aynı kalır. Tamamlanmış kayıtlar değişmez. Öğretmenin aktif planda yaptığı taşıma mevcut plan değişikliği bildirim akışını kullanır.

Bu aşama, düşük nete dayanarak belirli konuların eksik olduğunu varsayıp otomatik yeni kazanım/ders saati eklemez. Ders saati artırma ve yılın geri kalanını yeniden dağıtma, test alanı–müfredat eşleştirmesi ve ayrıca kapasite önizlemesi gerektirir. Mevcut tekrar önerileri bu nedenle yönlendiricidir; onayla uygulanabilen öneri telafi taşımasıdır.

## Veritabanı ve temizlik

Yeni tablo eklenmedi. `academic_goals` tablosuna iki alan eklendi:

| Alan | Tür | Amaç |
|---|---|---|
| `target_practice_exam_code` | nullable varchar(20) | LGS/TYT/AYT alanı/YDT İngilizce kapsamı |
| `target_net` | nullable numeric(5,2) | Kullanıcının net hedefi |

`ck_goal_net_target`, iki alanın birlikte boş/dolu olmasını ve kapsamın sınırlarını doğrular. EF modeli migration ile uyumludur.

Geçiş: `20261020091007_PracticeGoalsAndLegacyExamCleanup`. Canlıdaki son sürümden uygulanacak SQL, önce `PracticeExamBreakdown` kolon geçişini, sonra bu geçişi içerir: `artifacts/practice-progress-20261020/migrations.sql`.

Temizliğin kapsamı:

- Sınav türü **LGS/YKS** olup `practice_exam IS NULL` olan eski sonuçlar silinir. Eski `lesson_answers` dolu olsa bile standart deneme kapsamı bulunmayan kayıt bu gruptadır.
- Yalnız silinen sonuçların `coaching.exam-results.create` idempotency kayıtları temizlenir.
- Sonucu kalmayan, öğrenciye ait veya geçmiş tarihli LGS/YKS sınav tanımları ve bunlara ait `coaching.exams.create` idempotency kayıtları temizlenir. Geleceğe planlanmış boş öğretmen sınavları kalır.
- Yeni ayrıntılı sonuçlar, okul/haftalık/branş sınavları, hesaplar, hedefler, planlar ve çalışma/kazanım bağlantıları bu silme işleminin dışındadır.
- Yeni LGS/YKS sonuçlarında standart kapsam ve ders cevapları zorunlu hale getirildi; temizlenen eski biçimde yeni deneme kaydı tekrar oluşmaz.

Silinen eski test sonuçları `Down` ile geri getirilmez. Canlı dağıtımda temizlik öncesi veritabanı yedeği alınmalıdır. Henüz canlı temizlik çalıştırılmadı.

## Kod temizliği

- Kullanılmayan karışık yüzde trendinin şablonu ve `examTrend()` metodu kaldırıldı.
- Gereksiz test verisi export'u kaldırıldı.
- Önceki aşamadaki geçici `LegacyExamRegression.csproj` kaldırıldı; asıl Identity test projesi yeniden derlenebilir hale getirildi.
- Yinelenen `[Fact]` temizlendi. Kaldırılmış üniversite programı `Create/Edit` imzalarını kullanan testler güncel modele uyarlandı.
- Eski şemayı bugünkü entity ile okuyan tarihsel migration testi kaldırıldı; aynı iki senaryo kendi tarihsel şeması üzerinde yerel PostgreSQL testlerine taşındı. Migration geçmişi silinmedi.
- Türkçe **DİL/SÖZ/LİSANS/ÖNLİSANS** karşılıklarındaki bozuk karakter sabitleri düzeltildi; normalleştirme testleri eklendi.
- `LessonAnswersJson` ve `SubjectScoresJson` okul/haftalık sınavlar ve mevcut raporlarda kullanılıyor; kullanılmayan alan olarak değerlendirilip silinmedi.

## API

- `GET /api/coaching/study-planning/practice-progress?fromDate=2026-10-01&toDate=2026-10-20`
- `GET /api/coaching/staff/students/{studentId}/study/practice-progress`
- `PUT /api/coaching/study-planning/goals/{goalId}/target/net`
- `PUT /api/coaching/staff/students/{studentId}/study/goals/{goalId}/target/net`

Net hedefi gövdesi: `{ "expectedVersion": 3, "examCode": "LGS", "targetNet": 75 }`. Kaldırmak için `examCode` ve `targetNet` birlikte null gönderilir. `expectedVersion` zorunludur.

Telafi onayı, mevcut `.../plans/{planId}/calendar-move` API'sine `expectedVersion`, `taskIds`, `plannedDate`, `startMinute` gönderir. Taşıma için ikinci bir uygulama yolu yazılmadı; mevcut kilit, çakışma, tamamlanmış çalışma ve bildirim kuralları kullanılır.

## Doğrulama

Son birleşik koşuda **192 backend testi**, öğrenci/ortak arayüzde **38**, öğretmen arayüzünde **34 test** geçti; atlanan veya başarısız test yok. Ders parçalarını birlikte taşıma son düzenlemesinden sonra ilgili **10 test** ayrıca tekrar geçti. İki panelin üretim derlemesi başarılıdır; EF modelinde bekleyen değişiklik yoktur.

Gerçek izole PostgreSQL üzerinde temizlik, kapsam/kaynak ayrımı, aynı gün sıralaması, net hedefi sahipliği ve sürümü, saat dilimi, bitiş tarihi, bitişik pencereler, çakışma, tamamlanan kayıt/kazanım bağlantıları ve üretimdeki retry execution strategy sınandı. Yeni rapor tek tutarlı okuma işlemi içinde üretilir; işlem, üretimin yeniden deneme stratejisiyle sarılır.

Öğrenci/öğretmen arayüz testleri ve iki üretim derlemesi başarılıdır. Sayısal sonuç ve koşu dosyaları: `artifacts/practice-progress-20261020/verification.json`. Mevcut uygulamanın stil/paket boyutu ve geniş servis bağımlılıklarındaki PKCS uyarıları bu işin dışında kalmıştır.
