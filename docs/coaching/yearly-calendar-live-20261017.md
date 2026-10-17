# Yıllık çalışma planı takvimi — canlıya alma

Tarih: 17 Ekim 2026, Europe/Istanbul.

## Dağıtım

- Sürüm: `coaching-yearly-calendar-20261017`.
- Canlıya geçiş: **17 Ekim 2026 16:21:02 (Türkiye)** / `2026-10-17T13:21:02Z`.
- Uygulama kaynağı: `1e7c488f52071d0f243dac58bb827362a3a0d7d4`.
- Önceki canlı kaynak: `5411fc01fe62ddc40231ca941d3b76e395527d8b`.
- Değişen üretim dosyaları: yalnızca öğrenci çalışma planı/takvim arayüzünün **7 dosyası**.
- Dağıtılan `admin-panel` imajı: `sha256:35f4b6c548ec9499b3f879d78bd5d4eb075eee8da7a8b989aebe6b4f6dd89894`.
- Koçluk imajı değişmedi: `sha256:4fac5d3cadbd2d45fbdfefe61fb3699001fbb70d1a1d916903ad100ffef434f2`.
- Koçluk, Identity ve diğer servislerin konteyner kimlikleri değişmedi. Yalnız `admin-panel` yenilendi.
- **Veritabanı migration'ı yok.** Son migration `20261017061920_WeeklyOutcomePlans` olarak kaldı.

Paket önceki canlı sürümün temiz kaynak kopyasına yalnız ilgili commit farkları uygulanarak
üretildi. Çalışma dizinindeki ilgisiz değişiklikler pakete alınmadı. Bağımlılık kilit dosyası
aynı; yeni bağımlılık kurulmadı. Paket SHA-256 değeri:
`421ab15e54255f7dbfa48b60c34a7c63f07d434355c8ca45b0ec6f8497de9329`.

## Yedekleme ve veri koruma

Dağıtımdan önce `identity_db` ve `coaching_db` için özel sunucu dizinine PostgreSQL
yedekleri alındı. Yedekler, canlı yapılandırma ve gizli ortam değişkenleri yerel kanıt
paketine indirilmedi. Önceki arayüz imajıyla geri dönüş compose dosyası sunucuda saklı;
geri dönüş veritabanını eski yedeğe döndürmez ve yeni çalışma kayıtlarını silmez.

Dağıtımın hemen öncesi ve sonrasında:

- Hesapların kalıcı alanları; roller ve ürün erişimleri aynı parmak izini verdi.
  Oturum açma zamanı/MFA sayaçları gibi geçici güvenlik alanları karşılaştırma dışındadır.
- Koçluk iş tablolarının tüm satır içerikleri, haftalık taslak dahil, aynı kaldı.
  Teknik mesaj kutuları ve yönetici audit kayıtları iş verisi karşılaştırmasına dahil edilmedi.
- **6 plan, 338 çalışma, 4 tamamlanan çalışma, 86 gerçek dakika ve 1 haftalık taslak** korundu.

## Canlı öğrenci doğrulaması

Açık öğrenci oturumunda, gerçek canlı API/veritabanı üzerinden doğrulandı.

| Akış | Gözlenen sonuç |
|---|---|
| İlk açılış | Kayıtlı yıllık taslak otomatik Takvim görünümünde açıldı; ilk dolu gün **19.10.2026**, ders **18:00–19:00**. Dar ekranda Gün varsayılandı. |
| Dolu saat odağı | İç saat kaydırması **1050 px**, yani 17:30'a geldi; 18:00 kartı görünür alandaydı. |
| Hafta | 19–25 Ekim; 19, 20, 21 ve 23 Ekim kartları. Salı **18:00–20:00 / 120 dk / 3 çalışma** tek ders kartında gösterildi. |
| Kazanım ayrıntısı | Salı kartı 60 + 15 + 45 dakikalık özgün üç kaydı açtı. `M.8.1.1.1`, `M.8.1.1.2` kodları, açıklamalar ve **süre sürümü 2** göründü. |
| Ay | Ekim'in kayıtlı dolu tarihleri gösterildi. Sayfa yatay taşmadı. |
| Boş dönem | Haziran 2027'de “Bu aralıkta çalışma yok” gösterildi; haftalık şablon boş aya tekrar edilmedi. |
| Dolu güne dönüş | “Yakın çalışmaya git” dolu güne döndü; “Sonraki dolu gün” 20 Ekim'e geçti. |
| Yılın son çalışması | **05.04.2027, 18:00–18:15**; Geometrik Cisimler, `M.8.3.4.6`, 15 dk, süre sürümü 2. |
| Çalışma kaydetme | Yıllık test planı yayımlandı; 19 Ekim'in ilk çalışması **17 dk / 0 soru** ile tamamlandı. |
| Yeniden açma | Sayfa yenilenince 17 dk ve 0 soru kaydı duruyordu. İlk odak sıradaki tamamlanmamış gün **20 Ekim / 18:00** oldu. |
| Eski plan erişimi | Önceki aktif plan arşivden açıldı; Sentetik doğrulama B **12 dk / 0 soru**, A **17 dk / 8 soru** kayıtları göründü. |
| Eski saatsiz kayıtlar | Takvimde “Saati belirtilmemiş” alanında gösterildi; eski kayıtlara saat atanmadı. |

### Testin bilinçli kayıt etkisi

Çalışma kaydetmeyi test etmek için mevcut yıllık test taslağı
`2f7e2d07-e044-471d-aedc-dfe004df7e2e` yayımlandı. Önceki aktif test planı
`17bf39c1-a949-424a-9f80-d9d8aa61fa1d` normal ürün akışıyla arşivlendi; plan ve
çalışmaları silinmedi. Yeni aktif yıllık plan test hesabında bırakıldı.

Tamamlanan test çalışması: `51031e12-b6ad-4a0c-96e9-7d6add866469`.
Bu kayıt gerçek öğrenme yapıldığı anlamına gelmez; canlı akış doğrulaması için sentetiktir.
Planlanan 60 dk değişmedi; gerçekleşen süre 17 dk ve soru sayısı 0 olarak kaydedildi.

Canlı UI testi sonrası **6 plan, 338 çalışma, 5 tamamlanan çalışma, 103 gerçek dakika** var.
Önceki 4 tamamlanmış kaydın satır içerikleri ve 86 dakikalık toplamı aynen korundu.
Tüm diğer iş satırları da aynı parmak izini verdi. Yalnız iki test planının durum/sürüm/audit
alanları ve belirtilen tek çalışmanın tamamlama/sürüm/audit alanları karşılaştırmada
izin verilen değişikliklerdir; beklenen yeni değerler ayrıca SQL ile doğrulandı.

Yıllık plandaki **167 çalışma, 52 benzersiz kazanım ve 7215 planlanan dakika** değişmedi.
Kaynak haftalık taslak sürümü 1, eğitim bağlamı sürümü 2; kaynak saat dilimi Europe/Istanbul.

## Testler ve sağlık

- Geliştirme doğrulaması: **279 başarılı** koçluk arayüz/servis testi.
- Yeni takvim bileşeni: %100 ifade, %88,89 dal; takvim yardımcıları: %100 ifade/dal kapsamı.
- Canlıya çıkacak **izole üretim paketinde 28/28 masaüstü/mobil tarayıcı senaryosu geçti**.
  Bu otomatik testler taklit API kullanır; yukarıdaki canlı kontroller ayrıca yapıldı.
- İzole üretim derlemesi başarılı. Bilinen stil bütçesi uyarıları var, derleme hatası yok.
- Canlı tarayıcıda gün/hafta/ay, ayrıntılar, yayınlama, tamamlama, yenileme ve arşiv okuma doğrulandı.
- Dağıtım sonrası uygulama günlüklerinde hata/fatal satırı görülmedi.
- Korunan üç koçluk API uç noktası anonim istekte **401** döndürdü.
- 15 dakikalık sağlık izlemi **31/31 başarılı**. Sonuç **16:36:31 Türkiye saatiyle**
  (`2026-10-17T13:36:31Z`) kesinleşti. Dört uygulama adresi her örnekte HTTP 200 döndürdü;
  Koçluk ve arayüz servislerinin yeniden başlama sayısı 0 kaldı.

## Kanıtlar ve tekrar çalıştırma

Yerel kanıt dizini: `artifacts/coaching-yearly-calendar-live-20261017/`.

- `source-provenance.json`, `archive.sha256`: tam kaynak ve paket kimliği.
- `build.log`, `browser.log`, `browser.json`: üretim derlemesi ve 28 tarayıcı senaryosu.
- `calendar-data.log`: yıllık planın veritabanında kaynak, süre ve kazanım sayısı.
- `live/`: dağıtım, veri karşılaştırması ve sağlık sonuçları.
- `proof/week.jpg`, `proof/month.jpg`, `proof/outcomes.jpg`, `proof/completion-reopened.jpg`: canlı ekran kanıtları.
- `tools/releases/coaching-yearly-calendar-20261017/`: paket hazırlama, yalnız arayüzü dağıtma,
  koruma karşılaştırması, izleme ve kanıt toplama araçları.

Test komutu: `node node_modules/@playwright/test/cli.js test -c playwright.coaching-yearly-calendar-release.config.mjs`
(`tests/E2E` dizininde). Canlı deploy araçları üretim işlemleridir; sıradan test komutu olarak çalıştırılmaz.
