# Sakkad süreleri ve akış düzeltmesi — v1

Bu paket yalnız dört yaş grubundaki 20 Sakkad eğitim kaydının hedef geçiş aralığını ve aktif çalışma süresini günceller. Koçluk, diğer egzersizler, seviye tespit programları, eğitim atamaları ve geçmiş sonuçlar değiştirilmez. Yeni tablo veya migration yoktur.

## Onaylanan ayarlar

Her dizinin sırası seviye 1 → 5'tir. Aralık milisaniye, çalışma süresi saniyedir.

| Yaş grubu | Hedef geçiş aralığı (ms) | Aktif süre (sn) |
| --- | --- | --- |
| Çocuk (9–12) | 900 / 850 / 800 / 750 / 700 | 40 / 45 / 50 / 55 / 60 |
| Genç (13–16) | 850 / 800 / 750 / 700 / 650 | 45 / 50 / 60 / 70 / 80 |
| Genç yetişkin (17–21) | 800 / 750 / 700 / 650 / 600 | 50 / 60 / 70 / 80 / 90 |
| Yetişkin (22+) | 850 / 800 / 750 / 700 / 650 | 50 / 60 / 70 / 80 / 90 |

Mevcut yatay/dikey/rastgele desenler, tek basamaklı rakamlar ve hedef boyutu korunur. Otomatik tempo değişimi yoktur. Bu değerler onaylanan ürün ayarlarıdır; klinik norm veya okuma hızında artış garantisi değildir.

## Çalışma düzeni

- Kullanıcı hedefi gözleriyle izler; tıklama veya cevap yazma gerekmez. Süreli akışta tıklama/boşluk tuşu hedefi erkenden değiştirmez.
- Otomatik hedef listesi bittikçe yeni hedefler üretilir; aynı 40 hedef döngü halinde yeniden oynatılmaz. Ardışık rakamlar aynı olmaz. Rastgele hedefler arasında en az 30 göreli konum birimi ayrım vardır; bu fiziksel görme açısı değildir.
- Duraklatma süresi aktif süreden çıkarılır. Kayıtlı oturumda başlangıç ve duraklatma sunucuya bildirilir; erken ya da duraklatılmış tamamlama reddedilir.
- Eski, açıkça verilmiş sonlu `Targets` yapılandırmaları ve eski oturum snapshot'ları yeniden yazılmaz. Süresiz eski protokol için yeni süre zorunluluğu getirilmez.
- Kayıtlı ayarlar ve özel önizleme aynı aralık değerini kullanır. Özel ayarlar kataloğu değiştirmez; önizleme sonuç kaydetmez.
- Sonuçlarda aktif süre, hedef geçiş sayısı ve hedef geçiş aralığı gösterilir. Göz hareketi ölçülmediği açıkça belirtilir; başarı puanı, doğruluk veya okuma hızı uydurulmaz.

## Yerel doğrulama

Önce hata üreten testler yazıldı; düzeltmelerden sonra tekrar çalıştırıldı.

- Backend birim testleri: 1089/1089 geçti.
- Frontend birim testleri: 791/791 geçti; tüm frontend statement kapsamı %62,07, MotionPath motoru %85,07. Bu, tüm projenin %100 kod kapsaması olduğu anlamına gelmez.
- Gerçek PostgreSQL seans testleri (Sakkad/Takip/Sabitleme): 5/5 geçti.
- Paket PostgreSQL testleri: 15/15 geçti; yalnız 20 kayıt güncelleme, tarihçe koruma, idempotence, baseline/kimlik/yaş/seviye/aktiflik/engine/mükerrer kontrolü, yetkisiz payload alanı değişiklikleri ve eksik/geçersiz süreleri kapsar.
- Tarayıcı testleri: dört yaşın 1. ve 5. seviyeleri ve özel ayarlar; masaüstü ve mobilde 18/18 geçti. Gerçek Angular motoru çalışır, Identity/API yanıtları test fixture'ıdır; canlı kullanıcı oturumu testi değildir.
- Üretim derlemesi geçti. Önceden mevcut SCSS bütçe uyarısı devam eder; bu değişiklik SCSS dosyasını düzenlemez.

Temel komutlar (repo kökünden, tarayıcı komutu `tests/E2E` altında):

```text
node content-packs/saccade/v1/generate.mjs
node --test tests/E2E/saccade-catalog.test.mjs
node --test tests/E2E/saccade-catalog.postgres.test.mjs
npx playwright test --config speed-reading-saccade.config.mjs
```

## Yayın ve geri dönüş

Bu çalışma yereldir; GitHub'a gönderim veya canlı veritabanı uygulaması yapılmış sayılmaz. Yayın için ayrıca onay gerekir.

1. Speed Reading veritabanının yedeğini ve 20 kaydın mevcut yapılandırma/sürüm bilgilerini alın; eski servis/frontend imajlarını saklayın.
2. `settings.json` kimliklerini canlı kayıtlarla karşılaştırın. Yönetici ayarı veya kayıt durumu değişmişse betik durur; kontrolü atlamayın.
3. `apply.sql` betiğini `psql -v ON_ERROR_STOP=1` ile yalnız Speed Reading veritabanında çalıştırın. Transaction ve kilitler kullanır; hedef yapılandırmada yalnız onaylı iki timing alanının değişmesine izin verir. Tekrar uygulama audit/sürüm alanlarını değiştirmez.
4. Uyumlu frontend/backend sürümünü yayımlayın; kayıtlı ve önizleme oturumlarının başlangıç, duraklatma ve tamamlanmasını kontrol edin.
5. Geri dönüşte eski imajları kullanın; yalnız 20 kaydın yapılandırmasını alınmış yedekten geri yükleyin. Güncelleme sonrasında oluşmuş kullanıcı sonuçlarını silmeyin veya tüm veritabanını koşulsuz eski yedeğe döndürmeyin.
