# Kelime Hazinesi v1 — geliştirme ve doğrulama raporu

11 Ekim 2026. Bu paket henüz GitHub'a gönderilmedi ve canlı veritabanına uygulanmadı.

## Yaş ve seviye ayarları

| Yaş grubu | Seviye 1 | 2 | 3 | 4 | 5 |
|---|---:|---:|---:|---:|---:|
| Çocuk (9–12) | 8 | 10 | 12 | 14 | 16 |
| Genç (13–16) | 10 | 12 | 14 | 16 | 18 |
| Genç yetişkin (17–21) | 12 | 14 | 16 | 18 | 20 |
| Yetişkin (22+) | 12 | 15 | 18 | 20 | 24 |

Normal çalışmalarda kelime başına süre sınırı yoktur. Sayılar kullanıcı tarafından onaylanan ürün ayarlarıdır; bilimsel norm veya doğrulanmış yaş yeterlilik ölçümü olarak sunulmaz.

## Tamamlanan davranışlar

- 28 mevcut katalog ayarı güncellendi; çocuk seviye 3 için bir quiz kaydı eklendi. Çocuk kataloğu 91 egzersiz oldu.
- Öğrenme kartları tamamlandığında aynı oturumdaki aynı kelimelerle test başlar. Yeni kelime havuzu çekilmez.
- Birleşik akışta Biliyorum/Bilmiyorum puan, doğrulanmış başarı, SRS kutu artışı veya öğrenilmiş kelime ödülü oluşturmaz. Bunlar test cevaplarıyla oluşur. Eski oturumların konfigürasyon anlık görüntüleri değişmez.
- Yeniden açılışta öğrenme/test aşaması ve mevcut kelime sunucudan alınır. Testte sırasız ve mükerrer yanıt reddedilir. Son cevap sonrası yenileme aynı kelimeyi yeniden sordurmaz.
- Yeni normal havuzlarda belirsiz kelime/anlam çiftleri önce dışlanır. Yetersiz havuz daha kısa çalışma gibi sunulmaz; anlaşılır uyarı verir. Bu seçim bilinçli olarak muhafazakârdır: farklı kayıtları eşleştirerek kullanılabilecek bazı çiftleri de dışlayabilir.
- Kayıtlı oturumlarda kullanıcının daha az yanıtladığı kelimeler önceliklidir; eşit kullanımda rastgele seçim yapılır. Açık kelime listeleri varsa aynı tam sayı kontrolü uygulanır.
- Admin/öğretmen özel önizlemesinde öğrenme+test veya doğrudan test, 4–50 kelime, soru yönü ve isteğe bağlı süre seçilir. Yaş/seviye açılan katalog kaydından gelir; başka yaş/seviyeye içerik düşürülmez.
- Önizleme profil, program, sonuç ve tekrar verisi yazmaz; kayıtlı katalog ayarını değiştirmez. Eksik havuzda yükleme göstergesi kapanır.
- Yeni cevapların EF tarafından güncelleme sayılması nedeniyle oluşabilen başarısız kayıt/yeniden deneme sorunu giderildi.
- Kelime Hazinesi okuma hızı ölçümü değildir; WPM gösterilmez.

## İçerik yeterliliği

Canlı veritabanında yalnız salt okunur sayım yapıldı. Yeni belirsiz çift filtresinden sonra ortak havuzda seviye 1–5 için sırasıyla **287 / 422 / 268 / 238 / 91** uygun kayıt var. Yeni sayıları karşılıyor.

Kelimeler ortak havuzda; yaşa özel etiketler yok. Seviye ve sayısal yeterlilik doğrulandı, fakat çocuklara uygunluk ve anlamların editoryal doğruluğu ayrıca insan tarafından değerlendirilmelidir. İçerik incelemesi yapılmış veya yaşa özgü pedagojik geçerliği kanıtlanmış gibi sunulmaz.

## Testler

- Hızlı Okuma uygulama birim testleri: 1.166/1.166 geçti.
- Arayüz hedefli birim testleri: 78/78 geçti.
- Kelime motoru kapsamı: %97,59 ifade, %88,70 dal, %98,24 fonksiyon, %98,66 satır. Bu tüm frontend'in kapsamı değildir.
- Gerçek geçici PostgreSQL'de öğrenme, tekrar, quiz ve birleşik akış: 4/4 geçti.
- PostgreSQL katalog geçişi: 17/17 geçti; geri alma, yanlış ayar, eksik havuz, çakışan çiftler, ID çakışması, geçmiş korunması ve yeniden çalıştırma dahil.
- Katalog sayıları: 4/4 geçti.
- Masaüstü/mobil tarayıcı: dört kayıtlı akışta 8/8; admin/öğretmen özel önizleme ve eksik havuzda 6/6 geçti. Tarayıcı testleri API fixture kullanır; PostgreSQL kalıcılığı ayrıca gerçek backend testleriyle doğrulanır.
- Üretim frontend derlemesi geçti. Mevcut büyük player SCSS dosyası için bütçe uyarısı var; bu pakette stil dosyası değiştirilmedi.
- Genel kod ve C# son incelemeleri onaylandı; havuz sayısı, test cevap sırası ve eksik havuz yükleme durumu bulguları giderildi. Bağımsız C# kontrolünde ilgili 83 test geçti, derleme 0 hata/uyarıyla tamamlandı.
- Katalog generator'ı yeniden çalıştırıldığında üretilen dosyaların SHA-256 değerleri değişmedi.

## Kontrollü yayın için

1. Yalnız Hızlı Okuma DB yedeği alın; backend/frontend sürümlerini geri dönüş için saklayın.
2. Yeni backend ve frontend'i hazırlayın. Diğer servis ve veritabanları bu paket kapsamı dışında.
3. `apply.sql` dosyasını yeni runtime ile kontrollü geçişte uygulayın. Önce disposable DB testini tekrar çalıştırın.
4. SQL yalnız belirtilen 28 ayarı günceller ve bir yeni quiz ekler. Şema değişikliği, kullanıcı geçmişini silme veya aktif oturum anlık görüntülerini değiştirme yoktur.
5. Baseline farklıysa SQL tüm işlemi reddeder; zorla güncellemeyin. Eski geniş katalog SQL'lerini sonradan çalıştırmayın: v1 vocabulary paketi son overlay olarak uygulanmalıdır.
6. Dört yaşta seviye 1–5 havuz sayısını, öğrenme→testi, özel önizlemeyi ve yeni kayıtlı sonuçları gerçek adreslerde kontrol edin.
7. Geri dönüşte eski backend/frontend ve yedeği birlikte kullanın; yeni birleşik oturumları eski backend ile sürdürmeyin. Veri kaybı yaratabilecek yedekten dönüş kullanıcıyla koordine edilmelidir.

Tekrarlanabilir üretim: `node content-packs/vocabulary/v1/generate.mjs`. Generator önceki ayarları `settings.json` içindeki baseline'dan korur.
