# Odaklanma düzeltmeleri — 6 Ekim 2026

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
