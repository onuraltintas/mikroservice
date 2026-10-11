# Hedefi Yakala — yerel prototip

Tarih: 11 Ekim 2026. Kapsam: kullanımı zor bulunan Odaklanma/N-back egzersizine alternatifin ilk denemesi. **Canlıya alınmadı; GitHub'a gönderilmedi.** Yerel geliştirme ve test aşamaları commitlendi.

## Kullanım

Yıldız her turda aynı hedef olarak kalır; diğer şekillerin arasından bulunup tıklanır. Hedefin konumu ardışık turlarda değişir. Önce üç süre sınırı olmayan alıştırma yapılır; bunlar sonuçlara dahil edilmez. Alıştırmada yanlış seçilen şekil açıklanır ve yeniden denemeye izin verilir.

Gerçek denemede tur başına bir yanıt alınır. Doğru hedef, yanlış şekil ve süre dolması ayrı sayılır. Geri bildirim 700 ms boyunca gösterilir; bu sırada ikinci yanıt alınmaz. Görünmez sekme ve kullanıcı duraklatması zamanı durdurur; geri dönmek otomatik devam ettirmez. Duraklama doğru yanıtların ortalama süresine dahil değildir.

| Pilot seçenek | Şekil sayısı | Tur | Tur başına yanıt süresi |
| --- | ---: | ---: | ---: |
| Sakin | 6 | 12 | 5 sn |
| Dengeli | 12 | 20 | 4 sn |
| Yoğun | 20 | 24 | 3 sn |

Bunlar kullanılabilirlik denemesi ayarlarıdır, yaş normları veya bilimsel değerlendirme eşikleri değildir. Yaş/seviye ayarları prototip değerlendirmesinden sonra ayrıca belirlenmelidir.

Başarı oranı = doğru / (doğru + yanlış + kaçırılan). Ortalama yanıt süresi yalnız doğru yanıtlarda, tur açılmasından tıklamaya kadar geçen etkin süredir; saniye olarak gösterilir. Görsel arama ve motor yanıtı birlikte içerir. Klinik dikkat tanısı, göz hareketi veya okuma hızı ölçümü yapılmaz.

## Erişim ve korunan sınırlar

- Uygulama yolu: `/student/attention-preview`. Admin, SystemAdmin veya Teacher gerekir; Teacher+Student gibi karma roller de bu salt okunur prototipi kullanabilir.
- İzinli kullanıcının Egzersiz Önizlemesi/kataloğunda “Yeni dikkat prototipini dene” bağlantısı bulunur.
- Oyun modeli ve bileşeni HTTP, kalıcı depolama veya sonuç kaydetme servisi kullanmaz. Uygulama kabuğu normal Identity oturum açılışını kullanmaya devam eder.
- Eski Focus motoru, veritabanı kataloğu, eğitim programları ve kullanıcı geçmişi değiştirilmedi.
- Yeni şekiller inline SVG'dir; metin/soru havuzu veya dış görsel servisi gerekmez.

## Doğrulama

- Hızlı Okuma ön uç testleri: **830/830 geçti**, atlanan test yok.
- Prototip modeli/bileşeni ve kabuk guardı odaklı testler: **23/23 geçti**.
- Masaüstü ve Pixel 5 boyutunda tarayıcı senaryoları: **20/20 geçti**. Tam deneme, yanlış alıştırma, sayaçlar, zaman aşımı, üç pilot seçenek, 44 px ve üzeri dokunma hedefleri, tüm şekillerin ekrana sığması, doğal Tab/Shift+Tab/Enter akışı, görünmez sekme, yeniden başlatma, rol reddi, karma roller ve eski Focus kartının korunması kontrol edildi.
- Bu tarayıcı testlerinde Identity yanıtları yerel fixture ile sağlandı; gerçek kullanıcı oturumu veya canlı veri üzerinde test yapılmadı. Egzersiz akışında beklenmeyen API yazısı ve JavaScript sayfa hatası saptanmadı.
- Yeni `attention-preview` model/bileşen kodu kapsaması: satırlar **%100**, fonksiyonlar **%100**, ifadeler **%98,59**, dallar **%96,42**. Bu oranlar tüm uygulamanın kapsaması değildir.
- Üretim derlemesi geçti. Değiştirilmemiş Universal Player SCSS dosyasındaki mevcut 128,99 kB / 120 kB bütçe uyarısı sürüyor; prototipten kaynaklanmıyor.
- TypeScript incelemesi: engelleyici bulgu kalmadı. Başlangıç ve tur geçişinde odak yönetimi, OnPush kabuk altında zamanlayıcı güncellemesi ve mobil görünüm düzeltilip tekrar doğrulandı.

## Yeniden çalıştırma

`clients/speed-reading` içinde:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless
npm run build -- --configuration production
```

`tests/E2E` içinde:

```powershell
npx playwright test --config=speed-reading-attention.config.mjs
```

Bu test yapılandırması yalnız `127.0.0.1:4337` adresindeki yerel ön ucu kullanır. Normal tarayıcıdan uygulama yolunu açmak için çalışan Identity/Gateway ve gerçek izinli oturum gerekir; test fixture'ları uygulamaya eklenmemiştir.

Tarayıcı raporu: `artifacts/attention-ui/report/index.html`. Ekran görüntüleri: `artifacts/attention-ui/{desktop,mobile}-{intro,playing,paused,result}.png`.

## Sonraki onay kapısı

Önce prototipin kuralı, temposu ve tasarımı kullanıcı tarafından değerlendirilmelidir. Ardından yaş/seviye parametreleri, sunucu doğrulamalı öğrenci sonuçları, admin ayarları ve mevcut programlara geçiş ayrı geliştirme olarak yapılabilir. Prototipin tamamlanması, eski Odaklanma'nın değiştirildiği veya canlı öğrenci egzersizinin hazır olduğu anlamına gelmez.
