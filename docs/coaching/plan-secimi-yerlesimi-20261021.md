# Çalışma planı seçimi ve tek aktif plan görünümü

21 Ekim 2026. Yerel geliştirme tamamlandı; canlıya dağıtılmadı.

## İncelemenin sonucu

- Öğrenci başına tek aktif plan zaten veritabanında `ux_study_plan_active_student` benzersiz indeksiyle korunuyor.
- Öğrenci başına tek taslak da `ux_study_plan_draft_student` indeksiyle korunuyor. Birden fazla aktif plan veya taslak bu modelin normal davranışı değildir.
- Yeni plan yayımlanınca önceki aktif plan aynı işlem içinde arşivleniyor. Tamamlanan çalışmalar, süreler ve kazanım bağlantıları eski plan üzerinde kalıyor; rapor geçmişi korunuyor.
- Öğrenci açısından bir güncel plan yeterlidir. Taslak, yayımlanmadan hazırlık yapmak; geçmiş planlar ise önceki kayıtları okumak ve gerektiğinde geri dönmek içindir. Geçmiş görünümü günlük çalışmanın merkezinde olmak zorunda değildir.
- Bu değişiklik aktif planı silme, tamamlanan işleri değiştirme veya genel plan düzenleme yetkilerini genişletmez. Mevcut tarih/saat taşıma, tamamlama, taslak düzenleme/yayımlama kuralları devam eder.

## Yapılan değişiklik

`student-study-plans.component.html/.scss/.ts`:

- Sol taraftaki geniş plan listesi kaldırılıp takvimin üstüne kompakt bir seçim alanı yerleştirildi. Detay ve takvim artık tek sütunda bütün çalışma alanını kullanıyor.
- Öğrencide varsayılan filtre **Aktif plan**. Aktif plan otomatik açılıyor. Öğretmenin mevcut başlangıç filtre davranışı korundu.
- Plan listesi aktif plan açıldıktan sonra kapalı bir **Plan seçimi** alanında bulunuyor. İhtiyaç halinde açılabiliyor; taslak/geçmiş/tüm planlar seçildiğinde ilgili liste açılıyor.
- Arşiv filtrede **Geçmiş planlar** olarak adlandırıldı. Geçmiş erişimi, doğrudan plan/çalışma bağlantıları ve kayıt koruması kaldırılmadı.
- Tek sayfalık listede gereksiz önceki/sonraki düğmeleri gizlendi.
- Aktif filtre mevcut taslağı gizlediği için öğrencinin taslak açma işlemi gerektiğinde Draft listesini ayrıca kontrol ediyor. Mevcut taslağa devam ediliyor; okuma başarısızsa aktif plan ve girilmiş bilgiler korunuyor. Öğretmenin mevcut taslak hazırlama akışı değiştirilmedi.

## Testler

- Öğrenci plan bileşeni ve gerçek yönlendirme grubu: **35 başarılı**. Varsayılan aktif filtre, aktif planın açılması, yan sütunun kaldırılması, kapalı plan seçimi, gizli taslağa devam etme, taslak sorgu hatasında mevcut planın korunması, kayıtlı plana/çalışmaya bağlantı ve önceki korumalar dahil.
- Öğretmen takip merkezi, bağlam ve öğrenci raporu grubu: **27 başarılı**. Ortak bileşenin öğretmen kullanımındaki taslak ve ayrılma korumaları dahil.
- Öğrenci üretim derlemesi ve öğretmen üretim derlemesi başarılı. Mevcut stil/paket bütçe uyarıları devam ediyor.
- Yerel tarayıcı örneğinde gerçek bileşen ve sentetik plan verileriyle aktif filtrenin açılması, seçicinin kapalı kalması ve geçmiş listesinin açılması kontrol edildi. Bu kontrol canlı hesap testi değildir.
- Masaüstü 1440 px: çalışma alanı ve plan detayı aynı **1200 px** genişlikte; sol yan sütun yok. Mobil 390 px: iki alan da **358 px**, sayfa düzeyinde yatay taşma yok. Koyu tema da incelendi.

Kanıtlar:

- `artifacts/plan-picker-red-retry.log`
- `artifacts/plan-picker-green-final.log`
- `artifacts/plan-picker-staff-regression-final.log`
- `artifacts/plan-picker-student-build-final.log`
- `artifacts/plan-picker-staff-build.log`
- `artifacts/plan-picker-light-desktop.png`
- `artifacts/plan-picker-dark-mobile.png`

İlk test derlemesi ilerlemediği için yalnız bu görevin test süreci kapatılıp Angular işçi sayısı sınırlandırılarak yeniden denendi; uygulama bağımlılıkları veya ortak derleme ayarları değiştirilmedi. İlk öğretmen kontrolü, yeni öğrenci taslak sorgusunun ortak bileşen üzerinden öğretmene de gitmesini yakaladı; sorgu öğrenci akışıyla sınırlandırıldı ve öğretmen grubu tekrar geçti.

Canlı hesap, plan, çalışma veya kazanım kaydına dokunulmadı. Yeni tablo/sütun/migration yoktur. Bu dosyalar önceki günlük işler geliştirmesiyle birlikte henüz yerel çalışma alanındadır; canlı dağıtım bu tur yapılmadı.
