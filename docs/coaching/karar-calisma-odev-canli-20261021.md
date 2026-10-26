# Karar–çalışma–ödev bağlantıları: canlı doğrulama

Tarih: 21 Ekim 2026, Europe/Istanbul. Durum: canlıya yayımlandı; öğrenci ve öğretmen uçtan uca akışları geçti.

## Yayımlanan sürüm

| Bileşen | Üretim imajı |
|---|---|
| Coaching API | `eduivme/coaching-service:coaching-decision-work-20261020-r2` |
| Öğrenci arayüzü | `eduivme/admin-panel:coaching-decision-work-focus-20261021` |
| Öğretmen arayüzü | `eduivme/staff-portal:coaching-decision-work-focus-20261021` |

Sunucudaki dağıtım dizini: `/var/lib/eduivme/releases/coaching-decision-work-20261020`. Son üç imaj `production.compose.json` dosyasında birleştirildi. Diğer servislerin kapsayıcı kimlikleri değişmedi; Notification servisi yeniden yayımlanmadı. Çalışma ortamı, portlar ve sağlık kontrolü ayarları korundu.

`20261020143601_WeeklyDecisionWorkLinks` geçişi yalnız `coaching.weekly_decisions` tablosuna boş bırakılabilir `StudyTaskId` ve `AssignmentId` UUID sütunlarını ekledi. Silme, zorunlu alan veya silme zinciri eklenmedi. Geçiş önce üretim yedeğinden geri yüklenen ayrı veritabanında denendi; geçici veritabanı doğrulama sonunda kaldırıldı. Yedekler sunucuda tutuldu.

İlk dağıtım denemesinde, geri yüklenmiş şemanın PostgreSQL tarafından yeniden yazılan eski CHECK ifadeleri metinsel karşılaştırmayı durdurdu. Uygulamalar önceki sürüme döndü; eklenen iki boş sütun güvenle kaldı. Kontrol, canlı şemanın kendi geçiş öncesi çıktısıyla karşılaştırılması ve yalnız beklenen iki sütunun ayrılması şeklinde düzeltildi. Yeniden deneme mevcut geçişi tekrar uygulamadan geçti. İlk hata ve başarılı tekrar günlükleri saklandı.

## Canlı doğrulama

Testler mevcut test öğrencisi ve atanmış öğretmenin kendi tarayıcı oturumlarında yapıldı. Eski kararları değiştirmemek için `CANLI KARAR BAĞLANTI TESTİ · 21.10.2026` başlıklı ayrı karar oluşturuldu.

| Senaryo | Doğrulanan sonuç |
|---|---|
| Mevcut çalışma ve ödev bağlama | Öğrenci tamamlanmış çalışmanın 55 dakikasını ve önceden puanlanmış ödevin 95/100 değerlendirmesini gördü. Eski kayıtlar değişmedi. |
| Öğrenciden ilgili işe geçiş | Bağlantılar doğru ödevi ve doğru planın seçili çalışma ayrıntısını açtı. Öğrenci kartında öğretmene ait düzenleme kontrolleri bulunmadı. |
| Çakışma | 22 Ekim 18.00 için yeni çalışma isteği sunucu tarafından çakışma açıklamasıyla reddedildi. |
| Takvim önizlemesi | 22 Ekim 10.00–10.30 için günün diğer işleri, toplam süre ve İstanbul saat dilimi gösterildi. |
| Açık onay | Onay kutusu işaretlenmeden uygulama düğmesi kapalıydı. Onay öncesi veritabanı kontrolünde çalışma sayısı 62.016, plan sürümü 7 ve önceki karar bağlantısı değişmemişti. |
| Yeni çalışma ve taşıma | Onayla yeni 30 dakikalık çalışma oluşturuldu; ikinci önizleme ve onayla aynı kimlik korunarak 23 Ekim 11.00–11.30'a taşındı. Eski/yeni zamanlar gösterildi. |
| Tamamlanmış çalışmanın korunması | Önce eski tamamlanmış işte, sonra yeni iş tamamlandığında taşıma seçeneğinin bulunmadığı doğrulandı. Sunucu korumaları regresyon testlerinde de geçti. |
| Karardan ödev oluşturma | 24 Ekim 16.00 son teslimli, tahmini 30 dakikalık ayrı ödev oluşturuldu. Önceki ödev silinmedi. |
| Öğrenci gerçekleşmesi | Öğrenci bağlantıdan yeni çalışmayı açıp 25 dakika ile tamamladı; bağlı yeni ödevi açıklamayla teslim etti. |
| Öğretmende takip | Hafta yenilendiğinde çalışma tamamlandı/25 dakika, ödev teslim edildi olarak göründü. Karar bu sırada hâlâ planlandı durumundaydı. |
| Kararın değerlendirilmesi | Öğretmen ayrıca kararı tamamlandı yaptı. Öğrenci yenilediğinde kararın tamamlandığını ve iki işin son durumunu gördü. |
| Mobil ve tema | Yeni öğrenci/öğretmen kartları koyu temada 390×844 görünümde kontrol edildi; yatay taşma ve denetimde kontrast uyarısı görülmedi. Sonunda açık tema ve normal ekran boyutu geri yüklendi. |

Bu akıştaki takvim onayını işlemi yapan öğretmen verir; ayrı bir öğrenci onay kuyruğu yoktur. İşin tamamlanması karar durumunu kendiliğinden değiştirmez. Yeni çalışma genel çalışma olarak oluşturulur; otomatik kazanım atamaz. Yeni ödev test sonunda teslim edildi durumundadır, henüz puanlanmamıştır.

## Bulunan ve düzeltilen hatalar

1. **Bağlantı kaydetmede HTTP 500:** `DecisionLinksSave` record parametresinin `Range` doğrulaması property üzerinde tanımlanmıştı; ASP.NET MVC parametre doğrulamasını reddediyordu. `JsonRequired` property üzerinde korunup `Range` yapıcı parametresine taşındı. Gerçek Kestrel/MVC HTTP testleriyle önce hata üretildi (2 başarısız/4 başarılı), ardından 6 test geçti. Geçerli istekler, eksik/negatif sürüm ve geçersiz tahmini süre denetlendi. Düzeltilmiş API imajı yayımlandı.
2. **Bağlantıdan çalışma ayrıntısına odaklanma:** İlk arayüz paketi mevcut `task` sorgu parametresini işleyen öğrenci plan dosyasını içermiyordu. Dar dosya farkı incelenip bu dosya dağıtıma dahil edildi. İlgili çalışma ayrıntısının açılması ve başka/geçersiz çalışma kimliğinin yok sayılması için iki yönlendirme testi eklendi. İki arayüz yeniden derlenip yayımlandı; öğrenci bağlantısı canlıda tekrar doğrulandı.

## Testler ve servis kontrolü

- Son sunucu/PostgreSQL/HTTP regresyon grubu: **59 başarılı, 0 başarısız, 0 atlanan**. İlk dağıtım öncesindeki grup 53 testti; HTTP sınırı için 6 test eklendi.
- Öğrenci arayüzü: **120 başarılı**; öğretmen arayüzü: **124 başarılı**. Ortak bileşenler iki uygulamada da çalıştığından bunlar 244 bağımsız senaryo anlamına gelmez.
- Odaklanma düzeltmesinden sonraki yönlendirme/plan/bileşen grubu: **38 başarılı**; yukarıdaki gruplarla örtüşür ve iki yeni yönlendirme testini içerir.
- API üretim derlemeleri ve her iki arayüzün son üretim derlemeleri başarılı. Önceden bulunan paket/stil bütçe uyarıları sürüyor; derlemeyi durduran hata yok.
- Üç değişen servis çalışıyor, yeniden başlama sayıları 0. Genel erişimde JS/CSS dosyaları 200 ve doğru MIME türüyle dönüyor.
- HTTP düzeltmesi sonrasındaki **16,7 dakikalık** son API günlük aralığında ERR/FTL veya işlenmemiş istisna bulunmadı. Bu sonuç belirtilen akışları ve gözlem aralığını kapsar.

## Mevcut kayıtların korunması

Geçişten önceki Coaching, Identity ve Notification iş satırları kimlik ve içerik özetleriyle karşılaştırıldı. Eski karar satırlarında yalnız yeni boş bağlantı alanları karşılaştırma için normalize edildi. Sadece testte kullanılan aktif planın beklenen sürüm ve güncelleme zamanı değişikliği ayrı denetlendi.

| Veri | Önce → sonra |
|---|---|
| Plan sayısı | 44 → 44 |
| Çalışma sayısı | 62.016 → 62.017; yalnız yeni test çalışması |
| Tamamlanan çalışma | 1 → 2; yalnız yeni test tamamlaması |
| Çalışma–kazanım bağlantısı | 61.979 → 61.979 |
| Aktif test planı sürümü | 7 → 10; yeni iş ekleme, taşıma, tamamlama |

Eski hesaplar, çalışma içerikleri/tamamlamaları, kararlar, ödevler, teslimler ve kazanım ilişkileri korundu. Önceden puanlanmış ödev 95/100 olarak kaldı. Silme yapılmadı. Yeni test kararı, yeni çalışma, yeni ödev/teslim ve bunların bildirim/geçmiş kayıtları kanıt olarak bırakıldı.

| Yeni test kaydı | Kimlik |
|---|---|
| Karar | `568842fe-b450-4b59-bf1a-2ffe8e9c00e8` |
| Çalışma | `6f8117eb-59fd-4268-b69e-53a9d804025d` |
| Ödev | `24602014-478c-4ace-b2e5-9d682877cffa` |
| Korunan aktif plan | `a4cea155-5485-4db6-9f52-1ec4df8080c3` |

## Kanıtlar

- [Son veritabanı ve servis kontrolü](../../artifacts/coaching-decision-work-live-20261020/final-verification.log)
- [Onay öncesi veritabanı kontrolü](../../artifacts/coaching-decision-work-live-20261020/preview-proof.log)
- [Başarılı geçiş tekrarı](../../artifacts/coaching-decision-work-live-20261020/promotion-verified.log)
- [HTTP düzeltmesi dağıtımı](../../artifacts/coaching-decision-work-live-20261020/http-validation-fix.log)
- [Arayüz odaklanma düzeltmesi dağıtımı](../../artifacts/coaching-decision-work-live-20261020/focus-promotion.log)
- [59 sunucu testi](../../artifacts/decision-work-deployment-postgres-fixed.log)
- [Arayüz testleri](../../artifacts/decision-work-deployment-ui.log)
- [38 yönlendirme/plan testi](../../artifacts/decision-work-focus-regression.log)
- [Canlı arayüz gözlemleri](../../artifacts/coaching-decision-work-live-20261020/live-ui-proof.json)
- [Öğrencide son durum](../../artifacts/coaching-decision-work-live-20261020/student-decision-final.png)
- [Öğretmende son durum](../../artifacts/coaching-decision-work-live-20261020/teacher-decision-final.png)
- [Taşıma önizlemesi ve onay](../../artifacts/coaching-decision-work-live-20261020/teacher-move-preview.png)
