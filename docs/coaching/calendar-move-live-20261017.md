# Takvim taşıma ve öğretmen takibi: canlı dağıtım

17 Ekim 2026. Coaching ve iki portal canlıya alındı. Öğrenci taşıma/çakışma/tamamlama işaretleri,
öğretmen toplu takibi ve öğretmenden öğrenciye taslak oluşturma canlıda doğrulandı.
Son arayüz paketinin 15 dakikalık izlemesi 17:55:12 Türkiye saatinde 31 başarılı örnekle tamamlandı.
Üç serviste yeniden başlama ve uygulama hata kaydı yok.

## Dağıtılan paketler

- `coaching-calendar-move-20261017`: 17:27:09 Türkiye saati, Coaching / admin-panel / staff-portal.
- `coaching-calendar-move-20261017-r2`: 17:39:42 Türkiye saati, yalnız admin-panel / staff-portal.
- Coaching uygulama kaynağı: `2acdd750`; arayüzlerin son kaynağı: `7f1f61de`.
- İkinci paket çalışan Coaching konteynerini, Identity'yi ve diğer ürünlerin konteynerlerini değiştirmedi.
- Öğretmen portalı mevcut yönlendirmeyle `https://onuraltintas.net/staff/` altında sunuluyor.
  Öğrenci/yönetici uygulaması `https://eduivme.com` üzerinde kalıyor.

Her servis kendi canlı kaynak kopyasından hazırlandı. İlk pakette Coaching'in 9 dosyalık
farkı, öğrenci panelinin 7 dosyalık farkı, öğretmen portalının koçluk ekranları ve ortak plan
bileşeninin bağımlılıkları alındı. İlgisiz çalışma ağacı değişiklikleri dahil edilmedi.
İmajlar tam mevcut canlı imajların üstüne, ağ erişimi kapalı Docker derlemesiyle kuruldu.
Kaynak kökeni ve paket SHA-256 listeleri `artifacts/coaching-calendar-move-live-20261017*/` altında.

Son canlı imajlar:

- Coaching: `sha256:b1dcddda49514ca37f232e9e9faa73d2338491493730067ddb709b02246d1b0d`.
- admin-panel: `sha256:be9aae3825f10de6de3dcb562fb8f9d2bda889baeca46219df1623e043a8c6c9`.
- staff-portal: `sha256:c53761cf825c90b388fdd9f02a4a111107d5aabf175a060c7ba449d47d495bc7`.

## Veritabanı ve veri koruma

`20261017140000_ManualTimedStudyTasks` uygulandı. Yalnız mevcut
`ck_study_task_weekly_time` CHECK kuralı genişledi; satır güncelleme/silme yapılmadı.
Mevcut haftalık kayıtların ders kimliği/snapshot eşleşmesi ve gün içindeki süre sınırları korunuyor.

Canlı Coaching yedeği ayrı veritabanına geri yüklendi, geçiş iki kez çalıştırıldı.
Bütün koçluk iş tablolarının satır sayısı ve içerik özetleri aynı kaldı. Canlı geçişten
önce Identity ve Coaching için yeni yedekler alındı; geçiş ve servis açılışı sonrasında
hesap/rol/ürün erişimi ile koçluk iş tabloları yeniden karşılaştırıldı.
Kimlik doğrulamanın olağan son giriş/MFA sayaçları hesap karşılaştırmasından ayrıldı.

Başlangıç ve dağıtım sonrası: **6 plan, 338 çalışma, 5 tamamlanmış çalışma, 103 gerçek dakika**.
Haftalık taslak, eğitim bağlamı, müfredat ve öğretmen–öğrenci ilişkileri değişmedi.

Canlı hareket testinin ardından ilk canlı yedek yeniden ayrı veritabanına açıldı.
Yalnız testte taşınan 3 çalışmanın ve ilgili 1 planın `Version/UpdatedAt/UpdatedBy`
alanları istisna tutularak **bütün özgün iş satırları birebir eşit** bulundu.
Tarih, başlangıç, süre, başlık, sahiplik ve kazanım bağlantıları istisna tutulmadı.
Bu karşılaştırma dersin özgün yerine döndüğünü de doğruladı.

Öğretmen testi sonrasında yalnız aşağıdaki kesin kimlikli 1 taslak ve 1 çalışma karşılaştırmadan
ayrıldı; yeni satırlar ayrıca içerikleriyle doğrulandı. Son toplam **7 plan / 339 çalışma**;
özgün **6 plan / 338 çalışma**, tamamlanmış **5 kayıt / 103 dakika** korunuyor.
Yıllık plan aktif ve sürüm 10. Sürüm artışı, öğrenci/öğretmenin toplam 8 başarılı taşıma işlemi;
üç parçanın tarih, başlangıç ve süreleri özgün haline döndü. Yeni taslak aktif planı değiştirmedi.

Yedekler ve gizli bağlantı bilgileri sunucunun özel sürüm klasörlerinde kalıyor;
yerel kanıtlara yalnız özetler ve doğrulama sonuçları indiriliyor.

## Canlı öğrenci doğrulaması

Test hesabının mevcut yıllık aktif planı: `2f7e2d07-e044-471d-aedc-dfe004df7e2e`.

1. 20 Ekim 18:00–20:00 Matematik kartı fareyle 20:00–22:00'a taşındı.
   60 + 15 + 45 dakikalık üç özgün kazanım parçası birlikte taşındı.
2. 19 Ekim 18:00'a, tamamlanmış çalışmanın üstüne taşıma denenince çakışma uyarısı çıktı;
   kayıtlı konum değişmedi. Yerel PostgreSQL testleri aynı engeli sunucu düzeyinde de kapsıyor.
3. Formla 22 Ekim'e, ay takviminde fareyle 20 Ekim'e taşıma doğrulandı.
4. Testin sonunda üç parça özgün **20 Ekim 18:00 / 19:00 / 19:15** başlangıçlarına döndü.
5. Tamamlanan 19 Ekim kartı `Tamamlandı` işareti taşıyor ve sürüklenebilir değil.
   Ayrıntıda özgün `M.8.1.1.1`, süre sürümü 2, **17 gerçek dakika**, 0 soru ve tamamlanma zamanı görüldü.
6. Son arayüz düzeltmesinden sonra ay takviminde tekrar taşıma/özgün güne dönüş yapıldı;
   hemen ardından tamamlanmış karta **tek tıkla** ayrıntı açılması canlıda doğrulandı.

Öğrenci kanıtları: `artifacts/coaching-calendar-move-live-20261017/student-drag.jpg`,
`student-collision.jpg`, `artifacts/coaching-calendar-move-live-20261017-r2/student-completed.jpg`.
Eski tamamlanmış çalışmalarda yeni tamamlama işlemi yapılmadı.

Eski `17bf39c1-a949-424a-9f80-d9d8aa61fa1d` arşiv planı da güncel arayüzde açıldı:
12 dakika / 0 soru ve 17 dakika / 8 soru kayıtları erişilebilir, düzenleme kapalı.

## Canlı öğretmen doğrulaması

`testogretmen@eduivme.com` ile mevcut atanmış test öğrencisi kullanıldı; yeni atama/yetki verilmedi.

1. **Çalışma takibi** açıldı. 10–24 Ekim aralığında **5 tamamlanan / 6 bekleyen / 0 geciken /
   103 gerçek dakika** görüldü. Tamamlananlar ortak listesinde arşiv ve aktif planların beş
   özgün kaydı, gerçek süreleri ve plan bağlantıları doğru geldi.
2. Aynı tamamlanmamış 120 dakikalık ders, öğrenci ekranından geçici olarak 16 Ekim'e taşındı.
   Öğretmen **Listeyi yenile → Geciken çalışmalar** seçince 18:00 / 19:00 / 19:15 başlangıçlı
   **60 / 15 / 45 dakikalık üç parça** ortak listede `Gecikmiş` göründü.
3. Öğretmen **İlgili planı aç → Takvim → Gün ve saat değiştir** ile dersi özgün
   **20 Ekim 18:00–20:00** konumuna geri aldı. Öğrenci yenileyince aynı konumu gördü.
   Öğretmen toplu listesi yeniden **0 geciken** gösterdi.
4. Öğretmen **Yeni taslak** ile `Canlı doğrulama · Öğretmen taslağı · 17.10.2026` oluşturdu:
   `Sentetik doğrulama · Öğretmen Matematik`, 18 Ekim 2026 17:00, 30 dakika.
5. Öğrenci listeyi yenileyip aynı taslağı açtı; başlık, tarih, saat ve süre aynı geldi.
   İki panel aynı veritabanı satırını kullanıyor; öğrenci için ayrı kopya oluşturulmadı.

Taslak kimliği: `b5536157-dd00-4f63-b522-9f250c77b33b`.
Çalışma kimliği: `afbc9fea-ae32-4bb5-bc65-00395d3755f7`.
Sahiplik `StudentId = öğrenci UserId` ile saklandı; saat `PlannedStartMinute = 1020`, süre 30.
Taslak `Draft / IsActive=false`, mevcut yıllık plan `Active / IsActive=true` kaldı.

**Yayın kapsamı:** mevcut yıllık planı aktif tutmak için bu canlı test taslağı yayımlanmadı.
Taslak kaydı ve öğrencide görünmesi doğrulandı. Öğretmenden yayımlama → öğrencinin tamamlama
→ öğretmenin yenilemeyle görmesi zinciri yerel gerçek servis/veritabanı ve tarayıcı testlerinde
kapsanıyor; bu yeni taslak üzerinde canlı yayın/tamamlama yapıldığı iddia edilmiyor.

İzlenebilirlik notu: mevcut ortak taslak oluşturma servisinde `CreatedBy` boş kalıyor.
Sahiplik/yetki UserId ve mevcut atamayla doğru; oluşturan öğretmeni ayrı audit alanında saklamak
bu dağıtımda değiştirilmedi.

Canlı ekran kanıtları: `artifacts/coaching-calendar-move-live-20261017-r2/teacher-completed.jpg`,
`teacher-overdue.jpg`, `teacher-summary.jpg`, `student-teacher-draft.jpg`, `student-completed-detail.jpg`.

## Canlıda bulunan ve giderilen sorunlar

- Sürükleme sonunda otomatik tıklama oluşmazsa ilk tamamlanmış kart tıklaması yutulabiliyordu.
  Yeni gerçek basışta tıklama engeli temizlendi. Test önce hata verdi (`74294e4b`),
  küçük düzeltmeyle geçti (`dd7aebca`); canlıda tek tıkla yeniden doğrulandı.
- Öğretmen portalının üretim HTML'i `<base href="/">` kullandığından `/staff/` altında
  JS/CSS dosyalarını yanlış adreste arıyordu. Üretim ayarına `/staff/` eklendi.
  Paket kontrolü önce hata verdi (`000455b7`), doğru dosya yollarıyla geçti (`7f1f61de`).
  Canlı `main-ZOAQG7CY.js`: HTTP 200 / application/javascript;
  `styles-IES7KTLG.css`: HTTP 200 / text/css. Giriş ekranı tarayıcıda yeniden açıldı.
- Tarayıcı testindeki bir masaüstü sürükleme başlangıcı ekran dışında kalabiliyordu.
  Test önce kartı görünür alana getiriyor; gerçek üründeki sürükleme kuralı değiştirilmedi.

Otomatik onay denetimi bir geri alma adımını yalnız saat belirtildiği için reddetti.
Kayıtlı gün önce okundu, ardından özgün **20 Ekim ve 18:00 birlikte** belirtilerek işlem tamamlandı.
Bu konuda bekleyen engel yok.

## Testler

- 77 API/servis/gerçek PostgreSQL testi: `artifacts/calendar-move-api-ready.log`.
- Son düzeltmelerle 285 öğrenci UI/servis testi: `artifacts/calendar-move-ui-live-final.log`.
- 170 öğretmen UI testi: `artifacts/calendar-move-staff-live-final.log`.
- Son temiz üretim paketleri üzerinde 32 masaüstü/mobil tarayıcı testi:
  `artifacts/coaching-calendar-move-live-20261017-r2/browser-tests-verified.log`.
- Yeni ilk tıklama hatasının RED/GREEN kanıtı:
  `artifacts/calendar-move-click-red.log`, `artifacts/calendar-move-click-green.log`.
- Öğretmen paketi için yayın yolu kontrolü:
  `tools/releases/coaching-calendar-move-20261017/verify-staff-assets.mjs`.

Son tam arayüz testinde takvim bileşeni %98,95 ifade / %92,56 dal / %100 satır kapsamında.
Bu oranlar değişen takvim bileşenine aittir; tüm uygulamanın kapsamı olarak sunulmaz.

API/sahiplik/eşzamanlılık/atomiklik testleri gerçek yerel PostgreSQL ile; tarayıcı senaryoları
temiz üretim derlemeleri ve taklit API ile çalıştı. Canlı kontroller yukarıda ayrı belirtildi.
Üretim derlemeleri başarılı; mevcut boyut bütçesi uyarıları hata seviyesinde değil.

## Geri dönüş

İlk sürümün önceki üç imajı ve ikinci sürümün önceki iki imajı özel sürüm klasörlerinde sabitlendi.
Hazırlanmış rollback compose dosyaları yalnız ilgili servisleri eski imajlarına döndürür.
Yeni manuel saatli kayıtlar oluşturulmuşsa eski CHECK kuralına zorla dönülmez;
uygulama geri dönüşü yapılır, genişletilmiş şema ve geçmiş kayıtlar korunur.
Canlı veritabanı otomatik olarak eski yedeğe çevrilmez.
