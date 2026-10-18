# Koçluk temiz başlangıç — 19.10.2026

Kullanıcının eski planların tümünün test olduğu ve silinebileceği talimatıyla canlı temizlik yapıldı. Hesaplar, profiller, ödevler, hedefler, ücretler, öğretmen–öğrenci ilişkileri ve diğer sınav katalogları korunmuştur.

## Silinen test ve eski veriler

- 9 test planı, 681 bağlı çalışma/görev ve 676 çalışma–kazanım bağlantısı. Kullanıcının yeni silme talimatı doğrultusunda bunların içindeki 6 tamamlanan test çalışması / 115 test dakikası da kaldırıldı.
- Eski plan öğrencisinin 1 haftalık taslağı (8 ders kartı), 1 çalışma uygunluğu kaydı ve 3 StudyDay hatırlatma kaydı.
- LGS’de yeni yayımlanmış sürümden eski olan ve herhangi bir görev/pay tarafından kullanılmayan 269 süre tahmini.
- Güncel süre paylarının kullanmadığı, onaysız 85 eski LGS konu–kazanım eşleştirmesi.
- Pasif, kullanılmayan fazladan Din Kültürü konusu: `lgs-2026:din:ornek-davranislar`.
- Pasif `LIVE261016` canlı doğrulama sınavı ve bağlı boş test dersi. Başka sınavlar bu kodla topluca filtrelenmedi.
- Silinen plan kimliklerine bağlı StudyPlanPublished / StudyPlanUpdated / StudyPlanReminder bildirimleri. Diğer bildirimlerin satır özeti aynı kaldı.

## Kaldırılan şema ve kod

`coaching.study_catalog_lesson_groups` tablosu ve `study_catalog_lessons.GroupId` sütunu, bağlı indeks ve FK kaldırıldı. Canlı ön kontrolde tablo boştu, 170 dersin hiçbirinde GroupId bulunmuyordu. Domain varlığı, EF konfigürasyonu ve yönetim servisindeki eski grup CRUD/taşıma/durum/lock sorguları kaldırıldı. Eski `groups` API türü artık reddedilir. Derslerin üst kaydı dinamik sınav türüdür.

Migration: `20261018205936_RemoveObsoleteLessonGroups`. Herhangi bir eski grup veya grup bağlantısı varsa kilit altında kontrol eder ve veri silmeden hata verir. Tarihî migration dosyaları ve EF migration geçmişi tutuldu; bu dosyalar güncel şemada grubun bulunduğu anlamına gelmez.

Diğer sütunlar sırf boş olabildiği için silinmedi. Sınıf alanı okul kataloğunda, CurriculumVersionId eğitim bağlamında; kaynak kimlikleri tekrar aktarımda, resmî program/konu kayıtları kazanım kaynağı ve kapsam doğrulamasında; sürüm numaraları süre seçimi ve eşzamanlılıkta kullanılıyor. Yeni süre sürümlerinin numarası geriye alınmadı.

## Korunan güncel katalog

| Veri | Son durum |
| --- | ---: |
| Ortak hesaplar | 8, değişmedi |
| LGS dersleri | 6 |
| LGS üst konuları | 49 |
| LGS etkin alt konuları | 144 |
| Kazanımlar | 269 |
| Güncel yayımlanmış süre tahminleri | 269 |
| Onaylı konu–kazanım bağlantıları | 319 |
| Sürümlü kapsam/süre payları | 319 |
| Kayıtlı plan / görev / bağlantı | 0 / 0 / 0 |
| Haftalık taslak | 0 |

Kaynak programlar (6), resmî konu grupları (48) ve kazanımların kaynak eşlemeleri (2) işlevsel olduğundan tutuldu. TYT/AYT/YDT/TDP ve okul dersleri/konuları bu LGS temizliğinin kapsamına alınmadı. Güncel ilk tur toplam bütçesi 11970 dakikadır; önceki rapordaki gibi düşük güvenli editoryal başlangıç tahminidir.

## Doğrulama ve geri dönüş

- 98 koçluk testi gerçek yerel PostgreSQL ile geçti; atlanan test yok. Yeni testler eski grup şemasının/API’sinin kalkmasını, ders–sınav ilişkisini ve dolu grup bulunan ortamda migrationın veri kaybını engellemesini denetler.
- Canlı temel kaynağına yalnız bu değişiklikler eklendi; çalışma alanındaki ilgisiz, henüz yayımlanmamış model değişiklikleri pakete alınmadı. Paket EF model/migration eşleşme kontrolünden geçti.
- Coaching ve Notification veritabanlarının izole kopyalarında temizlik denendi. Temizlik tekrar çalıştırıldı ve güncel katalog değişmedi.
- Canlıdan önce Identity, Coaching ve Notification için yeniden tam yedek alındı. Yedekler ve özel ayarlar sunucudaki erişimi kısıtlı release dizinindedir; repoya alınmadı.
- Silinmesine izin verilmeyen tablo/satırlar önce/sonra özetleriyle birebir karşılaştırıldı: kimlikler ve yetkiler, profiller, öğrenci bağlamı/konu durumu, ödevler, hedefler, ödemeler ve güncel katalog aynı kaldı.
- Koçluk servisi kısa süre durdurularak şema/temizlik uygulandı; yeni imaj açıldı ve sağlık kontrolü geçti. Diğer servislerin konteyner kimlikleri aynı kaldı.
- Canlıda 144 alt konunun yayımlanmış payları yeniden okundu. Örnek haftalık kapasiteyle yıllık önizleme 144 konu / 11970 dakikanın tamamını yerleştirdi; öğrenci için yeni test planı kaydedilmedi.
- Öğrencinin açık tarayıcı sekmesinde `/coaching-portal/study-plans` yenilendi. “Bu görünümde plan yok. Yeni bir taslakla başlayabilirsin.” ve “1 · 0 plan” görüldü; eski seçili plan bağlantısı kaldırıldı.

Veri temizliği geri alınacaksa otomatik `Down` migrationı silinen planları geri getirmez. Sunucudaki `*.before-cleanup.backup` yedekleri izole veritabanına geri yüklenip ilgili kayıtlar kontrollü olarak taşınmalıdır. Şema dönüşü tek başına yalnız boş grup tablosu/sütununu yeniden oluşturur.

Kontrollü betikler: `tools/releases/coaching-clean-start-20261018/`. Arındırılmış canlı sonuç: `artifacts/coaching-clean-start-20261018/live-result.json`.
