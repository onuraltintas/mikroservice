# Hedefi Yakala — yaş/seviye ve görsel hedef

11 Ekim 2026. Kullanıcının onayladığı yaş/seviye tablosu uygulanmıştır. Bu geliştirme **yereldedir; GitHub'a gönderilmedi ve canlıya alınmadı**. Önceki [katalog yayını](attention-release-20261011.md) canlıda üç eski pilot tempoyu kullanmaktadır.

## Kullanım ve ayarlar

Admin/SystemAdmin/Teacher önizlemesinde yaş grubu ve Seviye 1–5 seçilir. Yaş grubunu değiştirmek seçili seviyeyi korur. Üç süresiz alıştırmadan sonra seçili ayarlarla deneme başlar. Ayarlar çalışma sırasında değiştirilemez; yeniden denemek aynı seçimi korur. Yaş grubu seçimi kullanıcının Identity profilini değiştirmez.

Hücreler: **şekil sayısı / cevap sınırı (saniye) / tur**.

| Grup | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
| --- | --- | --- | --- | --- | --- |
| Çocuk | 6 / 6 / 24 | 8 / 5,5 / 28 | 10 / 5 / 32 | 12 / 4,5 / 36 | 16 / 4 / 40 |
| Genç | 8 / 5,5 / 28 | 10 / 5 / 32 | 12 / 4,5 / 36 | 16 / 4 / 40 | 20 / 3,5 / 44 |
| Genç yetişkin | 10 / 5 / 32 | 12 / 4,5 / 36 | 16 / 4 / 40 | 20 / 3,5 / 44 | 20 / 3 / 48 |
| Yetişkin | 10 / 5 / 32 | 12 / 4,5 / 36 | 16 / 4 / 40 | 20 / 3,5 / 44 | 20 / 3 / 48 |

Toplam çalışma süresi sabit değildir: erken yanıt turu sonlandırır. Her dönüt 700 ms sürer; tüm cevap sınırları kullanılırsa tur bütçeleri yaklaşık 161–188 saniyedir. Alıştırma, duraklama ve tarayıcı zamanlayıcısı gecikmeleri bu hesaba dahil değildir. Bunlar pilot ürün ayarlarıdır, bilimsel yaş normları veya klinik değerlendirme eşikleri değildir.

## Görsel hedef ve korunan sınırlar

- Aranacak yıldız oyun tahtasının **üstünde**, kontrastlı kenarlık/zemin içindeki SVG olarak gösterilir. Hedef şeklin adı görünen yazıyla verilmez; kısa “HEDEF” etiketi bulunur.
- Ekran okuyucu için `role="img"` ve “Aranacak sembol: yıldız” etiketi korunur. Referans SVG tahtadaki yıldızla aynı polygon'u kullanır; tıklanabilir bir yanıt kontrolü değildir.
- Hedef şeridi alıştırma, gerçek tur, dönüt ve duraklama sırasında aynı yerde kalır. Masaüstünde 64 px, mobilde 48 px'dir. Tahta kontrolleri en az 44 px dokunma sınırını korur.
- N-back, roller/rota korumaları, öğrenci programları, API ve veritabanı değiştirilmedi. Sonuçlar hâlâ kaydedilmez. Bu sürüm öğrenci programı ataması veya merkezi admin CRUD geliştirmesi değildir.

## Doğrulama

- İlk model/bileşen RED: 24 başarısız, 15 başarılı (`fe402e96`). Uygulama sonrası aynı hedef 39/39 GREEN (`f533b50a`).
- Seçim/reset/geçersiz girdi ve mevcut katalog kontrolleri dahil odaklı testler: **49/49**.
- Tüm Hızlı Okuma arayüz birim testleri: **862/862**; atlanan yok. Hata yollarını bilinçli çalıştıran mevcut testler konsola beklenen hata mesajlarını yazabilir; test sonucu başarılıdır.
- Yeni model/bileşen klasöründe satır/fonksiyon kapsamı %100, ifade kapsamı %98,7, dal kapsamı %96,66. Hedefli koşunun genel kapsamı bu klasörün kapsamı değildir.
- TypeScript ve E2E sözdizimi kontrolü geçti; kod incelemesinde engelleyici bulgu yok. Projede ESLint yapılandırması/binary'si bulunmuyor.
- İlk masaüstü/mobil tarayıcı koşusunda 21/22 geçti; 20 şekilli mobil tahtanın son satırı ekrana sığmadı. Boşluk azaltımı yeterli olmadığından geri alındı. Test gevşetilmeden, sembol ve dokunma hedefleri küçültülmeden mobilde 375 px ve üzerinde 20 şekilli tahta beş sütuna düzenlendi; daha dar ekranlarda dört sütun korunur. Aynı taşma testi GREEN oldu (`4813525f`). Son tam tarayıcı koşusu, 320/375 px sınır testleri eklenerek **26/26 geçti**, atlanan yok.
- Son mobil yerleşim değişikliğinden sonra üretim derlemesi tekrar geçti. Önceden mevcut Universal Player SCSS 128,99 kB / 120 kB bütçe uyarısı bu değişiklikle ilgili değildir.
- Aynı 26 akış yerel üretim yapılandırması/optimizasyonlarıyla da **26/26 geçti**, atlanan yok. Bu koşunun raporu `artifacts/attention-production-ui/report` altında tutulur.

## Tekrar çalıştırma

`clients/speed-reading` içinde `npm test -- --watch=false --browsers=ChromeHeadless` ve `npm run build -- --configuration production`.

`tests/E2E` içinde `npx playwright test --config=speed-reading-attention.config.mjs`.

Tarayıcı senaryoları yalnız yerel ön uçta Identity fixture'ıyla çalışır; canlı kullanıcı testi değildir. Yaş/seviye seçimleri, doğru/yanlış/kaçırılan sayaçları, tamamlama/reset, duraklama, klavye odağı, mobil sınırlar, görsel hedefin sabit konumu, rol reddi ve N-back kartının korunmasını kapsar. Ekran görüntüleri `artifacts/attention-ui` altında tutulur.
