# Sınav Simülasyonu geliştirme ve doğrulama raporu

## Sonuç ve kapsam

Sınav Simülasyonu düzeltmeleri yerelde tamamlandı. Genç, genç yetişkin ve yetişkin gruplarında mevcut seviye 5 egzersizleri korunuyor. Çocuk/yeni seviyeler eklenmedi. GitHub’a gönderim veya canlı yayın bu çalışma kapsamında yapılmadı. Diğer servisler ve veritabanları değiştirilmedi.

## Yapılanlar

- Yeni normal oturumlarda ilk okuma aşaması soru aşamasından önce zorunlu; minimum ve üst sınır sunucuda doğrulanıyor.
- Sunucu soru başlatma zamanı ve timeout’ı doğruluyor; istemcinin gönderdiği süre puanlama kaynağı değil. Tekrar soru başlatma süreyi sıfırlamıyor. Tekrar yanıt/tamamlamada çift sonuç oluşmuyor.
- Doğru cevap, açıklama ve doğru/yanlış istatistiği sınav devam ederken yanıt/yenileme/ilerleme/ayrıntı çıktılarında gizli. Tamamlanan sonuçta cevap anahtarı açılıyor.
- Duraklatma hem UI hem API’de engelli. İlk okuma sayfa yenilenince sunucu saatinden devam ediyor.
- Metin seçimine yaş, seviye, kelime bandı ve geçerli soru sayısı koşulları eklendi. Önizleme yetersiz içerikte güvenli uyarı gösteriyor.
- Soru ekranı yazı boyutu/satır aralığına uyuyor. Özel ayarlar gerçek katalog akışında uygulanıyor; eşit/geçersiz okuma pencereleri reddediliyor.
- Sonuçta toplam etkin süre sunucu kaydıyla tutarlı; WPM üretilmiyor. Sonuç simgesinde yinelenen HTML class nedeniyle oluşan harf taşması giderildi.
- Üç ayar için tekrar uygulanabilir, atomik, geçmişi koruyan uygulama/geri dönüş paketi hazırlandı. Şema migration’ı gerekmiyor.

## Parametreler ve mevcut içerik

| Grup | Seviye | Kelime bandı | Okuma üst sınırı | Sorular | Soru süresi | Canlıda salt okunur doğrulanan uygun metin |
| --- | --- | --- | --- | --- | --- | --- |
| Genç | 5 | 540–650 | 180 sn | 8 | 48 sn | 15 |
| Genç yetişkin | 5 | 430–650 | 180 sn | 8 | 60 sn | 15 |
| Yetişkin | 5 | 480–700 | 240 sn | 8 | 60 sn | 4 |

Hepsinde minimum okuma 10 sn. Canlı içerik yalnız okunarak denetlendi; yeni ayarlar canlıya yazılmadı. Yetişkin havuzu daha küçüktür; zamanla içerik çeşitliliği artırılabilir. Bu bir süreli paragraf alıştırmasıdır, tam sınav veya akademik olarak normlanmış ölçme aracı değildir.

## Test sonuçları

- Hızlı Okuma backend birim/regresyon: **1.219/1.219 geçti**; sınav odaklı filtre 22/22 geçti.
- Hızlı Okuma frontend birim/regresyon: **923/923 geçti**.
- Gerçek PostgreSQL: **4/4 geçti**. Üç yaşta sekiz sorulu tam oturum, tekrar uygulama, geri dönüş, katalog/içerik uyuşmazlığında atomik durdurma ve geçmiş koruma doğrulandı.
- Paket Node testleri: **2/2 geçti**.
- Masaüstü/mobil tarayıcı: **12/12 geçti**; öğrenci manuel/süre dolması, admin önizlemesi, öğretmen özel ayarlı katalog önizlemesi, yetersiz içerik ve eski protokol geriye uyumluluğu.
- Üretim Angular derlemesi başarılı. Mevcut `exercise-player.component.scss` boyutu 128,99 kB; 120 kB uyarı eşiği aşımı sürüyor. Bu kapsamda SCSS yeniden yapılandırılmadı.
- Sınav motoru kapsamı: satır **%96,61**, dal **%84,31**, fonksiyon **%100**. Tüm frontend genel satır kapsamı **%64,86**; tüm proje için %100 kapsam iddiası yok.

Tarayıcı testleri gerçek Angular UI, kontrollü API fixture’ları ve Edge üzerinde yapıldı. Gerçek oturum/kayıt mantığı ayrıca PostgreSQL’de doğrulandı; gerçek Identity oturumuyla canlı uçtan uca test yapılmadı. Görseller `artifacts/exam-ui` altındadır. Başta sandbox’ın tarayıcı başlatmasını engellemesi test hatası sayılmadı; izinli yeniden çalıştırma geçti. PostgreSQL jsonb boşluk normalizasyonuna bağlı test karşılaştırması anlamsal JSON karşılaştırmasına düzeltildi.

## Kod incelemesi ve sınırlar

Test-önce kontrol noktaları aktif dalda yerel RED/GREEN commit’leriyle saklandı. Son kod; erişim/cevap sızıntısı, istemci süresine güven, tekrar denemeler, içerik kısıtları, SQL atomikliği, geriye uyumluluk ve mobil görünüm açısından yapılandırılmış öz-incelemeden geçirildi. Bağımsız bir değerlendirici incelemesi olarak sunulmuyor.

Kapsamdaki geliştirme veya doğrulama engeli kalmadı. Canlıya uygulama ve gerçek oturumla yayın sonrası doğrulama ayrı onaylı yayın adımıdır. Yayın/geri dönüş talimatları `content-packs/exam-simulation/v1/README.md` içindedir.
