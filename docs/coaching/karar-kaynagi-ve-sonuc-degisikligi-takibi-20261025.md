# Karar kaynağı ve inceleme sonrası sonuç takibi — 25 Ekim 2026

## 7. Yeni kararın kaynağı

Kaydedilmiş değerlendirme bulunan bir haftada yeni karar oluşturulduğunda, değerlendirme kimliği, sürümü ve o sürümün metin/sonuç görüntüsü karar üzerinde saklanır. Öğretmenin karar verirken gördüğü güncel sonuçlar da ayrıca korunur; öğrencinin değerlendirmeyi kaydettiği sonuçlarla karıştırılmaz.

Öğretmen isteğe bağlı önceki kararı seçebilir veya önceki kararın sonuç kartından “Bu kararın devamını oluştur” işlemini kullanabilir. Önceki karar aynı öğrencinin önceki bir haftasına ait olmalı ve kaynak haftanın kaydedilmiş veya güncel sonuçlarında bulunmalıdır. Önceki kararın o sıradaki çalışma/ödev sonucu ile öğrencinin etki değerlendirmesi de korunur. Başka öğrencinin kararı, bilinmeyen kimlik veya geriye dönük kaynak değiştirme reddedilir. Bağlantılar yalnız oluşturulurken yazılır; kararın sonraki başlık, not, durum, sorumlu öğretmen veya çalışma bağlantısı değişiklikleri kaynağı değiştirmez.

Yeni arayüz, gösterilen değerlendirme sürümünü ve güncel sonuç parmak izini gönderir. Kaynak değerlendirme veya sonuçlar değişmişse kayıt 409 ile reddedilir; girilen karar ve önceki karar seçimi ekranda korunur. Tekrarlanan aynı oluşturma isteği yeni karar veya kaynak görüntüsü üretmez. Karar ve kaynağı bildirim yayınıyla aynı işlemde kaydedilir; başarısızlıkta birlikte geri alınır.

Henüz öğrenci değerlendirmesi olmayan haftada mevcut bağımsız karar oluşturma yolu korunur. Bu karara olmayan bir değerlendirme uydurulmaz. Eski kararlara da tahminle kaynak veya devam bağlantısı eklenmez.

## 8. İnceleme sonrasında değişen sonuçlar

Her yeni öğretmen incelemesi, değerlendirme sürümünün yanında öğretmenin gerçekten gördüğü güncel sonuç görüntüsünü ve karşılaştırma parmak izini saklar. Aynı değerlendirme sürümü, sonuçlar değişirse yeniden incelenebilir. Yeni inceleme ayrı satırdır; eski not ve görüntü değiştirilmez. Kuyruğun kapanması son öğretmen incelemesinin değerlendirme sürümü ve güncel sonuçlarla uyuşmasına bağlıdır.

Takip merkezi, eski haftalar dahil sonuçları değişmiş incelemeyi “İnceleme sonrasında sonuçlar değişti” gerekçesiyle yeniden listeler. Öğretmen haftayı açınca eski inceleme görüntüleri ile güncel sonuçları ayrı görür. Gerçekleşme miktarı, fiili gün, tamamlanma, ilgili çalışma/plan durumu, ödev puanı veya geri bildirimi değişince yeniden takip gerekir. İş, öğrenci metninin sürümü değişmese de açılır.

Anlamlı gerçekleşme değişiklikleri ve ödev puanı/geri bildirim değişim sayısı karşılaştırmaya katılır. Sonuç ara bir değere geçip öğretmen görmeden eski değere dönse bile değişiklik kaybolmaz. Yalnız kalan kapsam değişikliği, aynı puan/geri bildirimin yeniden kaydı, gözlem zamanı, gecikmişlik bayrağı veya yetki bayrağı tek başına yeni sonuç işi üretmez. Fiili çalışma değişiklikleri ilgili haftalara göre değerlendirilir; ilgisiz haftalar yeniden açılmaz.

İnceleme onayı, ekranda gösterilen sonuç parmak iziyle doğrulanır. Eski sekme, daha sonra değişen sonucu kapatamaz. Aynı sonucun eşzamanlı iki onayı öğrenci kilidi sayesinde tek inceleme üretir. Sonuç tekrar değişirse sonraki inceleme yeni satır olarak eklenir. Kuyruk sorgusu da mevcut kayıtları yeniden yazmaz; kalıcı son inceleme görüntüsünü ve güncel veriyi karşılaştırır. Ortak öğrenci verileri haftalar için toplu okunur.

## Eski kayıtların korunması ve geçiş

- `20261024225038_DecisionOriginsAndReviewEvidence`: karar için kaynak/önceki karar alanları, inceleme için sonuç görüntüsü/parmak izi alanları eklenir. Alanlar eski satırlarda boş kalır. Aynı sürüme birden fazla inceleme eklenebilmesi için tekillik kısıtı kaldırılır; mevcut satırlar korunur.
- `20261024230308_WeeklyGradeResultRevision`: ödev öğrenci kaydına nullable değişim sayacı eklenir. Eski puan, geri bildirim, durum veya teslim tarihleri değişmez. Aynı puanın tekrar kaydı sayacı ilerletmez; gerçek yeniden puanlama ilerletir.

Eski incelemede güncel sonuç görüntüsü hiç saklanmamışsa geçmişte ne görüldüğü tahmin edilmez. “Eski incelemenin sonuçları yeniden doğrulanmalı” gerekçesiyle bir kez yeni inceleme istenir; eski inceleme aynen kalır. Güncel sonuç parmak izi olmadan değişmiş veya belirsiz kanıtı kapatmaya çalışan eski istemci reddedilir. Değişmemiş, kaydedilmiş öğrenci sonucu üzerinden yapılan ilk eski istemci onayı uyumluluk için korunur.

Yeni bağlantı, tekrarlı inceleme görüntüsü veya puan değişim sayacını kaybettirecek sürüm düşürme engellenir. Dışa aktarım mevcut karar/inceleme kayıtlarıyla bu yeni isteğe bağlı bilgileri de taşır.

## Doğrulama

Testler kaynak sürümünün ve önceki kararın korunmasını, kaynak değişikliği/başka öğrenci engelini, başarısız yayında geri almayı, puan/gerçekleşme düzeltmelerini, ara değişimin eski değere dönmesini, eski sekme onayını, eşzamanlı onayı, yalnız etkilenen haftanın yeniden açılmasını, saatin ilerlemesiyle gereksiz iş oluşmamasını, eski sütunların değişmeden taşınmasını ve kayıplı sürüm düşürmenin engellenmesini kapsar.

Test/derleme günlükleri: `artifacts/coaching-decision-origins-review-followup-20261025/`. Testler geçici PostgreSQL veritabanlarında çalıştırıldı. Canlı ortama dağıtım veya canlı veritabanı geçişi yapılmadı.

Sonuçlar:

- PostgreSQL destekli sunucu regresyonu: 250 geçti, 0 hata, 0 atlanan.
- API, rapor hesabı ve veri koruma: 24 geçti.
- Öğrenci/admin arayüzü: 166 geçti.
- Öğretmen/personel arayüzü: 147 geçti.
- Toplam: 587 başarılı test çalıştırması.
- API Release: 0 hata, 0 uyarı. İki arayüzün üretim derlemeleri başarılı; mevcut paket/stil boyutu bütçesi uyarıları sürüyor.
