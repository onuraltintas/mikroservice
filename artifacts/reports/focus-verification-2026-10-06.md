# Odaklanma düzeltmeleri - 6 Ekim 2026

## Nihai durum — önceki ara notların yerine geçer

Odaklanma düzeltme planı yerel geliştirme kapsamında tamamlandı.

- Özel ayarlar: konum/kelime/çift kanal, N-back 1–5, tablo 3–7, süre 100–10000 ms ve tur 2–500. Tur sayısı N-back seviyesini aşar; geçersiz etkin ayarlar dizi üretiminden önce reddedilir. Katalog değişmez; yalnız hız değişimi diziyi korur.
- Yaşam döngüsü: ilk uyaran, tekrarlanan hücre geçişi, duraklatma/devam, yeniden başlatma ve geç cevaplar doğrulandı. Geçişte yanıt alınmaz. Eski match girdisi tek kanala gider; tamamlanan adım toplamı aşmaz; reddedilen mevcut yanıt tekrar denenebilir.
- Sunucu gerçek N-back dizisinden hedef hesaplar; çelişen hedef indeksleri reddedilir. Yanıtsız ama süresi dolmuş oturum tamamlanır, erken tamamlama engellenir. Değerlendirmede tüm uyaranlar ve son yanıt penceresi gereklidir.
- Duraklamalar milisaniye hassasiyetindedir; başlatmadan önceki duraklama etkin süreden düşülmez.
- Yeni/geçmiş sonuç ve özetlerde Odaklanma WPM, ağırlıklı WPM veya okunan kelime üretmez. Doğruluk isabet/(isabet+kaçırılan hedef+yanlış alarm) oranıdır. D-prime iddiası kaldırıldı. Bu teknik doğrulama, okuma becerisine bilimsel aktarım etkisini kanıtlamaz.
- Kelime karşılaştırması büyük/küçük harften bağımsızdır. JavaScript ile .NET'in uç Unicode karşılaştırmaları birebir eşdeğer değildir; mevcut Türkçe havuz dışı içerikte ek sınır testi gerekir.

Son doğrulamalar: arayüz **605/605**, sunucu **886/886**, gerçek PostgreSQL **3/3**, masaüstü/mobil **8/8** geçti. Tarayıcı testleri gerçek Angular arayüzünü, kontrollü Identity/API yanıtlarıyla çalıştırır; canlı oturum testi değildir.

Üretim derlemesi geçti. Ortak player SCSS'inde mevcut 120 kB uyarı bütçesi 8,56 kB aşılıyor; derlemeyi engellemiyor ve bu kapsam dışında. Bağımsız TypeScript/C# incelemelerinin kapsam içi bulguları düzeltildi.

Yerel TDD commitleri oluşturuldu. Bu turda GitHub'a gönderilmedi ve canlıya alınmadı. Kapsam dışı TextFade değişiklikleri korundu.

## Tarihsel ara aşama notları (aşağıdaki eksikler artık kapatıldı)

## Tamamlanan adım: zamanlama ve istemci tutarlılığı

Önce başarısız testler çalıştırıldı, ardından düzeltmeler uygulandı:

- İlk uyaranın iki uyaran süresi gösterilmesi düzeltildi.
- Değerlendirme uyaranı duraklatma sonrası kalan süresiyle devam eder.
- Tekrarlanan hücrenin 150 ms görsel geçişi duraklatılır ve kalan süresinden devam eder.
- Görsel geçiş sırasında zamanlayıcının yeniden adım atlayabilmesi engellendi.
- Durdurulan oturumun gecikmiş geçişi temizlenir.
- Çalışan/tamamlanmış oturumda ikinci başlatma yok sayılır.
- Canlı doğruluk göstergesi sonuç ekranındaki yanıt temelli formülle eşleştirildi. Bu oran tüm uyaranların doğru reddedilmesini ölçen genel bir doğruluk değildir.

## Doğrulama

- Yeni Odaklanma testleri: 6/6 geçti.
- Tüm frontend birim testleri: 593/593 geçti.
- Bağımsız TypeScript kod incelemesi: engelleyici bulgu yok; TypeScript kontrolü geçti.
- İnceleme aşamasında mevcut 14 sunucu Odaklanma testi geçti; bu adımda sunucu kodu değiştirilmedi.

Bu adım henüz PostgreSQL veya uçtan uca tarayıcı doğrulaması değildir.

## Sıradaki işler — henüz tamamlanmadı

### İkinci adım ilerlemesi

- Özel ayarlara N-back seviyesi (1–5) ve tablo boyutu (3–7) eklendi; katalog değiştirilmeden etkin SessionData'ya uygulanır. Uyaran süresi kontrolü korunur.
- İstemci hedef indekslerini etkin N-back ve diziden türetir; eski ayarlardan taşınmış indeksler yeni seviyeyi bozmaz.
- Doğrulanmamış D-prime, hit-rate ve false-alarm-rate sonuç sunumundan kaldırıldı; buna ait artık kullanılmayan hesaplama kodu temizlendi.
- İlgili 32 özel ayar ve 7 Odaklanma testi geçti. Bu değişiklikler sunucu tamamlanma/WPM kurallarını henüz değiştirmiyor.
- Son tam arayüz regresyonu: 595/595 geçti; bağımsız TypeScript incelemesinde engelleyici bulgu yok.

1. Mod ve tur sayısı için özel ayarların genişletilmesi; mod/dizi uyumluluğu ve geçersiz içerik doğrulaması. N-back ve tablo boyutu kontrolleri tamamlandı.
2. Sunucu/istemci ölçüm tutarlılığı. D-prime sunumdan kaldırıldı.
3. Hiç cevap verilmeden süresi dolan geçerli oturumun güvenli tamamlanması. Erken tamamlamayı açacak şekilde yalnız cevap şartı kaldırılmamalı.
4. Odaklanmanın yeni ve geçmiş sonuçlarında bağlı metin olsa bile WPM/kelime sayısı üretiminin açıkça dışlanması.
5. Oturum yeniden yapılandırma ve geç sunucu cevaplarının kalan yaşam döngüsü kontrolleri.
6. Gerçek PostgreSQL, üç mod için masaüstü/mobil uçtan uca akışlar ve üretim derlemesi.

Yerel TDD commitleri oluşturuldu. GitHub'a gönderilmedi ve canlıya alınmadı. Kapsam dışı TextFade değişiklikleri korunmuştur.
