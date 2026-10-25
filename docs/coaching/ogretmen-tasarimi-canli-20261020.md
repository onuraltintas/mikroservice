# Koçluk öğretmen paneli — tasarım ve canlı doğrulama

Tarih: 20.10.2026

## Sonuç

Öğretmen paneli öğrenci panelinin renk, yazı, yüzey ve etkileşim kurallarıyla uyumlu hale getirildi. `coaching-teacher-design-20261020-r6` canlıya alındı. Son sağlık kontrolü başarılı; mevcut Coaching, Identity ve Notification satırlarının dağıtım öncesi özetleriyle dağıtım sonrası özetleri eşleşiyor.

## Yapılan değişiklikler

- Koçluk öğretmen çalışma alanında öğrenci panelinin ortak açık/koyu tema değişkenleri kullanılıyor. Tema seçimi yenileme sonrasında korunuyor.
- Öğrenci listesi, raporlar, profil, takip merkezi, çalışma takibi, ödevler, seanslar, hedefler, sınavlar ve öğretmen planı ortak renk ve tipografi kurallarına uyarlandı. Hesap ve bildirim yüzeyleri de tema kapsamına alındı.
- Mobil menü açılır çekmece olarak çalışıyor; klavye odağı menüde tutuluyor, Escape ile kapatılınca açma düğmesine dönüyor. İçeriğe geç bağlantısı ve görünür odak stilleri eklendi.
- Dar ekranlarda formlar tek sütuna geçiyor; sekmeler ve geniş tablolar kendi alanlarında kaydırılıyor. 320 pikselde taşmaya neden olan eski `body` minimum genişliği yalnız Koçluk öğretmen alanında kaldırıldı.
- Alan kontrolleri ve temel düğmeler en az 44 piksel yüksekliğe getirildi. Sınıfsız düğmelerin varsayılan tarayıcı görünümü ve arama düğmesinin gereksiz yüksekliği düzeltildi.
- Öğretmen bileşenlerinin başarı/hata sonuçları mevcut merkezi toaster ile gösteriliyor. Alan hataları yerinde kalıyor.
- Ödev değerlendirme ve öğrenci profili ekranındaki kalan yerel `window.confirm` kullanımları merkezi onay penceresine taşındı; üst çağrılar onayı bekleyecek şekilde güncellendi.
- Mobil menü, kaydedilmemiş form onayından önce kapanıyor; böylece onay penceresi menünün arkasında kalmıyor.
- Öğretmen çalışma alanının ertelenmiş yüklenmesini bozan sınıf referansı şablon referansına çevrildi. Başlangıç paketi yaklaşık 769 KB; bütçe eşikleri artırılmadı.
- Ödev türü/kaynağı gibi görünen etiketler Türkçeleştirildi; öğretmen ödev tarihleri sayısal, okunabilir biçime getirildi. Veritabanı enum değerleri değiştirilmedi.

## Otomatik kontroller

`node tools/test-coaching-student-design.mjs`

| Kapsam | Dosya | Geçen test |
| --- | ---: | ---: |
| Öğrenci/ortak bileşenler, tema ve toaster | 46 | 310 |
| Personel/öğretmen, ana kabuk, hesap ve bildirimler | 34 | 281 |
| Toplam yürütme | 80 | 591 |

Ortak senaryolar iki uygulama kapsamında çalışabilir; bu sayı benzersiz iş senaryosu sayısı değildir. Son çıktı: `artifacts/coaching-teacher-design-20261020/regression-final-5.log`.

- Öğretmen ve öğrenci production derlemeleri başarılı. Son R6 paketi ayrıca sunucuda izole production derlemesinden geçti.
- `node tools/check-coaching-student-design.mjs`: ortak açık/koyu metin ve durum renklerinin 4.5:1 kontrast kontrolü başarılı.
- Öğretmen bileşenlerinde yerel confirm/alert/prompt taraması ve ilgili `git diff --check` başarılı.
- Derlemede 500 KB başlangıç uyarısı ve ana bileşenin stil boyutu uyarısı devam ediyor; derleme hatası yok. Eşikler değiştirilmedi.

## Canlı doğrulama

Yetkili test öğretmeniyle mevcut kayıtlar üzerinde, kayıt kaydetmeden kontrol edildi:

1. Masaüstü açık/koyu tema, koyu temanın sayfa yenilemesinde korunması ve son açık tema görünümü.
2. 320, 390 ve 768 piksel testleri; son R6 sürümünde 320 piksel görünümde `clientWidth=305`, `scrollWidth=305`. Sayfa yatay taşmıyor. Kaydırma çubuğu 15 piksel yer kaplıyor.
3. Mobil menünün açılması, Escape ile kapanması ve odağın menü düğmesine dönmesi.
4. Öğretmen profilinde yalnız test metniyle oluşturulan kaydedilmemiş taslak: ürün değiştirme sırasında merkezi onay açıldı. İptal ile içerik ve Koçluk seçimi korundu; ardından onaylanarak kaydedilmeden kapatıldı.
5. Takip merkezinde ters tarih aralığı: alan içi hata ve merkezi hata toasterı birlikte görüldü. Tarihler geri alındı.
6. Çalışma takibi mevcut öğrenciyi ve aktif LGS planını açtı. Takvimde tamamlanan çalışma işareti ve mevcut çalışmalar görünür. Plan taşıma, yayımlama veya çalışma kaydetme yapılmadı.
7. Aktif `deneme` ödevinde mevcut **95/100** puan ve kayıtlı geri bildirim korundu. Kaydet düğmesine basılmadı. Yeni sürümde **Bireysel**, **Dijital** ve sayısal ödev tarihleri doğrulandı.
8. Hedef ekranında atanmış öğrenci, mevcut iki hedef ve bağlı LGS okul kataloğu formu açıldı. Hedef seçme/kaldırma/kaydetme yapılmadı.
9. Seanslar, sınavlar, hesap ayarları ve bildirim listesi açıldı. Şifre, finansal işlem, ürün katılımı veya davet işlemi yapılmadı; bildirimler okundu işaretlenmedi/silinmedi.
10. Öğrenci ana sayfası ve aktif LGS planı bağlantısı erişilebilir. Öğrenci uygulamasının canlı paketi değişmedi.
11. Son öğretmen tarayıcı konsolu hata listesi boş. Geçici ekran boyutu ayarları sıfırlandı; öğretmen oturumu açık bırakıldı.

Bu tasarım testi tüm iş akışlarının yeniden veri yazarak sınandığı bir regresyon değildir. Kaydedilmemiş formların mevcut uygulama içi geçiş korumaları test edildi; tarayıcı yenilemesi/kapanması için kalıcı taslak özelliği eklenmedi.

## Dağıtım ve veri koruma

- Yalnız `staff-portal` değiştirildi; veritabanı geçişi ve backend dağıtımı yapılmadı.
- Canlı kaynak tabanı üzerinde yalnız öğretmen tasarımı dönüşümleri ve stilleri uygulandı. Yerel çalışma ağacındaki henüz yayımlanmamış karar–iş bağlantısı değişiklikleri bu pakete dahil edilmedi.
- Öğrenci/admin ve backend konteyner kimliklerinin değişmediği doğrulandı.
- Dağıtım öncesindeki Coaching, Identity ve Notification kayıtlarının satır özetleri korunuyor. Teknik Inbox/Outbox/RefreshToken tabloları karşılaştırma dışında; normal oturum işleyişi devam ediyor.
- Mevcut Nginx çalışma ayarları, entrypoint, komut, ortam ve healthcheck yeni görüntüde korundu.
- Son durum: `healthy`, yeniden başlatma sayısı `0`.
- Öğretmen ve öğrenci JS/CSS dosyaları HTTP 200 ve doğru MIME türleriyle yanıt veriyor.
- R6 arşiv SHA-256: `8b5af3371e3aed2ff3c159eb928c65425aa743c43b238f36cb67615ce17aca86`.
- Canlıya geçiş: `2026-10-20T17:22:48Z`.
- Kanıt: `artifacts/coaching-teacher-design-20261020-r6/promotion.log`, `verification-final.log`.
- Geri dönüş için sunucudaki R6 klasöründe `rollback.compose.json` önceki R4 görüntüsünü kimliğiyle sabitler. İhtiyaç halinde yalnız `staff-portal` bu dosyayla geri alınabilir; veritabanı geri alma işlemi gerekmez.

## Ekran görüntüleri

- `artifacts/coaching-teacher-design-20261020/live-final-roster-light.png`
- `artifacts/coaching-teacher-design-20261020/live-final-roster-dark.png`
- `artifacts/coaching-teacher-design-20261020/live-final-mobile-light.png`
- `artifacts/coaching-teacher-design-20261020/live-confirm-mobile-dark.png`
- `artifacts/coaching-teacher-design-20261020/live-tracking-mobile-dark.png`

Son iki görüntü aynı tasarımın R4 canlı doğrulamasından; final R6 ekran görüntüleri dosya adında `live-final` taşır.
