# Karar ve çalışma sonuçlarının sonraki haftalık değerlendirmeye bağlanması

25 Ekim 2026. Değişiklik kaynak kodunda uygulanmıştır. Canlı ortama veya kullanıcı veritabanına dağıtım yapılmamıştır.

## Döngü

Önceki hafta alınan kararlar, sonraki haftanın değerlendirmesinde **Önceki kararlar ve bu haftanın çalışmaları** bölümüne gelir. Bu bölüm karar listesinin sayfasından bağımsızdır; kapanmış veya iptal edilmiş önceki hafta kararları kaybolmaz. Daha eski açık kararlar da görünür. Eski kapalı kararlar bu haftaya denk gelen son tarih, tamamlanma veya iptal kaydı üzerinden yeniden görünür.

Her sonuç; karar kimliği, sürümü, kaynak haftası, başlığı, durumu ve son tarihini taşır. Bağlı çalışmanın gerçekleşen süre/soru sayısı ve tamamlanma durumu; bağlı ödevin teslim durumu, puanı ve öğretmen geri bildirimi birlikte gösterilir. Haftanın planlanmış çalışmalarının ayrı listesi ve özeti de değerlendirmeye bağlanır. Devredilen çalışmalarda kök çalışma kimliği korunur; kararın eski çalışmasıyla bu haftaki kopyası ilişkilendirilir. Planlanan yük ve emek toplamları önceki rapor kurallarını kullanır.

Öğrenci karar başına **İşe yaradı / Kısmen işe yaradı / İşe yaramadı / Uygulanmadı** seçebilir ve 5–1000 karakterlik sonuç notu ekleyebilir. Bu alan isteğe bağlı öğrenci değerlendirmesidir; yalnız bir işi tamamlamak otomatik olarak olumlu etki kaydı oluşturmaz. Öğretmen kaydedilmiş sonuçları ve öğrenci notlarını inceleyerek gelecek haftanın kararlarını mevcut karar alanından oluşturur.

## Geçmişin korunması

- Değerlendirme kaydedilince sunucu sonuçların o anki görüntüsünü saklar. Kaynak kararlar, çalışma kayıtları ve ödevler değiştirilmez.
- Güncel sonuçlar ile **Değerlendirmenin kaydedildiği andaki sonuçlar** ayrı gösterilir. Sonraki bir gerçekleşme veya not düzeltmesi önceki görüntüyü değiştirmez.
- Öğrenci değerlendirmeyi güncellerse önceki metin, puan, odak, sonuç notları ve sonuç görüntüsü sürüm geçmişine alınır. Önceki sürümler ekranda okunabilir.
- Öğretmenin inceleme kaydı, incelediği değerlendirme sürümünün sonuç görüntüsünü de içerir. Yeni öğrenci sürümü, mevcut inceleme kuyruğu kuralıyla yeniden inceleme gerektirir.
- Eski değerlendirmelere tarihsel sonuç görüntüsü uydurulmaz. Yeni alanları boş kalır. Kullanıcı güncel sonuçlarla yeniden kaydederse eski metin ve eski sürüm korunarak ilk görüntü eklenir.
- Aynı içerik ve aynı sonuçlarla tekrar gönderim yeni sürüm veya kopya geçmiş oluşturmaz. Eski istemcilerin metin düzenlemeleri, daha önce kaydedilmiş etki notlarını silmez.

Raporlar, planlanan haftaya ait kayıtların okuma anındaki durumunu gösterir. Sonuç görüntüsündeki alınma zamanı açıkça belirtilir; geçmiş bir haftaya bugün yapılan ilk kayıt, o haftanın sonunda alınmış gibi sunulmaz.

## Tutarlılık ve yetki

Ekrana gelen güncel sonuçların parmak izi kayıt isteğine eklenir. Karar, çalışma veya ödev sonuçları arada değişirse eski ekran üzerinden sonuç kaydetme reddedilir. Kullanıcının girdileri korunur; güncel sonuçlar incelenerek tekrar kayıt yapılır. Değerlendirme sürümü ve öğrenci bazındaki mevcut işlem kilidi de korunmuştur.

Başka öğrenciye ait, bu sonuç listesinde bulunmayan, yinelenen veya geçersiz etki kayıtları kabul edilmez. Öğrenci yalnız kendi değerlendirmesini yazar. Öğretmen/kurum erişimi mevcut atama ve öğrenci verisini okuma politikalarıyla doğrulanır. Bir haftada 500 karar veya 10000 çalışma sınırı aşılırsa sonuçlar sessizce kesilmez; istek açıklayıcı hatayla reddedilir.

## Veri geçişi ve API

`20261024213209_WeeklyReviewResultEvidence` yalnız `weekly_reflections` tablosuna iki alan ekler:

- `ResultsJson`: kaydedilen sonuç görüntüsü; eski satırlarda NULL.
- `ReflectionHistoryJson`: önceki değerlendirme sürümleri; eski satırlarda boş dizi.

Eski satırların metinleri, sürümleri ve tarihleri değiştirilmez. Sonuç veya sürüm geçmişi oluşmuşsa bunları kaybettirecek şema geri dönüşü engellenir. Dışa aktarım şeması **1.8**; sonuçlar ve sürüm geçmişi mevcut değerlendirme ve öğretmen inceleme kayıtlarıyla birlikte dışa aktarılır. Yeni bir kişisel veri tablosu oluşmaz; mevcut silme akışı bu verileri değerlendirme kaydıyla birlikte kapsar.

Mevcut haftalık değerlendirme API'si kullanılır:

- GET yanıtında `results` güncel sonuçları, `reflection.results` kaydedilen sonuçları, `reflection.history` eski sürümleri içerir.
- Öğrencinin PUT isteği ek olarak `expectedResultsFingerprint` ve `decisionEffects: [{ decisionId, effect, note }]` gönderebilir. Yeni ekran bu alanları gönderir.
- `effect`: `Helped`, `PartlyHelped`, `DidNotHelp`, `NotTried`.

## Doğrulama

Yerel ve geçici PostgreSQL 17 veritabanlarında tüm koçluk regresyonu **226 başarılı, 0 başarısız, 0 atlanan** testle tamamlandı. kapalı/iptal/devreden kararların sonraki haftaya gelmesi, sayfalamadan bağımsız sonuçlar, süre/soru/puan ve geri bildirim görüntülerinin korunması, kaynak düzeltmelerinde çakışma, aynı isteğin tekrarı, eski istemci uyumluluğu, başka öğrenciye erişimin reddi, devreden çalışma bağlantısı, eski kayıttan veri geçişi ve kayıplı geri dönüş engeliyle sınanmıştır.

Arayüz koçluk regresyonları: öğrenci tarafında **155**, öğretmen tarafında **140** başarılı test. Üretim derlemeleri her iki uygulama için başarılıdır. API Release derlemesi başarılıdır. Veri dışa aktarımı, silme, değerlendirme ve API sözleşmelerinin ek grubunda **20** test başarılıdır.

Kesin sunucu test özeti ve derleme çıktıları `artifacts/coaching-weekly-results-20261025/` altında tutulur.
