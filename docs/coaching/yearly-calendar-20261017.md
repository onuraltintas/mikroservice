# Kayıtlı yıllık çalışma planı takvimi

Tarih: 17 Ekim 2026. Durum: geliştirme ve yerel doğrulama tamamlandı; **16:21 Türkiye saatiyle canlıya alındı**. Ayrıntılar: [canlı dağıtım ve veri koruma raporu](yearly-calendar-live-20261017.md).

## Kullanım

1. Öğrenci **Çalışma planlarım** ekranından oluşturulmuş yıllık planını seçer.
2. Haftalık taslaktan üretilmiş plan otomatik olarak **Takvim** görünümünde açılır.
   Masaüstünde Hafta, mobilde Gün varsayılandır. Gün / Hafta / Ay arasında geçilebilir.
3. Takvim yaklaşan, tamamlanmamış çalışma bulunan tarihe açılır. İleride çalışma yoksa
   son kayıtlı tarihe gider. Planın kayıtlı saat dilimi kullanılır; eski planlarda varsayılan Europe/Istanbul'dur.
4. Sayfa takvimi görünür alana getirir. Günün dolu saatine kayar; dar hafta görünümünde
   ilgili günün sütununu da gösterir. Saat sütunu yatay kaydırırken görünür kalır.
5. **Önceki dolu gün**, **Sonraki dolu gün**, **Yakın çalışmaya git** ve **Tarihe git** ile
   boş dönemler arasında gezinilir. Bugün veya elle seçilen boş tarih kendiliğinden değiştirilmez;
   boş aralık açıklaması ve dolu güne geçiş seçenekleri gösterilir.
6. Aynı derse ait bitişik kazanım parçaları tek ders kartında toplanır. Kart seçilince
   özgün çalışma parçaları, kazanım metinleri, süre sürümleri ve mevcut çalışma ayrıntıları açılır.
7. Aktif planda mevcut Tamamla ve Tarihi değiştir işlemleri kullanılabilir. Taslak ve arşiv
   durumları aynı yetki/işlem kurallarını korur. Liste görünümüne her zaman geçilebilir.

Ay görünümünde bir günde en fazla üç ders kartı gösterilir; fazlası günü açan düğmeyle
incelenir. Aylar arasında haftalık şablon tekrar edilmez: yalnız seçilmiş planın **gerçek
kayıt tarihleri** gösterilir. Aynı saate denk gelen kayıtlar yan yana sütunlarda kalır.
Tamamlanma durumu ve bir ders kartındaki tamamlanmış parça sayısı gösterilir.

## Eski plan ve çalışma kayıtlarının korunması

- Veritabanı, migration, API sözleşmesi veya dağıtım servisi değişmedi. Yeni takvim mevcut
  `StudyPlan.tasks` verisini okur; görünüm değiştirmek veya tarih gezinmek yazma isteği göndermez.
- Ders kartlarını birleştirme yalnız ekran modelindedir. Plan/görev kimliği, süre, konu,
  kazanım bağlantısı, gerçek dakika, soru sayısı veya tamamlanma bilgisi yeniden yazılmaz.
- Saat bilgisi olmayan eski kayıtlar **Saati belirtilmemiş** alanında gösterilir. Bunlara
  00.00 veya başka bir saat atanmaz. Eski manuel taslak editörü ve eski planların varsayılan
  liste görünümü korunur; görüntülenebilir eski planlarda Takvim seçilebilir.
- Kart ayrıntıları mevcut form ve servisleri kullanır. Tamamlama/tarih değiştirmede yine
  planın güncel sürümü gönderilir. Başka çalışmalardaki kaydedilmemiş süre/soru değerleri korunur.
- Aynı plan güncellendiğinde takvimin seçilmiş dönemi ve kaydırma konumu sıfırlanmaz.
  Başka plan seçilince yeni planın dolu tarihine yeniden odaklanılır.
- Öğretmen/personel için kullanılan ortak bileşenin salt okunur ve öğrenci kapsamı kuralları değişmedi.

Takvimdeki kart seçimi ayrıntıları açar. Haftalık şablondaki ders ekleme, taşıma ve
boyutlandırma ekranı mevcut ayrı işlevini sürdürür. Bu değişiklik yıllık planın yeni
görüntüleme biçimidir; yeni bir plan üretme işlemi değildir.

## Teknik kapsam

- `study-plan-calendar.ts`: salt ekran modeli; tarih/saat dilimi yardımcıları, bitişik
  parça gruplama ve dolu tarihe odak seçimi. Girdi görev listesi değiştirilmez.
- `study-plan-calendar.component.*`: gün/hafta/ay takvimi, dolu gün gezinmesi,
  saat çizgileri, tamamlanma göstergeleri ve görev kimliklerini bildiren seçim olayı.
- `student-study-plans.component.*`: Takvim/Liste seçimi; seçilmiş kartın özgün görevlerini
  mevcut ayrıntı/formlarla gösterme. Kayıtlı plan DTO'su değiştirilmez.
- Takvim grupları plan yüklenince tarih bazında indekslenir; çizimde yalnız seçilen gün,
  hafta veya ay kullanılır. Aylık kart sayısı sınırlandırılır; liste sayfalaması korunur.
- Dakika satırları tam piksel ölçeği kullanır. Tarayıcıda gözlenen kesirli satır birikimi
  nedeniyle oluşan 13,5 px saat hizası sapması giderildi. Gün sonundaki kaydırma tarayıcının
  doğal 24 saat sınırında kalır; dersin görünürlüğü ayrıca doğrulanır.
- Ortak öğrenci temasının takvim kartlarını tek renge çevirmesi önlendi. Seçili görünüm
  ve ders renkleri tarayıcıda hesaplanan stillerle test edilir.

## Doğrulama

| Kontrol | Sonuç / kanıt |
|---|---|
| TDD başlangıcı | Eksik takvim için 1 başarısız / 21 başarılı: `artifacts/yearly-calendar-red.log` |
| Öğrenci koçluk ekranları ve servisler | **279 başarılı, 0 başarısız**: `artifacts/yearly-calendar-ui-verified.log` |
| Masaüstü + mobil gerçek tarayıcı | **28 başarılı**: `artifacts/yearly-calendar-e2e-ready.log` |
| Üretim derlemesi | Başarılı: `artifacts/yearly-calendar-build-ready.log` |
| Yeni takvim bileşeni | %100 ifade kapsamı, %88,89 dal kapsamı |
| Yeni gruplama/tarih yardımcıları | %100 ifade ve dal kapsamı |

Tarayıcı testleri yerel üretim derlemesinde taklit API verileriyle çalıştırıldı; canlı
veritabanı testi veya canlı dağıtım olarak sunulmuyor. Yeni veri tabanı davranışı olmadığı
için bu aşamada migration/PostgreSQL geçişi yapılmadı.

Senaryolar: gerçek tarihlerin gün/hafta/ayda aynı kalması; boş aylara hayalî tekrar
yazılmaması; pazar günündeki ilk dersin dar haftada görünmesi; seçilen günün saatine
odaklanma; çakışan kartların birbirini örtmemesi; eski tamamlanmış kaydın 25 dk/0 soru
değerlerinin korunması; saat uydurulmaması; ayrıntıya yeniden odaklanma; tamamlama ve
tarih değiştirmede doğru plan sürümü; diğer çalışmadaki kaydedilmemiş alanların korunması.

Ara çalıştırmalarda tema, saat hizası, ekran odağı ve tarayıcı kaydırma sınırı sorunları
bulundu ve düzeltildi. `42b600b6` ara commit mesajındaki 26 başarılı ifadesi o çalıştırmanın
sonucunu doğru yansıtmıyordu: o aşamada 25/26 geçmişti. Nihai başarılı sonuç yukarıdaki
28 senaryolu günlükle doğrulanır; önceki başarısız günlükler korunmuştur.

Derleme stil bütçesi uyarıları içerir; yeni takvim SCSS'i 4 KiB uyarı eşiğini aşar,
8 KiB hata sınırının altındadır. Derleme hatası yoktur. İlgisiz mevcut kod/stil değişiklikleri yapılmadı.

## Ekran örnekleri

- `artifacts/yearly-calendar-desktop.png`
- `artifacts/yearly-calendar-mobile.png`
- `artifacts/yearly-calendar-month-desktop.png`
- `artifacts/yearly-calendar-month-mobile.png`

Bu görsellerdeki veriler yerel test örnekleridir.

## Canlıya alma sınırı

İlk geliştirme aşaması canlı verileri değiştirmedi. Sonraki canlıya alma isteğiyle yalnız
ilgili 7 arayüz dosyası mevcut canlı kaynağa uygulandı; migration yapılmadı.
Takvim odağı, kazanım ayrıntıları, eski planlara erişim ve test planında çalışma kaydetme
canlı öğrenci oturumunda doğrulandı. Testin plan durumuna ve eklenen 17 dakikalık sentetik
kayda etkisi [canlı raporda](yearly-calendar-live-20261017.md) açıklanmıştır.
