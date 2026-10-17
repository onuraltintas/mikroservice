# Haftalık takvim taslağı — 17 Ekim 2026

Öğrenci paneline **Haftalık programım** ekranı eklendi: `/coaching-portal/weekly-plan`.
Bu aşama haftalık ders şablonunu oluşturur ve kaydeder. Kazanımları 1 Haziran'a kadar
dağıtan yıllık plan üretimi sonraki aşamadır. Özellik 17 Ekim 2026'da canlıya alındı;
gerçek öğrenci akışının ve veri koruma kontrollerinin sonuçları
[canlı yayın raporunda](weekly-calendar-live-release-20261017.md) bulunur.

## Kullanım

1. Menüden **Haftalık programım** ekranını aç. Masaüstünde hafta, dar ekranda gün görünümü açılır.
2. **Gün / Hafta / Ay** düğmeleriyle görünümü değiştir; oklarla tarihleri gez.
3. Gün veya haftada boş saate sağ tıkla ve **Ders ekle** seç. Dokunma, normal tıklama ve
   üstteki **+ Ders ekle** düğmesi de aynı formu açar.
4. Ders, gün, başlangıç ve 30 dakika / 1 saat seç. Ders seçenekleri öğrencinin eğitim
   bağlamına uygun mevcut katalogdan gelir. Gece yarısından sonraki güne taşan kart eklenmez.
5. Kartın gövdesinden tutarak gününü/saatini değiştir. Sağ üst ve sağ alt tutamaçlarla
   süreyi 30 veya 60 dakikaya getir. Hareketler yarım saatlere oturur. Örneğin
   Matematik 18.00–18.30, Türkçe 18.30–19.00 aynı saate yerleşebilir.
6. Karta tıklayarak aynı değişiklikleri formdan yap veya **Dersi kaldır** seç.
7. **Taslağı kaydet** düğmesine bas. Kaydetme açık bir kullanıcı eylemidir; otomatik kayıt yoktur.
   Yeniden açıldığında kayıtlı haftalık düzen yüklenir.

Ay görünümü aynı haftalık düzeni tekrar ederek gösterir; bağımsız tarihli çalışma kayıtları
oluşturmaz. Bir güne tıklamak günlük saat görünümünü açar. Haftalık toplam ayda çoğaltılmaz.
Escape sürüklemeyi iptal eder. Çakışan veya takvim dışına bırakılan kart önceki yerinde kalır.
Sayfadan ayrılırken kaydedilmemiş değişiklikler için uyarı verilir. İki sekme aynı taslağı
değiştirirse eski sürümün kaydı 409 ile reddedilir; yerel kartlar korunur ve kullanıcı
**Kaydedileni yeniden yükle** seçeneğiyle devam eder.

## Veri modeli ve servis

Yeni tablo: `coaching.study_weekly_drafts`. Öğrenci başına tek taslak tutulur.

| Alan | Anlamı |
|---|---|
| `Id` | Taslağın kimliği |
| `StudentId` | Oturumdaki öğrencinin ortak `UserId` değeri; benzersiz indeks |
| `TimeZoneId` | Doğrulanan saat dilimi, varsayılan `Europe/Istanbul` |
| `SlotsJson` | JSONB ders kartları: kart kimliği, ders kimliği, ders adı/sınav kodu anlık görüntüsü, haftanın günü, başlangıç dakikası ve süre |
| `Version` | Eşzamanlı güncelleme kontrolü |
| Oluşturma/güncelleme alanları | Mevcut AggregateRoot denetim alanları |

`GET /api/coaching/study-planning/weekly-draft` taslak ve seçilebilir dersleri döndürür;
okuma boş taslak kaydı oluşturmaz.

`PUT` aynı adrese `expectedVersion`, `timeZoneId` ve kartları gönderir. İlk kayıtta sürüm
`null`, sonraki kayıtlarda son okunan sürüm kullanılır. İstemci öğrenci kimliği göndermez;
ders adı ve sınav kodu sunucudaki katalogdan alınır. API yalnız Student rolüne ve kendi
öğrenci kapsamına açıktır. Süreler, yarım saat sınırları, gün, çakışma, ders uygunluğu,
yinelenen kart kimlikleri ve aşırı büyük sayılar sunucuda da doğrulanır.

PostgreSQL kaydı öğrenci bazında işlem kilidi ve sürüm kontrolü kullanır. Hesap silme
işlemiyle aynı planlama kilidi kullanılır. Taslak kişisel veri dışa aktarımına ve öğrenciye
ait verilerin silinmesi sürecine dahil edilmiştir. Katalogdan kaldırılan bir dersin son
kaydedilmiş adı görünmeye devam eder; yeniden kaydetmeden önce kart kaldırılmalı veya
uygun bir dersle değiştirilmelidir.

## Mevcut verilerin korunması

Geçiş: `20261016213358_WeeklyStudyDraft`. Yalnız yeni tabloyu ve indeksini oluşturur.
Hesaplara, mevcut planlara, görev/çalışma kayıtlarına veya mevcut çalışma saatlerine veri
geçişi uygulanmaz. Taslağı kaydetmek bu tablolara yazmaz. **Mevcut planlarım** bağlantısı
eski ekranı açmaya devam eder. Bu taslak sonraki aşamada yıllık üretimin girdisi olacaktır.

Dağıtımda önce veritabanı yedeği alınmalı, ardından Coaching geçişi, API ve öğrenci
arayüzü birlikte yayımlanmalıdır. Uygulama geri alınırken taslak tablosunu korumak gerekir;
`Down` geçişi yeni taslakları siler. İlk geliştirme doğrulamasında yerel geçici test
veritabanlarında geçiş ve geri geçiş provası yapıldı.

## Doğrulama

| Kontrol | Sonuç | Kanıt |
|---|---|---|
| Müfredat + haftalık taslak servis/API testleri | 51 başarılı, 0 atlanan | `artifacts/coaching-weekly-backend-all-r2.log` |
| Mevcut koçluk regresyon testleri | 694 başarılı, 0 atlanan | `artifacts/coaching-weekly-coaching-regression-final.log` |
| Öğrenci paneli arayüz testleri | 184 başarılı | `artifacts/coaching-weekly-ui-regression-final.log` |
| Masaüstü + mobil tarayıcı testleri | 8 başarılı | `artifacts/coaching-weekly-e2e-verified.log` |
| Üretim derlemesi | Başarılı | `artifacts/coaching-weekly-build-verified.log` |
| EF model/geçiş kontrolü | Bekleyen model farkı yok | `artifacts/coaching-weekly-model-final.log` |

Tarayıcı testleri yerel üretim derlemesinde, oturum ve API yanıtları test verisiyle
çalıştırıldı; canlı doğrulama değildir. Mobil boyutlandırma Chrome dokunma olaylarıyla
sınandı. Testler sağ tıkla ekleme, taşıma, iki kenardan boyutlandırma, yarım saate ikinci
ders ekleme, çakışmanın geri alınması, Escape ile iptal, kayıt/yeniden açma, sürüm
çatışması ve tamamlanmış eski plana erişimi kapsar.

Gerçek yerel PostgreSQL testinde geçiş geri/ileri çalıştırıldı, JSONB kayıt yeniden
okundu ve başka DbContext üzerinden eski sürümle yazma reddedildi. Eski görevlerin
değerlerinin, aktif planın ve çalışma saatlerinin korunduğu ayrıca sınandı.

Yeni takvim/helper kodunda satır kapsamı %95,83; ifadeler %89,23. Backend yeni domain
modeli %100, servis %95, controller %83,3, yapılandırma %100 satır kapsamına ulaştı.
Üretim derlemesi başarılıdır; takvim stili 4 KB uyarı eşiğini geçer, 8 KB hata sınırının
altındadır. Projedeki diğer mevcut derleme uyarıları da günlükte görülebilir.

Ekran görüntüleri: `artifacts/coaching-weekly/calendar-desktop.png` ve
`artifacts/coaching-weekly/calendar-mobile.png`.
