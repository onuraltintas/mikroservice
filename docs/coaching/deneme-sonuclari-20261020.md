# Deneme sonuçları — ilk aşama

Tarih: 20 Ekim 2026. Bu geliştirme yerel olarak uygulanmış ve test edilmiştir; bu çalışma kapsamında canlıya dağıtım veya canlı veritabanı değişikliği yapılmamıştır.

## Tamamlanan akış

- Öğrenci **Sınav sonuçlarım → Yeni sonuç → Deneme kapsamı** yolundan LGS, TYT, AYT Sayısal/Eşit Ağırlık/Sözel veya İngilizce YDT seçer. İlgili ders/test satırları sunucunun sunduğu kapsamdan gelir.
- Her satır için doğru, yanlış ve boş girilir. Toplam soru sayısı o testin soru sayısına eşit olmalıdır. Eksik/fazla, negatif, kesirli, mükerrer veya kapsam dışı satırlar kabul edilmez.
- Genel doğru/yanlış/boş, ders satırlarından hesaplanır. Sunucu da toplamları bağımsız olarak doğrular; istemcinin gönderdiği bir net değerine güvenilmez.
- Öğrenci bilgileri kontrol ettiğini onaylayıp kaydeder; yeniden açılan kayıtta aynı kapsam ve cevaplar görünür. Eski sürümle üzerine yazma mevcut `ExpectedVersion` kontrolüyle reddedilir.
- Öğretmen **Sınavlarım → Sonuçları yönet** bölümünden atanmış öğrencisine aynı tabloyla sonuç ekler/düzeltir. LGS sınavına LGS; YKS sınavına TYT/AYT/YDT kapsamları sunulur. Yapılmış bir denemeyi girmek için geçmiş tarihli sınav oluşturulabilir; geleceğe sınav planlama da devam eder.
- Öğretmenin sonucu öğrencinin **Gelişimim** geçmişine net ve ders ayrıntılarıyla yansır. Öğrencinin kendi beyanı da öğretmenin **Öğrencilerim → Öğrenci → Geçmiş kayıtlar → Sınavlar** bölümünde netleri ve kaynağıyla görünür. Mevcut atama/okuma yetki kontrolleri korunur.
- Kaydedilmiş kapsam arayüzde kilitlenir; cevaplar düzeltilebilir. Eski serbest sonuçların ders/konu ve ders puanı alanları korunur. Standart deneme satırları ile serbest konu satırları aynı öğrenci kaydında birleştirilmez.

## Net hesabı ve kapsam

| Kapsam | Standart toplam soru | Net |
|---|---:|---|
| LGS | 90 | Doğru − yanlış / 3 |
| TYT | 120 | Doğru − yanlış / 4 |
| AYT Sayısal | 80 | Doğru − yanlış / 4 |
| AYT Eşit Ağırlık | 80 | Doğru − yanlış / 4 |
| AYT Sözel | 80 | Doğru − yanlış / 4 |
| YDT İngilizce | 80 | Doğru − yanlış / 4 |

Toplam net, yuvarlanmış ders netlerinin toplanmasıyla bulunmaz: toplam doğru ve yanlış üzerinden hesaplanır. Görüntüleme iki ondalıktır; negatif netler korunur. Örneğin LGS'de altı testin her birinde bir yanlış, toplam −2 net verir; −1,98 net olmaz.

Bu tablo standart tam deneme şablonudur. Din kültürü muafiyetine bağlı alternatif sorular, iptal edilmiş gerçek sınav soruları, İngilizce dışındaki deneme dilleri ve branş denemeleri için özel şablonlar bu aşamanın kapsamında değildir; mevcut serbest kayıt yolu açık kalır. AYT kapsamları ilgili puan alanı için takip edilen iki testi ifade eder; AYT kitapçığındaki tüm testlerin toplamı değildir.

Test alanları, düzenlenebilir çalışma planı ders/konu kataloğundan ayrıdır. Örneğin TYT Temel Matematik testinin 40 sorusu, müfredattaki Matematik ve Geometri başlıklarıyla aynı yönetim nesnesi değildir. Kapsamlar sunucudan gelir; net kuralları her iki arayüz için aynı sunucu sözleşmesini kullanır.

Netten resmî puan, sıralama veya yüzdelik üretilmez. Mevcut puan alanına denemenin/yayınevinin bildirdiği puan girilir; bu aşamada puan zorunluluğu değiştirilmemiştir.

## Veritabanı ve servisler

- Yeni tablo yoktur. `coaching.exam_results.practice_exam` nullable `jsonb` alanı eklendi.
- Migration: `20261020083716_PracticeExamBreakdown`. Yalnız kolon ekler; eski satırları yeniden yazmaz, silmez veya yeniden kimliklendirmez.
- JSON; `ExamCode` ve her testin `SectionCode`, `Correct`, `Wrong`, `Empty` değerlerini saklar. Ders adları, standart soru sınırları ve hesaplanan netler sunucu kurallarından üretilir.
- Eski kayıtlarda alan NULL kalır. Bu kayıtlara sınav kapsamı tahmin edilip net ataması yapılmaz. Eski `LessonAnswersJson` ve `SubjectScoresJson` alanları silinmedi.
- `GET /api/exams/profiles`: giriş yapmış kullanıcı için kapsamlar ve soru sınırları.
- Öğrencinin mevcut `/api/coaching/study-planning/exams` sözleşmesine isteğe bağlı `practiceExam` eklendi; cevapta `practiceExam` ve `netSummary` döner.
- Öğretmenin mevcut sonuç ekleme/düzeltme sözleşmesine isteğe bağlı `practiceExam` eklendi. Ayrıntı ve öğrenci geçmişi okumaları `netSummary` döndürür.
- Eski isteklerde bu alan gönderilmediğinde mevcut davranış ve eski idempotency hash biçimi korunur. Ders ayrıntısı değişen istekte aynı idempotency anahtarını tekrar kullanmak reddedilir.
- Geçersiz düzenleme, izlenen sonucu kısmen değiştirmeden reddedilir. Öğretmen düzeltmesinde sonuç kimliği ve öğrenci bağlantısı korunur.

## Doğrulama

Son durum: **163 backend testi (138 regresyon/PostgreSQL + 25 eski sınav/API/idempotency), 56 arayüz testi; toplam 219 test başarılı.** Öğrenci ve öğretmen üretim derlemeleri başarılı. Ayrıntılı sonuç: [verification.json](../../artifacts/practice-exams-20261020/verification.json).

- Gerçek, izole yerel PostgreSQL üzerinde altı kapsamı oluşturma/okuma/düzeltme, eski sürümle yazmanın reddi ve bozuk istek sonrası değişmezlik sınandı.
- Öğretmenin oluşturması, aynı isteğin idempotent tekrarı, farklı gövdeyle aynı anahtarın reddi, düzeltmesi ve öğrencinin geçmişinde görmesi sınandı.
- Yeni kolonu kaldırıp tekrar ekleyen geçiş provasında eski sonuç, puan, ders puanları, hedef, tamamlanan çalışma ve kazanım bağlantıları karşılaştırıldı.
- İlgili arayüz testleri; kapsam seçimi, veri gönderimi, yeniden açma, net ayrıntıları, sınırlar, kilitli kapsam ve geçmiş tarihli deneme girişini kapsar.
- Üretim derlemeleri, EF model/migration uyumu ve değişikliklerin boşluk kontrolleri yapıldı. Koşu çıktıları `artifacts/practice-exams-20261020/` klasöründedir.
- Ana Identity test projesinde bu çalışmadan önce mevcut `CoachingTargetCatalogTests.cs` dosyasındaki yinelenen `[Fact]` tüm test projesinin derlenmesini engelledi. İlgili eski deneme/idempotency testleri, kaynaklarını değiştirmeden ayrı bir test projesine bağlandı. Bu bağımsız sorun burada düzeltilmedi.
- Derleme çıktılarındaki mevcut şablon ve paket boyutu uyarıları bu aşamada değiştirilmedi. Geniş Identity/Notification bağımlılıklarında görülen mevcut PKCS paket uyarıları da bu değişikliğin dışında kaldı.

## Kaynak dayanağı

- [ÖSYM 2026 YKS Kılavuzu sayfası](https://www.osym.gov.tr/2026yuksekogretim-kurumlari-sinavi-yks-kilavuzu)
- [ÖSYM 2026 YKS başvuru kılavuzu PDF](https://dokuman.osym.gov.tr/pdfdokuman/2026/YKS/basvuru_kilavuz06022026.pdf)
- [MEB 2026 LGS başvuru ve uygulama kılavuzu](https://www.meb.gov.tr/meb_iys_dosyalar/2026_03/23190605_LGS_BaYvuru_ve_Uygulama_Kilavuzu_2026.pdf)
- [MEB okul alan adında yayımlanan 2026 LGS kılavuzu kopyası](https://msyo.meb.k12.tr/meb_iys_dosyalar/61/02/727852/dosyalar/2026_06/6a228a6dcfff7148164892_2026_LGS_BA%C5%9EVURU_VE_UYGULAMA_KILAVUZU.pdf): net kuralı bölüm 11.

## Sonraki aşama

Takip geliştirmesi ve temizlik tamamlandı: [Net gelişimi, hedef karşılaştırması ve onaylı telafi](hedef-net-gelisimi-ve-telafi-20261020.md). Bu rapordaki eski test projesi derleme engelleri takip çalışmasında giderildi; geçici test projesi kaldırıldı. Aşağıdaki paragraf ilk aşamanın o tarihteki kapsamını anlatır.

Hedefe ilerleme ve çalışma planı önerileri henüz bu deneme verilerinden üretilmez. Bir sonraki aşamada aynı kapsamdaki denemelerin eğilimi, ders/test alanları ile çalışma kataloğu arasındaki açık eşleştirme, geciken çalışmalar ve hedefin veri yılı birlikte değerlendirilmelidir. Plan değişiklikleri öğrenci/öğretmen onayına sunulmalıdır.

