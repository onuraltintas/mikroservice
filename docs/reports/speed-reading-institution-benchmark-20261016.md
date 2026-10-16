# Hızlı Okuma kurum karşılaştırması — 16 Ekim 2026

## Hata ve kaynak

Kurum özetinde ve öğrenci raporunda anlama %87 iken ayrıntılı raporun kurum karşılaştırması %0 görünüyordu. Canlı veritabanındaki salt okunur inceleme, test öğrencisinin aktif Hızlı Okuma `Student` kurum üyeliğini doğruladı; eski `user_profiles.InstitutionId` alanı boştu. Ayrıntılı karşılaştırma bu eski alandan hem kurum seçiyor hem de kurum öğrencilerini buluyordu. Ölçüm bulunamayınca `0` döndürüyordu. Kurum özeti ise güncel üyelikleri kullandığı için doğruydu.

Öğrencinin soru içeren üç kayıtlı okuması %100, %80, %80: aritmetik ortalama %86,6667; ekranda %87. Tek ölçümlü öğrencisi bu hesap olan test kurumunun aynı dönem ortalaması da %87 olmalı.

## Değişiklik

- Karşılaştırma, aktif `institution_memberships` / `Student` kayıtlarından hesaplanır. Öğretmen, pasif üye ve başka kurum öğrencisi kohorta girmez.
- Kurum raporu, yetkilendirme kontrolünden sonra açıldığı kurumun kimliğini hesaplamaya aktarır. Çoklu kurumlu öğrencide her kurum ayrı hesaplanır ve önbellek anahtarları ayrıdır.
- Kurum seçilmeyen öğrenci/öğretmen isteğinde tek aktif kurum varsa o kurum kullanılır; çoklu kurumda rastgele seçim yapılmaz.
- Anlama için yalnız soru içeren okumalar; WPM için yalnız pozitif ve ölçülmüş hızlar kullanılır. Kurum ortalaması, kurum özetindeki gibi okuma kayıtlarının ağırlığıyla hesaplanır.
- Ölçüm bulunmayan karşılaştırma ile gerçek %0 iki ayrı durumdur. API ek alanlarla bunu bildirir; personel raporu “Veri yok” gösterir ve öğrenci karşılaştırma grafiği olmayan kohortu çizmez. Gerçek %0 korunur.
- Performans karşılaştırmasına yalnız mevcut ortalamalar girer; eksik kurum ortalaması sıfır gibi hesaba katılmaz.
- Sonuç, puan, XP, günlük ve program ilerlemesi değiştirilmez; şema/migration değişikliği yoktur.

## Doğrulama

- PostgreSQL RED: yeni üyelikli öğrencinin %86,67 karşılaştırması yerine `0` döndüğü testle tekrarlandı (`6757d23d`).
- UI RED: personel raporunun ölçümsüz kohortu `0%` göstermesi ve öğrenci grafiğinin bu kohortu çizmesi çalıştırılan testlerde başarısız oldu (`1399739f`).
- GREEN (`bc891bec`): 4 yeni PostgreSQL senaryosu, 65 personel arayüz testi, 31 öğrenci rapor testi geçti.
- Geniş doğrulama: 1.319 C# birim testi ve 32 PostgreSQL/HTTP entegrasyon testi geçti.
- Değişen C# satırlarının ölçülebilen kapsamı: 38/40, %95. Kurum controller kapsam aktarımı ayrıca izin ve erişim reddi birim testleriyle doğrulandı.
- C# ve TypeScript incelemeleri onaylandı; yayın betikleri ayrıca incelendi.
- Kaynak yalıtımı: önceki canlı kaynaklara göre yalnız bu talebin üç backend dosyası, iki personel dosyası ve öğrenci rapor eşleyicisi değişir; ilgili testler ayrı listelenir. Çalışma alanındaki diğer değişiklikler yayıma alınmaz.

## Yayın durumu

`institution-benchmark-20261016` yayını **11:42:26 TSİ** tamamlandı. Kaynak revizyonu `d6ac56cf` (hesap düzeltmesi `bc891bec`). API, Application ve Infrastructure derlemeleri birlikte taşındı; doğrulayıcıyla ve aday imajla SHA-256 eşitlikleri kontrol edildi.

- Canlı veritabanı kopyasında 10 senaryolu prova başarılı: **86 tablo / 8.609 mevcut satır korundu**. Klon temizlendi.
- Personel ve öğrenci paketlerinin ayrı önizlemeleri HTML ve ana JS için HTTP 200 döndürdü.
- Üretim yazıcısı durdurulup yedek alındı; aday oluşturulmadan önce/sonra 86 tablonun tüm satır hash'leri birebir eşit çıktı. Migration uygulanmadı. Diğer servislerin konteyner kimlikleri değişmedi. Üç hedef için önceki imajlara geri dönüş hazır.
- Gerçek kurum yöneticisi oturumunda kurum özeti **%87**, öğrenci ayrıntısı **Kurum ortalaması %87 · Öğrenci %87**, performans **Ortalama**. Özet/hız/anlama/etkinlik isteklerinin tamamı HTTP 200.
- Canlı anlama API yanıtında öğrenci ve kurum ayrı ayrı **86,66666666666667**; iki ölçüm varlığı bayrağı `true`. Platform ortalaması **77,5**; başka öğrenciler de içerdiğinden kurum ortalamasıyla aynı olması beklenmez.
- Yeni öğrenci paketi `main-YOYFOZKQ.js` ile rapor anlama **%87**, etkinlik **21**, ölçümsüz hız **Veri yok**; tarayıcıda yeni hata yok. Yeni personel ana paketi `main-Z7GJ5K7W.js`.
- Test öğrencisinin yayından önce/sonra tam JSON'u aynı: **476 XP, 21 sonuç, 12 günlük kayıt, 12 kelime ilerleme kaydı, Gün 3**. Başarımlar ve program alanları da aynı karşılaştırmanın içinde korundu.

| Hedef | İmaj |
|---|---|
| API | `sha256:81a65a3bcb5e554417bd36189bd3114bf4d3a6abd9f5006b5afac9e08e10cb70` |
| Personel | `sha256:f010cbea52da4124a706d0c733498e63700a3ac35cc463f3541c3ad554435401` |
| Öğrenci | `sha256:7b11605ebff4bc202257438a84659a24d5fd8fb28f6084d7763675d1ba84f4ba` |

15 dakikalık otomatik gözlem **11:57:27 TSİ** tamamlandı. Son kontrol **11:58:06 TSİ** (yayından 15 dakika 40 saniye sonra): **57 API isteğinde 0 sunucu 5xx, 0 uygulama ERR/FTL**, iki dış adres HTTP 200; API yeniden başlama sayısı 0. Üç hedefin imajları ve diğer servislerin konteyner kimlikleri son kontrolde doğrulandı. `student-final.log` başlangıç tam JSON'uyla yine birebir aynı.

Son kanıtlar: `monitoring-complete.txt`, `monitoring.log`, `final-health.log`, `student-final.log`, `live-checks.json`, `institution-comprehension-response.json`, `institution-after.jpg`. Sunucudaki altı operasyon dosyasının incelenen yerel kaynaklarla aynı olduğu `operations-source.sha256` içinde kayıtlıdır.

Kanıt dizini: `artifacts/institution-benchmark-20261016/`. Operasyon betikleri: `tools/releases/institution-benchmark-20261016/`.
